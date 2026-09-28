using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.V1206;
using System.Numerics;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using FandNEL.Proxy.Models;
using FandNEL.Proxy.Heypixel;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.Minecraft.Nbt;
using Serilog;

namespace FandNEL.Proxy.Sessions;

/// <summary>一个客户端 channel 与一个服务端 channel 的协议协调器。</summary>
public sealed class MinecraftConnection
{
    private readonly IChannel _client;
    private readonly IEventLoopGroup _workerGroup;
    private readonly PacketRegistry _registry;
    private readonly ProxyOptions _options;
    private readonly Lazy<HeypixelConnection> _heypixel;
    private readonly Action<string> _onJoined;
    private readonly Action<Exception> _onFailed;
    private readonly CancellationTokenSource _connectionLifetime = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _failureReported;
    private int _clientState;
    private int _serverState;
    private int _version = -1;
    private int _closed;

    internal MinecraftConnection(IChannel client, IEventLoopGroup workerGroup, ProxyOptions options,
        ServerTarget target, PlayerRole role, PacketRegistry registry, Action<string> onJoined,
        Action<Exception> onFailed)
    {
        _client = client;
        _workerGroup = workerGroup;
        _registry = registry;
        _options = options;
        _heypixel = new Lazy<HeypixelConnection>(() => new HeypixelConnection(this));
        _onJoined = onJoined;
        _onFailed = onFailed;
        Target = target;
        Role = role;
    }

    public Guid Id { get; } = Guid.NewGuid();
    public ServerTarget Target { get; }
    public PlayerRole Role { get; }
    internal IChannel ClientChannel => _client;
    internal ProxyOptions Options => _options;
    internal CancellationToken LifetimeToken => _connectionLifetime.Token;
    internal HeypixelConnection Heypixel => _heypixel.Value;
    internal bool IsClosed => Volatile.Read(ref _closed) != 0 || !_client.Active;
    public IChannel? ServerChannel { get; private set; }
    public ProtocolVersion Version { get => (ProtocolVersion)Volatile.Read(ref _version); internal set => Volatile.Write(ref _version, (int)value); }
    public ConnectionState ClientState { get => (ConnectionState)Volatile.Read(ref _clientState); internal set => Volatile.Write(ref _clientState, (int)value); }
    public ConnectionState ServerState { get => (ConnectionState)Volatile.Read(ref _serverState); internal set => Volatile.Write(ref _serverState, (int)value); }
    public Guid? PlayerUuid { get; internal set; }
    public string? PlayerName { get; internal set; }
    internal bool AddForgeHandshakeSuffix => _options.AddForgeHandshakeSuffix;

    internal async Task PrepareAsync(CancellationToken cancellationToken)
    {
            Log.Debug("Proxy connection {ConnectionId}: connecting to {TargetHost}:{TargetPort} (client={ClientAddress})", Id, Target.Host, Target.Port, _client.RemoteAddress);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connectionLifetime.Token);
        timeout.CancelAfter(_options.ConnectTimeout);
        Socks5ClientHandler? socksHandler = null;
        var bootstrap = new Bootstrap()
            .Group(_workerGroup)
            .Channel<TcpSocketChannel>()
            .Option(ChannelOption.TcpNodelay, true)
            .Option(ChannelOption.SoKeepalive, true)
            .Option(ChannelOption.Allocator, PooledByteBufferAllocator.Default)
            .Option(ChannelOption.ConnectTimeout, _options.ConnectTimeout)
            .Handler(new ActionChannelInitializer<IChannel>(channel =>
            {
                if (_options.Socks5 is null)
                    ConfigurePipeline(channel, PacketDirection.ClientBound);
                else
                {
                    socksHandler = new Socks5ClientHandler(this, Target, _options.Socks5);
                    channel.Pipeline.AddLast("socks5", socksHandler);
                }
            }));
        Task<IChannel>? connectOperation = null;
        try
        {
            var endpoint = _options.Socks5 is { } socks5
                ? new DnsEndPoint(socks5.Host, socks5.Port)
                : new DnsEndPoint(Target.Host, Target.Port);
            connectOperation = bootstrap.ConnectAsync(endpoint);
            ServerChannel = await connectOperation.WaitAsync(timeout.Token).ConfigureAwait(false);
            Log.Debug("Proxy connection {ConnectionId}: target TCP connected (server={ServerAddress})", Id, ServerChannel.RemoteAddress);
            if (socksHandler is not null)
            {
                await socksHandler.Completed.WaitAsync(timeout.Token).ConfigureAwait(false);
            }
            if (Volatile.Read(ref _closed) == 0 && ServerChannel.Active)
            {
                _ready.TrySetResult();
                _client.Configuration.AutoRead = true;
                _client.Read();
            }
            else
                _ready.TrySetCanceled();
            if (Volatile.Read(ref _closed) != 0)
                await ServerChannel.CloseAsync().ConfigureAwait(false);
        }
        catch
        {
            _ready.TrySetCanceled();
            if (connectOperation is not null && ServerChannel is null)
                _ = CloseWhenConnectedAsync(connectOperation);
            throw;
        }
    }

    internal async Task CloseAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        _connectionLifetime.Cancel();
        _ready.TrySetCanceled();
        var server = ServerChannel;
        try { await _client.CloseAsync().ConfigureAwait(false); } catch { }
        if (server is not null) { try { await server.CloseAsync().ConfigureAwait(false); } catch { } }
    }

    private static async Task CloseWhenConnectedAsync(Task<IChannel> connectOperation)
    {
        try
        {
            var channel = await connectOperation.ConfigureAwait(false);
            await channel.CloseAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    internal void ConfigurePipeline(IChannel channel, PacketDirection direction)
    {
        if (channel.Pipeline.Get("frame-decoder") is not null)
            return;
        channel.Pipeline
            .AddLast("frame-decoder", new MinecraftFrameDecoder(this, direction))
            .AddLast("packet-handler", direction == PacketDirection.ServerBound
                ? new MinecraftClientChannelHandler(this)
                : new MinecraftServerChannelHandler(this))
            .AddLast("frame-encoder", new MinecraftFrameEncoder());
    }

    internal async Task SendAsync(PacketDirection direction, int packetId, ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        var channel = direction == PacketDirection.ServerBound ? ServerChannel : _client;
        if (channel is null || !channel.Active) throw new IOException("Minecraft 对端连接已经关闭。");
        var buffer = channel.Allocator.Buffer(payload.Length + 5);
        MinecraftFrameEncoder.WriteVarInt(buffer, packetId);
        buffer.WriteBytes(payload.ToArray());
        await channel.WriteAndFlushAsync(buffer).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    internal void EnableServerCompression(int threshold) => ConfigureCompression(ServerChannel, threshold);

    internal void EnableClientCompression(int threshold)
    {
        ConfigureCompression(_client, threshold);
    }

    internal IChannel GetDestination(PacketDirection direction) =>
        direction == PacketDirection.ServerBound
            ? ServerChannel ?? throw new IOException("Minecraft 服务端连接已经关闭。")
            : _client;

    private static void ConfigureCompression(IChannel? channel, int threshold)
    {
        if (channel is null) return;
        if (channel.Pipeline.Get("frame-decoder") is not MinecraftFrameDecoder decoder)
            throw new InvalidOperationException("Minecraft 解码 pipeline 尚未就绪，无法切换压缩状态。");
        decoder.CompressionThreshold = threshold;
        if (threshold < 0)
        {
            if (channel.Pipeline.Get("compress") is not null)
                channel.Pipeline.Remove("compress");
            return;
        }
        if (channel.Pipeline.Get("compress") is MinecraftCompressionEncoder encoder)
            encoder.Threshold = threshold;
        else
            channel.Pipeline.AddAfter("frame-encoder", "compress", new MinecraftCompressionEncoder(threshold));
    }

    internal async Task AuthenticateAsync(string serverId, byte[] publicKey, byte[] verifyToken,
        bool shouldAuthenticate, CancellationToken cancellationToken)
    {
        _ = shouldAuthenticate;
        var secret = RandomNumberGenerator.GetBytes(16);
        try
        {
            Log.Debug("Proxy connection {ConnectionId}: Minecraft authentication started (version={Version}, serverIdLength={ServerIdLength}, authenticate={Authenticate})", Id, Version, serverId.Length, shouldAuthenticate);
            var authenticate = _options.JoinServerAsync ?? throw new InvalidOperationException("当前通道未绑定 Codexus 远程进服认证服务。");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            hash.AppendData(Encoding.Latin1.GetBytes(serverId));
            hash.AppendData(secret);
            hash.AppendData(publicKey);
            var signedHash = new BigInteger(hash.GetHashAndReset(), isUnsigned: false, isBigEndian: true);
            var certification = (signedHash.Sign < 0 ? "-" : string.Empty) + BigInteger.Abs(signedHash).ToString("x").TrimStart('0');
            await authenticate(certification.Length == 0 ? "0" : certification, cancellationToken).ConfigureAwait(false);
            Log.Debug("Proxy connection {ConnectionId}: remote authentication accepted", Id);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            var encryptedSecret = rsa.Encrypt(secret, RSAEncryptionPadding.Pkcs1);
            var encryptedToken = rsa.Encrypt(verifyToken, RSAEncryptionPadding.Pkcs1);
            byte[] response;
            if (Version == ProtocolVersion.V1076)
            {
                using var legacy = new PacketWriter();
                legacy.WriteUnsignedShort(checked((ushort)encryptedSecret.Length)).WriteBytes(encryptedSecret)
                    .WriteUnsignedShort(checked((ushort)encryptedToken.Length)).WriteBytes(encryptedToken);
                response = legacy.ToArray();
            }
            else
                response = new EncryptionResponsePacket(encryptedSecret, encryptedToken).Write();
            var channel = ServerChannel ?? throw new IOException("服务端连接已经关闭。");
            await SendAsync(PacketDirection.ServerBound, MinecraftPacketIds.Login.ServerboundEncryptionResponse, response, cancellationToken).ConfigureAwait(false);
            channel.Pipeline.AddBefore("frame-decoder", "decrypt", new MinecraftEncryptionDecoder(secret));
            // 与参考实现一致：加密插在分帧编码器之前，出站逆序为压缩 -> 分帧 -> 加密。
            channel.Pipeline.AddBefore("frame-encoder", "encrypt", new MinecraftEncryptionEncoder(secret));
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    internal void NotifyJoined(string username) => _onJoined(username);

    internal ValueTask DispatchAsync(PacketContext context) => _registry.DispatchAsync(context, CancellationToken.None);

    internal Task WaitUntilReadyAsync() => _ready.Task;

    internal Task FailAsync(Exception exception)
    {
        if (Volatile.Read(ref _closed) != 0)
            return Task.CompletedTask;
        if (Interlocked.Exchange(ref _failureReported, 1) == 0)
        {
            Log.Error(exception, "Proxy connection {ConnectionId}: failed (clientState={ClientState}, serverState={ServerState}, version={Version})", Id, ClientState, ServerState, Version);
            _onFailed(exception);
        }
        return FailAndCloseAsync(exception);
    }

    private async Task FailAndCloseAsync(Exception exception)
    {
        try
        {
            if (ClientState == ConnectionState.Login && _client.Active)
            {
                var message = GetClientErrorMessage(exception);
                Log.Information("Proxy connection {ConnectionId}: sending login disconnect to client: {Reason}", Id, message);
                await SendAsync(PacketDirection.ClientBound, GetLoginDisconnectPacketId(), LoginDisconnectPacket.FromText(message).Write()).ConfigureAwait(false);
            }
            else if (HeypixelProtocol.IsEnabled(this) && Version == ProtocolVersion.V1206 && _client.Active
                && ClientState is ConnectionState.Configuration or ConnectionState.Play)
            {
                var packet = new DisconnectPacket(new NbtCompound().Set("text", new NbtString(GetClientErrorMessage(exception))));
                var packetId = ClientState == ConnectionState.Configuration
                    ? MinecraftPacketIds.Configuration.ClientboundDisconnect : MinecraftPacketIds.Clientbound.Disconnect;
                await SendAsync(PacketDirection.ClientBound, packetId, packet.Write()).ConfigureAwait(false);
            }
        }
        catch (Exception sendException)
        {
            Log.Error(sendException, "Proxy connection {ConnectionId}: failed to send login disconnect", Id);
            _onFailed(new IOException($"发送登录失败原因时连接已关闭：{sendException.Message}", sendException));
        }
        finally
        {
            Log.Information("Proxy connection {ConnectionId}: closing client and target channels", Id);
            await CloseAsync().ConfigureAwait(false);
        }
    }

    private int GetLoginDisconnectPacketId() => Version switch
    {
        // 1.13-pre3 through 1.13-pre5 temporarily shifted login packet IDs.
        // These snapshots are not represented by the supported protocol enum.
        _ => 0
    };


    private static string GetClientErrorMessage(Exception exception)
    {
        var message = exception is HttpRequestException http && !string.IsNullOrWhiteSpace(http.Message)
            ? http.Message
            : exception.Message;
        message = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return message.Length <= 512 ? message : message[..512];
    }
}

internal abstract class MinecraftPacketHandler(MinecraftConnection connection, PacketDirection direction) : ChannelHandlerAdapter
{
    private readonly SemaphoreSlim _dispatchLock = new(1, 1);

    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        if (message is not IByteBuffer buffer)
        {
            context.FireChannelRead(message);
            return;
        }
        try
        {
            var bytes = new byte[buffer.ReadableBytes];
            buffer.GetBytes(buffer.ReaderIndex, bytes);
            _ = DispatchAsync(context, bytes);
        }
        finally { buffer.Release(); }
    }

    private async Task DispatchAsync(IChannelHandlerContext context, byte[] packet)
    {
        await _dispatchLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await connection.WaitUntilReadyAsync().ConfigureAwait(false);
            var reader = new PacketReader(packet);
            var packetId = reader.ReadVarInt();
            var state = direction == PacketDirection.ServerBound ? connection.ClientState : connection.ServerState;
            var packetContext = new PacketContext(connection, direction, state, packetId, reader.ReadBytes(reader.Remaining));
            await connection.DispatchAsync(packetContext).ConfigureAwait(false);
            if (packetContext.IsCancelled) return;
            var destination = connection.GetDestination(direction);
            var output = destination.Allocator.Buffer(packetContext.Payload.Length + 5);
            MinecraftFrameEncoder.WriteVarInt(output, packetId);
            output.WriteBytes(packetContext.Payload.ToArray());
            await destination.WriteAndFlushAsync(output).ConfigureAwait(false);
            await packetContext.CompleteForwardAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await connection.FailAsync(exception).ConfigureAwait(false);
        }
        finally
        {
            _dispatchLock.Release();
        }
    }

    public override void ChannelInactive(IChannelHandlerContext context)
    {
        _ = connection.CloseAsync();
        context.FireChannelInactive();
    }

    public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
    {
        _ = connection.FailAsync(exception);
        _ = context.CloseAsync();
    }
}

internal sealed class MinecraftClientChannelHandler(MinecraftConnection connection)
    : MinecraftPacketHandler(connection, PacketDirection.ServerBound);

internal sealed class MinecraftServerChannelHandler(MinecraftConnection connection)
    : MinecraftPacketHandler(connection, PacketDirection.ClientBound);

internal sealed class Socks5ClientHandler(MinecraftConnection connection, ServerTarget target, Socks5Options options) : ChannelHandlerAdapter
{
    private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<byte> _pending = [];
    private int _stage;
    public Task Completed => _completed.Task;

    public override void ChannelActive(IChannelHandlerContext context)
    {
        var methods = options.Username is null ? new byte[] { 0x00 } : new byte[] { 0x00, 0x02 };
        Write(context, new byte[] { 0x05, (byte)methods.Length }.Concat(methods).ToArray());
        _stage = 1;
    }

    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        if (message is not IByteBuffer buffer)
        {
            context.FireChannelRead(message);
            return;
        }
        try
        {
            var data = new byte[buffer.ReadableBytes]; buffer.GetBytes(buffer.ReaderIndex, data); _pending.AddRange(data);
            Parse(context);
        }
        catch (Exception exception) { _completed.TrySetException(exception); context.CloseAsync(); }
        finally { buffer.Release(); }
    }

    private void Parse(IChannelHandlerContext context)
    {
        while (!_completed.Task.IsCompleted)
        {
            if (_stage == 1)
            {
                if (_pending.Count < 2) return;
                if (_pending[0] != 5) throw new InvalidDataException("SOCKS5 版本无效。");
                var method = _pending[1]; Consume(2);
                if (method == 0xff) throw new InvalidDataException("SOCKS5 代理拒绝了认证方式。");
                if (method == 2)
                {
                    if (options.Username is null)
                        throw new InvalidDataException("SOCKS5 代理选择了未提供的用户名密码认证方式。");
                    var user = Encoding.UTF8.GetBytes(options.Username ?? string.Empty); var pass = Encoding.UTF8.GetBytes(options.Password ?? string.Empty);
                    if (user.Length > 255 || pass.Length > 255) throw new InvalidDataException("SOCKS5 用户名或密码过长。");
                    Write(context, new byte[] { 1, (byte)user.Length }.Concat(user).Concat(new[] { (byte)pass.Length }).Concat(pass).ToArray()); _stage = 2;
                }
                else if (method == 0) WriteConnect(context);
                else throw new InvalidDataException("SOCKS5 返回了不支持的认证方式。");
            }
            else if (_stage == 2)
            {
                if (_pending.Count < 2) return;
                if (_pending[0] != 1) throw new InvalidDataException("SOCKS5 认证响应版本无效。");
                var status = _pending[1]; Consume(2);
                if (status != 0) throw new InvalidDataException("SOCKS5 用户名密码认证失败。");
                WriteConnect(context);
            }
            else if (_stage == 3)
            {
                if (_pending.Count < 4) return;
                if (_pending[0] != 5 || _pending[2] != 0)
                    throw new InvalidDataException("SOCKS5 连接响应格式无效。");
                var addressLength = _pending[3] switch { 1 => 4, 4 => 16, 3 => _pending.Count >= 5 ? 1 + _pending[4] : -1, _ => throw new InvalidDataException("SOCKS5 返回了未知地址类型。") };
                if (addressLength < 0 || _pending.Count < 4 + addressLength + 2) return;
                var status = _pending[1]; Consume(4 + addressLength + 2);
                if (status != 0) throw new InvalidDataException("SOCKS5 连接目标失败。");
                var trailingBytes = _pending.ToArray();
                _pending.Clear();
                context.Channel.Pipeline.Remove(this);
                connection.ConfigurePipeline(context.Channel, PacketDirection.ClientBound);
                if (trailingBytes.Length > 0)
                {
                    var trailing = context.Allocator.Buffer(trailingBytes.Length);
                    trailing.WriteBytes(trailingBytes);
                    context.Channel.Pipeline.FireChannelRead(trailing);
                }
                _completed.TrySetResult();
            }
            else return;
        }
    }

    private void Consume(int count) => _pending.RemoveRange(0, count);

    private void WriteConnect(IChannelHandlerContext context)
    {
        var ip = IPAddress.TryParse(target.Host, out var parsed)
            ? parsed.GetAddressBytes()
            : Encoding.ASCII.GetBytes(new IdnMapping().GetAscii(target.Host));
        var type = parsed is null ? (byte)3 : parsed.AddressFamily == AddressFamily.InterNetwork ? (byte)1 : (byte)4;
        if (type == 3 && ip.Length > byte.MaxValue)
            throw new InvalidDataException("SOCKS5 目标域名过长。");
        var bytes = new List<byte> { 5, 1, 0, type };
        if (type == 3) bytes.Add((byte)ip.Length);
        bytes.AddRange(ip);
        bytes.Add((byte)(target.Port >> 8));
        bytes.Add((byte)target.Port);
        Write(context, bytes.ToArray());
        _stage = 3;
    }

    private void Write(IChannelHandlerContext context, byte[] bytes)
    {
        var buffer = context.Allocator.Buffer(bytes.Length);
        buffer.WriteBytes(bytes);
        _ = ObserveWriteAsync(context, context.WriteAndFlushAsync(buffer));
    }

    private async Task ObserveWriteAsync(IChannelHandlerContext context, Task write)
    {
        try
        {
            await write.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _completed.TrySetException(exception);
            await context.CloseAsync().ConfigureAwait(false);
        }
    }

    public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
    {
        _completed.TrySetException(exception);
        _ = context.CloseAsync();
    }

    public override void ChannelInactive(IChannelHandlerContext context)
    {
        _completed.TrySetException(new IOException("SOCKS5 代理在握手完成前关闭了连接。"));
        context.FireChannelInactive();
    }
}
