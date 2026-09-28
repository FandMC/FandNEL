using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public sealed record EncryptionResponsePacket(byte[] SharedSecret, byte[] VerifyToken)
{
    public static EncryptionResponsePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var packet = new EncryptionResponsePacket(reader.ReadByteArray(4096), reader.ReadByteArray(4096));
        if (reader.Remaining != 0) throw new InvalidDataException("Encryption Response 包末尾存在多余数据。");
        return packet;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteByteArray(SharedSecret).WriteByteArray(VerifyToken).ToArray();
    }
}
