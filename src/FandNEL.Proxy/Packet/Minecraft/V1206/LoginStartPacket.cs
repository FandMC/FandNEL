using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public sealed record LoginStartPacket(string Name, Guid PlayerUuid)
{
    public static LoginStartPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var packet = new LoginStartPacket(reader.ReadString(16), reader.ReadUuid());
        if (reader.Remaining != 0) throw new InvalidDataException("Login Start 包末尾存在多余数据。");
        return packet;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteString(Name, 16).WriteUuid(PlayerUuid).ToArray();
    }
}
