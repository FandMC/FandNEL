using FandNEL.Proxy.Protocol;
using System.Text.Json;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

[RegisterPacketModel(ConnectionState.Login, PacketDirection.ClientBound, MinecraftPacketIds.Login.ClientboundDisconnect, ProtocolVersion.V1206, ProtocolVersion.V1210, ProtocolVersion.V1218, ProtocolVersion.V12110)]
public sealed record LoginDisconnectPacket(string ReasonJson)
{
    public static LoginDisconnectPacket FromText(string text) => new(JsonSerializer.Serialize(new { text }));

    public static LoginDisconnectPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var packet = new LoginDisconnectPacket(reader.ReadString());
        if (reader.Remaining != 0) throw new InvalidDataException("Login Disconnect 包末尾存在多余数据。");
        return packet;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteString(ReasonJson).ToArray();
    }
}
