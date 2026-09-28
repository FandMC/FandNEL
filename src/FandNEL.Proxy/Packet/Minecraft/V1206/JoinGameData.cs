using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>Join Game 的实体 ID 后续字段，与原始尾部载荷按需转换。</summary>
public sealed record JoinGameData(bool IsHardcore, IReadOnlyList<string> DimensionNames, int MaxPlayers,
    int ViewDistance, int SimulationDistance, bool ReducedDebugInfo, bool EnableRespawnScreen,
    bool DoLimitedCrafting, CommonPlayerSpawnInfo SpawnInfo, bool EnforcesSecureChat)
{
    public static JoinGameData Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var hardcore = reader.ReadBoolean();
        var count = MinecraftPacketValidation.ReadCount(reader);
        var dimensions = new string[count];
        for (var index = 0; index < count; index++) dimensions[index] = reader.ReadString();
        var result = new JoinGameData(hardcore, dimensions, reader.ReadVarInt(), reader.ReadVarInt(), reader.ReadVarInt(),
            reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadBoolean(), CommonPlayerSpawnInfo.Read(reader), reader.ReadBoolean());
        MinecraftPacketValidation.RequireEnd(reader);
        return result;
    }

    public byte[] Write()
    {
        if (DimensionNames.Count > MinecraftPacketValidation.MaximumCollectionLength)
            throw new InvalidDataException("维度名称数量超出允许范围。");
        using var writer = new PacketWriter();
        writer.WriteBoolean(IsHardcore).WriteVarInt(DimensionNames.Count);
        foreach (var dimension in DimensionNames) writer.WriteString(dimension);
        writer.WriteVarInt(MaxPlayers).WriteVarInt(ViewDistance).WriteVarInt(SimulationDistance)
            .WriteBoolean(ReducedDebugInfo).WriteBoolean(EnableRespawnScreen).WriteBoolean(DoLimitedCrafting);
        SpawnInfo.Write(writer);
        return writer.WriteBoolean(EnforcesSecureChat).ToArray();
    }
}
