using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>Configuration 和 Play 共用的自定义载荷字段。</summary>
public sealed record CustomPayloadPacket(string Channel, byte[] Data)
{
    public static CustomPayloadPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var channel = reader.ReadString();
        return new(channel, reader.ReadBytes(reader.Remaining));
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteString(Channel).WriteBytes(Data).ToArray();
    }
}
