using FandNEL.Proxy.Packet.IO;
using System.Text;

namespace FandNEL.Proxy.Packet.Minecraft.Metadata;

/// <summary>Entity Metadata value list（不含实体 ID）的通用读写。</summary>
public static class MetadataCodec
{
    public static bool TryRead(ReadOnlyMemory<byte> payload, MetadataProtocol protocol,
        out IReadOnlyList<MetadataEntry> entries, out int consumed)
    {
        try
        {
            entries = Read(payload, protocol, out consumed);
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or DecoderFallbackException or OverflowException)
        {
            entries = [];
            consumed = 0;
            return false;
        }
    }

    public static IReadOnlyList<MetadataEntry> Read(ReadOnlyMemory<byte> payload, MetadataProtocol protocol, out int consumed)
    {
        var reader = new PacketReader(payload);
        var entries = new List<MetadataEntry>();
        while (true)
        {
            var index = reader.ReadByte();
            if (index == 0xff) break;
            var serializerId = reader.ReadVarInt();
            var valueStart = reader.Position;
            var value = MetadataValueCodec.ReadValue(payload[valueStart..], serializerId, protocol, out var valueLength);
            reader.Skip(valueLength);
            entries.Add(new MetadataEntry(index, serializerId, value.RawValue));
        }
        consumed = reader.Position;
        return entries;
    }

    public static void Write(PacketWriter writer, IReadOnlyList<MetadataEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var entry in entries)
        {
            if (entry.Index == 0xff) throw new InvalidDataException("实体元数据索引 255 保留为结束标记。");
            writer.WriteByte(entry.Index).WriteVarInt(entry.SerializerId).WriteBytes(entry.RawValue.Span);
        }
        writer.WriteByte(0xff);
    }
}
