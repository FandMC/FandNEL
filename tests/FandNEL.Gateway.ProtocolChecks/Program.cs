using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FandNEL.Accounts;
using FandNEL.Core.Protocol;
using FandNEL.Core.Utils.Http;
using FandNEL.Gateway;
using FandNEL.Gateway.Management;

internal static class Program
{
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("codexus.today.websocket.establishing");
    private static readonly byte[] Info = Encoding.UTF8.GetBytes("codexus.today.aes.key");

    public static async Task<int> Main()
    {
        var checks = new ProtocolChecks();
        try
        {
            await checks.RunAsync().ConfigureAwait(false);
            Console.WriteLine("协议回归检查通过。");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"协议回归检查失败：{exception.Message}");
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            await checks.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class ProtocolChecks : IAsyncDisposable
    {
        private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "fandnel-protocol-checks", Guid.NewGuid().ToString("N"));
        private readonly Dictionary<string, List<string>> _requests = new(StringComparer.Ordinal);
        private readonly WPFLauncher _launcher;
        private readonly AccountService _accounts;
        private readonly GatewayRuntime _runtime;
        private readonly GameCatalogMessages _messages;
        private bool _disposed;

        public ProtocolChecks()
        {
            Directory.CreateDirectory(_dataDirectory);
            _launcher = CreateUninitializedLauncher(_requests);
            _accounts = new AccountService(_launcher, _dataDirectory);
            _runtime = new GatewayRuntime(_accounts, _launcher, new GameProxyService(_accounts));
            _runtime.JavaUsers.AddUser(new ManagedUser
            {
                UserId = "java-user",
                Authorized = true,
                AutoLogin = false,
                Channel = "cookie",
                Type = "cookie",
                Details = string.Empty,
                Alias = "测试账号"
            }, saveToDisk: false);
            _runtime.JavaUsers.AddUserToMaintain("java-user", "test-access-token");
            _messages = new GameCatalogMessages(_runtime, () => throw new InvalidOperationException("基岩版依赖不应被 Java 角色流程调用"));
        }

        public async Task RunAsync()
        {
            await CheckGameAliasesAsync().ConfigureAwait(false);
            await CheckMissingGameDoesNotUseAccountIdAsync().ConfigureAwait(false);
            await CheckRentalJavaIsolationAsync().ConfigureAwait(false);
            await CheckBedrockRequiresActivationAsync().ConfigureAwait(false);
            await CheckLocalWebSocketOrderAsync().ConfigureAwait(false);
        }

        private async Task CheckGameAliasesAsync()
        {
            foreach (var (field, gameId) in new[]
            {
                ("game", "game-primary"),
                ("game_id", "game-alias"),
                ("entity_id", "entity-alias"),
                ("item_id", "item-alias")
            })
            {
                _requests.Clear();
                var values = new Dictionary<string, string?>
                {
                    ["id"] = "java-user",
                    ["name"] = "角色",
                    ["game"] = field == "game" ? gameId : string.Empty,
                    ["type"] = "net_game",
                    [field] = gameId
                };
                var payload = JsonSerializer.Serialize(values);
                var response = await _messages.HandleAsync("create_role", payload, CancellationToken.None).ConfigureAwait(false);
                Assert(string.Equals(response as string, string.Empty, StringComparison.Ordinal), $"{field} 角色创建响应应为空字符串");
                var request = LastRequest("/game-character");
                using var body = JsonDocument.Parse(request);
                Assert(body.RootElement.GetProperty("game_id").GetString() == gameId, $"{field} 未映射到 game_id");
                Assert(body.RootElement.GetProperty("name").GetString() == "角色", $"{field} 角色名映射错误");
            }

            _requests.Clear();
            var nameAliasPayload = "{\"id\":\"java-user\",\"game\":\"name-alias-game\",\"role_name\":\"别名角色\",\"type\":\"net_game\"}";
            await _messages.HandleAsync("create_role", nameAliasPayload, CancellationToken.None).ConfigureAwait(false);
            using var nameBody = JsonDocument.Parse(LastRequest("/game-character"));
            Assert(nameBody.RootElement.GetProperty("name").GetString() == "别名角色", "role_name 别名未映射");
        }

        private async Task CheckMissingGameDoesNotUseAccountIdAsync()
        {
            _requests.Clear();
            var payload = "{\"id\":\"java-user\",\"name\":\"不会创建\",\"type\":\"net_game\"}";
            await AssertThrowsAsync<ArgumentException>(
                () => _messages.HandleAsync("create_role", payload, CancellationToken.None),
                "缺少游戏字段时必须返回明确错误").ConfigureAwait(false);
            Assert(!_requests.ContainsKey("/game-character"), "缺少游戏 ID 时不能调用创建接口，更不能回退到账号 id");

            _requests.Clear();
            var blankPayload = "{\"id\":\"java-user\",\"name\":\"不会创建\",\"game\":\"   \",\"entity_id\":\"\",\"type\":\"net_game\"}";
            await AssertThrowsAsync<ArgumentException>(
                () => _messages.HandleAsync("create_role", blankPayload, CancellationToken.None),
                "游戏字段全部为空时必须返回明确错误").ConfigureAwait(false);
            Assert(!_requests.ContainsKey("/game-character"), "空游戏 ID 时不能调用创建接口");
        }

        private async Task CheckRentalJavaIsolationAsync()
        {
            _requests.Clear();
            var payload = "{\"id\":\"java-user\",\"name\":\"租赁角色\",\"game\":\"rental-server\",\"type\":\"rental_game\"}";
            await _messages.HandleAsync("create_role", payload, CancellationToken.None).ConfigureAwait(false);
            var request = LastRequest("/rental-server-player");
            using var body = JsonDocument.Parse(request);
            Assert(body.RootElement.GetProperty("server_id").GetString() == "rental-server", "Java 租赁服务器 ID 映射错误");
            Assert(!_requests.ContainsKey("/game-character"), "租赁角色不能调用 Java 网络服角色接口");
        }

        private async Task CheckBedrockRequiresActivationAsync()
        {
            await AssertThrowsAsync<BedrockAccountNotActivatedException>(
                () => _messages.HandleAsync("pe_net_games", null, CancellationToken.None),
                "PE 网络服列表缺少激活账号时必须返回登录错误").ConfigureAwait(false);
            await AssertThrowsAsync<BedrockAccountNotActivatedException>(
                () => _messages.HandleAsync("pe_rental_games", "{}", CancellationToken.None),
                "PE 租赁服列表缺少激活账号时必须返回登录错误").ConfigureAwait(false);
            await AssertThrowsAsync<BedrockAccountNotActivatedException>(
                () => _messages.HandleAsync("g79_net_game_address", "{\"game\":\"pe-game\"}", CancellationToken.None),
                "PE 网络服地址查询缺少激活账号时必须返回登录错误").ConfigureAwait(false);
            await AssertThrowsAsync<BedrockAccountNotActivatedException>(
                () => _messages.HandleAsync("g79_rental_game_address", "{\"game\":\"pe-rental\"}", CancellationToken.None),
                "PE 租赁服地址查询缺少激活账号时必须返回登录错误").ConfigureAwait(false);
        }

        private async Task CheckLocalWebSocketOrderAsync()
        {
            _requests.Clear();
            var serverTask = _runtime.WebSocket.StartAsync();
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(_runtime.WebSocket.WebSocketAddress), CancellationToken.None).ConfigureAwait(false);

            using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            await SendPlainAsync(socket, new WireMessage("handshake", Convert.ToBase64String(clientKey.PublicKey.ExportSubjectPublicKeyInfo()), null, "protocol-check")).ConfigureAwait(false);
            var handshake = await ReceivePlainAsync(socket).ConfigureAwait(false);
            Assert(handshake.Type == "handshake", "握手响应类型错误");
            using var serverKey = ECDiffieHellman.Create();
            serverKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(handshake.Payload ?? string.Empty), out _);
            var sessionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256,
                clientKey.DeriveRawSecretAgreement(serverKey.PublicKey), 32, Salt, Info);

            var payload = "{\"id\":\"java-user\",\"name\":\"WebSocket角色\",\"game\":\"ws-game\",\"type\":\"net_game\"}";
            await SendEncryptedAsync(socket, new WireMessage("create_role", payload, null, "protocol-check"), sessionKey).ConfigureAwait(false);
            var received = new List<WireMessage>();
            for (var index = 0; index < 3; index++) received.Add(await ReceiveEncryptedAsync(socket, sessionKey).ConfigureAwait(false));
            Assert(received.Select(message => message.Type).SequenceEqual(new[] { "success_notification", "create_role", "get_roles" }), "create_role 响应顺序必须为成功通知、空响应、角色列表");
            using var roles = JsonDocument.Parse(received[2].Payload ?? string.Empty);
            Assert(roles.RootElement.ValueKind == JsonValueKind.Array && roles.RootElement.GetArrayLength() == 1, "get_roles 响应应保持角色数组格式");
            Assert(roles.RootElement[0].GetProperty("name").GetString() == "角色-网络", "get_roles 返回角色内容错误");

            // 服务端收到关闭帧后会立即关闭连接；这里直接中止客户端，避免烟测依赖第二次关闭握手。
            socket.Abort();
            Assert(serverTask is not null, "服务器启动任务未返回");
        }

        private string LastRequest(string path)
        {
            if (!_requests.TryGetValue(path, out var requests) || requests.Count == 0)
                throw new InvalidOperationException($"未观察到请求 {path}");
            return requests[^1];
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            try { await _runtime.DisposeAsync().ConfigureAwait(false); } catch { }
            foreach (var field in typeof(WPFLauncher).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (field.GetValue(_launcher) is IDisposable disposable) disposable.Dispose();
            try { Directory.Delete(_dataDirectory, recursive: true); } catch { }
        }
    }

    private static WPFLauncher CreateUninitializedLauncher(Dictionary<string, List<string>> requests)
    {
        var launcher = (WPFLauncher)RuntimeHelpers.GetUninitializedObject(typeof(WPFLauncher));
        var game = new HttpWrapper("https://fake-game", null, new RoutingHandler(requests));
        var rental = new HttpWrapper("https://fake-rental", null, new RoutingHandler(requests));
        SetField(launcher, "_game", game);
        SetField(launcher, "_rental", rental);
        return launcher;
    }

    private static void SetField(object target, string name, object value) =>
        (target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().FullName, name)).SetValue(target, value);

    private sealed class RoutingHandler(Dictionary<string, List<string>> requests) : HttpClientHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? string.Empty;
            lock (requests)
            {
                if (!requests.TryGetValue(path, out var values)) requests[path] = values = [];
                values.Add(body);
            }

            var responseBody = path switch
            {
                "/game-character/query/user-game-characters" => "{\"code\":0,\"message\":\"\",\"entities\":[{\"name\":\"角色-网络\"}],\"total\":1}",
                "/rental-server-player/query/search-by-user-server" => "{\"code\":0,\"message\":\"\",\"entities\":[],\"total\":0}",
                "/rental-server-player" => "{\"code\":0,\"message\":\"\",\"entity\":{}}",
                _ => "{\"code\":0,\"message\":\"\",\"entity\":{}}"
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed record WireMessage(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("payload")] string? Payload,
        [property: JsonPropertyName("sign")] string? Sign,
        [property: JsonPropertyName("identify")] string? Identify);

    private static async Task SendPlainAsync(ClientWebSocket socket, WireMessage message)
    {
        var json = JsonSerializer.Serialize(new { type = message.Type, payload = message.Payload, sign = message.Sign, identify = message.Identify });
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<WireMessage> ReceivePlainAsync(ClientWebSocket socket)
    {
        var bytes = await ReceiveBytesAsync(socket).ConfigureAwait(false);
        return JsonSerializer.Deserialize<WireMessage>(bytes) ?? throw new InvalidDataException("握手响应为空");
    }

    private static async Task SendEncryptedAsync(ClientWebSocket socket, WireMessage message, byte[] key)
    {
        var json = JsonSerializer.Serialize(new { type = message.Type, payload = message.Payload, sign = message.Sign, identify = message.Identify });
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(json), 0, Encoding.UTF8.GetByteCount(json));
        await socket.SendAsync(aes.IV.Concat(cipher).ToArray(), WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<WireMessage> ReceiveEncryptedAsync(ClientWebSocket socket, byte[] key)
    {
        var encrypted = await ReceiveBytesAsync(socket).ConfigureAwait(false);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = encrypted[..16];
        using var decryptor = aes.CreateDecryptor();
        var plain = decryptor.TransformFinalBlock(encrypted, 16, encrypted.Length - 16);
        return JsonSerializer.Deserialize<WireMessage>(plain) ?? throw new InvalidDataException("加密响应为空");
    }

    private static async Task<byte[]> ReceiveBytesAsync(ClientWebSocket socket)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("WebSocket 提前关闭");
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return stream.ToArray();
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> operation, string message)
        where TException : Exception
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
