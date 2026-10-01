using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1122;

/// <summary>1.12.2 客户端位置及视角更新。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.PlayerPosition, ProtocolVersion.V1122)]
public sealed record PlayerPositionPacket(double X, double Y, double Z, float Yaw, float Pitch, bool OnGround)
{
    public static PlayerPositionPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var packet = new PlayerPositionPacket(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(),
            reader.ReadFloat(), reader.ReadFloat(), reader.ReadBoolean());
        RequireFinite(packet);
        if (reader.Remaining != 0) throw new InvalidDataException("玩家位置包末尾包含多余字段。");
        return packet;
    }

    public byte[] Write()
    {
        RequireFinite(this);
        using var writer = new PacketWriter();
        return writer.WriteDouble(X).WriteDouble(Y).WriteDouble(Z).WriteFloat(Yaw).WriteFloat(Pitch)
            .WriteBoolean(OnGround).ToArray();
    }

    private static void RequireFinite(PlayerPositionPacket packet)
    {
        if (!double.IsFinite(packet.X) || !double.IsFinite(packet.Y) || !double.IsFinite(packet.Z) ||
            !float.IsFinite(packet.Yaw) || !float.IsFinite(packet.Pitch))
            throw new InvalidDataException("玩家位置或角度不是有限数值。");
    }
}
