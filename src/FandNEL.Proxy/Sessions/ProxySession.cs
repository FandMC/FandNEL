using System.Collections.Concurrent;
using System.Net;
using DotNetty.Buffers;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using FandNEL.Proxy.Models;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Services;

namespace FandNEL.Proxy.Sessions;

/// <summary>基于 DotNetty 的 Minecraft 代理会话。</summary>
public sealed class ProxySession : IProxySession
{
    private readonly object _stateLock = new();
    private readonly ConcurrentDictionary<IChannelId, MinecraftConnection> _connections = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ProxyOptions _initialOptions;
    private readonly IEventLoopGroup _workerGroup;
    private IChannel? _listener;
    private LanDiscoveryBroadcaster? _lanDiscovery;
    private ServerTarget _target;
    private PlayerRole _role;
    private ProxySessionState _state = ProxySessionState.Created;
    private IPEndPoint? _localEndpoint;
    private DateTimeOffset? _stoppedAt;
    private string? _lastError;
    private int _activeConnections;
    private PacketRegistry? _registry;

    internal ProxySession(ProxyOptions options, IEventLoopGroup workerGroup)
    {
        options.Validate();
        _initialOptions = options;
        _workerGroup = workerGroup;
        _target = options.Target;
        _role = options.Role;
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }
    public PacketRegistry Registry => _registry ?? throw new InvalidOperationException("代理会话尚未启动。");
    public event EventHandler<ProxyEventArgs>? EventOccurred;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public ProxySessionSnapshot Snapshot
    {
        get
        {
            lock (_stateLock)
                return new ProxySessionSnapshot(Id, _state, _localEndpoint, _target, _role,
                    Volatile.Read(ref _activeConnections), _startedAt, _stoppedAt,
                    _initialOptions.UserId, _initialOptions.GameId, _initialOptions.GameVersion,
                    _initialOptions.RentalServerId, _lastError);
        }
    }

    internal async Task StartAsync(IEventLoopGroup acceptorGroup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_stateLock)
        {
            if (_state != ProxySessionState.Created) throw new InvalidOperationException("代理会话已经启动。");
            _registry = new PacketRegistry();
        }
        _initialOptions.ConfigureRegistry?.Invoke(_registry);
        var bootstrap = new ServerBootstrap()
            .Group(acceptorGroup, _workerGroup)
            .Channel<TcpServerSocketChannel>()
            .Option(ChannelOption.SoReuseaddr, true)
            .Option(ChannelOption.TcpNodelay, true)
            .Option(ChannelOption.SoKeepalive, true)
            .Option(ChannelOption.Allocator, PooledByteBufferAllocator.Default)
            .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                channel.Pipeline.AddLast("connection", new ProxyClientHandler(this))));
        try
        {
            _listener = await bootstrap.BindAsync(new IPEndPoint(_initialOptions.ListenAddress, _initialOptions.ListenPort)).ConfigureAwait(false);
            _localEndpoint = (IPEndPoint)_listener.LocalAddress;
            lock (_stateLock) _state = ProxySessionState.Running;
            if (_initialOptions.EnableLanBroadcast)
                _lanDiscovery = new LanDiscoveryBroadcaster(_target, _role, _localEndpoint.Port, _initialOptions.LanMotd);
            Publish(ProxyEventKind.Started);
        }
        catch (Exception exception)
        {
            SetFault(exception);
            throw;
        }
    }

    public Task UpdateServerAsync(ServerTarget target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(target); target.Validate();
        lock (_stateLock) { EnsureRunning(); _target = target; }
        Publish(ProxyEventKind.ServerChanged, $"目标服务器已切换为 {target.Host}:{target.Port}。");
        return Task.CompletedTask;
    }

    public Task UpdateRoleAsync(PlayerRole role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(role);
        if (string.IsNullOrWhiteSpace(role.Name)) throw new ArgumentException("角色名不能为空。", nameof(role));
        lock (_stateLock) { EnsureRunning(); _role = role; }
        Publish(ProxyEventKind.RoleChanged, $"当前角色已切换为 {role.Name}。");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_state is ProxySessionState.Stopped or ProxySessionState.Stopping) return;
            _state = ProxySessionState.Stopping;
        }
        _lifetime.Cancel();
        if (_lanDiscovery is not null) { await _lanDiscovery.DisposeAsync().ConfigureAwait(false); _lanDiscovery = null; }
        var listener = _listener; if (listener is not null) await listener.CloseAsync().ConfigureAwait(false);
        var connections = _connections.Values.ToArray();
        await Task.WhenAll(connections.Select(static connection => connection.CloseAsync())).ConfigureAwait(false);
        foreach (var connection in connections)
            RemoveClient(connection.ClientChannel);
        lock (_stateLock) { _state = ProxySessionState.Stopped; _stoppedAt = DateTimeOffset.UtcNow; }
        Publish(ProxyEventKind.Stopped);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async ValueTask DisposeAsync() { await StopAsync().ConfigureAwait(false); _lifetime.Dispose(); }

    internal async Task AcceptClientAsync(IChannel channel, CancellationToken cancellationToken)
    {
        MinecraftConnection connection;
        lock (_stateLock)
        {
            if (_state != ProxySessionState.Running)
            {
                _ = channel.CloseAsync();
                return;
            }

            connection = new MinecraftConnection(channel, _workerGroup, _initialOptions, _target, _role,
                _registry ?? throw new InvalidOperationException("协议注册表尚未初始化。"),
                username => Publish(ProxyEventKind.JoinServer, $"{username} 已进入服务器。"),
                exception => Publish(ProxyEventKind.ConnectionFailed, exception.Message));
            if (!_connections.TryAdd(channel.Id, connection))
            {
                _ = channel.CloseAsync();
                return;
            }
            Interlocked.Increment(ref _activeConnections);
        }

        Publish(ProxyEventKind.ClientConnected);
        try
        {
            connection.ConfigurePipeline(channel, PacketDirection.ServerBound);
            await connection.PrepareAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await connection.CloseAsync().ConfigureAwait(false);
            RemoveClient(channel);
        }
        catch (Exception exception) { await connection.FailAsync(exception).ConfigureAwait(false); }
    }

    internal void RemoveClient(IChannel channel)
    {
        if (_connections.TryRemove(channel.Id, out var connection))
        {
            _ = connection.CloseAsync();
            Interlocked.Decrement(ref _activeConnections);
            Publish(ProxyEventKind.ClientDisconnected);
        }
    }

    private void EnsureRunning() { if (_state != ProxySessionState.Running) throw new InvalidOperationException("代理会话当前不可更新。"); }
    private void SetFault(Exception exception)
    {
        lock (_stateLock) { if (_state is ProxySessionState.Stopped or ProxySessionState.Stopping) return; _state = ProxySessionState.Faulted; _lastError = exception.Message; }
        Publish(ProxyEventKind.Faulted, exception.Message);
    }
    private void Publish(ProxyEventKind kind, string? message = null)
    {
        try { EventOccurred?.Invoke(this, new ProxyEventArgs(new ProxyEvent(Id, kind, DateTimeOffset.UtcNow, Snapshot, message))); } catch { }
    }

    private sealed class ProxyClientHandler(ProxySession session) : ChannelHandlerAdapter
    {
        private int _started;
        public override void ChannelActive(IChannelHandlerContext context)
        {
            context.Channel.Configuration.AutoRead = false;
            context.FireChannelActive();
            if (Interlocked.Exchange(ref _started, 1) == 0)
            {
                _ = session.AcceptClientAsync(context.Channel, session._lifetime.Token);
            }
        }
        public override void ChannelInactive(IChannelHandlerContext context)
        {
            session.RemoveClient(context.Channel);
            context.FireChannelInactive();
        }
        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        { session.Publish(ProxyEventKind.ConnectionFailed, exception.Message); _ = context.CloseAsync(); }
    }
}
