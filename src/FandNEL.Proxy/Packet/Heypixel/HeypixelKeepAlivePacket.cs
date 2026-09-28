using DotNetty.Buffers;

namespace FandNEL.Proxy.Packet.Heypixel;

public sealed record HeypixelKeepAlivePacket(long Timestamp) : IHeypixelPacket
{
    public int Id => HeypixelPacketIds.KeepAlive;
    public void WriteBody(IByteBuffer buffer) => MessagePackBufferExtensions.WriteInt64(buffer, Timestamp);
}
