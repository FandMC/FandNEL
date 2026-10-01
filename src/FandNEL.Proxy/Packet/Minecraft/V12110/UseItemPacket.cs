using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1218.UseItemPacket;

namespace FandNEL.Proxy.Packet.Minecraft.V12110;

/// <summary>字段与 V1218 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.UseItem, ProtocolVersion.V12110)]
public sealed record UseItemPacket(int Hand, int Sequence, float Yaw, float Pitch)
{
    public static UseItemPacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.Hand, packet.Sequence, packet.Yaw, packet.Pitch);
    }

    public byte[] Write() => new WirePacket(Hand, Sequence, Yaw, Pitch).Write();
}
