using FandNEL.Proxy.Protocol;
using MinecraftPacketValidation = FandNEL.Proxy.Packet.Minecraft.V1206.MinecraftPacketValidation;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1218;

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.UseItem, ProtocolVersion.V1218)]
public sealed record UseItemPacket(int Hand, int Sequence, float Yaw, float Pitch)
{
    public static UseItemPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new UseItemPacket(reader.ReadVarInt(), reader.ReadVarInt(), reader.ReadFloat(), reader.ReadFloat());
        MinecraftPacketValidation.RequireEnd(reader);
        MinecraftPacketValidation.RequireHand(result.Hand);
        MinecraftPacketValidation.RequireFinite(null, null, null, result.Yaw, result.Pitch);
        return result;
    }

    public byte[] Write()
    {
        MinecraftPacketValidation.RequireHand(Hand);
        MinecraftPacketValidation.RequireFinite(null, null, null, Yaw, Pitch);
        using var writer = new PacketWriter();
        return writer.WriteVarInt(Hand).WriteVarInt(Sequence).WriteFloat(Yaw).WriteFloat(Pitch).ToArray();
    }
}
