using DotNetty.Buffers;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Heypixel;

public static class HeypixelPacketCodec
{
    public static byte[] Encode(IHeypixelPacket packet, long timestamp, Uuid128 sessionId,
        SaltedDesCipher cipher, bool protectReport)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(cipher);
        var body = Unpooled.Buffer();
        try
        {
            MessagePackBufferExtensions.WriteInt64(body, timestamp);
            packet.WriteBody(body);
            var payload = new byte[body.ReadableBytes];
            body.ReadBytes(payload);
            var protect = packet.Id == HeypixelPacketIds.Report && protectReport;
            if (protect) payload = cipher.Encrypt(payload);
            using var envelope = new PacketWriter();
            // 私有协议约定长度包含额外的 1，但线上没有额外的尾字节。
            envelope.WriteVarInt(packet.Id).WriteVarInt(checked(payload.Length + 1));
            if (protect)
            {
                body.Clear();
                PayloadScrambler.Write(body, payload, sessionId);
                body.ReadBytes(payload);
            }
            return envelope.WriteBytes(payload).ToArray();
        }
        finally { body.Release(); }
    }
}
