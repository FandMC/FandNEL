using System.IO.Compression;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace FandNEL.Proxy.Protocol;

/// <summary>Minecraft 单端连接的分帧、压缩与加密状态。客户端和服务端各自独立。</summary>
internal sealed class MinecraftPeer(Stream stream) : IDisposable
{
    private const int MaximumFrameLength = (1 << 21) - 1;
    private const int MaximumPacketLength = 8 << 20;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private CfbBlockCipher? _encryptor;
    private CfbBlockCipher? _decryptor;
    private int _compressionThreshold = -1;

    public int CompressionThreshold
    {
        get => Volatile.Read(ref _compressionThreshold);
        set => Volatile.Write(ref _compressionThreshold, value);
    }

    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        var firstByte = new byte[1];
        if (await stream.ReadAsync(firstByte, cancellationToken).ConfigureAwait(false) == 0)
            return null;
        Transform(_decryptor, firstByte);
        var value = firstByte[0];
        var length = value & 0x7f;
        var count = 1;
        while ((value & 0x80) != 0)
        {
            if (count == 3)
                throw new InvalidDataException("Minecraft 帧长度超过 21 位。");
            await stream.ReadExactlyAsync(firstByte, cancellationToken).ConfigureAwait(false);
            Transform(_decryptor, firstByte);
            value = firstByte[0];
            length |= (value & 0x7f) << (7 * count++);
        }
        if (length == 0 || length > MaximumFrameLength)
            throw new InvalidDataException($"Minecraft 帧长度非法：{length}。");
        var frame = new byte[length];
        await stream.ReadExactlyAsync(frame, cancellationToken).ConfigureAwait(false);
        Transform(_decryptor, frame);
        if (CompressionThreshold < 0)
            return frame;

        var reader = new PacketReader(frame);
        var expandedLength = reader.ReadVarInt();
        if (expandedLength == 0)
            return reader.ReadBytes(reader.Remaining);
        if (expandedLength < CompressionThreshold || expandedLength > MaximumPacketLength)
            throw new InvalidDataException($"Minecraft 解压长度非法：{expandedLength}。");
        using var source = new MemoryStream(frame, reader.Position, reader.Remaining, writable: false);
        using var inflater = new ZLibStream(source, CompressionMode.Decompress);
        var packet = new byte[expandedLength];
        await inflater.ReadExactlyAsync(packet, cancellationToken).ConfigureAwait(false);
        if (await inflater.ReadAsync(firstByte, cancellationToken).ConfigureAwait(false) != 0)
            throw new InvalidDataException("Minecraft 压缩包超过声明长度。");
        return packet;
    }

    public async Task WriteAsync(byte[] packet, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteCoreAsync(packet, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task WriteAndEnableEncryptionAsync(byte[] packet, byte[] secret, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_encryptor is not null)
                throw new InvalidOperationException("连接已经启用加密。");
            await WriteCoreAsync(packet, cancellationToken).ConfigureAwait(false);
            _encryptor = CreateCipher(secret, true);
            _decryptor = CreateCipher(secret, false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task WriteCoreAsync(byte[] packet, CancellationToken cancellationToken)
    {
        if (packet.Length == 0 || packet.Length > MaximumPacketLength)
            throw new InvalidDataException("Minecraft 包长度非法。");
        var content = packet;
        var threshold = CompressionThreshold;
        if (threshold >= 0)
        {
            using var compressed = new MemoryStream();
            using (var prefix = new PacketWriter())
            {
                prefix.WriteVarInt(packet.Length >= threshold ? packet.Length : 0);
                compressed.Write(prefix.ToArray());
            }
            if (packet.Length >= threshold)
            {
                using (var deflater = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
                    await deflater.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
            }
            else
                compressed.Write(packet);
            content = compressed.ToArray();
        }
        if (content.Length > MaximumFrameLength)
            throw new InvalidDataException("Minecraft 帧超过允许长度。");
        using var writer = new PacketWriter();
        var frame = writer.WriteVarInt(content.Length).WriteBytes(content).ToArray();
        Transform(_encryptor, frame);
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    private static CfbBlockCipher CreateCipher(byte[] secret, bool encrypt)
    {
        var cipher = new CfbBlockCipher(new AesEngine(), 8);
        cipher.Init(encrypt, new ParametersWithIV(new KeyParameter(secret), secret));
        return cipher;
    }

    private static void Transform(CfbBlockCipher? cipher, byte[] bytes)
    {
        if (cipher is null)
            return;
        for (var i = 0; i < bytes.Length; i++)
            cipher.ProcessBlock(bytes, i, bytes, i);
    }

    public void Dispose() => _writeLock.Dispose();
}
