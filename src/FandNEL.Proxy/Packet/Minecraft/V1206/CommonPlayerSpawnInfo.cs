using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public sealed record GlobalPosition(string DimensionName, BlockPosition Position);

/// <summary>Join Game 和 Respawn 共用的协议 766 出生信息。</summary>
public sealed record CommonPlayerSpawnInfo(int DimensionType, string DimensionName, long HashedSeed,
    byte GameMode, sbyte PreviousGameMode, bool IsDebug, bool IsFlat, GlobalPosition? LastDeathLocation, int PortalCooldown)
{
    public static CommonPlayerSpawnInfo Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = Read(reader);
        MinecraftPacketValidation.RequireEnd(reader);
        return result;
    }

    public static CommonPlayerSpawnInfo Read(PacketReader reader)
    {
        var dimensionType = reader.ReadVarInt();
        var dimensionName = reader.ReadString();
        var seed = reader.ReadLong();
        var gameMode = reader.ReadByte();
        var previousGameMode = unchecked((sbyte)reader.ReadByte());
        var debug = reader.ReadBoolean();
        var flat = reader.ReadBoolean();
        var death = reader.ReadBoolean() ? new GlobalPosition(reader.ReadString(), reader.ReadPosition()) : null;
        var result = new CommonPlayerSpawnInfo(dimensionType, dimensionName, seed, gameMode, previousGameMode,
            debug, flat, death, reader.ReadVarInt());
        result.Validate();
        return result;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        Write(writer);
        return writer.ToArray();
    }

    public void Write(PacketWriter writer)
    {
        Validate();
        writer.WriteVarInt(DimensionType).WriteString(DimensionName).WriteLong(HashedSeed).WriteByte(GameMode)
            .WriteByte(unchecked((byte)PreviousGameMode)).WriteBoolean(IsDebug).WriteBoolean(IsFlat)
            .WriteBoolean(LastDeathLocation is not null);
        if (LastDeathLocation is not null)
            writer.WriteString(LastDeathLocation.DimensionName).WritePosition(LastDeathLocation.Position);
        writer.WriteVarInt(PortalCooldown);
    }

    private void Validate()
    {
        if (DimensionType < 0 || GameMode > 3 || PreviousGameMode is < -1 or > 3)
            throw new InvalidDataException("出生信息包含未知维度类型或游戏模式。");
    }
}
