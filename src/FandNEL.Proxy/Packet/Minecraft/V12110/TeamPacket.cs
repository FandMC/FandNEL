using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1218.TeamPacket;

namespace FandNEL.Proxy.Packet.Minecraft.V12110;

/// <summary>字段与 V1218 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.Team, ProtocolVersion.V12110)]
public sealed record TeamPacket(string TeamName, byte Method, byte[] RemainingData)
{
    public static TeamPacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.TeamName, packet.Method, packet.RemainingData);
    }

    public byte[] Write() => new WirePacket(TeamName, Method, RemainingData).Write();
}
