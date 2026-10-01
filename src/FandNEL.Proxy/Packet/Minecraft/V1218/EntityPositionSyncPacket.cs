using FandNEL.Proxy.Protocol;
using MinecraftPacketValidation = FandNEL.Proxy.Packet.Minecraft.V1206.MinecraftPacketValidation;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1218;

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.EntityPositionSync, ProtocolVersion.V1218)]
public sealed record EntityPositionSyncPacket(int EntityId, double X, double Y, double Z,
    double VelocityX, double VelocityY, double VelocityZ, float Yaw, float Pitch, bool OnGround)
{
    public static EntityPositionSyncPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new EntityPositionSyncPacket(reader.ReadVarInt(), reader.ReadDouble(), reader.ReadDouble(),
            reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadFloat(),
            reader.ReadFloat(), reader.ReadBoolean());
        MinecraftPacketValidation.RequireEnd(reader);
        MinecraftPacketValidation.RequireFinite(result.X, result.Y, result.Z, result.Yaw, result.Pitch);
        MinecraftPacketValidation.RequireFinite(result.VelocityX, result.VelocityY, result.VelocityZ);
        return result;
    }

    public byte[] Write()
    {
        MinecraftPacketValidation.RequireFinite(X, Y, Z, Yaw, Pitch);
        MinecraftPacketValidation.RequireFinite(VelocityX, VelocityY, VelocityZ);
        using var writer = new PacketWriter();
        return writer.WriteVarInt(EntityId).WriteDouble(X).WriteDouble(Y).WriteDouble(Z)
            .WriteDouble(VelocityX).WriteDouble(VelocityY).WriteDouble(VelocityZ)
            .WriteFloat(Yaw).WriteFloat(Pitch).WriteBoolean(OnGround).ToArray();
    }
}
