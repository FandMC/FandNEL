using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public sealed record EntityTeleportPacket(int EntityId, double X, double Y, double Z, byte Yaw, byte Pitch, bool OnGround)
{
    public static EntityTeleportPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new EntityTeleportPacket(reader.ReadVarInt(), reader.ReadDouble(), reader.ReadDouble(),
            reader.ReadDouble(), reader.ReadByte(), reader.ReadByte(), reader.ReadBoolean());
        MinecraftPacketValidation.RequireEnd(reader);
        MinecraftPacketValidation.RequireFinite(result.X, result.Y, result.Z);
        return result;
    }

    public byte[] Write()
    {
        MinecraftPacketValidation.RequireFinite(X, Y, Z);
        using var writer = new PacketWriter();
        return writer.WriteVarInt(EntityId).WriteDouble(X).WriteDouble(Y).WriteDouble(Z)
            .WriteByte(Yaw).WriteByte(Pitch).WriteBoolean(OnGround).ToArray();
    }
}
