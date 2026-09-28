using DotNetty.Buffers;
using MessagePack = FandNEL.Proxy.Packet.Heypixel.MessagePackBufferExtensions;

namespace FandNEL.Proxy.Packet.Heypixel;

public sealed record BlackModuleReportData(int Count, string EncryptedModuleName, string EncryptedModuleVersion) : HeypixelReportData
{
    public override HeypixelReportType Type => HeypixelReportType.BlackModule;
    public override void Write(IByteBuffer buffer)
    {
        MessagePack.WriteInt32(buffer, Count);
        MessagePack.WriteInt32(buffer, 1);
        MessagePack.WriteArrayHeader(buffer, 1);
        MessagePack.WriteString(buffer, EncryptedModuleName + ":" + EncryptedModuleVersion);
    }
}
