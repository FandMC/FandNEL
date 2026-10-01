using FandNEL.Proxy.Protocol;
using MinecraftPacketValidation = FandNEL.Proxy.Packet.Minecraft.V1206.MinecraftPacketValidation;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1200;

/// <summary>1.20.0 通用实体生成包；尾部实体数据原样保留。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SpawnEntity, ProtocolVersion.V1200)]
public sealed record SpawnEntityPacket(int EntityId, Guid Uuid, int EntityType, double X, double Y, double Z,
    byte Pitch, byte Yaw, byte[] RemainingData)
{
    public static SpawnEntityPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new SpawnEntityPacket(reader.ReadVarInt(), reader.ReadUuid(), reader.ReadVarInt(),
            reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadByte(), reader.ReadByte(),
            reader.ReadBytes(reader.Remaining));
        MinecraftPacketValidation.RequireFinite(result.X, result.Y, result.Z);
        return result;
    }

    public byte[] Write()
    {
        MinecraftPacketValidation.RequireFinite(X, Y, Z);
        using var writer = new PacketWriter();
        return writer.WriteVarInt(EntityId).WriteUuid(Uuid).WriteVarInt(EntityType)
            .WriteDouble(X).WriteDouble(Y).WriteDouble(Z).WriteByte(Pitch).WriteByte(Yaw)
            .WriteBytes(RemainingData).ToArray();
    }
}
