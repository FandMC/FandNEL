using FandNEL.Proxy.Protocol;
using WirePacket = FandNEL.Proxy.Packet.Minecraft.V1218.UseItemOnPacket;

namespace FandNEL.Proxy.Packet.Minecraft.V12110;

/// <summary>字段与 V1218 相同，复用其编解码；包 ID 使用本版本 MinecraftPacketIds。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.UseItemOn, ProtocolVersion.V12110)]
public sealed record UseItemOnPacket(int Hand, BlockPosition Location, int Face,
    float CursorPositionX, float CursorPositionY, float CursorPositionZ, bool InsideBlock,
    bool WorldBorderHit, int Sequence)
{
    public static UseItemOnPacket Read(ReadOnlyMemory<byte> payload)
    {
        var packet = WirePacket.Read(payload);
        return new(packet.Hand, packet.Location, packet.Face, packet.CursorPositionX, packet.CursorPositionY, packet.CursorPositionZ, packet.InsideBlock, packet.WorldBorderHit, packet.Sequence);
    }

    public byte[] Write() => new WirePacket(Hand, Location, Face, CursorPositionX, CursorPositionY, CursorPositionZ, InsideBlock, WorldBorderHit, Sequence).Write();
}
