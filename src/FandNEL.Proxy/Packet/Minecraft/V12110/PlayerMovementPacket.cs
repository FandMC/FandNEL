using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1218.PlayerMovementPacket;

namespace FandNEL.Proxy.Packet.Minecraft.V12110;

/// <summary>两个版本的四种移动包 ID 与字段相同，保留完整标志字节。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.Position, ProtocolVersion.V12110)]
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.PositionAndRotation, ProtocolVersion.V12110)]
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.Rotation, ProtocolVersion.V12110)]
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.OnGround, ProtocolVersion.V12110)]
public sealed record PlayerMovementPacket(int PacketId, double? X, double? Y, double? Z,
    float? Yaw, float? Pitch, byte Flags)
{
    public bool OnGround => (Flags & 0x01) != 0;

    public static PlayerMovementPacket Read(int packetId, ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(packetId, payload);
        return new(packetId, packet.X, packet.Y, packet.Z, packet.Yaw, packet.Pitch, packet.Flags);
    }

    public byte[] Write() => new WirePacket(PacketId, X, Y, Z, Yaw, Pitch, Flags).Write();
}
