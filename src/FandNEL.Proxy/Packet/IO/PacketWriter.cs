using FandNEL.Proxy.Packet.Minecraft;
using System.Buffers.Binary;
using System.Text;
using FandNEL.Proxy.Packet.Minecraft.Nbt;

namespace FandNEL.Proxy.Packet.IO;

/// <summary>构建数据包载荷，供各协议的包类型共用。</summary>
public sealed class PacketWriter : IDisposable
{
    private readonly MemoryStream _stream = new();

    public PacketWriter WriteByte(byte value)
    {
        _stream.WriteByte(value);
        return this;
    }

    public PacketWriter WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    public PacketWriter WriteInt(int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return WriteBytes(bytes);
    }

    public PacketWriter WriteLong(long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return WriteBytes(bytes);
    }

    public PacketWriter WriteFloat(float value) => WriteInt(BitConverter.SingleToInt32Bits(value));
    public PacketWriter WriteDouble(double value) => WriteLong(BitConverter.DoubleToInt64Bits(value));

    public PacketWriter WriteUuid(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        return WriteBytes(bytes);
    }

    public PacketWriter WritePosition(BlockPosition value)
    {
        if (value.X is < -33554432 or > 33554431 || value.Z is < -33554432 or > 33554431 || value.Y is < -2048 or > 2047)
            throw new ArgumentOutOfRangeException(nameof(value), "方块坐标超出网络 Position 范围。");
        var packed = ((long)value.X & 0x3ffffff) << 38 | ((long)value.Z & 0x3ffffff) << 12 | ((long)value.Y & 0xfff);
        return WriteLong(packed);
    }

    public PacketWriter WriteVarInt(int value)
    {
        var remaining = unchecked((uint)value);
        do
        {
            var current = (byte)(remaining & 0x7f);
            remaining >>= 7;
            _stream.WriteByte(remaining == 0 ? current : (byte)(current | 0x80));
        } while (remaining != 0);
        return this;
    }

    public PacketWriter WriteVarLong(long value)
    {
        var remaining = unchecked((ulong)value);
        do
        {
            var current = (byte)(remaining & 0x7f);
            remaining >>= 7;
            _stream.WriteByte(remaining == 0 ? current : (byte)(current | 0x80));
        } while (remaining != 0);
        return this;
    }

    public PacketWriter WriteUnsignedShort(ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        _stream.Write(bytes);
        return this;
    }

    public PacketWriter WriteBytes(ReadOnlySpan<byte> value)
    {
        _stream.Write(value);
        return this;
    }

    public PacketWriter WriteByteArray(ReadOnlySpan<byte> value) => WriteVarInt(value.Length).WriteBytes(value);

    public PacketWriter WriteString(string value, int maximumLength = 32767)
    {
        if (value.Length > maximumLength)
            throw new InvalidDataException("字符串超过允许长度。");
        return WriteByteArray(Encoding.UTF8.GetBytes(value));
    }

    public PacketWriter WriteNbt(NbtTag tag, NbtLimits? limits = null) { NbtCodec.Write(this, tag, limits); return this; }

    public PacketWriter WriteNetworkNbt(NbtTag tag, NbtLimits? limits = null) { NbtCodec.WriteNetwork(this, tag, limits); return this; }

    public PacketWriter WriteNetworkNbtCompound(NbtCompound compound, NbtLimits? limits = null) { NbtCodec.WriteNetworkCompound(this, compound, limits); return this; }

    public byte[] ToArray() => _stream.ToArray();
    public void Dispose() => _stream.Dispose();
}
