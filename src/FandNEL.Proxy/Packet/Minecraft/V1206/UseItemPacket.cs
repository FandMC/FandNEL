using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.UseItem, ProtocolVersion.V1206)]
public sealed record UseItemPacket(int Hand, int Sequence)
{
    public static UseItemPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new UseItemPacket(reader.ReadVarInt(), reader.ReadVarInt());
        MinecraftPacketValidation.RequireEnd(reader);
        MinecraftPacketValidation.RequireHand(result.Hand);
        return result;
    }

    public byte[] Write()
    {
        MinecraftPacketValidation.RequireHand(Hand);
        using var writer = new PacketWriter();
        return writer.WriteVarInt(Hand).WriteVarInt(Sequence).ToArray();
    }
}
