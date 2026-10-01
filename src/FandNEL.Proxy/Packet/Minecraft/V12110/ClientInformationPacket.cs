using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1218.ClientInformationPacket;

namespace FandNEL.Proxy.Packet.Minecraft.V12110;

/// <summary>字段与 V1218 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Configuration, PacketDirection.ServerBound, MinecraftPacketIds.Configuration.ServerboundClientInformation, ProtocolVersion.V12110)]
public sealed record ClientInformationPacket(string Locale, byte ViewDistance, int ChatMode, bool ChatColors,
    byte DisplayedSkinParts, int MainHand, bool EnableTextFiltering, bool AllowServerListings, int ParticleStatus)
{
    public static ClientInformationPacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.Locale, packet.ViewDistance, packet.ChatMode, packet.ChatColors, packet.DisplayedSkinParts, packet.MainHand, packet.EnableTextFiltering, packet.AllowServerListings, packet.ParticleStatus);
    }

    public byte[] Write() => new WirePacket(Locale, ViewDistance, ChatMode, ChatColors, DisplayedSkinParts, MainHand, EnableTextFiltering, AllowServerListings, ParticleStatus).Write();
}
