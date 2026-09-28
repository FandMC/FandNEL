using System.Text;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Heypixel;

/// <summary>Floodgate 表单频道的原始表单；内容是否应当响应由调用方决定。</summary>
public sealed record FloodgateFormPacket(byte Type, ushort FormId, string Content)
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static FloodgateFormPacket Read(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length > HeypixelPacketIds.MaximumPayloadLength)
            throw new InvalidDataException("Floodgate form exceeds its size limit.");
        var reader = new PacketReader(payload);
        var type = reader.ReadByte();
        var formId = reader.ReadUnsignedShort();
        try
        {
            return new FloodgateFormPacket(type, formId, StrictUtf8.GetString(reader.ReadBytes(reader.Remaining)));
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException("Invalid Floodgate form encoding.");
        }
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteByte(Type).WriteUnsignedShort(FormId).WriteBytes(StrictUtf8.GetBytes(Content)).ToArray();
    }
}
