using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1122;

/// <summary>1.12.2 区块数据；保留方块实体列表的原始尾部字节。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.ChunkData, ProtocolVersion.V1122)]
public sealed record ChunkDataPacket(int ChunkX, int ChunkZ, bool LoadChunk, int AvailableSections,
    byte[] Data, byte[] RemainingData)
{
    public static ChunkDataPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var x = reader.ReadInt();
        var z = reader.ReadInt();
        var loadChunk = reader.ReadBoolean();
        var sections = reader.ReadVarInt();
        var length = reader.ReadVarInt();
        if (length < 0 || length > reader.Remaining)
            throw new InvalidDataException($"区块数据长度非法：{length}。");
        return new(x, z, loadChunk, sections, reader.ReadBytes(length), reader.ReadBytes(reader.Remaining));
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteInt(ChunkX).WriteInt(ChunkZ).WriteBoolean(LoadChunk).WriteVarInt(AvailableSections)
            .WriteVarInt(Data.Length).WriteBytes(Data)
            .WriteBytes(RemainingData.Length == 0 ? new byte[] { 0 } : RemainingData).ToArray();
    }
}
