using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1200.InteractPacket;

namespace FandNEL.Proxy.Packet.Minecraft.V12110;

/// <summary>字段与 V1200 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.Interact, ProtocolVersion.V12110)]
public sealed record InteractPacket(int EntityId, int Type, float? TargetX, float? TargetY, float? TargetZ,
    int? Hand, bool Sneaking)
{
    public static InteractPacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.EntityId, packet.Type, packet.TargetX, packet.TargetY, packet.TargetZ, packet.Hand, packet.Sneaking);
    }

    public byte[] Write() => new WirePacket(EntityId, Type, TargetX, TargetY, TargetZ, Hand, Sneaking).Write();
}
