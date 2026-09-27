using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FandNEL.Proxy.Models;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;
using Serilog;

namespace FandNEL.Proxy.Irc;

/// <summary>IRC 聊天室桥接配置：参数以代码默认值固化，不读写任何配置文件，账号为聊天室公益账号。</summary>
public sealed record IrcChatOptions
{
    public bool Enabled { get; init; } = true;
    public string BaseUrl { get; init; } = "https://neoeastside.636.ltd";
    /// <summary>聊天室账号（脱盒用户名），游戏内显示的名字就是它；留空则不启用。</summary>
    public string Username { get; init; } = "neo123696559";
    public string Password { get; init; } = "neonb123696559";
    /// <summary>每 6 秒在游戏内提示聊天室在线人数。</summary>
    public bool ShowOnlineHint { get; init; } = true;
    /// <summary>每 15 秒在游戏内提示「/IRC 内容」用法。</summary>
    public bool ShowUsageHint { get; init; } = true;
    public int PollIntervalMilliseconds { get; init; } = 1200;

    [JsonIgnore]
    public bool IsUsable => Enabled && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

    [JsonIgnore]
    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(Math.Clamp(PollIntervalMilliseconds, 500, 10000));

    /// <summary>聊天室接口 JSON 约定（camelCase、大小写不敏感）。</summary>
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

/// <summary>各协议版本的 IRC 包 ID 与系统聊天包载荷构造（包 ID 已用 start_configuration 交叉验证）。</summary>
internal static class IrcProtocol
{
    internal sealed record VersionSpec(
        ProtocolVersion Version, int JoinGameId, int SystemChatId,
        int? ChatMessageId, int? ChatCommandId, int? SignedChatCommandId);

    internal static readonly VersionSpec[] Specs =
    [
        new(ProtocolVersion.V1076, 0x01, 0x02, 0x01, null, null),
        new(ProtocolVersion.V108X, 0x01, 0x02, 0x01, null, null),
        new(ProtocolVersion.V1122, 0x23, 0x0F, 0x02, null, null),
        new(ProtocolVersion.V1165, 0x24, 0x0E, 0x03, null, null),
        new(ProtocolVersion.V1180, 0x26, 0x0F, 0x03, null, null),
        new(ProtocolVersion.V1200, 0x28, 0x64, 0x05, 0x04, null),
        new(ProtocolVersion.V1206, 0x2B, 0x6C, 0x06, 0x04, 0x05),
        new(ProtocolVersion.V1210, 0x2B, 0x6C, 0x06, 0x04, 0x05),
        new(ProtocolVersion.V1218, 0x2B, 0x72, 0x08, 0x06, 0x07),
        new(ProtocolVersion.V12110, 0x30, 0x77, 0x08, 0x06, 0x07)
    ];

    internal static VersionSpec? TryGetSpec(ProtocolVersion version) => Array.Find(Specs, spec => spec.Version == version);

    /// <summary>解析 /IRC 输入：1.18 及更早走聊天包（带斜杠），1.19+ 走命令包（内容不含斜杠）。</summary>
    internal static bool TryParseIrcCommand(string? text, bool isCommandPacket, out string content)
    {
        content = string.Empty;
        var keyword = isCommandPacket ? "irc" : "/irc";
        if (text is null || !text.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)) return false;
        if (text.Length > keyword.Length && text[keyword.Length] != ' ') return false; // 排除 "/ircabc"
        content = text.Length > keyword.Length ? text[keyword.Length..].Trim() : string.Empty;
        return true;
    }

    /// <summary>系统聊天包载荷（服务端 → 客户端），按版本拼接不同的文本组件布局。</summary>
    internal static byte[] BuildSystemChatPayload(ProtocolVersion version, string text)
    {
        var safe = SingleLine(text);
        using var writer = new PacketWriter();
        if (version >= ProtocolVersion.V1206)
        {   // 1.20.5+：匿名 NBT 文本组件 + 是否动作栏
            WriteNbtText(writer, safe);
            writer.WriteBoolean(false);
            return writer.ToArray();
        }

        WriteJsonText(writer, safe);
        if (version >= ProtocolVersion.V1200) writer.WriteBoolean(false);                                     // 1.20：+ 动作栏
        else if (version >= ProtocolVersion.V1165) { writer.WriteByte(1); writer.WriteBytes(new byte[16]); }  // 1.16.5/1.18：+ 位置 + UUID
        else if (version >= ProtocolVersion.V108X) writer.WriteByte(1);                                       // 1.8/1.12.2：+ 位置
        return writer.ToArray();                                                                              // 1.7.6：只有 JSON
    }

    private static void WriteJsonText(PacketWriter writer, string text) => writer.WriteString(JsonSerializer.Serialize(new { text }));

    /// <summary>匿名 NBT（1.20.2+ 网络格式）：根节点不带名字，写 TAG_Compound { text: TAG_String }。</summary>
    private static void WriteNbtText(PacketWriter writer, string text)
    {
        using var nbt = new MemoryStream();
        nbt.WriteByte(0x0A); // TAG_Compound
        nbt.WriteByte(0x08); // TAG_String
        WriteNbtString(nbt, "text");
        WriteNbtString(nbt, text);
        nbt.WriteByte(0x00); // TAG_End
        writer.WriteBytes(nbt.ToArray());
    }

    private static void WriteNbtString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var length = Math.Min(bytes.Length, ushort.MaxValue); // NBT 字符串长度只有 2 字节，超长截断
        stream.WriteByte((byte)(length >> 8));
        stream.WriteByte((byte)(length & 0xFF));
        stream.Write(bytes, 0, length);
    }

    /// <summary>聊天栏是单行显示：换行/回车统一压成空格（空串给一个空格，避免空组件）。</summary>
    internal static string SingleLine(string? text)
    {
        if (string.IsNullOrEmpty(text)) return " ";
        return text.Contains('\r') || text.Contains('\n') ? text.Replace('\r', ' ').Replace('\n', ' ') : text;
    }
}

/// <summary>聊天室消息。Id/Sender/Text/IsIrc 与接口返回的 camelCase 字段一一对应。</summary>
internal sealed record IrcMessage(long Id, string Sender, string Text, bool IsIrc);

/// <summary>一次轮询的结果。</summary>
internal sealed record IrcPollResult(bool Success, IReadOnlyList<IrcMessage> Messages, int Online, bool Disabled)
{
    internal static readonly IrcPollResult Failure = new(false, [], 0, false);
}

/// <summary>一次发言的结果。</summary>
internal sealed record IrcSendResult(bool Success, string Message);

/// <summary>聊天室 HTTP 会话：登录 → 增量轮询 → 发送（Bearer 令牌 + X-HWID 头）。</summary>
internal sealed class IrcChatSession : IDisposable
{
    private sealed record LoginResponse(bool Success, string? SessionToken, string? Message);
    private sealed record SendResponse(bool Success, string? Message);
    private sealed record PollResponse(bool Success, IReadOnlyList<IrcMessage>? Messages, int Online, bool Disabled, string? Message);

    private readonly HttpClient _http;
    private readonly IrcChatOptions _options;
    private readonly string _hwid;
    private string? _token;
    private long _lastId;
    private string? _lastFailureKey;

    internal IrcChatSession(IrcChatOptions options, string hwid)
    {
        _options = options;
        _hwid = hwid;
        _http = new HttpClient { BaseAddress = new Uri(options.BaseUrl.Trim().TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(15) };
    }

    public void Dispose() => _http.Dispose();

    /// <summary>确保已登录；失败返回 false，重试节奏交给调用方。</summary>
    internal async Task<bool> EnsureLoginAsync(CancellationToken cancellationToken)
    {
        if (_token is not null) return true;
        try
        {
            using var request = CreateRequest(HttpMethod.Post, "api/auth/login",
                new { username = _options.Username, password = _options.Password }, authenticate: false);
            var (_, payload) = await PostAsync<LoginResponse>(request, cancellationToken).ConfigureAwait(false);
            if (payload is { Success: true, SessionToken.Length: > 0 })
            {
                _token = payload.SessionToken;
                _lastFailureKey = null;
                Log.Information("IRC: logged in as {Username}", _options.Username);
                return true;
            }
            WarnOnce("login|" + (payload?.Message ?? "请求失败。"), "IRC: login rejected: " + (payload?.Message ?? "请求失败。"));
            return false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 取消（会话结束）向上传递，其余失败按同一原因只告警一次。
            WarnOnce("login-ex|" + exception.Message, "IRC: login request failed", exception);
            return false;
        }
    }

    /// <summary>增量轮询。首次调用（lastId=0）服务端返回最近 50 条，是否展示由上层决定。</summary>
    internal async Task<IrcPollResult> PollAsync(CancellationToken cancellationToken)
    {
        if (_token is null) return IrcPollResult.Failure;
        try
        {
            using var request = CreateRequest(HttpMethod.Post, "api/chat/poll",
                new { lastId = Interlocked.Read(ref _lastId) }, authenticate: true);
            var (status, payload) = await PostAsync<PollResponse>(request, cancellationToken).ConfigureAwait(false);
            if (payload is not { Success: true })
            {
                if (status == HttpStatusCode.Unauthorized) _token = null;
                var reason = status == HttpStatusCode.Unauthorized ? "session expired" : payload?.Message ?? "请求失败。";
                WarnOnce("poll|" + reason, "IRC: poll rejected: " + reason);
                return IrcPollResult.Failure;
            }

            // 缺 id 的消息无法推进游标，直接跳过，避免整批丢失。
            var messages = (payload.Messages ?? []).Where(message => message.Id > 0).ToList();
            var maximumId = Math.Max(Interlocked.Read(ref _lastId), messages.Count == 0 ? 0 : messages.Max(message => message.Id));
            Interlocked.Exchange(ref _lastId, maximumId);
            _lastFailureKey = null;
            return new IrcPollResult(true, messages, payload.Online, payload.Disabled);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WarnOnce("poll-ex|" + exception.Message, "IRC: poll request failed", exception);
            return IrcPollResult.Failure;
        }
    }

    /// <summary>发送一条消息；文本加 /IRC 前缀，由服务端标记为 IRC 消息并清理前缀后入库。</summary>
    internal async Task<IrcSendResult> SendAsync(string text, CancellationToken cancellationToken)
    {
        if (!await EnsureLoginAsync(cancellationToken).ConfigureAwait(false))
            return new IrcSendResult(false, "尚未登录聊天室。");
        try
        {
            using var request = CreateRequest(HttpMethod.Post, "api/chat/send",
                new { text = "/IRC " + text, hwid = _hwid }, authenticate: true);
            var (status, payload) = await PostAsync<SendResponse>(request, cancellationToken).ConfigureAwait(false);
            if (payload is { Success: true }) return new IrcSendResult(true, string.Empty);
            if (status == HttpStatusCode.Unauthorized) _token = null;
            return new IrcSendResult(false, payload?.Message ?? "请求失败。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log.Warning(exception, "IRC: send request failed");
            return new IrcSendResult(false, exception.Message);
        }
    }

    /// <summary>统一构造请求：登录时还没有会话令牌，其余接口自动带上 Bearer 与 X-HWID。</summary>
    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object body, bool authenticate)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        if (!authenticate) return request;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.TryAddWithoutValidation("X-HWID", _hwid);
        return request;
    }

    private async Task<(HttpStatusCode Status, T? Payload)> PostAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return (response.StatusCode, string.IsNullOrWhiteSpace(text)
            ? default
            : JsonSerializer.Deserialize<T>(text, IrcChatOptions.JsonOptions));
    }

    /// <summary>同一原因连续失败只告警一次，避免断网或凭据错误时刷日志。</summary>
    private void WarnOnce(string key, string message, Exception? exception = null)
    {
        if (string.Equals(_lastFailureKey, key, StringComparison.Ordinal)) { Log.Debug("{Message}", message); return; }
        _lastFailureKey = key;
        if (exception is null) Log.Warning("{Message}", message);
        else Log.Warning(exception, "{Message}", message);
    }
}

/// <summary>
/// 游戏内聊天 ↔ 聊天室的桥：拦截 /IRC 转发聊天室，聊天室消息以系统聊天包注入游戏
/// （显示为 §b[§cES §a昵称§b]§f 内容）。每个代理会话一个实例，同账号多开互不影响。
/// </summary>
public sealed class IrcChatBridge : IAsyncDisposable
{
    private const string UsageHint = "输入 /IRC 内容 即可发送消息到聊天室";
    private const int ChatMessageMaximumLength = 256;
    private static readonly Regex ColorCode = new("§.", RegexOptions.Compiled);

    private readonly IrcChatOptions _options;
    private readonly IrcChatSession _session;
    private readonly ConcurrentDictionary<MinecraftConnection, byte> _connections = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recentLocalEcho = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _pumpGate = new();
    private Task? _pump;
    private string _displayName;
    private int _disposed;

    public IrcChatBridge(IrcChatOptions options, string hwid)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _displayName = options.Username;
        _session = new IrcChatSession(options, hwid);
    }

    /// <summary>把拦截器注册进会话协议注册表（由 ProxyOptions.ConfigureRegistry 调用）。</summary>
    public void AttachRegistry(PacketRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        foreach (var spec in IrcProtocol.Specs)
        {
            var versions = new[] { spec.Version };
            // 聊天包与命令包都是 /IRC 的来源：1.19+ 的普通命令、以及带签名的命令包。
            (int? Id, bool IsCommand)[] inbound = [(spec.ChatMessageId, false), (spec.ChatCommandId, true), (spec.SignedChatCommandId, true)];
            foreach (var (id, isCommand) in inbound)
                if (id is { } packetId)
                    registry.Register(ConnectionState.Play, PacketDirection.ServerBound, packetId,
                        (context, _) => HandleChatPacketAsync(context, isCommand), versions);
            registry.Register(ConnectionState.Play, PacketDirection.ClientBound, spec.JoinGameId,
                (context, _) => { RegisterConnection(context.Connection); return ValueTask.CompletedTask; }, versions);
        }
        Log.Information("IRC: interceptors attached for {VersionCount} protocol versions (account {Username})",
            IrcProtocol.Specs.Length, _options.Username);
    }

    /// <summary>绑定代理会话生命周期：会话停止或出错时自动停止轮询。</summary>
    public void BindSession(IProxySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Snapshot.State is ProxySessionState.Stopped or ProxySessionState.Faulted) { _ = DisposeAsync(); return; }
        session.EventOccurred += OnSessionEvent;
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return ValueTask.CompletedTask;
        try { _lifetime.Cancel(); }
        catch (Exception exception) { Log.Debug(exception, "IRC: cancelling lifetime failed"); } // 取消失败不影响后续释放
        _connections.Clear();
        _session.Dispose();
        return ValueTask.CompletedTask; // 不释放 CancellationTokenSource：轮询任务可能仍在等待，交给 GC 更安全
    }

    private void OnSessionEvent(object? sender, ProxyEventArgs args)
    {
        if (args.Value.Kind is not (ProxyEventKind.Stopped or ProxyEventKind.Faulted)) return;
        if (sender is IProxySession session) session.EventOccurred -= OnSessionEvent;
        Log.Information("IRC: proxy session ended, bridge stopped");
        _ = DisposeAsync();
    }

    private ValueTask HandleChatPacketAsync(PacketContext context, bool isCommandPacket)
    {
        string text;
        try { text = context.CreateReader().ReadString(ChatMessageMaximumLength); }
        catch (Exception) { return ValueTask.CompletedTask; } // 读不出来的包不是我们的目标，原样放行
        if (!IrcProtocol.TryParseIrcCommand(text, isCommandPacket, out var content)) return ValueTask.CompletedTask;

        // 是 /IRC：吞掉原件（不再发给游戏服务器），转交聊天室。
        context.Cancel();
        RegisterConnection(context.Connection);
        if (content.Length == 0) _ = InjectAsync(context.Connection, "用法：/IRC 内容（例：/IRC 大家好）");
        else _ = Task.Run(() => SendAndEchoAsync(context.Connection, content));
        return ValueTask.CompletedTask;
    }

    private async Task SendAndEchoAsync(MinecraftConnection connection, string content)
    {
        try
        {
            var result = await _session.SendAsync(content, _lifetime.Token).ConfigureAwait(false);
            if (!result.Success)
            {
                Log.Warning("IRC: message rejected: {Reason}", result.Message);
                await InjectAsync(connection, $"§c发送失败：{result.Message}").ConfigureAwait(false);
                return;
            }
            // 本地毫秒级回显；服务器回环的同一条消息会被去重跳过。广播给所有连接（局域网多开）。
            RememberLocalEcho(content);
            await BroadcastAsync(Format(_displayName, content)).ConfigureAwait(false);
            Log.Information("IRC: message sent: {Text}", content);
        }
        catch (OperationCanceledException) { } // 会话已结束
        catch (Exception exception)
        {
            Log.Warning(exception, "IRC: send failed");
            await InjectAsync(connection, "§c发送失败：" + exception.Message).ConfigureAwait(false);
        }
    }

    private void RegisterConnection(MinecraftConnection connection)
    {
        if (Volatile.Read(ref _disposed) != 0 || !_connections.TryAdd(connection, 0)) return;
        Log.Information("IRC: game client joined, chat bridge active");
        lock (_pumpGate)
            if (_pump is null && Volatile.Read(ref _disposed) == 0) _pump = Task.Run(() => PumpAsync(_lifetime.Token));
        _ = Task.Run(() => WelcomeAsync(connection));
    }

    private async Task WelcomeAsync(MinecraftConnection connection)
    {
        try
        {
            // 稍等一下再提示，避开进服瞬间的加载提示刷屏。
            await Task.Delay(TimeSpan.FromSeconds(2), _lifetime.Token).ConfigureAwait(false);
            await InjectAsync(connection, "§b[§cES§b]§f 已接入聊天室，" + UsageHint + "。").ConfigureAwait(false);
        }
        catch (Exception) { } // 会话结束或注入失败都不影响主流程
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        // 首次轮询只用来对齐游标（服务端会返回最近 50 条，直接展示会把历史消息倒进游戏）。
        var primed = false;
        var nextUsageHint = DateTimeOffset.UtcNow.AddSeconds(15);
        var nextOnlineHint = DateTimeOffset.UtcNow.AddSeconds(6);
        while (!cancellationToken.IsCancellationRequested)
        {
            var wait = _options.PollInterval;
            try
            {
                if (_connections.IsEmpty || !await _session.EnsureLoginAsync(cancellationToken).ConfigureAwait(false))
                    wait = _connections.IsEmpty ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(5); // 无人在线不打扰服务器；登录失败稍后重试
                else
                {
                    var poll = await _session.PollAsync(cancellationToken).ConfigureAwait(false);
                    if (!poll.Success) wait = TimeSpan.FromSeconds(3);
                    else if (poll.Disabled)
                    {
                        Log.Warning("IRC: chat account disabled, bridge stopped");
                        await BroadcastAsync("§c聊天室账号已被禁用，IRC 功能已停止。").ConfigureAwait(false);
                        return;
                    }
                    else
                    {
                        if (primed) await RelayAsync(poll.Messages).ConfigureAwait(false);
                        primed = true;
                        var now = DateTimeOffset.UtcNow;
                        if (_options.ShowUsageHint && now >= nextUsageHint)
                        { nextUsageHint = now.AddSeconds(15); await BroadcastAsync(UsageHint).ConfigureAwait(false); }
                        if (_options.ShowOnlineHint && now >= nextOnlineHint)
                        { nextOnlineHint = now.AddSeconds(6); await BroadcastAsync($"聊天室在线人数：{poll.Online}").ConfigureAwait(false); }
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Log.Warning(exception, "IRC: unexpected poll loop error");
                wait = TimeSpan.FromSeconds(5);
            }
            if (cancellationToken.IsCancellationRequested) break;
            await DelayAsync(wait, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>把一批聊天室消息注入游戏；自己的服务器回环只用来校准显示名，不再重复显示。</summary>
    private async Task RelayAsync(IReadOnlyList<IrcMessage> messages)
    {
        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Text)) continue;
            if (message.IsIrc)
            {
                LearnDisplayName(message.Sender);
                if (WasRecentlyEchoed(message.Text)) continue;
            }
            await BroadcastAsync(Format(message.Sender, message.Text)).ConfigureAwait(false);
        }
    }

    private async Task BroadcastAsync(string text)
    {
        foreach (var connection in _connections.Keys) await InjectAsync(connection, text).ConfigureAwait(false);
    }

    /// <summary>把一行文本注入游戏内聊天栏（系统聊天包）。</summary>
    private async Task InjectAsync(MinecraftConnection connection, string text)
    {
        try
        {
            // 只在双方都处于 Play 状态时注入：服务器切回 Configuration（重配置）时注入会让客户端收错包。
            if (connection.ClientState != ConnectionState.Play || connection.ServerState != ConnectionState.Play) return;
            if (IrcProtocol.TryGetSpec(connection.Version) is not { } spec) return;
            var payload = IrcProtocol.BuildSystemChatPayload(connection.Version, text);
            await connection.SendAsync(PacketDirection.ClientBound, spec.SystemChatId, payload, _lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { } // 会话已结束
        catch (Exception exception)
        {
            Log.Warning(exception, "IRC: failed to inject chat message, dropping connection");
            _connections.TryRemove(connection, out _);
        }
    }

    /// <summary>聊天室消息在游戏内的统一显示格式：§b[§cES §a昵称§b]§f 内容。</summary>
    private static string Format(string sender, string text)
    {
        var plain = IrcProtocol.SingleLine(text);
        return string.IsNullOrWhiteSpace(sender) ? plain : $"§b[§cES §a{sender.Trim()}§b]§f {plain}";
    }

    private void LearnDisplayName(string sender)
    {
        var name = sender.Trim();
        if (name.Length == 0 || string.Equals(name, _displayName, StringComparison.Ordinal)) return;
        _displayName = name;
        Log.Information("IRC: display name resolved to {Name}", name);
    }

    private void RememberLocalEcho(string content)
    {
        var now = DateTimeOffset.UtcNow;
        _recentLocalEcho[content] = now;
        if (_recentLocalEcho.Count <= 300) return;
        var stale = _recentLocalEcho.Where(pair => now - pair.Value > TimeSpan.FromSeconds(60)).Select(pair => pair.Key).ToArray();
        foreach (var key in stale) _recentLocalEcho.TryRemove(key, out _);
    }

    /// <summary>服务器回环的消息是否就是本机刚回显过的那条（剥掉颜色码后按内容包含比较）。</summary>
    private bool WasRecentlyEchoed(string serverText)
    {
        var plain = StripColors(serverText);
        var now = DateTimeOffset.UtcNow;
        return _recentLocalEcho.Any(pair => now - pair.Value <= TimeSpan.FromSeconds(60)
            && plain.Contains(pair.Key, StringComparison.Ordinal));
    }

    /// <summary>去掉 MC 颜色/格式控制码（§+单字符）。</summary>
    private static string StripColors(string text) => ColorCode.Replace(text, string.Empty);

    /// <summary>可取消的等待；被取消时静默返回，由轮询循环的条件结束整个任务。</summary>
    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try { await Task.Delay(delay, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { } // 会话已结束
    }
}
