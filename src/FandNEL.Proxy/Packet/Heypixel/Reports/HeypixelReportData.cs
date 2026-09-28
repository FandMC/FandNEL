using DotNetty.Buffers;

namespace FandNEL.Proxy.Packet.Heypixel;

public abstract record HeypixelReportData
{
    public abstract HeypixelReportType Type { get; }
    public abstract void Write(IByteBuffer buffer);
}
