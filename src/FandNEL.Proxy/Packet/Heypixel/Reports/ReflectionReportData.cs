using DotNetty.Buffers;
using MessagePack = FandNEL.Proxy.Packet.Heypixel.MessagePackBufferExtensions;

namespace FandNEL.Proxy.Packet.Heypixel;

/// <summary>已由业务层查表并加密的反射结果，包层不执行反射或读取元数据。</summary>
public sealed record ReflectionReportData(int HashCode, string EncryptedContent) : HeypixelReportData
{
    public override HeypixelReportType Type => HeypixelReportType.Reflect;
    public override void Write(IByteBuffer buffer)
    {
        MessagePack.WriteInt32(buffer, HashCode);
        MessagePack.WriteString(buffer, EncryptedContent);
    }
}
