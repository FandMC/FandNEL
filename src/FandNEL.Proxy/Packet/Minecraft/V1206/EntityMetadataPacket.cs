using System.Diagnostics.CodeAnalysis;
using System.Text;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>解析元数据边界并保留字段原始字节；不决定哪些实体或字段需要过滤。</summary>
public sealed record EntityMetadataPacket(int EntityId, IReadOnlyList<EntityMetadataEntry> Entries)
{
    public static EntityMetadataPacket Read(ReadOnlyMemory<byte> payload)
    {
        // 字段独立于网络输入缓冲区的生命周期。
        ReadOnlyMemory<byte> data = payload.ToArray();
        var reader = new PacketReader(data);
        var entityId = reader.ReadVarInt();
        List<EntityMetadataEntry> entries = [];
        while (true)
        {
            var start = reader.Position;
            var index = reader.ReadByte();
            if (index == byte.MaxValue)
            {
                MinecraftPacketValidation.RequireEnd(reader);
                return new(entityId, entries);
            }
            var serializer = reader.ReadVarInt();
            var valueOffset = reader.Position;
            EntityMetadataValueReader.SkipValue(reader, (MetadataSerializer)serializer);
            entries.Add(new(index, serializer, data[start..reader.Position], data[valueOffset..reader.Position]));
        }
    }

    /// <summary>遇到未知或损坏字段返回 false，由调用方决定原包透传或断开。</summary>
    public static bool TryRead(ReadOnlyMemory<byte> payload, [NotNullWhen(true)] out EntityMetadataPacket? packet)
    {
        try
        {
            packet = Read(payload);
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or DecoderFallbackException or OverflowException)
        {
            packet = null;
            return false;
        }
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        writer.WriteVarInt(EntityId);
        foreach (var entry in Entries) writer.WriteBytes(entry.RawData.Span);
        return writer.WriteByte(byte.MaxValue).ToArray();
    }
}
