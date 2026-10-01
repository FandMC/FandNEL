using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.Nbt;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SystemChat, ProtocolVersion.V1206)]
public sealed record SystemChatPacket(NbtTag Content, bool Overlay)
{
    public static SystemChatPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new SystemChatPacket(reader.ReadNetworkNbt(), reader.ReadBoolean());
        MinecraftPacketValidation.RequireEnd(reader);
        return result;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteNetworkNbt(Content).WriteBoolean(Overlay).ToArray();
    }
}
