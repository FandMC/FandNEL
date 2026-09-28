using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>玩家移动的四种线格式；缺席坐标用 null 表示，不持有连接状态。</summary>
public sealed record PlayerMovementPacket(int PacketId, double? X, double? Y, double? Z,
    float? Yaw, float? Pitch, bool OnGround)
{
    public static PlayerMovementPacket Read(int packetId, ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        double? x = null, y = null, z = null;
        float? yaw = null, pitch = null;
        var (position, rotation) = Fields(packetId);
        if (position) { x = reader.ReadDouble(); y = reader.ReadDouble(); z = reader.ReadDouble(); }
        if (rotation) { yaw = reader.ReadFloat(); pitch = reader.ReadFloat(); }
        var result = new PlayerMovementPacket(packetId, x, y, z, yaw, pitch, reader.ReadBoolean());
        MinecraftPacketValidation.RequireEnd(reader);
        MinecraftPacketValidation.RequireFinite(x, y, z, yaw, pitch);
        return result;
    }

    public byte[] Write()
    {
        var (position, rotation) = Fields(PacketId);
        if (X.HasValue != position || Y.HasValue != position || Z.HasValue != position ||
            Yaw.HasValue != rotation || Pitch.HasValue != rotation)
            throw new InvalidDataException("移动包字段与包类型不匹配。");
        MinecraftPacketValidation.RequireFinite(X, Y, Z, Yaw, Pitch);
        using var writer = new PacketWriter();
        if (position) writer.WriteDouble(X!.Value).WriteDouble(Y!.Value).WriteDouble(Z!.Value);
        if (rotation) writer.WriteFloat(Yaw!.Value).WriteFloat(Pitch!.Value);
        return writer.WriteBoolean(OnGround).ToArray();
    }

    private static (bool Position, bool Rotation) Fields(int packetId) => packetId switch
    {
        MinecraftPacketIds.Serverbound.Position => (true, false),
        MinecraftPacketIds.Serverbound.PositionAndRotation => (true, true),
        MinecraftPacketIds.Serverbound.Rotation => (false, true),
        MinecraftPacketIds.Serverbound.OnGround => (false, false),
        _ => throw new ArgumentOutOfRangeException(nameof(packetId), packetId, "不是玩家移动包。")
    };
}
