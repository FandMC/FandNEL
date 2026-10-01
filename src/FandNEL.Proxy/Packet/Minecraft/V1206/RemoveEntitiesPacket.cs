using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.RemoveEntities, ProtocolVersion.V1206)]
public sealed record RemoveEntitiesPacket(int[] EntityIds)
{
    public static RemoveEntitiesPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var count = MinecraftPacketValidation.ReadCount(reader);
        var ids = new int[count];
        for (var i = 0; i < count; i++) ids[i] = reader.ReadVarInt();
        MinecraftPacketValidation.RequireEnd(reader);
        return new(ids);
    }

    public byte[] Write()
    {
        if (EntityIds.Length > MinecraftPacketValidation.MaximumCollectionLength)
            throw new InvalidDataException("移除实体数量超出允许范围。");
        using var writer = new PacketWriter();
        writer.WriteVarInt(EntityIds.Length);
        foreach (var id in EntityIds) writer.WriteVarInt(id);
        return writer.ToArray();
    }
}
