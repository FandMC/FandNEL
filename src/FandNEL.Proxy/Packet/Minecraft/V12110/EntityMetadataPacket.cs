using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.Metadata;

namespace FandNEL.Proxy.Packet.Minecraft.V12110;

/// <summary>实体元数据尾部原样保留；需要解释时显式选择 1.21.10 serializer 表。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.EntityMetadata, ProtocolVersion.V12110)]
public sealed record EntityMetadataPacket(int EntityId, byte[] MetadataData)
{
    public static EntityMetadataPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        return new(reader.ReadVarInt(), reader.ReadBytes(reader.Remaining));
    }

    public IReadOnlyList<MetadataEntry> ReadEntries()
    {
        var entries = MetadataCodec.Read(MetadataData, MetadataProtocol.V12110, out var consumed);
        if (consumed != MetadataData.Length) throw new InvalidDataException("实体元数据末尾包含多余字段。");
        return entries;
    }

    public bool TryReadEntries(out IReadOnlyList<MetadataEntry> entries)
    {
        if (MetadataCodec.TryRead(MetadataData, MetadataProtocol.V12110, out entries, out var consumed)
            && consumed == MetadataData.Length) return true;
        entries = [];
        return false;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteVarInt(EntityId).WriteBytes(MetadataData).ToArray();
    }
}
