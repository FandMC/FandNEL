using FandNEL.Proxy.Protocol;
using MinecraftPacketValidation = FandNEL.Proxy.Packet.Minecraft.V1206.MinecraftPacketValidation;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1218;

[RegisterPacketModel(ConnectionState.Configuration, PacketDirection.ServerBound, MinecraftPacketIds.Configuration.ServerboundClientInformation, ProtocolVersion.V1218)]
public sealed record ClientInformationPacket(string Locale, byte ViewDistance, int ChatMode, bool ChatColors,
    byte DisplayedSkinParts, int MainHand, bool EnableTextFiltering, bool AllowServerListings, int ParticleStatus)
{
    public static ClientInformationPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new ClientInformationPacket(reader.ReadString(16), reader.ReadByte(), reader.ReadVarInt(),
            reader.ReadBoolean(), reader.ReadByte(), reader.ReadVarInt(), reader.ReadBoolean(),
            reader.ReadBoolean(), reader.ReadVarInt());
        MinecraftPacketValidation.RequireEnd(reader);
        return result;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteString(Locale, 16).WriteByte(ViewDistance).WriteVarInt(ChatMode)
            .WriteBoolean(ChatColors).WriteByte(DisplayedSkinParts).WriteVarInt(MainHand)
            .WriteBoolean(EnableTextFiltering).WriteBoolean(AllowServerListings).WriteVarInt(ParticleStatus).ToArray();
    }
}
