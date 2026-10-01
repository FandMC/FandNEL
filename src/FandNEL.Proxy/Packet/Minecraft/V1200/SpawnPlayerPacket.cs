using FandNEL.Proxy.Protocol;
using MinecraftPacketValidation = FandNEL.Proxy.Packet.Minecraft.V1206.MinecraftPacketValidation;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1200;

/// <summary>1.20.0 玩家实体生成包。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SpawnPlayer, ProtocolVersion.V1200)]
public sealed record SpawnPlayerPacket(int EntityId, Guid PlayerUuid, double X, double Y, double Z,
    byte Yaw, byte Pitch)
{
    public static SpawnPlayerPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new SpawnPlayerPacket(reader.ReadVarInt(), reader.ReadUuid(), reader.ReadDouble(),
            reader.ReadDouble(), reader.ReadDouble(), reader.ReadByte(), reader.ReadByte());
        MinecraftPacketValidation.RequireEnd(reader);
        MinecraftPacketValidation.RequireFinite(result.X, result.Y, result.Z);
        return result;
    }

    public byte[] Write()
    {
        MinecraftPacketValidation.RequireFinite(X, Y, Z);
        using var writer = new PacketWriter();
        return writer.WriteVarInt(EntityId).WriteUuid(PlayerUuid).WriteDouble(X).WriteDouble(Y).WriteDouble(Z)
            .WriteByte(Yaw).WriteByte(Pitch).ToArray();
    }
}
