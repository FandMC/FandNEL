using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FandNEL.Core.Connection;

namespace FandNEL.Core.Connection.Protocols;

/// <summary>Authlib 握手适配器。用户令牌由调用方显式提供，Core 不持有全局用户管理器。</summary>
public sealed class AuthLibProtocol : IDisposable
{
    private readonly IPAddress _address;
    private readonly int _requestedPort;
    private readonly string _modList;
    private readonly string _version;
    private readonly string _nexusToken;
    private readonly Func<string, CancellationToken, Task<string?>> _userTokenProvider;
    private readonly CancellationTokenSource _shutdown = new();
    private TcpListener? _listener;
    private Task? _acceptLoop;
    private int _disposed;

    public AuthLibProtocol(
        IPAddress address,
        int port,
        string modList,
        string version,
        string nexusToken,
        Func<string, CancellationToken, Task<string?>> userTokenProvider)
    {
        _address = address;
        _requestedPort = port;
        _modList = modList;
        _version = version;
        _nexusToken = nexusToken;
        _userTokenProvider = userTokenProvider ?? throw new ArgumentNullException(nameof(userTokenProvider));
    }

    public int Port => _listener?.LocalEndpoint is IPEndPoint endpoint ? endpoint.Port : _requestedPort;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        if (_listener is not null) throw new InvalidOperationException("Authlib 服务已启动。");
        _listener = new TcpListener(_address, _requestedPort);
        _listener.Start();
        _acceptLoop = AcceptLoopAsync(_shutdown.Token);
    }

    public void Stop() => Dispose();

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener!.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            uint result = 1;
            try
            {
                var gameId = await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false);
                var userId = await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false);
                var certificate = Encoding.Unicode.GetString(
                    await ReadFieldAsync(stream, cancellationToken).ConfigureAwait(false));
                if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(certificate))
                    throw new InvalidDataException("Authlib 请求字段缺失。");
                var userToken = await _userTokenProvider(userId, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(userToken)) throw new InvalidOperationException("未找到对应用户的网易游戏令牌。");
                await NetEaseConnection.AuthenticateAsync(new JavaJoinRequest(certificate, gameId, _version, _modList, _nexusToken, int.Parse(userId), userToken), cancellationToken).ConfigureAwait(false);
                result = 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch { result = 1; }
            var response = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(response, result);
            await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string> ReadStringAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        return Encoding.UTF8.GetString(await ReadFieldAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<byte[]> ReadFieldAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var lengthBytes = new byte[4];
        await stream.ReadExactlyAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        if (length is < 0 or > 1024 * 1024) throw new InvalidDataException("Authlib 字段长度无效。");
        var data = new byte[length];
        await stream.ReadExactlyAsync(data, cancellationToken).ConfigureAwait(false);
        return data;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _shutdown.Cancel();
        _listener?.Stop();
        try { _acceptLoop?.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        _shutdown.Dispose();
    }
}
