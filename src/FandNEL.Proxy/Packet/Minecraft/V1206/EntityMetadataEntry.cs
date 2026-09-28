using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.Nbt;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>原始字段包含 index、serializer 和 value；仅改动的字段需要重新编码。</summary>
public sealed class EntityMetadataEntry
{
    internal EntityMetadataEntry(byte index, int serializerId, ReadOnlyMemory<byte> rawData, ReadOnlyMemory<byte> rawValue)
    {
        Index = index;
        SerializerId = serializerId;
        RawData = rawData;
        RawValue = rawValue;
    }

    public byte Index { get; }
    public int SerializerId { get; }
    public ReadOnlyMemory<byte> RawData { get; }
    public ReadOnlyMemory<byte> RawValue { get; }

    public static EntityMetadataEntry Byte(byte index, byte value) =>
        Create(index, MetadataSerializer.Byte, writer => writer.WriteByte(value));

    public static EntityMetadataEntry Boolean(byte index, bool value) =>
        Create(index, MetadataSerializer.Boolean, writer => writer.WriteBoolean(value));

    public static EntityMetadataEntry VarInt(byte index, int value) =>
        Create(index, MetadataSerializer.VarInt, writer => writer.WriteVarInt(value));

    public static EntityMetadataEntry Component(byte index, NbtTag value) =>
        Create(index, MetadataSerializer.Component, writer => writer.WriteNetworkNbt(value));

    public static EntityMetadataEntry OptionalComponent(byte index, NbtTag? value) =>
        Create(index, MetadataSerializer.OptionalComponent, writer =>
        {
            writer.WriteBoolean(value is not null);
            if (value is not null) writer.WriteNetworkNbt(value);
        });

    public static EntityMetadataEntry Create(byte index, MetadataSerializer serializer, Action<PacketWriter> writeValue)
    {
        if (index == byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(index), "255 是元数据终止标志。");
        using var writer = new PacketWriter();
        writer.WriteByte(index).WriteVarInt((int)serializer);
        var valueOffset = writer.ToArray().Length;
        writeValue(writer);
        ReadOnlyMemory<byte> data = writer.ToArray();
        var value = data[valueOffset..];
        var reader = new PacketReader(value);
        EntityMetadataValueReader.SkipValue(reader, serializer);
        MinecraftPacketValidation.RequireEnd(reader);
        return new(index, (int)serializer, data, value);
    }

    public byte ReadByte() => Reader(MetadataSerializer.Byte).ReadByte();
    public bool ReadBoolean() => Reader(MetadataSerializer.Boolean).ReadBoolean();
    public int ReadVarInt() => Reader(MetadataSerializer.VarInt).ReadVarInt();
    public NbtTag ReadComponent() => Reader(MetadataSerializer.Component).ReadNetworkNbt();

    public NbtTag? ReadOptionalComponent()
    {
        var reader = Reader(MetadataSerializer.OptionalComponent);
        return reader.ReadBoolean() ? reader.ReadNetworkNbt() : null;
    }

    private PacketReader Reader(MetadataSerializer expected)
    {
        if (SerializerId != (int)expected)
            throw new InvalidDataException($"元数据字段类型不是 {expected}。");
        return new PacketReader(RawValue);
    }
}
