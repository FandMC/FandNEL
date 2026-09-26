using System.Buffers.Binary;
using System.Text;

namespace FandNEL.Proxy.Protocol;

/// <summary>构建包载荷；未修改字段可通过 PacketContext.ReplaceRange 原样保留。</summary>
public sealed class PacketWriter : IDisposable
{
    private readonly MemoryStream _stream = new();

    public PacketWriter WriteByte(byte value)
    {
        _stream.WriteByte(value);
        return this;
    }

    public PacketWriter WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

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

    public byte[] ToArray() => _stream.ToArray();
    public void Dispose() => _stream.Dispose();
}
