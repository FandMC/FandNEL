using System.Buffers.Binary;
using System.Text;
using DotNetty.Buffers;

namespace FandNEL.Proxy.Protocol.Nbt;

public sealed record NbtLimits(int MaxDepth = 512, int MaxStringBytes = 1 << 20, int MaxArrayLength = 1 << 20, int MaxListLength = 1 << 20, int MaxCompoundEntries = 1 << 20) { public static NbtLimits Default { get; } = new(); }

public static class NbtCodec
{
    public static NbtTag Read(ReadOnlyMemory<byte> data, NbtLimits? limits = null, bool requireEnd = true)
    { var reader = new PacketReader(data); var tag = ReadNamed(reader, limits ?? NbtLimits.Default, 0); if (requireEnd && reader.Remaining != 0) throw new InvalidDataException("NBT 根标签后存在多余数据。"); return tag; }
    public static NbtTag Read(IByteBuffer buffer, NbtLimits? limits = null, bool requireEnd = false) { var bytes = new byte[buffer.ReadableBytes]; buffer.GetBytes(buffer.ReaderIndex, bytes); return Read(bytes, limits, requireEnd); }
    public static byte[] Write(NbtTag tag, NbtLimits? limits = null) { using var writer = new PacketWriter(); WriteNamed(writer, tag, limits ?? NbtLimits.Default, 0); return writer.ToArray(); }
    public static void Write(IByteBuffer buffer, NbtTag tag, NbtLimits? limits = null) => buffer.WriteBytes(Write(tag, limits));
    public static void Write(PacketWriter writer, NbtTag tag, NbtLimits? limits = null) => WriteNamed(writer, tag, limits ?? NbtLimits.Default, 0);
    public static NbtCompound ReadNetworkCompound(PacketReader reader, NbtLimits? limits = null)
    {
        var effective = limits ?? NbtLimits.Default;
        var type = ReadType(reader.ReadByte());
        if (type != NbtTagType.Compound) throw new InvalidDataException("网络 NBT 根标签必须是 Compound。");
        return (NbtCompound)ReadPayload(reader, type, string.Empty, effective, 0);
    }
    public static void WriteNetworkCompound(PacketWriter writer, NbtCompound compound, NbtLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(compound);
        var effective = limits ?? NbtLimits.Default;
        writer.WriteByte((byte)NbtTagType.Compound);
        WritePayload(writer, compound, effective, 0);
    }

    private static NbtTag ReadNamed(PacketReader reader, NbtLimits limits, int depth)
    { var type = ReadType(reader.ReadByte()); if (type == NbtTagType.End) return NbtEnd.Instance; var name = ReadUtf8(reader, limits); return ReadPayload(reader, type, name, limits, depth); }
    private static NbtTag ReadPayload(PacketReader r, NbtTagType type, string name, NbtLimits l, int depth)
    {
        if (depth > l.MaxDepth) throw new InvalidDataException("NBT 嵌套深度超过限制。");
        return type switch
        {
            NbtTagType.Byte => new NbtByte(unchecked((sbyte)r.ReadByte())), NbtTagType.Short => new NbtShort(ReadInt16(r)), NbtTagType.Int => new NbtInt(ReadInt32(r)), NbtTagType.Long => new NbtLong(ReadInt64(r)),
            NbtTagType.Float => new NbtFloat(BitConverter.Int32BitsToSingle(ReadInt32(r))), NbtTagType.Double => new NbtDouble(BitConverter.Int64BitsToDouble(ReadInt64(r))), NbtTagType.ByteArray => new NbtByteArray(ReadArray(r, l.MaxArrayLength)), NbtTagType.String => new NbtString(ReadUtf8(r, l)),
            NbtTagType.List => ReadList(r, l, depth), NbtTagType.Compound => ReadCompound(r, name, l, depth), NbtTagType.IntArray => new NbtIntArray(ReadIntArray(r, l.MaxArrayLength)), NbtTagType.LongArray => new NbtLongArray(ReadLongArray(r, l.MaxArrayLength)), _ => throw new InvalidDataException($"非法 NBT 标签类型：{type}。")
        };
    }
    private static NbtList ReadList(PacketReader r, NbtLimits l, int depth) { var type = ReadType(r.ReadByte()); var count = ReadLength(r, l.MaxListLength, "列表"); if (type == NbtTagType.End && count != 0) throw new InvalidDataException("非空列表不能使用 End 元素类型。"); var list = new NbtList(type); for (var i = 0; i < count; i++) list.Add(ReadPayload(r, type, "", l, depth + 1)); return list; }
    private static NbtCompound ReadCompound(PacketReader r, string name, NbtLimits l, int depth) { var compound = new NbtCompound(name); var count = 0; while (true) { var type = ReadType(r.ReadByte()); if (type == NbtTagType.End) return compound; if (++count > l.MaxCompoundEntries) throw new InvalidDataException("Compound 项数超过限制。"); var key = ReadUtf8(r, l); compound.Set(key, ReadPayload(r, type, key, l, depth + 1)); } }

    private static void WriteNamed(PacketWriter w, NbtTag tag, NbtLimits l, int depth) { if (depth > l.MaxDepth) throw new InvalidDataException("NBT 嵌套深度超过限制。"); w.WriteByte((byte)tag.Type); if (tag.Type == NbtTagType.End) return; WriteUtf8(w, tag is NbtCompound c ? c.Name : "", l); WritePayload(w, tag, l, depth); }
    private static void WritePayload(PacketWriter w, NbtTag tag, NbtLimits l, int depth)
    {
        switch (tag)
        {
            case NbtByte x: w.WriteByte(unchecked((byte)x.Value)); break; case NbtShort x: WriteInt16(w, x.Value); break; case NbtInt x: WriteInt32(w, x.Value); break; case NbtLong x: WriteInt64(w, x.Value); break; case NbtFloat x: WriteInt32(w, BitConverter.SingleToInt32Bits(x.Value)); break; case NbtDouble x: WriteInt64(w, BitConverter.DoubleToInt64Bits(x.Value)); break;
            case NbtByteArray x: WriteArray(w, x.Value, l.MaxArrayLength); break; case NbtString x: WriteUtf8(w, x.Value, l); break; case NbtList x: w.WriteByte((byte)x.ElementType); WriteInt32(w, x.Count); foreach (var item in x.Values) WritePayload(w, item, l, depth + 1); break;
            case NbtCompound x: foreach (var item in x.Values) { w.WriteByte((byte)item.Value.Type); WriteUtf8(w, item.Key, l); WritePayload(w, item.Value, l, depth + 1); } w.WriteByte(0); break; case NbtIntArray x: WriteIntArray(w, x.Value, l.MaxArrayLength); break; case NbtLongArray x: WriteLongArray(w, x.Value, l.MaxArrayLength); break; default: throw new InvalidDataException("End 标签不能作为 payload 写入。");
        }
    }
    private static NbtTagType ReadType(byte value) => value <= 12 ? (NbtTagType)value : throw new InvalidDataException($"非法 NBT 标签类型：{value}。");
    private static int ReadLength(PacketReader r, int max, string label) { var value = ReadInt32(r); if (value < 0 || value > max) throw new InvalidDataException($"{label}长度非法：{value}。"); return value; }
    private static string ReadUtf8(PacketReader r, NbtLimits l) { var length = r.ReadUnsignedShort(); if (length > l.MaxStringBytes) throw new InvalidDataException("NBT 字符串超过限制。"); try { return new UTF8Encoding(false, true).GetString(r.ReadBytes(length)); } catch (DecoderFallbackException ex) { throw new InvalidDataException("NBT 字符串不是合法 UTF-8。", ex); } }
    private static short ReadInt16(PacketReader r) => unchecked((short)r.ReadUnsignedShort()); private static int ReadInt32(PacketReader r) => unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(r.ReadBytes(4))); private static long ReadInt64(PacketReader r) => unchecked((long)BinaryPrimitives.ReadUInt64BigEndian(r.ReadBytes(8))); private static byte[] ReadArray(PacketReader r, int max) => r.ReadBytes(ReadLength(r, max, "数组"));
    private static int[] ReadIntArray(PacketReader r, int max) { var n = ReadLength(r, max, "数组"); var a = new int[n]; for (var i = 0; i < n; i++) a[i] = ReadInt32(r); return a; } private static long[] ReadLongArray(PacketReader r, int max) { var n = ReadLength(r, max, "数组"); var a = new long[n]; for (var i = 0; i < n; i++) a[i] = ReadInt64(r); return a; }
    private static void WriteUtf8(PacketWriter w, string value, NbtLimits l) { var bytes = Encoding.UTF8.GetBytes(value); if (bytes.Length > ushort.MaxValue || bytes.Length > l.MaxStringBytes) throw new InvalidDataException("NBT 字符串超过限制。"); w.WriteUnsignedShort((ushort)bytes.Length).WriteBytes(bytes); }
    private static void WriteInt16(PacketWriter w, short value) => w.WriteUnsignedShort(unchecked((ushort)value)); private static void WriteInt32(PacketWriter w, int value) { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(bytes, value); w.WriteBytes(bytes); } private static void WriteInt64(PacketWriter w, long value) { Span<byte> bytes = stackalloc byte[8]; BinaryPrimitives.WriteInt64BigEndian(bytes, value); w.WriteBytes(bytes); }
    private static void WriteArray(PacketWriter w, byte[] value, int max) { if (value.Length > max) throw new InvalidDataException("数组超过限制。"); WriteInt32(w, value.Length); w.WriteBytes(value); } private static void WriteIntArray(PacketWriter w, int[] value, int max) { if (value.Length > max) throw new InvalidDataException("数组超过限制。"); WriteInt32(w, value.Length); foreach (var item in value) WriteInt32(w, item); } private static void WriteLongArray(PacketWriter w, long[] value, int max) { if (value.Length > max) throw new InvalidDataException("数组超过限制。"); WriteInt32(w, value.Length); foreach (var item in value) WriteInt64(w, item); }
}
