using System.Security.Cryptography;
using DotNetty.Buffers;
using FandNEL.Proxy.Packet.IO;
using MessagePack = FandNEL.Proxy.Packet.Heypixel.MessagePackBufferExtensions;

namespace FandNEL.Proxy.Packet.Heypixel;

public enum HeypixelReportType { Info, BlackClass, BlackModule, Reflect }

/// <summary>挑战的纯解码结果；是否接受挑战由调用方在业务校验后决定。</summary>
public sealed record HeypixelChallengePacket(string Key, long Timestamp, HeypixelReportType ReportType, string Payload)
{
    public static HeypixelChallengePacket? Read(ReadOnlyMemory<byte> payload, SaltedDesCipher cipher)
    {
        ArgumentNullException.ThrowIfNull(cipher);
        if (payload.Length > HeypixelPacketIds.MaximumPayloadLength)
            throw new InvalidDataException("Heypixel challenge exceeds its size limit.");
        var reader = new PacketReader(payload);
        if (reader.ReadVarInt() != HeypixelPacketIds.InboundDiscriminator) return null;
        if (reader.ReadInt() != HeypixelPacketIds.Challenge) return null;
        var encrypted = reader.ReadByteArray(HeypixelPacketIds.MaximumPayloadLength);
        if (reader.Remaining != 0)
            throw new InvalidDataException("Heypixel challenge has trailing data.");
        IByteBuffer? body = null;
        try
        {
            body = Unpooled.WrappedBuffer(cipher.Decrypt(encrypted));
            var key = MessagePack.ReadString(body);
            var timestamp = MessagePack.ReadInt64(body);
            var type = (HeypixelReportType)MessagePack.ReadInt32(body);
            if (!Enum.IsDefined(type)) throw new InvalidDataException("Unknown Heypixel report type.");
            var content = type == HeypixelReportType.Reflect ? MessagePack.ReadString(body) : string.Empty;
            if (body.IsReadable()) throw new InvalidDataException("Heypixel challenge body has trailing data.");
            return new HeypixelChallengePacket(key, timestamp, type, content);
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException or IndexOutOfRangeException or OverflowException)
        {
            throw new InvalidDataException("Invalid Heypixel challenge payload.");
        }
        finally { body?.Release(); }
    }
}
