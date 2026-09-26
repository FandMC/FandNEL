using System.Buffers;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FandNEL.Core.Protocol;
using FandNEL.Core.Entities.G79;
using FandNEL.Protocol.Authentication;
using FandNEL.Proxy.Models;
using Serilog;

namespace FandNEL.Gateway.Management;

/// <summary>
/// FandNEL 的本地控制面。协议与 Codexus Gateway 保持兼容：ECDH P-256 握手后，
/// 每条消息使用 AES-CBC 传输，静态页面与 WebSocket 共用同一个本地端口。
/// </summary>
public sealed class LocalGatewayWebSocketServer : IAsyncDisposable
{
    private const int FirstPort = 19541;
    private const int LastPort = 19560;
    private const int RentalVersionId = 1169603340;
    private static readonly byte[] Salt = "codexus.today.websocket.establishing"u8.ToArray();
    private static readonly byte[] Info = "codexus.today.aes.key"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly GatewayRuntime _runtime;
    private readonly string _webRoot;
    private readonly GameCatalogMessages _gameMessages;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Lazy<G79> _g79 = new(() => new G79(), LazyThreadSafetyMode.ExecutionAndPublication);
    private readonly List<Task> _connections = [];
    private readonly object _connectionsLock = new();
    private HttpListener? _listener;
    private Task? _acceptTask;
    private int _port;

    public LocalGatewayWebSocketServer(GatewayRuntime runtime, string? webRoot = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _webRoot = webRoot ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        _gameMessages = new GameCatalogMessages(_runtime, () => _g79.Value);
    }

    public int Port => _port;
    public string WebSocketAddress => $"ws://localhost:{_port}/gateway/";
    public string HttpAddress => $"http://localhost:{_port}/";

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_acceptTask is not null) return _acceptTask;
        Directory.CreateDirectory(_webRoot);
        for (var port = FirstPort; port <= LastPort; port++)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            try
            {
                listener.Start();
                _listener = listener;
                _port = port;
                break;
            }
            catch (HttpListenerException)
            {
                listener.Close();
            }
        }

        if (_listener is null)
            throw new InvalidOperationException($"无法监听本地网关端口 {FirstPort}-{LastPort}。");

        Log.Information("FandNEL gateway listening on {Address}", WebSocketAddress);
        _acceptTask = AcceptLoopAsync(_shutdown.Token);
        return _acceptTask;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) { break; }

            Task request = context.Request.IsWebSocketRequest &&
                           context.Request.Url?.AbsolutePath.TrimEnd('/').Equals("/gateway", StringComparison.OrdinalIgnoreCase) == true
                ? HandleWebSocketAsync(context, cancellationToken)
                : ServeStaticAsync(context, cancellationToken);
            lock (_connectionsLock) _connections.Add(request);
            _ = request.ContinueWith(completed =>
            {
                lock (_connectionsLock) _connections.Remove(completed);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private async Task ServeStaticAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var relative = context.Request.Url?.AbsolutePath.TrimStart('/') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(relative)) relative = "index.html";
            relative = Uri.UnescapeDataString(relative.Replace('/', Path.DirectorySeparatorChar));
            var candidate = Path.GetFullPath(Path.Combine(_webRoot, relative));
            var root = Path.GetFullPath(_webRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var file = candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate)
                ? candidate
                : Path.Combine(_webRoot, "index.html");
            if (!File.Exists(file))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            var bytes = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
            context.Response.ContentType = GetContentType(Path.GetExtension(file));
            // index.html contains a content-hashed bundle name. Do not let the
            // embedded browser keep an old shell after a local update.
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.ContentLength64 = bytes.LongLength;
            await context.Response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            context.Response.Close();
        }
        catch (OperationCanceledException) { context.Response.Abort(); }
        catch (Exception exception)
        {
            Log.Debug(exception, "Failed to serve local gateway asset");
            context.Response.Abort();
        }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        WebSocket? socket = null;
        try
        {
            socket = (await context.AcceptWebSocketAsync(null).ConfigureAwait(false)).WebSocket;
            await RunSessionAsync(socket, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (WebSocketException) { }
        catch (Exception exception) { Log.Debug(exception, "Local gateway WebSocket session failed"); }
        finally
        {
            if (socket is { State: WebSocketState.Open })
            {
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", CancellationToken.None).ConfigureAwait(false); }
                catch { }
            }
            context.Response.Close();
        }
    }

    private async Task RunSessionAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        using var serverKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        byte[]? sessionKey = null;
        string? identify = null;
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var wireBytes = await ReceiveMessageAsync(socket, cancellationToken).ConfigureAwait(false);
            if (wireBytes is null) break;
            var json = sessionKey is null ? Encoding.UTF8.GetString(wireBytes) : Decrypt(wireBytes, sessionKey);
            GatewayWireMessage? message;
            try { message = JsonSerializer.Deserialize<GatewayWireMessage>(json, JsonOptions); }
            catch (JsonException) { await SendAsync(socket, "error_notification", "无效的网关消息", identify, sessionKey, cancellationToken).ConfigureAwait(false); continue; }
            if (message is null || string.IsNullOrWhiteSpace(message.Type)) continue;
            identify = message.Identify ?? identify;

            if (message.Type.Equals("handshake", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var clientKey = ECDiffieHellman.Create();
                    clientKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(message.Payload ?? string.Empty), out _);
                    // Browser SubtleCrypto derives the raw P-256 ECDH secret and
                    // applies HKDF-SHA256 with Salt/Info below. DeriveKeyMaterial
                    // applies an additional platform KDF and produced a different
                    // AES key, so the first encrypted command was unreadable and
                    // the UI immediately entered its reconnect state.
                    var shared = serverKey.DeriveRawSecretAgreement(clientKey.PublicKey);
                    sessionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, Salt, Info);
                    await SendAsync(socket, "handshake", Convert.ToBase64String(serverKey.PublicKey.ExportSubjectPublicKeyInfo()), identify, null, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Log.Debug(exception, "Gateway handshake failed");
                    break;
                }
                continue;
            }

            if (sessionKey is null)
            {
                await SendAsync(socket, "error_notification", "请先完成握手", identify, null, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try { await DispatchAsync(socket, message, identify, sessionKey, cancellationToken).ConfigureAwait(false); }
            catch (BedrockAccountNotActivatedException exception)
            {
                await SendAsync(socket, message.Type,
                    JsonSerializer.Serialize(new { code = 1001, message = exception.Message, payload = string.Empty }, JsonOptions),
                    identify, sessionKey, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "FandNEL gateway message {Type} failed", message.Type);
                if (message.Type == "create_role")
                {
                    await SendAsync(socket, "error_notification", $"创建角色失败: {exception.Message}", identify, sessionKey, cancellationToken).ConfigureAwait(false);
                    await SendAsync(socket, "create_role", string.Empty, identify, sessionKey, cancellationToken).ConfigureAwait(false);
                }
                else if (message.Type == "join_game")
                {
                    await SendAsync(socket, "error_notification", exception.Message, identify, sessionKey, cancellationToken).ConfigureAwait(false);
                }
                else if (message.Type.StartsWith("java_edition/", StringComparison.Ordinal)
                         && message.Type.Contains("skin", StringComparison.OrdinalIgnoreCase))
                {
                    await SendAsync(socket, "error_notification", exception.Message, identify, sessionKey, cancellationToken).ConfigureAwait(false);
                    if (message.Type == "java_edition/skin_list")
                    {
                        await SendAsync(socket, message.Type,
                            JsonSerializer.Serialize(new { code = 1001, message = exception.Message, entities = Array.Empty<object>(), total = 0 }, JsonOptions),
                            identify, sessionKey, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await SendAsync(socket, "java_edition/skin_error", string.Empty, identify, sessionKey, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    await SendAsync(socket, message.Type, JsonSerializer.Serialize(new { code = 1001, message = exception.Message, payload = string.Empty }, JsonOptions), identify, sessionKey, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task DispatchAsync(WebSocket socket, GatewayWireMessage message, string? identify, byte[] key, CancellationToken cancellationToken)
    {
        if (_gameMessages.CanHandle(message.Type))
        {
            var response = await _gameMessages.HandleAsync(message.Type, message.Payload, cancellationToken).ConfigureAwait(false);
            if (message.Type == "create_role")
            {
                // 保持 Codexus Gateway 的角色创建消息顺序：创建确认后立即推送最新角色列表。
                await SendAsync(socket, "success_notification", "创建角色成功!", identify, key, cancellationToken).ConfigureAwait(false);
                await SendAsync(socket, "create_role", string.Empty, identify, key, cancellationToken).ConfigureAwait(false);

                var roles = await _gameMessages.HandleAsync("get_roles", message.Payload, cancellationToken).ConfigureAwait(false);
                await SendAsync(socket, "get_roles", JsonSerializer.Serialize(roles, JsonOptions), identify, key, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await SendAsync(socket, message.Type, JsonSerializer.Serialize(response, JsonOptions), identify, key, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        switch (message.Type)
        {
            case "join_game":
                await JoinGameAsync(socket, message.Payload, identify, key, cancellationToken).ConfigureAwait(false);
                break;
            case "java_edition/network/session/config":
                await SendSessionConfigAsync(socket, message.Payload, identify, key, cancellationToken).ConfigureAwait(false);
                break;
            case "java_edition/session/reselect_server":
                await ReselectServerAsync(socket, message.Payload, identify, key, cancellationToken).ConfigureAwait(false);
                break;
            case "java_edition/session/switch_role":
                await SwitchRoleAsync(socket, message.Payload, identify, key, cancellationToken).ConfigureAwait(false);
                break;
            case "get_accounts":
                await SendAsync(socket, "get_accounts", SerializeAccounts(message.Payload), identify, key, cancellationToken).ConfigureAwait(false);
                break;
            case "user_inactive":
                _runtime.JavaUsers.RemoveAvailableUser(message.Payload ?? string.Empty);
                _runtime.BedrockUsers.RemoveAvailableUser(message.Payload ?? string.Empty);
                await SendAsync(socket, "get_accounts", SerializeAccounts(string.Empty), identify, key, cancellationToken).ConfigureAwait(false);
                break;
            case "update_user_alias":
                UpdateAlias(message.Payload);
                break;
            case "delete_user":
                DeleteUser(message.Payload);
                await SendAsync(socket, "get_accounts", SerializeAccounts(string.Empty), identify, key, cancellationToken).ConfigureAwait(false);
                break;
            case "login":
                await LoginAsync(socket, message.Payload, identify, key, cancellationToken).ConfigureAwait(false);
                break;
            case "query_game_session":
                await SendAsync(socket, "query_game_session", SerializeGameSessions(), identify, key, cancellationToken).ConfigureAwait(false);
                break;
            case "cancel_game_session":
                await CancelGameSessionAsync(socket, message.Payload, identify, key, cancellationToken).ConfigureAwait(false);
                break;
            default:
                await SendAsync(socket, message.Type, JsonSerializer.Serialize(new { code = 1001, message = $"FandNEL 尚未实现消息：{message.Type}", payload = string.Empty }, JsonOptions), identify, key, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task JoinGameAsync(WebSocket socket, string? payload, string? identify, byte[] key,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<JoinGameWireRequest>(payload ?? string.Empty, JsonOptions)
            ?? throw new InvalidOperationException("无效的 Java 拦截器请求。");
        if (string.IsNullOrWhiteSpace(request.UserId)) throw new ArgumentException("Java 账号不能为空。");
        if (string.IsNullOrWhiteSpace(request.GameId)) throw new ArgumentException("游戏 ID 不能为空。");
        if (string.IsNullOrWhiteSpace(request.Role)) throw new ArgumentException("请选择或创建游戏角色。");
        if (string.IsNullOrWhiteSpace(request.Version)) throw new ArgumentException("游戏版本不能为空。");

        var socks5 = request.Socks5 is { Enabled: true }
            ? new Socks5Options(request.Socks5.Address, request.Socks5.Port, request.Socks5.Username, request.Socks5.Password)
            : null;
        var lease = await _runtime.Games.StartProxyAsync(new GameProxyRequest
        {
            UserId = request.UserId,
            NexusToken = request.Token,
            GameId = request.GameId,
            GameVersion = request.Version,
            Target = new ServerTarget(request.ServerIp, request.ServerPort),
            Role = new PlayerRole(request.Role),
            ListenPort = 0,
            RentalServerId = request.VersionId == RentalVersionId ? request.GameId : null,
            Socks5 = socks5,
        }, cancellationToken).ConfigureAwait(false);

        await SendAsync(socket, "success_notification", "已成功创建新的拦截器!", identify, key, cancellationToken).ConfigureAwait(false);
        await SendAsync(socket, "join_game/success", lease.Session.Id.ToString(), identify, key, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendSessionConfigAsync(WebSocket socket, string? payload, string? identify, byte[] key,
        CancellationToken cancellationToken)
    {
        var id = ReadSessionId(payload);
        if (!_runtime.Sessions.TryGet(id, out var session) || session is null)
            throw new KeyNotFoundException("不存在该拦截器。");

        var snapshot = session.Snapshot;
        var config = new
        {
            game_id = snapshot.GameId ?? string.Empty,
            is_rental = !string.IsNullOrWhiteSpace(snapshot.RentalServerId),
            server_name = snapshot.GameId ?? snapshot.Target.Host,
            user_id = snapshot.UserId ?? string.Empty,
            nickname = snapshot.Role.Name,
            server_version = snapshot.GameVersion ?? string.Empty,
            local_address = snapshot.LocalEndpoint?.Address.ToString() ?? string.Empty,
            local_port = snapshot.LocalEndpoint?.Port ?? 0,
            forward_address = snapshot.Target.Host,
            forward_port = snapshot.Target.Port,
            mod_info = "[]"
        };
        await SendAsync(socket, "java_edition/network/session/config", JsonSerializer.Serialize(config, JsonOptions), identify, key, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReselectServerAsync(WebSocket socket, string? payload, string? identify, byte[] key,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<ReselectServerWireRequest>(payload ?? string.Empty, JsonOptions)
            ?? throw new InvalidOperationException("无效的服务器切换请求。");
        if (!Guid.TryParse(request.Id, out var id) || !_runtime.Sessions.TryGet(id, out var session) || session is null)
            throw new KeyNotFoundException("不存在该拦截器。");
        await session.UpdateServerAsync(new ServerTarget(request.Address, request.Port), cancellationToken).ConfigureAwait(false);
        await SendAsync(socket, "java_edition/session/reselect_server", string.Empty, identify, key, cancellationToken).ConfigureAwait(false);
    }

    private async Task SwitchRoleAsync(WebSocket socket, string? payload, string? identify, byte[] key,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<SwitchRoleWireRequest>(payload ?? string.Empty, JsonOptions)
            ?? throw new InvalidOperationException("无效的角色切换请求。");
        if (!Guid.TryParse(request.Id, out var id) || !_runtime.Sessions.TryGet(id, out var session) || session is null)
            throw new KeyNotFoundException("不存在该拦截器。");
        await session.UpdateRoleAsync(new PlayerRole(request.Role), cancellationToken).ConfigureAwait(false);
        await SendAsync(socket, "java_edition/session/switch_role", string.Empty, identify, key, cancellationToken).ConfigureAwait(false);
    }

    private static Guid ReadSessionId(string? payload)
    {
        var value = (payload ?? string.Empty).Trim().Trim('"');
        return Guid.TryParse(value, out var id)
            ? id
            : throw new ArgumentException("拦截器 ID 无效。");
    }

    private string SerializeAccounts(string? selector)
    {
        if (string.Equals(selector, "available-for-mobile", StringComparison.OrdinalIgnoreCase))
            return JsonSerializer.Serialize(_runtime.BedrockUsers.GetAvailableUsers(), JsonOptions);
        if (string.Equals(selector, "available", StringComparison.OrdinalIgnoreCase))
        {
            var accounts = _runtime.JavaUsers.GetAvailableUserAccounts()
                .Select(user => new AvailableAccount(user.UserId, _runtime.JavaUsers.GetUserById(user.UserId)?.Alias ?? user.UserId));
            return JsonSerializer.Serialize(accounts, JsonOptions);
        }

        var java = _runtime.JavaUsers.GetUsersNoDetails();
        var bedrock = _runtime.BedrockUsers.GetUsersNoDetails();
        var all = java.Concat(bedrock)
            .GroupBy(user => user.UserId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(user => user.Authorized).First());
        return JsonSerializer.Serialize(all, JsonOptions);
    }

    private string SerializeGameSessions()
    {
        var sessions = _runtime.Sessions.GetSnapshots().Select(session => new
        {
            id = $"interceptor-{session.Id}",
            name = session.GameId ?? session.Id.ToString(),
            game_type = "Java",
            is_rental = !string.IsNullOrWhiteSpace(session.RentalServerId),
            server_name = session.Target.Host,
            guid = session.Id.ToString(),
            character_name = session.Role.Name,
            server_version = session.GameVersion ?? string.Empty,
            status_text = session.State switch
            {
                FandNEL.Proxy.Models.ProxySessionState.Running => "Running",
                FandNEL.Proxy.Models.ProxySessionState.Faulted => "Failed",
                FandNEL.Proxy.Models.ProxySessionState.Stopping => "Stopping",
                _ => "Stopped"
            },
            type = "Interceptor",
            progress_value = session.State == FandNEL.Proxy.Models.ProxySessionState.Running ? 100 : 0,
            local_address = session.LocalEndpoint?.ToString() ?? string.Empty
        });
        return JsonSerializer.Serialize(sessions, JsonOptions);
    }

    private async Task CancelGameSessionAsync(WebSocket socket, string? payload, string? identify, byte[] key,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("会话 ID 不能为空。");

        using var document = JsonDocument.Parse(payload);
        var ids = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().Select(item => item.ToString()).Where(item => Guid.TryParse(item, out _)).ToArray()
            : [document.RootElement.ToString()];
        foreach (var value in ids)
            if (Guid.TryParse(value, out var id))
                await _runtime.Sessions.StopAsync(id, cancellationToken).ConfigureAwait(false);

        await SendAsync(socket, "query_game_session", SerializeGameSessions(), identify, key, cancellationToken).ConfigureAwait(false);
    }

    private void UpdateAlias(string? payload)
    {
        var request = JsonSerializer.Deserialize<AliasRequest>(payload ?? string.Empty, JsonOptions)
            ?? throw new InvalidOperationException("无效的账号别名请求。");
        var platform = (GatewayPlatform)request.Platform;
        var user = platform == GatewayPlatform.Mobile ? _runtime.BedrockUsers.GetUserById(request.Id) : _runtime.JavaUsers.GetUserById(request.Id);
        if (user is null) return;
        user.Alias = request.Alias?.Trim() ?? string.Empty;
        if (platform == GatewayPlatform.Mobile) _runtime.BedrockUsers.SaveUsersToDisk();
        else _runtime.JavaUsers.MarkDirtyAndScheduleSave();
    }

    private void DeleteUser(string? payload)
    {
        var request = JsonSerializer.Deserialize<AliasRequest>(payload ?? string.Empty, JsonOptions)
            ?? throw new InvalidOperationException("无效的账号删除请求。");
        if ((GatewayPlatform)request.Platform == GatewayPlatform.Mobile) _runtime.BedrockUsers.RemoveUser(request.Id);
        else _runtime.JavaUsers.RemoveUser(request.Id);
        _runtime.Tokens.RemoveToken(request.Id);
    }

    private async Task LoginAsync(WebSocket socket, string? payload, string? identify, byte[] key, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<LoginWireRequest>(payload ?? string.Empty, JsonOptions)
            ?? throw new InvalidOperationException("无效的登录请求。");
        if (string.Equals(request.Channel, "send_code", StringComparison.OrdinalIgnoreCase))
        {
            await SendAsync(socket, "login", JsonSerializer.Serialize(new { code = 1001, message = "短信登录需要通过桌面认证流程完成。", payload = string.Empty }, JsonOptions), identify, key, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (string.Equals(request.Channel, "active", StringComparison.OrdinalIgnoreCase))
        {
            await ActivateSavedAccountAsync(socket, request, identify, key, cancellationToken).ConfigureAwait(false);
            return;
        }

        var details = request.Details ?? string.Empty;
        string channel = request.Channel ?? string.Empty;
        string account = string.Empty;
        string password = details;
        if (string.Equals(request.Type, "cookie", StringComparison.OrdinalIgnoreCase)) channel = "cookie";
        else if (string.Equals(request.Type, "password", StringComparison.OrdinalIgnoreCase))
        {
            var credentials = JsonSerializer.Deserialize<PasswordWireRequest>(details, JsonOptions)
                ?? throw new InvalidOperationException("密码登录参数无效。");
            account = credentials.Account;
            password = credentials.Password;
        }

        if ((GatewayPlatform)request.Platform == GatewayPlatform.Mobile)
        {
            var authentication = await LoginBedrockAsync(request, account, password, details, cancellationToken).ConfigureAwait(false);
            _runtime.BedrockUsers.AddUserToMaintain(authentication);
            _runtime.BedrockUsers.AddUser(new ManagedUser
            {
                UserId = authentication.EntityId,
                Authorized = true,
                AutoLogin = false,
                Channel = request.Channel ?? string.Empty,
                Type = request.Type ?? string.Empty,
                Details = details,
                Platform = GatewayPlatform.Mobile,
                Alias = authentication.EntityId
            });
            await SendAsync(socket, "login/success", authentication.EntityId, identify, key, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var session = await _runtime.Accounts.LoginAsync(
                new LoginRequest(channel, account, password, GatewayPlatform.Desktop),
                new NoopChallengeHandler(), cancellationToken).ConfigureAwait(false);
            await SendAsync(socket, "login/success", session.UserId, identify, key, cancellationToken).ConfigureAwait(false);
        }
        await SendAsync(socket, "login", JsonSerializer.Serialize(new { code = 0, message = "Success", payload = string.Empty }, JsonOptions), identify, key, cancellationToken).ConfigureAwait(false);
        await SendAsync(socket, "get_accounts", SerializeAccounts(string.Empty), identify, key, cancellationToken).ConfigureAwait(false);
    }

    private async Task ActivateSavedAccountAsync(WebSocket socket, LoginWireRequest request, string? identify, byte[] key,
        CancellationToken cancellationToken)
    {
        var userId = request.Details?.Trim();
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("待激活账号不能为空。");

        if ((GatewayPlatform)request.Platform == GatewayPlatform.Mobile)
        {
            var saved = _runtime.BedrockUsers.GetUserById(userId)
                ?? throw new KeyNotFoundException("找不到保存的基岩版账号。");
            var savedRequest = new LoginWireRequest(
                saved.Channel,
                saved.Type,
                saved.Details,
                (int)GatewayPlatform.Mobile,
                request.Token);
            var credentials = string.Equals(saved.Type, "password", StringComparison.OrdinalIgnoreCase)
                ? JsonSerializer.Deserialize<PasswordWireRequest>(saved.Details, JsonOptions)
                    ?? throw new InvalidOperationException("保存的基岩版密码登录参数无效。")
                : null;
            var authentication = await LoginBedrockAsync(savedRequest, credentials?.Account ?? string.Empty,
                credentials?.Password ?? string.Empty, saved.Details, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(authentication.EntityId, saved.UserId, StringComparison.Ordinal))
                throw new InvalidOperationException("保存的基岩版账号与认证结果不一致。");

            _runtime.BedrockUsers.AddUserToMaintain(authentication);
            saved.Authorized = true;
            _runtime.BedrockUsers.SaveUsersToDisk();
            await SendLoginCompletedAsync(socket, authentication.EntityId, identify, key, cancellationToken).ConfigureAwait(false);
            return;
        }

        var java = _runtime.JavaUsers.GetUserById(userId)
            ?? throw new KeyNotFoundException("找不到保存的 Java 版账号。");
        var storedAccounts = await _runtime.Accounts.ListAsync(cancellationToken).ConfigureAwait(false);
        string activatedUserId;
        if (storedAccounts.Any(account => account.Id == java.UserId))
        {
            var session = await _runtime.Accounts.GetSessionAsync(java.UserId, cancellationToken).ConfigureAwait(false);
            activatedUserId = session.UserId;
        }
        else
        {
            var credentials = string.Equals(java.Type, "password", StringComparison.OrdinalIgnoreCase)
                ? JsonSerializer.Deserialize<PasswordWireRequest>(java.Details, JsonOptions)
                    ?? throw new InvalidOperationException("保存的 Java 版密码登录参数无效。")
                : null;
            var session = await _runtime.Accounts.LoginAsync(
                new LoginRequest(credentials is null ? "cookie" : java.Channel,
                    credentials?.Account ?? string.Empty, credentials?.Password ?? java.Details, GatewayPlatform.Desktop),
                new NoopChallengeHandler(), cancellationToken).ConfigureAwait(false);
            if (!string.Equals(session.UserId, java.UserId, StringComparison.Ordinal))
                throw new InvalidOperationException("保存的 Java 版账号与认证结果不一致。");
            activatedUserId = session.UserId;
        }
        java.Authorized = true;
        _runtime.JavaUsers.MarkDirtyAndScheduleSave();
        await SendLoginCompletedAsync(socket, activatedUserId, identify, key, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendLoginCompletedAsync(WebSocket socket, string userId, string? identify, byte[] key,
        CancellationToken cancellationToken)
    {
        await SendAsync(socket, "login/success", userId, identify, key, cancellationToken).ConfigureAwait(false);
        await SendAsync(socket, "login", JsonSerializer.Serialize(new { code = 0, message = "Success", payload = string.Empty }, JsonOptions), identify, key, cancellationToken).ConfigureAwait(false);
        await SendAsync(socket, "get_accounts", SerializeAccounts(string.Empty), identify, key, cancellationToken).ConfigureAwait(false);
    }

    private async Task<EntityAuthenticationOtp> LoginBedrockAsync(
        LoginWireRequest request,
        string account,
        string password,
        string details,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string cookie = details;
        if (string.Equals(request.Type, "password", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(request.Channel, "4399pc", StringComparison.OrdinalIgnoreCase))
            {
                using var client = new Com4399();
                cookie = await client.LoginAndAuthorize(account, password).ConfigureAwait(false);
            }
            else if (string.Equals(request.Channel, "netease", StringComparison.OrdinalIgnoreCase))
            {
                var launcher = _runtime.Accounts.Service.Launcher;
                var user = await launcher.LoginWithEmailAsync(account, password).ConfigureAwait(false);
                cookie = JsonSerializer.Serialize(WPFLauncher.GenerateCookie(user, launcher.MPay.GetDevice()), JsonOptions);
            }
            else
            {
                throw new NotSupportedException($"不支持的基岩版登录渠道：{request.Channel}");
            }
        }

        // FandNEL 不再要求 Nexus 账号；空值交由远程服务按匿名/应用级策略处理。
        return await Task.Run(() => _g79.Value.AuthenticationOtp(cookie, request.Token ?? string.Empty), cancellationToken).ConfigureAwait(false);
    }

    private static async Task SendAsync(WebSocket socket, string type, string payload, string? identify, byte[]? key, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Serialize(new GatewayWireMessage(type, payload, Sha256(payload), identify), JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(message);
        if (key is not null) bytes = Encrypt(bytes, key);
        await socket.SendAsync(bytes, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]?> ReceiveMessageAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            using var stream = new MemoryStream();
            bool endOfMessage;
            do
            {
                var result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                if (result.MessageType != WebSocketMessageType.Text && result.MessageType != WebSocketMessageType.Binary) return null;
                stream.Write(buffer, 0, result.Count);
                endOfMessage = result.EndOfMessage;
            } while (!endOfMessage);
            return stream.ToArray();
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static byte[] Encrypt(byte[] plain, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
        return aes.IV.Concat(cipher).ToArray();
    }

    private static string Decrypt(byte[] encrypted, byte[] key)
    {
        if (encrypted.Length < 17) throw new CryptographicException("加密消息长度无效。");
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = encrypted[..16];
        using var decryptor = aes.CreateDecryptor();
        return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(encrypted, 16, encrypted.Length - 16));
    }

    private static string Sha256(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        _ => "application/octet-stream"
    };

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync().ConfigureAwait(false);
        _listener?.Close();
        if (_acceptTask is not null)
        {
            try { await _acceptTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
        Task[] connections;
        lock (_connectionsLock) connections = _connections.ToArray();
        try { await Task.WhenAll(connections).ConfigureAwait(false); } catch { }
        _listener = null;
        if (_g79.IsValueCreated) _g79.Value.Dispose();
        _shutdown.Dispose();
    }

    private sealed record GatewayWireMessage(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("payload")] string? Payload,
        [property: JsonPropertyName("sign")] string? Sign,
        [property: JsonPropertyName("identify")] string? Identify);

    private sealed record AvailableAccount(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("alias")] string Alias);

    private sealed record AliasRequest(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("platform")] int Platform,
        [property: JsonPropertyName("alias")] string? Alias);

    private sealed record LoginWireRequest(
        [property: JsonPropertyName("channel")] string Channel,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("details")] string? Details,
        [property: JsonPropertyName("platform")] int Platform,
        [property: JsonPropertyName("token")] string? Token);

    private sealed record PasswordWireRequest(
        [property: JsonPropertyName("account")] string Account,
        [property: JsonPropertyName("password")] string Password);

    private sealed record JoinGameWireRequest(
        [property: JsonPropertyName("id")] string UserId,
        [property: JsonPropertyName("name")] string GameName,
        [property: JsonPropertyName("game")] string GameId,
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("vid")] int VersionId,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("ip")] string ServerIp,
        [property: JsonPropertyName("port")] int ServerPort,
        [property: JsonPropertyName("nid")] string NexusId,
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("socks5")] Socks5WireRequest? Socks5);

    private sealed record Socks5WireRequest(
        [property: JsonPropertyName("enabled")] bool Enabled,
        [property: JsonPropertyName("address")] string Address,
        [property: JsonPropertyName("port")] int Port,
        [property: JsonPropertyName("username")] string? Username,
        [property: JsonPropertyName("password")] string? Password);

    private sealed record ReselectServerWireRequest(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("game_id")] string GameId,
        [property: JsonPropertyName("game_name")] string GameName,
        [property: JsonPropertyName("game_version_id")] int GameVersionId,
        [property: JsonPropertyName("game_version")] string GameVersion,
        [property: JsonPropertyName("address")] string Address,
        [property: JsonPropertyName("port")] int Port);

    private sealed record SwitchRoleWireRequest(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("role")] string Role);

    private sealed class NoopChallengeHandler : ILoginChallengeHandler
    {
        public Task<string?> RequestCaptchaAsync(string channel, ReadOnlyMemory<byte> image, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<bool> RequestVerificationAsync(string channel, Uri verificationUri, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
