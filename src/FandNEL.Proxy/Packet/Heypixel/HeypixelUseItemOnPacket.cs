using DotNetty.Buffers;
using MessagePack = FandNEL.Proxy.Packet.Heypixel.MessagePackBufferExtensions;

namespace FandNEL.Proxy.Packet.Heypixel;

/// <summary>物品交互遥测；调用方提供快照，编码过程不读取玩家或连接状态。</summary>
public sealed record HeypixelUseItemOnPacket(
    float PlayerX, float PlayerY, float PlayerZ, int Face,
    float HitX, float HitY, float HitZ,
    double BlockX, double BlockY, double BlockZ,
    bool InsideBlock, float Yaw, float Pitch, bool MainHand) : IHeypixelPacket
{
    private const int InteractionType = 1;
    public int Id => HeypixelPacketIds.UseItemOn;

    public void WriteBody(IByteBuffer buffer)
    {
        MessagePack.WriteSingle(buffer, PlayerX);
        MessagePack.WriteSingle(buffer, PlayerY);
        MessagePack.WriteSingle(buffer, PlayerZ);
        MessagePack.WriteInt32(buffer, Face);
        MessagePack.WriteInt32(buffer, InteractionType);
        MessagePack.WriteSingle(buffer, HitX);
        MessagePack.WriteSingle(buffer, HitY);
        MessagePack.WriteSingle(buffer, HitZ);
        MessagePack.WriteDouble(buffer, BlockX);
        MessagePack.WriteDouble(buffer, BlockY);
        MessagePack.WriteDouble(buffer, BlockZ);
        MessagePack.WriteBoolean(buffer, InsideBlock);
        MessagePack.WriteSingle(buffer, Yaw);
        MessagePack.WriteSingle(buffer, Pitch);
        MessagePack.WriteBoolean(buffer, MainHand);
    }
}
