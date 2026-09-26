using System.Buffers.Binary;
using System.Text;

namespace FandNEL.Proxy.Protocol;

/// <summary>只读包载荷游标；读取范围始终检查，避免跨包读取。</summary>
public sealed class PacketReader(ReadOnlyMemory<byte> payload)
{
    public int Position { get; private set; }
    public int Remaining => payload.Length - Position;

    public byte ReadByte()
    {
        EnsureAvailable(1);
        return payload.Span[Position++];
    }

    public bool ReadBoolean() => ReadByte() != 0;

    public int ReadVarInt()
    {
        uint result = 0;
        for (var i = 0; i < 5; i++)
        {
            var value = ReadByte();
            if (i == 4 && (value & 0xf0) != 0)
                throw new InvalidDataException("VarInt 超出 32 位范围。");
            result |= (uint)(value & 0x7f) << (i * 7);
            if ((value & 0x80) == 0)
                return unchecked((int)result);
        }
        throw new InvalidDataException("VarInt 长度超过 5 字节。");
    }

    public ushort ReadUnsignedShort()
    {
        EnsureAvailable(2);
        var value = BinaryPrimitives.ReadUInt16BigEndian(payload.Span[Position..]);
        Position += 2;
        return value;
    }

    public byte[] ReadBytes(int length)
    {
        EnsureAvailable(length);
        var bytes = payload.Slice(Position, length).ToArray();
        Position += length;
        return bytes;
    }

    public byte[] ReadByteArray(int maximumLength = 1 << 20)
    {
        var length = ReadVarInt();
        if (length < 0 || length > maximumLength)
            throw new InvalidDataException($"字节数组长度非法：{length}。");
        return ReadBytes(length);
    }

    public string ReadString(int maximumLength = 32767)
    {
        var bytes = ReadByteArray(checked(maximumLength * 3));
        var value = new UTF8Encoding(false, true).GetString(bytes);
        if (value.Length > maximumLength)
            throw new InvalidDataException("字符串超过允许长度。");
        return value;
    }

    private void EnsureAvailable(int length)
    {
        if (length < 0 || length > Remaining)
            throw new InvalidDataException($"包载荷不足：位置 {Position}，需要 {length} 字节，剩余 {Remaining} 字节。");
    }
}
