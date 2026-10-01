using FandNEL.Proxy.Protocol;
namespace FandNEL.Proxy.Packet.Minecraft.V108X;

/// <summary>1.8.x 玩家挥手动画包，没有载荷字段。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.Animation, ProtocolVersion.V108X)]
public sealed record AnimationPacket
{
    public static AnimationPacket Read(ReadOnlyMemory<byte> payload)
    {
        if (!payload.IsEmpty) throw new InvalidDataException("动画包不应包含载荷。");
        return new();
    }

    public byte[] Write() => [];
}
