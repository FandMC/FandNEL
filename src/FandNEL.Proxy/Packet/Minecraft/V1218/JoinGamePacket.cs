using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1200.JoinGamePacket;

namespace FandNEL.Proxy.Packet.Minecraft.V1218;

/// <summary>字段与 V1200 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.JoinGame, ProtocolVersion.V1218)]
public sealed record JoinGamePacket(int EntityId, byte[] RemainingData)
{
    public static JoinGamePacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.EntityId, packet.RemainingData);
    }

    public byte[] Write() => new WirePacket(EntityId, RemainingData).Write();
}
