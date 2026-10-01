using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1206.UseItemPacket;

namespace FandNEL.Proxy.Packet.Minecraft.V1200;

/// <summary>字段与 V1206 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.UseItem, ProtocolVersion.V1200)]
public sealed record UseItemPacket(int Hand, int Sequence)
{
    public static UseItemPacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.Hand, packet.Sequence);
    }

    public byte[] Write() => new WirePacket(Hand, Sequence).Write();
}
