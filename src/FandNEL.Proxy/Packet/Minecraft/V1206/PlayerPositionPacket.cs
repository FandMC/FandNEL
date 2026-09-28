using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>服务器位置校正；相对值标志的应用由连接的玩家状态负责。</summary>
public sealed record PlayerPositionPacket(double X, double Y, double Z, float Yaw, float Pitch,
    byte Flags, int TeleportId)
{
    public static PlayerPositionPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new PlayerPositionPacket(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(),
            reader.ReadFloat(), reader.ReadFloat(), reader.ReadByte(), reader.ReadVarInt());
        MinecraftPacketValidation.RequireEnd(reader);
        result.Validate();
        return result;
    }

    public byte[] Write()
    {
        Validate();
        using var writer = new PacketWriter();
        return writer.WriteDouble(X).WriteDouble(Y).WriteDouble(Z).WriteFloat(Yaw).WriteFloat(Pitch)
            .WriteByte(Flags).WriteVarInt(TeleportId).ToArray();
    }

    private void Validate()
    {
        MinecraftPacketValidation.RequireFinite(X, Y, Z, Yaw, Pitch);
        // 只解释低五位相对坐标标志；其余位原样保留，不能因此拒绝服务器传送包。
    }
}
