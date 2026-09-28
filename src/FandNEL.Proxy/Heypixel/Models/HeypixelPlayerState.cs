using FandNEL.Proxy.Packet.Minecraft.V1206;

namespace FandNEL.Proxy.Heypixel;

/// <summary>玩家运行态由会话维护，原版包只负责自己的字段读写。</summary>
public sealed class HeypixelPlayerState(int entityId)
{
    public int EntityId { get; } = entityId;
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double Z { get; internal set; }
    public float Yaw { get; internal set; }
    public float Pitch { get; internal set; }
    public bool OnGround { get; internal set; }

    internal void Apply(PlayerMovementPacket packet)
    {
        SetPosition(packet.X ?? X, packet.Y ?? Y, packet.Z ?? Z, packet.Yaw ?? Yaw, packet.Pitch ?? Pitch);
        OnGround = packet.OnGround;
    }

    internal void Apply(PlayerPositionPacket packet) => SetPosition(
        packet.X + ((packet.Flags & 0x01) != 0 ? X : 0),
        packet.Y + ((packet.Flags & 0x02) != 0 ? Y : 0),
        packet.Z + ((packet.Flags & 0x04) != 0 ? Z : 0),
        packet.Yaw + ((packet.Flags & 0x08) != 0 ? Yaw : 0),
        packet.Pitch + ((packet.Flags & 0x10) != 0 ? Pitch : 0));

    private void SetPosition(double x, double y, double z, float yaw, float pitch)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) || !float.IsFinite(yaw) || !float.IsFinite(pitch))
            throw new InvalidDataException("玩家坐标或角度不是有限数值。");
        (X, Y, Z, Yaw, Pitch) = (x, y, z, yaw, pitch);
    }
}
