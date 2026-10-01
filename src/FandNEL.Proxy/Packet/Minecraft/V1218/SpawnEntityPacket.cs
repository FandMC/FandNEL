using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1206.SpawnEntityPacket;

namespace FandNEL.Proxy.Packet.Minecraft.V1218;

/// <summary>字段与 V1206 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SpawnEntity, ProtocolVersion.V1218)]
public sealed record SpawnEntityPacket(int EntityId, Guid Uuid, int EntityType, double X, double Y, double Z,
    byte Pitch, byte Yaw, byte HeadYaw, int Data, short VelocityX, short VelocityY, short VelocityZ)
{
    public static SpawnEntityPacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.EntityId, packet.Uuid, packet.EntityType, packet.X, packet.Y, packet.Z, packet.Pitch, packet.Yaw, packet.HeadYaw, packet.Data, packet.VelocityX, packet.VelocityY, packet.VelocityZ);
    }

    public byte[] Write() => new WirePacket(EntityId, Uuid, EntityType, X, Y, Z, Pitch, Yaw, HeadYaw, Data, VelocityX, VelocityY, VelocityZ).Write();
}
