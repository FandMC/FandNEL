using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1206.CustomPayloadPacket;

namespace FandNEL.Proxy.Packet.Minecraft.V12110;

/// <summary>字段与 V1206 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Configuration, PacketDirection.ServerBound, MinecraftPacketIds.Configuration.ServerboundCustomPayload, ProtocolVersion.V12110)]
[RegisterPacketModel(ConnectionState.Configuration, PacketDirection.ClientBound, MinecraftPacketIds.Configuration.ClientboundCustomPayload, ProtocolVersion.V12110)]
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.CustomPayload, ProtocolVersion.V12110)]
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.CustomPayload, ProtocolVersion.V12110)]
public sealed record CustomPayloadPacket(string Channel, byte[] Data)
{
    public static CustomPayloadPacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.Channel, packet.Data);
    }

    public byte[] Write() => new WirePacket(Channel, Data).Write();
}
