using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public sealed record SetCompressionPacket(int Threshold)
{
    public static SetCompressionPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var packet = new SetCompressionPacket(reader.ReadVarInt());
        if (reader.Remaining != 0) throw new InvalidDataException("Set Compression 包末尾存在多余数据。");
        return packet;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteVarInt(Threshold).ToArray();
    }
}
