using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1218.EntityPositionSyncPacket;

namespace FandNEL.Proxy.Packet.Minecraft.V12110;

/// <summary>字段与 V1218 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.EntityPositionSync, ProtocolVersion.V12110)]
public sealed record EntityPositionSyncPacket(int EntityId, double X, double Y, double Z,
    double VelocityX, double VelocityY, double VelocityZ, float Yaw, float Pitch, bool OnGround)
{
    public static EntityPositionSyncPacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.EntityId, packet.X, packet.Y, packet.Z, packet.VelocityX, packet.VelocityY, packet.VelocityZ, packet.Yaw, packet.Pitch, packet.OnGround);
    }

    public byte[] Write() => new WirePacket(EntityId, X, Y, Z, VelocityX, VelocityY, VelocityZ, Yaw, Pitch, OnGround).Write();
}
