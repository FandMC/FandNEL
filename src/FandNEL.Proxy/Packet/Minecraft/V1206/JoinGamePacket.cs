using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>默认保留后续载荷；需要维度或游戏设置时显式读取强类型字段。</summary>
public sealed record JoinGamePacket(int EntityId, byte[] RemainingData)
{
    public JoinGamePacket(int entityId, JoinGameData data) : this(entityId, data.Write()) { }

    public JoinGameData ReadData() => JoinGameData.Read(RemainingData);
    public CommonPlayerSpawnInfo ReadSpawnInfo() => ReadData().SpawnInfo;

    public static JoinGamePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        return new(reader.ReadInt(), reader.ReadBytes(reader.Remaining));
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteInt(EntityId).WriteBytes(RemainingData).ToArray();
    }
}
