using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1206.SystemChatPacket;
using FandNEL.Proxy.Packet.Minecraft.Nbt;

namespace FandNEL.Proxy.Packet.Minecraft.V1218;

/// <summary>字段与 V1206 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SystemChat, ProtocolVersion.V1218)]
public sealed record SystemChatPacket(NbtTag Content, bool Overlay)
{
    public static SystemChatPacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.Content, packet.Overlay);
    }

    public byte[] Write() => new WirePacket(Content, Overlay).Write();
}
