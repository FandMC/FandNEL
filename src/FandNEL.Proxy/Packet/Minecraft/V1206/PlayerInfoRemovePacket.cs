using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public sealed record PlayerInfoRemovePacket(Guid[] PlayerIds)
{
    public static PlayerInfoRemovePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var count = MinecraftPacketValidation.ReadCount(reader);
        if (count > reader.Remaining / 16) throw new InvalidDataException("移除玩家数量超过剩余 UUID 载荷。");
        var ids = new Guid[count];
        for (var index = 0; index < count; index++) ids[index] = reader.ReadUuid();
        MinecraftPacketValidation.RequireEnd(reader);
        return new(ids);
    }

    public byte[] Write()
    {
        if (PlayerIds.Length > MinecraftPacketValidation.MaximumCollectionLength)
            throw new InvalidDataException("移除玩家数量超出允许范围。");
        using var writer = new PacketWriter();
        writer.WriteVarInt(PlayerIds.Length);
        foreach (var id in PlayerIds) writer.WriteUuid(id);
        return writer.ToArray();
    }
}
