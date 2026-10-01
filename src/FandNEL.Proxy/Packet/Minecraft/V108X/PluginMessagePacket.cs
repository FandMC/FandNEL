using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V108X;

/// <summary>1.8.x 插件消息；载荷占用包的剩余字节。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.PluginMessage, ProtocolVersion.V108X)]
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.PluginMessage, ProtocolVersion.V108X)]
public sealed record PluginMessagePacket(string Channel, byte[] Data)
{
    public static PluginMessagePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        return new(reader.ReadString(), reader.ReadBytes(reader.Remaining));
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteString(Channel, 32).WriteBytes(Data).ToArray();
    }
}
