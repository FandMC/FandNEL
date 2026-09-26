using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FandNEL.Proxy.Models;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Services;

namespace FandNEL.Proxy.Sessions;

/// <summary>
/// 基于 TCP 的代理会话。每个会话维护自己的监听器、目标快照和取消令牌，
/// 因而一个游戏实例的切换不会影响其它实例。
/// </summary>
public sealed class ProxySession : IProxySession
{
    private readonly object _stateLock = new();
    private readonly ConcurrentDictionary<int, Task> _connections = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ProxyOptions _initialOptions;
    private TcpListener? _listener;
    private LanDiscoveryBroadcaster? _lanDiscovery;
    private Task? _acceptLoop;
    private ServerTarget _target;
    private PlayerRole _role;
    private ProxySessionState _state = ProxySessionState.Created;
    private IPEndPoint? _localEndpoint;
    private DateTimeOffset? _stoppedAt;
    private string? _lastError;
    private int _connectionSequence;
    private int _activeConnections;
    private PacketRegistry? _registry;

    internal ProxySession(ProxyOptions options)
    {
        options.Validate();
        _initialOptions = options;
        _target = options.Target;
        _role = options.Role;
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }

    /// <summary>此会话独立的协议注册表，插件注册不会污染其它实例。</summary>
    public PacketRegistry Registry => _registry ?? throw new InvalidOperationException("代理会话尚未启动。");

    public event EventHandler<ProxyEventArgs>? EventOccurred;

    public ProxySessionSnapshot Snapshot
    {
        get
        {
            lock (_stateLock)
            {
                return new ProxySessionSnapshot(
                    Id,
                    _state,
                    _localEndpoint,
                    _target,
                    _role,
                    Volatile.Read(ref _activeConnections),
                    _startedAt,
                    _stoppedAt,
                    _initialOptions.UserId,
                    _initialOptions.GameId,
                    _initialOptions.GameVersion,
                    _initialOptions.RentalServerId,
                    _lastError);
            }
        }
    }

    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_stateLock)
        {
            if (_state != ProxySessionState.Created)
            {
                throw new InvalidOperationException("代理会话已经启动。");
            }

            _listener = new TcpListener(_initialOptions.ListenAddress, _initialOptions.ListenPort);
            _listener.Start();
            _localEndpoint = (IPEndPoint)_listener.LocalEndpoint;
            _state = ProxySessionState.Running;
            _registry = new PacketRegistry();
            if (_initialOptions.EnableLanBroadcast)
            {
                _lanDiscovery = new LanDiscoveryBroadcaster(
                    _target, _role, _localEndpoint.Port, _initialOptions.LanMotd);
            }
        }

        _initialOptions.ConfigureRegistry?.Invoke(_registry);

        _acceptLoop = AcceptLoopAsync(_lifetime.Token);
        Publish(ProxyEventKind.Started);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task UpdateServerAsync(ServerTarget target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(target);
        target.Validate();

        lock (_stateLock)
        {
            EnsureRunning();
            _target = target;
        }

        Publish(ProxyEventKind.ServerChanged, $"目标服务器已切换为 {target.Host}:{target.Port}。");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task UpdateRoleAsync(PlayerRole role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);
        if (string.IsNullOrWhiteSpace(role.Name))
        {
            throw new ArgumentException("角色名不能为空。", nameof(role));
        }

        lock (_stateLock)
        {
            EnsureRunning();
            _role = role;
        }

        Publish(ProxyEventKind.RoleChanged, $"当前角色已切换为 {role.Name}。");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? acceptLoop;
        lock (_stateLock)
        {
            if (_state is ProxySessionState.Stopped or ProxySessionState.Stopping)
            {
                return;
            }

            _state = ProxySessionState.Stopping;
            acceptLoop = _acceptLoop;
        }

        _lifetime.Cancel();
        _listener?.Stop();
        if (_lanDiscovery is not null)
        {
            await _lanDiscovery.DisposeAsync().ConfigureAwait(false);
            _lanDiscovery = null;
        }

        if (acceptLoop is not null)
        {
            await IgnoreCancellationAsync(acceptLoop).ConfigureAwait(false);
        }

        var connections = _connections.Values.ToArray();
        if (connections.Length > 0)
        {
            await IgnoreCancellationAsync(Task.WhenAll(connections)).ConfigureAwait(false);
        }

        lock (_stateLock)
        {
            _state = ProxySessionState.Stopped;
            _stoppedAt = DateTimeOffset.UtcNow;
        }

        Publish(ProxyEventKind.Stopped);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _lifetime.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        var listener = _listener ?? throw new InvalidOperationException("代理监听器尚未创建。");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var id = Interlocked.Increment(ref _connectionSequence);
                var task = HandleClientAsync(id, client, cancellationToken);
                _connections[id] = task;
                _ = task.ContinueWith(
                    static (_, state) =>
                    {
                        var (session, connectionId) = ((ProxySession session, int connectionId))state!;
                        session._connections.TryRemove(connectionId, out Task? ignored);
                    },
                    (this, id),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SetFault(exception);
        }
    }

    private async Task HandleClientAsync(int connectionId, TcpClient client, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _activeConnections);
        Publish(ProxyEventKind.ClientConnected);
        using (client)
        {
            try
            {
                ServerTarget target;
                lock (_stateLock)
                {
                    target = _target;
                }

                using var remote = await ConnectRemoteAsync(target, cancellationToken).ConfigureAwait(false);

                using var clientStream = client.GetStream();
                using var remoteStream = remote.GetStream();
                var connection = new MinecraftConnection(
                    clientStream,
                    remoteStream,
                    _initialOptions,
                    target,
                    Snapshot.Role,
                    _registry ?? throw new InvalidOperationException("协议注册表尚未初始化。"),
                    username => Publish(ProxyEventKind.JoinServer, $"{username} 已进入服务器。"));
                await connection.RunAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Publish(ProxyEventKind.ConnectionFailed, exception.Message);
            }
            finally
            {
                Interlocked.Decrement(ref _activeConnections);
                Publish(ProxyEventKind.ClientDisconnected);
            }
        }
    }

    private async Task<TcpClient> ConnectRemoteAsync(ServerTarget target, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_initialOptions.ConnectTimeout);
        var connectionToken = timeout.Token;
        var socks5 = _initialOptions.Socks5;
        if (socks5 is null)
        {
            var direct = new TcpClient();
            try
            {
                await direct.ConnectAsync(target.Host, target.Port, connectionToken).ConfigureAwait(false);
                return direct;
            }
            catch
            {
                direct.Dispose();
                throw;
            }
        }

        var proxy = new TcpClient();
        try
        {
            await proxy.ConnectAsync(socks5.Host, socks5.Port, connectionToken).ConfigureAwait(false);
            await NegotiateSocks5Async(proxy.GetStream(), target, socks5, connectionToken).ConfigureAwait(false);
            return proxy;
        }
        catch
        {
            proxy.Dispose();
            throw;
        }
    }

    private static async Task NegotiateSocks5Async(
        NetworkStream stream,
        ServerTarget target,
        Socks5Options options,
        CancellationToken cancellationToken)
    {
        var hasCredentials = options.Username is not null;
        var methods = hasCredentials ? new byte[] { 0x00, 0x02 } : new byte[] { 0x00 };
        await stream.WriteAsync(new byte[] { 0x05, (byte)methods.Length }, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(methods, cancellationToken).ConfigureAwait(false);
        var greeting = new byte[2];
        await stream.ReadExactlyAsync(greeting, cancellationToken).ConfigureAwait(false);
        if (greeting[0] != 0x05 || greeting[1] == 0xFF)
            throw new InvalidDataException("SOCKS5 代理拒绝了所有认证方式。");

        if (greeting[1] == 0x02)
        {
            var username = Encoding.UTF8.GetBytes(options.Username!);
            var password = Encoding.UTF8.GetBytes(options.Password ?? string.Empty);
            if (username.Length > byte.MaxValue || password.Length > byte.MaxValue)
                throw new InvalidDataException("SOCKS5 用户名或密码过长。");
            var credentials = new byte[3 + username.Length + password.Length];
            credentials[0] = 0x01;
            credentials[1] = (byte)username.Length;
            username.CopyTo(credentials, 2);
            credentials[2 + username.Length] = (byte)password.Length;
            password.CopyTo(credentials, 3 + username.Length);
            await stream.WriteAsync(credentials, cancellationToken).ConfigureAwait(false);
            var authResult = new byte[2];
            await stream.ReadExactlyAsync(authResult, cancellationToken).ConfigureAwait(false);
            if (authResult[0] != 0x01 || authResult[1] != 0x00)
                throw new InvalidDataException("SOCKS5 用户名密码认证失败。");
        }
        else if (greeting[1] != 0x00)
        {
            throw new InvalidDataException($"SOCKS5 返回了不支持的认证方式 0x{greeting[1]:X2}。");
        }

        var address = IPAddress.TryParse(target.Host, out var ipAddress)
            ? ipAddress.GetAddressBytes()
            : Encoding.ASCII.GetBytes(new IdnMapping().GetAscii(target.Host));
        var addressType = ipAddress is null ? (byte)0x03 : ipAddress.AddressFamily == AddressFamily.InterNetwork ? (byte)0x01 : (byte)0x04;
        if (addressType == 0x03 && address.Length > byte.MaxValue)
            throw new InvalidDataException("SOCKS5 目标地址过长。");
        var addressPrefixLength = addressType == 0x03 ? 1 : 0;
        var request = new byte[6 + address.Length + addressPrefixLength];
        request[0] = 0x05;
        request[1] = 0x01;
        request[2] = 0x00;
        request[3] = addressType;
        var addressOffset = 4;
        if (addressType == 0x03)
            request[addressOffset++] = (byte)address.Length;
        address.CopyTo(request, addressOffset);
        var portOffset = addressOffset + address.Length;
        request[portOffset] = (byte)(target.Port >> 8);
        request[portOffset + 1] = (byte)target.Port;
        await stream.WriteAsync(request, cancellationToken).ConfigureAwait(false);

        var responseHeader = new byte[4];
        await stream.ReadExactlyAsync(responseHeader, cancellationToken).ConfigureAwait(false);
        if (responseHeader[0] != 0x05 || responseHeader[1] != 0x00)
            throw new InvalidDataException($"SOCKS5 连接目标失败，状态码 0x{responseHeader[1]:X2}。");
        var addressLength = responseHeader[3] switch
        {
            0x01 => 4,
            0x04 => 16,
            0x03 => (await ReadByteAsync(stream, cancellationToken).ConfigureAwait(false)),
            _ => throw new InvalidDataException("SOCKS5 返回了未知地址类型。")
        };
        var responseAddress = new byte[addressLength];
        await stream.ReadExactlyAsync(responseAddress, cancellationToken).ConfigureAwait(false);
        var port = new byte[2];
        await stream.ReadExactlyAsync(port, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ReadByteAsync(Stream stream, CancellationToken cancellationToken)
    {
        var value = new byte[1];
        await stream.ReadExactlyAsync(value, cancellationToken).ConfigureAwait(false);
        return value[0];
    }

    private void EnsureRunning()
    {
        if (_state != ProxySessionState.Running)
        {
            throw new InvalidOperationException("代理会话当前不可更新。");
        }
    }

    private void SetFault(Exception exception, bool publish = true)
    {
        lock (_stateLock)
        {
            if (_state is ProxySessionState.Stopped or ProxySessionState.Stopping)
            {
                return;
            }

            _state = ProxySessionState.Faulted;
            _lastError = exception.Message;
        }

        _lifetime.Cancel();
        _listener?.Stop();

        if (publish)
        {
            Publish(ProxyEventKind.Faulted, exception.Message);
        }
    }

    private void Publish(ProxyEventKind kind, string? message = null)
    {
        try
        {
            EventOccurred?.Invoke(this, new ProxyEventArgs(new ProxyEvent(
                Id,
                kind,
                DateTimeOffset.UtcNow,
                Snapshot,
                message)));
        }
        catch
        {
            // 观察者异常不能破坏代理的数据面。
        }
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
