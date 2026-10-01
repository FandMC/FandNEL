using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>Configuration 和 Play 共用的自定义载荷字段。</summary>
[RegisterPacketModel(ConnectionState.Configuration, PacketDirection.ServerBound, MinecraftPacketIds.Configuration.ServerboundCustomPayload, ProtocolVersion.V1206)]
[RegisterPacketModel(ConnectionState.Configuration, PacketDirection.ClientBound, MinecraftPacketIds.Configuration.ClientboundCustomPayload, ProtocolVersion.V1206)]
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.CustomPayload, ProtocolVersion.V1206)]
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.CustomPayload, ProtocolVersion.V1206)]
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
