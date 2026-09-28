using DotNetty.Buffers;

namespace FandNEL.Proxy.Packet.Heypixel;

/// <summary>只描述消息体；信封时间戳和保护变换由编码器统一处理。</summary>
public interface IHeypixelPacket
{
    int Id { get; }
    void WriteBody(IByteBuffer buffer);
}
