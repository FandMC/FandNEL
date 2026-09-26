using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FandNEL.Core.Entities;
using FandNEL.Core.Entities.WPFLauncher.NetGame;
using FandNEL.Core.Entities.WPFLauncher.NetGame.Skin;
using FandNEL.Core.Entities.WPFLauncher.NetGame.Texture;
using FandNEL.Core.Entities.WPFLauncher.RPC;
using FandNEL.Core.Protocol;
using FandNEL.Core.Skip32;
using FandNEL.GameLauncher.Models;

namespace FandNEL.GameLauncher.Services.Java;

/// <summary>Java 客户端控制 RPC。使用长度前缀的小端消息，与网易 Launcher 控制协议保持一致。</summary>
internal sealed class JavaRpcService(WPFLauncher launcher, JavaLaunchRequest request, LauncherPaths paths, HttpClient http) : IAsyncDisposable
{
    private readonly Skip32Cipher _skip32 = new("SaintSteve"u8.ToArray());
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _writeLock = new();
    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private Task? _acceptTask;

    public int Port { get; private set; }

    public void Start()
    {
        if (_listener is not null)
            throw new InvalidOperationException("RPC 已启动。");
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptTask = AcceptLoopAsync(_shutdown.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener!.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                if (client.Client.RemoteEndPoint is not IPEndPoint endpoint || !IPAddress.IsLoopback(endpoint.Address))
                {
                    client.Dispose();
                    continue;
                }
                _client?.Dispose();
                _client = client;
                _stream = client.GetStream();
                _ = ReceiveLoopAsync(_stream, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task ReceiveLoopAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var length = new byte[2];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await ReadExactAsync(stream, length, cancellationToken).ConfigureAwait(false);
                var size = BinaryPrimitives.ReadUInt16LittleEndian(length);
                if (size < 2)
                    throw new InvalidDataException("RPC 消息长度无效。");
                var message = new byte[size];
                await ReadExactAsync(stream, message, cancellationToken).ConfigureAwait(false);
                await HandleAsync(BinaryPrimitives.ReadUInt16LittleEndian(message), message.AsMemory(2), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (EndOfStreamException) { }
        catch (IOException) { }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task HandleAsync(ushort id, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        switch (id)
        {
            case 18:
                await SendAsync(18, [payload.ToArray()], cancellationToken).ConfigureAwait(false);
                break;
            case 261 when request.GameVersion > EnumGameVersion.V_1_18:
                await SendAsync(1799, [Utf8(request.ServerHost), Int(request.ServerPort), Utf8(request.RoleName)], cancellationToken).ConfigureAwait(false);
                break;
            case 512:
                await SendAsync(512, [Utf8("i'am wpflauncher")], cancellationToken).ConfigureAwait(false);
                break;
            case 517:
                await SendAsync(1031, [Utf8(request.ServerHost), Int(request.ServerPort), Utf8(request.RoleName), Bool(false)], cancellationToken).ConfigureAwait(false);
                break;
            case 520:
                await HandleSkinAsync(payload, cancellationToken).ConfigureAwait(false);
                break;
            case 1298:
                await SendAsync(1298, [Bool(false), Long(0), Long(0)], cancellationToken).ConfigureAwait(false);
                break;
            case 19:
            case 4612:
                break;
        }
    }

    private async Task HandleSkinAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var reader = new RpcReader(payload.Span);
        _ = reader.Int16();
        var name = reader.String();
        var uuid = reader.String();
        var skinPath = string.Empty;
        var skinMode = EnumSkinMode.Default;
        try
        {
            var userId = _skip32.ComputeUserIdFromUuid(uuid).ToString();
            var skins = launcher.GetSkinListInGame(request.UserId, request.UserToken, new EntityUserGameTextureRequest
            {
                UserId = userId,
                ClientType = EnumGameClientType.Java
            });
            foreach (var skin in skins.Where(skin => skin.SkinId.Length > 5))
            {
                skinMode = (EnumSkinMode)skin.SkinMode;
                var target = Path.Combine(paths.Skins, $"skin_{skin.SkinId}.png");
                if (!File.Exists(target))
                {
                    var response = launcher.GetNetGameComponentDownloadList(request.UserId, request.UserToken, skin.SkinId);
                    var url = response.Data?.SubEntities.FirstOrDefault()?.ResUrl;
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        var bytes = await http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
                        Directory.CreateDirectory(paths.Skins);
                        await File.WriteAllBytesAsync(target, bytes, cancellationToken).ConfigureAwait(false);
                    }
                }
                if (File.Exists(target))
                {
                    skinPath = target;
                    break;
                }
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
        await SendAsync(520, [Utf8(name), Utf8(skinPath), Utf8(string.Empty), Int((int)skinMode)], cancellationToken).ConfigureAwait(false);
    }

    private async Task SendAsync(ushort id, byte[][] values, CancellationToken cancellationToken)
    {
        var payloadLength = 2 + values.Sum(value => value.Length);
        if (payloadLength > ushort.MaxValue)
            throw new InvalidDataException("RPC 消息过长。");
        var message = new byte[payloadLength + 2];
        BinaryPrimitives.WriteUInt16LittleEndian(message, (ushort)payloadLength);
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(2), id);
        var offset = 4;
        foreach (var value in values)
        {
            value.CopyTo(message, offset);
            offset += value.Length;
        }
        var stream = _stream;
        if (stream is null)
            return;
        lock (_writeLock)
            stream.Write(message, 0, message.Length);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReadExactAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[read..], cancellationToken).ConfigureAwait(false);
            if (count == 0)
                throw new EndOfStreamException();
            read += count;
        }
    }

    private static byte[] Utf8(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
            throw new ArgumentException("RPC 字符串过长。", nameof(value));
        var result = new byte[bytes.Length + 2];
        BinaryPrimitives.WriteUInt16LittleEndian(result, (ushort)bytes.Length);
        bytes.CopyTo(result, 2);
        return result;
    }
    private static byte[] Int(int value) => BitConverter.GetBytes(value);
    private static byte[] Long(long value) => BitConverter.GetBytes(value);
    private static byte[] Bool(bool value) => [value ? (byte)1 : (byte)0];

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _listener?.Stop();
        _client?.Dispose();
        if (_acceptTask is not null)
        {
            try { await _acceptTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
        _shutdown.Dispose();
    }

    private ref struct RpcReader(ReadOnlySpan<byte> data)
    {
        private ReadOnlySpan<byte> _data = data;
        private int _offset;
        public short Int16() => Read(2, static span => BinaryPrimitives.ReadInt16LittleEndian(span));
        public string String()
        {
            var length = BinaryPrimitives.ReadUInt16LittleEndian(Read(2));
            return Encoding.UTF8.GetString(Read(length));
        }
        private ReadOnlySpan<byte> Read(int length)
        {
            if (length < 0 || _offset > _data.Length - length)
                throw new InvalidDataException("RPC 字段超出消息长度。");
            var value = _data.Slice(_offset, length);
            _offset += length;
            return value;
        }
        private T Read<T>(int length, Func<ReadOnlySpan<byte>, T> parser) => parser(Read(length));
    }
}
