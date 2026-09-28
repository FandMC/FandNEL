using DotNetty.Buffers;
using MessagePack = FandNEL.Proxy.Packet.Heypixel.MessagePackBufferExtensions;

namespace FandNEL.Proxy.Packet.Heypixel;

public sealed record HeypixelReportPacket(
    Uuid128 SessionId, string Key, long ChallengeTimestamp, HeypixelReportData Data) : IHeypixelPacket
{
    public int Id => HeypixelPacketIds.Report;

    public void WriteBody(IByteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(SessionId);
        ArgumentNullException.ThrowIfNull(Data);
        MessagePack.WriteString(buffer, SessionId.ToString());
        MessagePack.WriteByte(buffer, (byte)Data.Type);
        MessagePack.WriteString(buffer, Key);
        MessagePack.WriteInt64(buffer, ChallengeTimestamp);
        Data.Write(buffer);
    }
}
