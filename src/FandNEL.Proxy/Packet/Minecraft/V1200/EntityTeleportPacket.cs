using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1206.EntityTeleportPacket;

namespace FandNEL.Proxy.Packet.Minecraft.V1200;

/// <summary>字段与 V1206 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.EntityTeleport, ProtocolVersion.V1200)]
public sealed record EntityTeleportPacket(int EntityId, double X, double Y, double Z, byte Yaw, byte Pitch, bool OnGround)
{
    public static EntityTeleportPacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.EntityId, packet.X, packet.Y, packet.Z, packet.Yaw, packet.Pitch, packet.OnGround);
    }

    public byte[] Write() => new WirePacket(EntityId, X, Y, Z, Yaw, Pitch, OnGround).Write();
}
