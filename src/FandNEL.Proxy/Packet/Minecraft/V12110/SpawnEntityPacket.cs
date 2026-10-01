using FandNEL.Proxy.Protocol;
using MinecraftPacketValidation = FandNEL.Proxy.Packet.Minecraft.V1206.MinecraftPacketValidation;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V12110;

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SpawnEntity, ProtocolVersion.V12110)]
public sealed record SpawnEntityPacket(int EntityId, Guid Uuid, int EntityType, double X, double Y, double Z,
    LpVec3 Velocity, byte Pitch, byte Yaw, byte HeadYaw, int Data)
{
    public static SpawnEntityPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new SpawnEntityPacket(reader.ReadVarInt(), reader.ReadUuid(), reader.ReadVarInt(),
            reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), LpVec3.Read(reader),
            reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadVarInt());
        MinecraftPacketValidation.RequireEnd(reader);
        MinecraftPacketValidation.RequireFinite(result.X, result.Y, result.Z);
        return result;
    }

    public byte[] Write()
    {
        MinecraftPacketValidation.RequireFinite(X, Y, Z);
        using var writer = new PacketWriter();
        writer.WriteVarInt(EntityId).WriteUuid(Uuid).WriteVarInt(EntityType).WriteDouble(X).WriteDouble(Y).WriteDouble(Z);
        Velocity.Write(writer);
        return writer.WriteByte(Pitch).WriteByte(Yaw).WriteByte(HeadYaw).WriteVarInt(Data).ToArray();
    }
}
