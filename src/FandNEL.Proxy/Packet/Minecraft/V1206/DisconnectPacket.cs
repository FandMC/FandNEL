using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.Nbt;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>Play 和 Configuration 的网络 NBT 断开提示；Login 使用 JSON 字符串。</summary>
[RegisterPacketModel(ConnectionState.Configuration, PacketDirection.ClientBound, MinecraftPacketIds.Configuration.ClientboundDisconnect, ProtocolVersion.V1206)]
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.Disconnect, ProtocolVersion.V1206)]
public sealed record DisconnectPacket(NbtTag Reason)
{
    public static DisconnectPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new DisconnectPacket(reader.ReadNetworkNbt());
        MinecraftPacketValidation.RequireEnd(reader);
        return result;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteNetworkNbt(Reason).ToArray();
    }
}
