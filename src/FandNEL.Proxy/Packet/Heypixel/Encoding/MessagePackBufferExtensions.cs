using System.IO;
using System.Text;
using DotNetty.Buffers;

namespace FandNEL.Proxy.Packet.Heypixel;

public static class MessagePackBufferExtensions
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public static void WriteBoolean(this IByteBuffer buffer, bool value)
    {
        buffer.WriteByte(value ? 0xC3 : 0xC2);
    }

    public static void WriteSingle(this IByteBuffer buffer, float value)
    {
        WriteSingleWithCode(buffer, 0xCA, value);
    }

    public static void WriteDouble(IByteBuffer buffer, double value)
    {
        WriteDoubleWithCode(buffer, 0xCB, value);
    }

    public static void WriteInt64(this IByteBuffer buffer, long value)
    {
        if (value >= -32)
        {
            if (value < 128)
            {
                buffer.WriteByte((byte)value);
            }
            else if (value < 256)
            {
                WriteByteWithCode(buffer, 0xCC, (byte)value);
            }
            else if (value < 65_536)
            {
                WriteInt16WithCode(buffer, 0xCD, (short)value);
            }
            else if (value < 4_294_967_296L)
            {
                WriteInt32WithCode(buffer, 0xCE, (int)value);
            }
            else
            {
                WriteInt64WithCode(buffer, 0xCF, value);
            }

            return;
        }

        if (value >= -128)
        {
            WriteByteWithCode(buffer, 0xD0, (byte)(sbyte)value);
        }
        else if (value >= -32_768)
        {
            WriteInt16WithCode(buffer, 0xD1, (short)value);
        }
        else if (value >= int.MinValue)
        {
            WriteInt32WithCode(buffer, 0xD2, (int)value);
        }
        else
        {
            WriteInt64WithCode(buffer, 0xD3, value);
        }
    }

    public static void WriteInt32(this IByteBuffer buffer, int value)
    {
        if (value >= -32)
        {
            if (value < 128)
            {
                buffer.WriteByte((byte)value);
            }
            else if (value < 256)
            {
                WriteByteWithCode(buffer, 0xCC, (byte)value);
            }
            else if (value < 65_536)
            {
                WriteInt16WithCode(buffer, 0xCD, (short)value);
            }
            else
            {
                WriteInt32WithCode(buffer, 0xCE, value);
            }

            return;
        }

        if (value >= -128)
        {
            WriteByteWithCode(buffer, 0xD0, (byte)(sbyte)value);
        }
        else if (value >= -32_768)
        {
            WriteInt16WithCode(buffer, 0xD1, (short)value);
        }
        else
        {
            WriteInt32WithCode(buffer, 0xD2, value);
        }
    }

    public static void WriteArrayHeader(this IByteBuffer buffer, int count)
    {
        if (count < 0)
        {
            throw new ArgumentException("array size must be >= 0", "arraySize");
        }

        if (count < 16)
        {
            buffer.WriteByte(0x90 | count);
        }
        else if (count < 65_536)
        {
            WriteInt16WithCode(buffer, 0xDC, (short)count);
        }
        else
        {
            WriteInt32WithCode(buffer, 0xDD, count);
        }
    }

    public static void WriteByte(this IByteBuffer buffer, byte value)
    {
        if ((sbyte)value >= -32)
        {
            buffer.WriteByte(value);
        }
        else
        {
            WriteByteWithCode(buffer, 0xD0, value);
        }
    }

    public static void WriteMapHeader(this IByteBuffer buffer, int count)
    {
        if (count < 0)
        {
            throw new ArgumentException("map size must be >= 0", "mapSize");
        }

        if (count < 16)
        {
            buffer.WriteByte(0x80 | count);
        }
        else if (count < 65_536)
        {
            WriteInt16WithCode(buffer, 0xDE, (short)count);
        }
        else
        {
            WriteInt32WithCode(buffer, 0xDF, count);
        }
    }

    public static void WriteString(this IByteBuffer buffer, string value, bool postprocessUuid = true)
    {
        if (string.IsNullOrEmpty(value))
        {
            buffer.WriteByte(0xA0);
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        if (postprocessUuid)
        {
            try
            {
                bytes = Encoding.UTF8.GetBytes(Uuid128.Parse(value).ToEncodedString());
            }
            catch (UuidFormatException)
            {
                // Ordinary strings are transmitted unchanged.
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("Encoding string failed", exception);
            }
        }

        if (bytes.Length <= byte.MaxValue)
        {
            buffer.WriteByte(0xD9);
            buffer.WriteByte((byte)bytes.Length);
        }
        else if (bytes.Length <= ushort.MaxValue)
        {
            buffer.WriteByte(0xDA);
            buffer.WriteShort((short)bytes.Length);
        }
        else
        {
            buffer.WriteByte(0xDB);
            buffer.WriteInt(bytes.Length);
        }

        buffer.WriteBytes(bytes);
    }

    public static string ReadString(this IByteBuffer buffer)
    {
        int length = ReadStringLength(buffer);
        if (length == 0)
        {
            return string.Empty;
        }

        if (buffer.ReadableBytes < length)
        {
            throw new InvalidDataException(
                $"Cannot unpack string of length {length:N0} because the buffer only has {buffer.ReadableBytes:N0} bytes available");
        }

        byte[] bytes = new byte[length];
        buffer.ReadBytes(bytes);
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (Exception exception)
        {
            throw new InvalidDataException("Decoding string failed", exception);
        }
    }

    public static long ReadInt64(this IByteBuffer buffer)
    {
        EnsureReadable(buffer, 1);
        byte code = buffer.ReadByte();
        if ((code & 0x80) == 0)
        {
            return code;
        }

        if ((code & 0xE0) == 0xE0)
        {
            return (sbyte)code;
        }

        EnsureReadable(buffer, code switch
        {
            0xCC or 0xD0 => 1,
            0xCD or 0xD1 => 2,
            0xCE or 0xD2 => 4,
            0xCF or 0xD3 => 8,
            _ => throw new InvalidDataException($"Expected integer type but got 0x{code:X2}")
        });
        return code switch
        {
            0xCC => buffer.ReadByte(),
            0xCD => buffer.ReadUnsignedShort(),
            0xCE => buffer.ReadUnsignedInt(),
            0xCF => ReadUnsignedInt64(buffer),
            0xD0 => (sbyte)buffer.ReadByte(),
            0xD1 => buffer.ReadShort(),
            0xD2 => buffer.ReadInt(),
            0xD3 => buffer.ReadLong(),
            _ => throw new InvalidDataException($"Expected integer type but got 0x{code:X2}")
        };
    }

    public static int ReadInt32(this IByteBuffer buffer)
    {
        return CheckedInt64ToInt32(ReadInt64(buffer));
    }

    private static int ReadStringLength(IByteBuffer buffer)
    {
        EnsureReadable(buffer, 1);
        byte code = buffer.ReadByte();
        if ((code & 0xE0) == 0xA0)
        {
            return code & 0x1F;
        }

        EnsureReadable(buffer, code switch
        {
            0xD9 => 1,
            0xDA => 2,
            0xDB => 4,
            _ => throw new InvalidDataException($"Expected string header, but got 0x{code:X2}")
        });
        return code switch
        {
            0xD9 => buffer.ReadByte(),
            0xDA => buffer.ReadUnsignedShort(),
            0xDB => ReadNonNegativeLength(buffer),
            _ => throw new InvalidDataException($"Expected string header, but got 0x{code:X2}")
        };
    }

    private static int ReadNonNegativeLength(IByteBuffer buffer)
    {
        int length = buffer.ReadInt();
        return length >= 0
            ? length
            : throw new InvalidDataException("String length cannot be negative");
    }

    private static long ReadUnsignedInt64(IByteBuffer buffer)
    {
        long value = buffer.ReadLong();
        return value >= 0 ? value : throw new OverflowException("MessagePack unsigned integer exceeds Int64.");
    }

    private static void EnsureReadable(IByteBuffer buffer, int count)
    {
        if (buffer.ReadableBytes < count)
        {
            throw new InvalidDataException("Truncated MessagePack value.");
        }
    }

    private static int CheckedInt64ToInt32(long value)
    {
        return value is >= int.MinValue and <= int.MaxValue
            ? (int)value
            : throw new OverflowException($"Long value {value} is outside the range of Int32");
    }

    private static void WriteByteWithCode(IByteBuffer buffer, byte code, byte value)
    {
        buffer.WriteByte(code);
        buffer.WriteByte(value);
    }

    private static void WriteInt16WithCode(IByteBuffer buffer, byte code, short value)
    {
        buffer.WriteByte(code);
        buffer.WriteShort(value);
    }

    private static void WriteInt32WithCode(IByteBuffer buffer, byte code, int value)
    {
        buffer.WriteByte(code);
        buffer.WriteInt(value);
    }

    private static void WriteInt64WithCode(IByteBuffer buffer, byte code, long value)
    {
        buffer.WriteByte(code);
        buffer.WriteLong(value);
    }

    private static void WriteSingleWithCode(IByteBuffer buffer, byte code, float value)
    {
        buffer.WriteByte(code);
        buffer.WriteFloat(value);
    }

    private static void WriteDoubleWithCode(IByteBuffer buffer, byte code, double value)
    {
        buffer.WriteByte(code);
        buffer.WriteDouble(value);
    }
}
