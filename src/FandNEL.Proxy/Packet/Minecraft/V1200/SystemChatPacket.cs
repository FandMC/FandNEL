using FandNEL.Proxy.Protocol;
using MinecraftPacketValidation = FandNEL.Proxy.Packet.Minecraft.V1206.MinecraftPacketValidation;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1200;

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SystemChat, ProtocolVersion.V1200)]
public sealed record SystemChatPacket(string Message, bool Overlay)
{
    public static SystemChatPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new SystemChatPacket(reader.ReadString(65536), reader.ReadBoolean());
        MinecraftPacketValidation.RequireEnd(reader);
        return result;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteString(Message, 65536).WriteBoolean(Overlay).ToArray();
    }
}
