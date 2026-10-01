using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>握手的原版字段；未识别的扩展尾部原样保留。</summary>
[RegisterPacketModel(ConnectionState.Handshaking, PacketDirection.ServerBound, MinecraftPacketIds.Handshake.Serverbound)]
public sealed record HandshakePacket(int ProtocolVersion, string ServerAddress, ushort ServerPort, int NextState, byte[] TrailingData)
{
    public static HandshakePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var version = reader.ReadVarInt();
        var address = reader.ReadString(255);
        var port = reader.ReadUnsignedShort();
        var state = reader.ReadVarInt();
        return new(version, address, port, state, reader.ReadBytes(reader.Remaining));
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteVarInt(ProtocolVersion).WriteString(ServerAddress, 255).WriteUnsignedShort(ServerPort)
            .WriteVarInt(NextState).WriteBytes(TrailingData).ToArray();
    }
}
