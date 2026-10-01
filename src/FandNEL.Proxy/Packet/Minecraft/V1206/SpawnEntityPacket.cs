using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SpawnEntity, ProtocolVersion.V1206)]
public sealed record SpawnEntityPacket(int EntityId, Guid Uuid, int EntityType, double X, double Y, double Z,
    byte Pitch, byte Yaw, byte HeadYaw, int Data, short VelocityX, short VelocityY, short VelocityZ)
{
    public static SpawnEntityPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new SpawnEntityPacket(reader.ReadVarInt(), reader.ReadUuid(), reader.ReadVarInt(),
            reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadByte(), reader.ReadByte(),
            reader.ReadByte(), reader.ReadVarInt(), unchecked((short)reader.ReadUnsignedShort()),
            unchecked((short)reader.ReadUnsignedShort()), unchecked((short)reader.ReadUnsignedShort()));
        MinecraftPacketValidation.RequireEnd(reader);
        MinecraftPacketValidation.RequireFinite(result.X, result.Y, result.Z);
        return result;
    }

    public byte[] Write()
    {
        MinecraftPacketValidation.RequireFinite(X, Y, Z);
        using var writer = new PacketWriter();
        return writer.WriteVarInt(EntityId).WriteUuid(Uuid).WriteVarInt(EntityType)
            .WriteDouble(X).WriteDouble(Y).WriteDouble(Z).WriteByte(Pitch).WriteByte(Yaw).WriteByte(HeadYaw)
            .WriteVarInt(Data).WriteUnsignedShort(unchecked((ushort)VelocityX))
            .WriteUnsignedShort(unchecked((ushort)VelocityY)).WriteUnsignedShort(unchecked((ushort)VelocityZ)).ToArray();
    }
}
