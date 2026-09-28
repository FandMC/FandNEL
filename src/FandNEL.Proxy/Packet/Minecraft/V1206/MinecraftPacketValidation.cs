using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

internal static class MinecraftPacketValidation
{
    internal const int MaximumCollectionLength = 65536;

    internal static int ReadCount(PacketReader reader)
    {
        var count = reader.ReadVarInt();
        if (count < 0 || count > MaximumCollectionLength || count > reader.Remaining)
            throw new InvalidDataException("协议集合长度超出允许范围或剩余载荷。");
        return count;
    }

    internal static void RequireEnd(PacketReader reader)
    {
        if (reader.Remaining != 0) throw new InvalidDataException("包末尾包含多余字段。");
    }

    internal static void RequireFinite(double? x, double? y, double? z, float? yaw = null, float? pitch = null)
    {
        if ((x.HasValue && !double.IsFinite(x.Value)) || (y.HasValue && !double.IsFinite(y.Value)) ||
            (z.HasValue && !double.IsFinite(z.Value)) || (yaw.HasValue && !float.IsFinite(yaw.Value)) ||
            (pitch.HasValue && !float.IsFinite(pitch.Value)))
            throw new InvalidDataException("坐标或角度不是有限数值。");
    }

    internal static void RequireHand(int hand)
    {
        if (hand is not (0 or 1)) throw new InvalidDataException("交互包包含未知的手。");
    }
}
