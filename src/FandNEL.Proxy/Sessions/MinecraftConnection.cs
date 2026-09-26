using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using FandNEL.Proxy.Models;
using FandNEL.Proxy.Protocol;

namespace FandNEL.Proxy.Sessions;

/// <summary>一对 Minecraft 连接；登录认证与密钥仅属于当前连接。</summary>
public sealed class MinecraftConnection
{
    private readonly MinecraftPeer _client;
    private readonly MinecraftPeer _server;
    private readonly PacketRegistry _registry;
    private readonly ProxyOptions _options;
    private readonly Action<string> _onJoined;
    private int _clientState;
    private int _serverState;
    private int _version = -1;

    internal MinecraftConnection(Stream client, Stream server, ProxyOptions options,
        ServerTarget target, PlayerRole role, PacketRegistry registry, Action<string> onJoined)
    {
        _client = new MinecraftPeer(client);
        _server = new MinecraftPeer(server);
        _registry = registry;
        _options = options;
        _onJoined = onJoined;
        Target = target;
        Role = role;
    }

    public Guid Id { get; } = Guid.NewGuid();
    public ServerTarget Target { get; }
    public PlayerRole Role { get; }
    public ProtocolVersion Version
    {
        get => (ProtocolVersion)Volatile.Read(ref _version);
        internal set => Volatile.Write(ref _version, (int)value);
    }
    public ConnectionState ClientState
    {
        get => (ConnectionState)Volatile.Read(ref _clientState);
        internal set => Volatile.Write(ref _clientState, (int)value);
    }
    public ConnectionState ServerState
    {
        get => (ConnectionState)Volatile.Read(ref _serverState);
        internal set => Volatile.Write(ref _serverState, (int)value);
    }
    public Guid? PlayerUuid { get; internal set; }
    internal bool AddForgeHandshakeSuffix => _options.AddForgeHandshakeSuffix;

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        using (_client)
        using (_server)
        using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var upload = PumpAsync(_client, _server, PacketDirection.ServerBound, lifetime.Token);
            var download = PumpAsync(_server, _client, PacketDirection.ClientBound, lifetime.Token);
            try
            {
                await await Task.WhenAny(upload, download).ConfigureAwait(false);
            }
            finally
            {
                await lifetime.CancelAsync().ConfigureAwait(false);
                await ObserveCompletionAsync(upload, download, lifetime.Token).ConfigureAwait(false);
            }
        }
    }

    public Task SendAsync(PacketDirection direction, int packetId, ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        using var writer = new PacketWriter();
        var packet = writer.WriteVarInt(packetId).WriteBytes(payload.Span).ToArray();
        return (direction == PacketDirection.ServerBound ? _server : _client).WriteAsync(packet, cancellationToken);
    }

    private async Task PumpAsync(MinecraftPeer source, MinecraftPeer destination, PacketDirection direction,
        CancellationToken cancellationToken)
    {
        while (await source.ReadAsync(cancellationToken).ConfigureAwait(false) is { } packet)
        {
            var reader = new PacketReader(packet);
            var packetId = reader.ReadVarInt();
            if (packetId < 0)
                throw new InvalidDataException("Minecraft 包 ID 不能为负数。");
            var state = direction == PacketDirection.ServerBound ? ClientState : ServerState;
            var context = new PacketContext(this, direction, state, packetId, reader.ReadBytes(reader.Remaining));
            await _registry.DispatchAsync(context, cancellationToken).ConfigureAwait(false);
            if (!context.IsCancelled)
            {
                using var writer = new PacketWriter();
                await destination.WriteAsync(writer.WriteVarInt(packetId).WriteBytes(context.Payload.Span).ToArray(), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    internal void EnableServerCompression(int threshold) => _server.CompressionThreshold = threshold;
    internal void NotifyJoined(string username) => _onJoined(username);

    internal async Task AuthenticateAsync(string serverId, byte[] publicKey, byte[] verifyToken,
        bool shouldAuthenticate, CancellationToken cancellationToken)
    {
        var secret = RandomNumberGenerator.GetBytes(16);
        try
        {
            // ShouldAuthenticate 是原版包字段，Codexus 的远程服务始终负责最终进服认证。
            _ = shouldAuthenticate;
            var authenticate = _options.JoinServerAsync
                ?? throw new InvalidOperationException("当前通道未绑定 Codexus 远程进服认证服务。");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            hash.AppendData(Encoding.Latin1.GetBytes(serverId));
            hash.AppendData(secret);
            hash.AppendData(publicKey);
            var signedHash = new BigInteger(hash.GetHashAndReset(), isUnsigned: false, isBigEndian: true);
            var certification = (signedHash.Sign < 0 ? "-" : string.Empty)
                + BigInteger.Abs(signedHash).ToString("x").TrimStart('0');
            if (certification.Length == 0)
                certification = "0";
            // 认证异常直接结束当前连接，不能继续发送加密响应。
            await authenticate(certification, cancellationToken).ConfigureAwait(false);

            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            var encryptedSecret = rsa.Encrypt(secret, RSAEncryptionPadding.Pkcs1);
            var encryptedToken = rsa.Encrypt(verifyToken, RSAEncryptionPadding.Pkcs1);
            using var response = new PacketWriter();
            response.WriteVarInt(1);
            if (Version == ProtocolVersion.V1076)
                response.WriteUnsignedShort(checked((ushort)encryptedSecret.Length)).WriteBytes(encryptedSecret)
                    .WriteUnsignedShort(checked((ushort)encryptedToken.Length)).WriteBytes(encryptedToken);
            else
                response.WriteByteArray(encryptedSecret).WriteByteArray(encryptedToken);
            await _server.WriteAndEnableEncryptionAsync(response.ToArray(), secret, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static async Task ObserveCompletionAsync(Task upload, Task download, CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(upload, download).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 当任意方向关闭时取消另一个正在等待数据的方向。
        }
    }
}
