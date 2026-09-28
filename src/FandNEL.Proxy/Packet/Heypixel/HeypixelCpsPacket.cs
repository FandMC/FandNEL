using DotNetty.Buffers;

namespace FandNEL.Proxy.Packet.Heypixel;

public sealed record HeypixelCpsPacket(int Left, int Right) : IHeypixelPacket
{
    public int Id => HeypixelPacketIds.Cps;
    public void WriteBody(IByteBuffer buffer)
    {
        MessagePackBufferExtensions.WriteInt32(buffer, Left);
        MessagePackBufferExtensions.WriteInt32(buffer, Right);
    }
}
