using System.IO.Compression;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using FandNEL.Proxy.Sessions;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace FandNEL.Proxy.Protocol;

internal sealed class MinecraftFrameDecoder(MinecraftConnection connection, PacketDirection direction) : ByteToMessageDecoder
{
    private const int MaximumFrameLength = (1 << 21) - 1;
    private const int MaximumDecompressedFrameLength = 8 << 20;
    private int _compressionThreshold = -1;

    public int CompressionThreshold
    {
        get => Volatile.Read(ref _compressionThreshold);
        set => Volatile.Write(ref _compressionThreshold, value);
    }

    protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
    {
        input.MarkReaderIndex();
        if (!TryReadVarInt(input, out var length))
        {
            input.ResetReaderIndex();
            return;
        }

        if (length <= 0 || length > MaximumFrameLength)
            throw new DecoderException($"Minecraft 帧长度非法：{length}。");
        if (input.ReadableBytes < length)
        {
            input.ResetReaderIndex();
            return;
        }

        var frame = new byte[length];
        input.ReadBytes(frame);
        var payload = CompressionThreshold >= 0 ? Decompress(context, frame) : frame;
        if (direction == PacketDirection.ClientBound && connection.ServerState == ConnectionState.Login
            && connection.Version != ProtocolVersion.V1076
            && TryReadVarInt(payload, out var packetId, out var packetIdLength) && packetId == 3
            && TryReadVarInt(payload.AsSpan(packetIdLength), out var threshold, out _))
        {
            connection.EnableServerCompression(threshold);
        }

        var result = context.Allocator.Buffer(payload.Length);
        result.WriteBytes(payload);
        output.Add(result);
    }

    private byte[] Decompress(IChannelHandlerContext context, byte[] frame)
    {
        var offset = 0;
        if (!TryReadVarInt(frame, out var expandedLength, out offset))
            throw new DecoderException("Minecraft 压缩帧缺少未压缩长度。");
        if (expandedLength == 0)
            return frame.AsSpan(offset).ToArray();
        if (expandedLength < CompressionThreshold || expandedLength > MaximumDecompressedFrameLength)
            throw new DecoderException($"Minecraft 解压长度非法：{expandedLength}。");

        using var source = new MemoryStream(frame, offset, frame.Length - offset, writable: false);
        using var inflater = new ZLibStream(source, CompressionMode.Decompress);
        var packet = new byte[expandedLength];
        inflater.ReadExactly(packet);
        if (source.Position != source.Length)
            throw new DecoderException("Minecraft 压缩包包含多余数据。");
        if (inflater.ReadByte() != -1)
            throw new DecoderException("Minecraft 解压结果超过声明长度。");
        return packet;
    }

    private static bool TryReadVarInt(ReadOnlySpan<byte> bytes, out int value, out int consumed)
    {
        value = 0;
        consumed = 0;
        for (var index = 0; index < Math.Min(bytes.Length, 5); index++)
        {
            var current = bytes[index];
            value |= (current & 0x7f) << (index * 7);
            consumed++;
            if ((current & 0x80) == 0)
                return true;
        }
        return false;
    }

    private static bool TryReadVarInt(IByteBuffer input, out int value)
    {
        value = 0;
        for (var index = 0; index < 3; index++)
        {
            if (!input.IsReadable())
                return false;
            var current = input.ReadByte();
            value |= (current & 0x7f) << (index * 7);
            if ((current & 0x80) == 0)
                return true;
        }

        throw new DecoderException("Minecraft 帧长度超过 21 位。");
    }
}

internal sealed class MinecraftFrameEncoder : MessageToByteEncoder<IByteBuffer>
{
    protected override void Encode(IChannelHandlerContext context, IByteBuffer message, IByteBuffer output)
    {
        var length = message.ReadableBytes;
        if (length <= 0 || length > (1 << 21) - 1)
            throw new EncoderException($"Minecraft 帧长度非法：{length}。");
        WriteVarInt(output, length);
        output.WriteBytes(message, message.ReaderIndex, length);
    }

    internal static void WriteVarInt(IByteBuffer output, int value)
    {
        while ((value & ~0x7f) != 0)
        {
            output.WriteByte((byte)((value & 0x7f) | 0x80));
            value >>>= 7;
        }
        output.WriteByte((byte)value);
    }
}

internal sealed class MinecraftCompressionEncoder(int threshold) : MessageToByteEncoder<IByteBuffer>
{
    private int _threshold = threshold;
    public int Threshold { get => Volatile.Read(ref _threshold); set => Volatile.Write(ref _threshold, value); }

    protected override void Encode(IChannelHandlerContext context, IByteBuffer message, IByteBuffer output)
    {
        var length = message.ReadableBytes;
        WriteVarInt(output, length >= Threshold ? length : 0);
        if (length < Threshold)
        {
            output.WriteBytes(message, message.ReaderIndex, length);
            return;
        }
        using var target = new MemoryStream();
        using (var deflater = new ZLibStream(target, CompressionLevel.Fastest, leaveOpen: true))
        {
            var bytes = new byte[length];
            message.GetBytes(message.ReaderIndex, bytes);
            deflater.Write(bytes);
        }
        output.WriteBytes(target.ToArray());
    }

    private static void WriteVarInt(IByteBuffer output, int value)
    {
        while ((value & ~0x7f) != 0)
        {
            output.WriteByte((byte)((value & 0x7f) | 0x80));
            value >>>= 7;
        }
        output.WriteByte((byte)value);
    }
}

internal sealed class MinecraftEncryptionDecoder(byte[] secret) : MessageToMessageDecoder<IByteBuffer>
{
    private readonly CfbBlockCipher _cipher = CreateCipher(secret, false);

    protected override void Decode(IChannelHandlerContext context, IByteBuffer message, List<object> output)
    {
        var bytes = new byte[message.ReadableBytes];
        message.GetBytes(message.ReaderIndex, bytes);
        Transform(_cipher, bytes);
        var buffer = context.Allocator.Buffer(bytes.Length);
        buffer.WriteBytes(bytes);
        output.Add(buffer);
    }

    internal static CfbBlockCipher CreateCipher(byte[] secret, bool encrypt)
    {
        var cipher = new CfbBlockCipher(new AesEngine(), 8);
        cipher.Init(encrypt, new ParametersWithIV(new KeyParameter(secret), secret));
        return cipher;
    }

    internal static void Transform(CfbBlockCipher cipher, byte[] bytes)
    {
        for (var index = 0; index < bytes.Length; index++)
            cipher.ProcessBlock(bytes, index, bytes, index);
    }
}

internal sealed class MinecraftEncryptionEncoder(byte[] secret) : MessageToByteEncoder<IByteBuffer>
{
    private readonly CfbBlockCipher _cipher = MinecraftEncryptionDecoder.CreateCipher(secret, true);

    protected override void Encode(IChannelHandlerContext context, IByteBuffer message, IByteBuffer output)
    {
        var bytes = new byte[message.ReadableBytes];
        message.GetBytes(message.ReaderIndex, bytes);
        MinecraftEncryptionDecoder.Transform(_cipher, bytes);
        output.WriteBytes(bytes);
    }
}
