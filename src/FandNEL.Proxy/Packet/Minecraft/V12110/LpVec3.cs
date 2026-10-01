using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V12110;

/// <summary>1.21.10 的压缩速度向量；零向量占一个字节，其余使用 48 位及可选缩放值。</summary>
public readonly record struct LpVec3(double X, double Y, double Z)
{
    private const double MaximumValue = 17_179_869_183d;
    private const double MinimumValue = 3.051944088384301E-5d;
    private const double MaximumQuantizedValue = 32_766d;

    public static LpVec3 Read(PacketReader reader)
    {
        var lowest = reader.ReadByte();
        if (lowest == 0) return default;
        var middle = reader.ReadByte();
        var highest = unchecked((uint)reader.ReadInt());
        var packed = (long)highest << 16 | (long)middle << 8 | lowest;
        long scale = lowest & 3;
        if ((lowest & 4) != 0) scale |= (long)unchecked((uint)reader.ReadVarInt()) << 2;
        return new(Unpack(packed >> 3) * scale, Unpack(packed >> 18) * scale, Unpack(packed >> 33) * scale);
    }

    public void Write(PacketWriter writer)
    {
        var x = Sanitize(X);
        var y = Sanitize(Y);
        var z = Sanitize(Z);
        var maximumAxis = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
        if (maximumAxis < MinimumValue) { writer.WriteByte(0); return; }
        var scale = checked((long)Math.Ceiling(maximumAxis));
        var hasContinuation = (scale & 3L) != scale;
        var markers = hasContinuation ? (scale & 3L) | 4L : scale;
        var packed = markers | Pack(x / scale) << 3 | Pack(y / scale) << 18 | Pack(z / scale) << 33;
        writer.WriteByte(unchecked((byte)packed)).WriteByte(unchecked((byte)(packed >> 8)))
            .WriteInt(unchecked((int)(packed >> 16)));
        if (hasContinuation) writer.WriteVarInt(unchecked((int)(scale >> 2)));
    }

    private static double Sanitize(double value) => double.IsNaN(value) ? 0d : Math.Clamp(value, -MaximumValue, MaximumValue);
    private static long Pack(double value) => (long)Math.Floor((value * 0.5d + 0.5d) * MaximumQuantizedValue + 0.5d);
    private static double Unpack(long value) => Math.Min(value & 32_767L, MaximumQuantizedValue) * 2d / MaximumQuantizedValue - 1d;
}
