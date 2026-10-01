using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1206.UseItemOnPacket;

namespace FandNEL.Proxy.Packet.Minecraft.V1200;

/// <summary>字段与 V1206 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.UseItemOn, ProtocolVersion.V1200)]
public sealed record UseItemOnPacket(int Hand, BlockPosition Location, int Face,
    float CursorPositionX, float CursorPositionY, float CursorPositionZ, bool InsideBlock, int Sequence)
{
    public static UseItemOnPacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.Hand, packet.Location, packet.Face, packet.CursorPositionX, packet.CursorPositionY, packet.CursorPositionZ, packet.InsideBlock, packet.Sequence);
    }

    public byte[] Write() => new WirePacket(Hand, Location, Face, CursorPositionX, CursorPositionY, CursorPositionZ, InsideBlock, Sequence).Write();
}
