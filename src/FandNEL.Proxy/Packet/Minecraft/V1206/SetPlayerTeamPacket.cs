using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.Nbt;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public sealed record SetPlayerTeamParameters(NbtTag DisplayName, byte FriendlyFlags, string NameTagVisibility,
    string CollisionRule, int Color, NbtTag Prefix, NbtTag Suffix);

public sealed record SetPlayerTeamData(SetPlayerTeamParameters? Parameters, IReadOnlyList<string> Players);

/// <summary>按操作模式读取队伍字段，并保留原始载荷；不包含显示或丢弃策略。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.Team, ProtocolVersion.V1206)]
public sealed record SetPlayerTeamPacket(string Name, byte Mode, byte[] RemainingData)
{
    public static SetPlayerTeamPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new SetPlayerTeamPacket(reader.ReadString(), reader.ReadByte(), reader.ReadBytes(reader.Remaining));
        result.Validate();
        return result;
    }

    public byte[] Write()
    {
        Validate();
        using var writer = new PacketWriter();
        return writer.WriteString(Name).WriteByte(Mode).WriteBytes(RemainingData).ToArray();
    }

    public SetPlayerTeamData ReadData() => ReadData(out _, out _);

    /// <summary>仅重写名字可见性，其余模式特有字段连同原始编码保持不变。</summary>
    public SetPlayerTeamPacket WithNameTagVisibility(string visibility)
    {
        var data = ReadData(out var start, out var end);
        if (data.Parameters is null)
            throw new InvalidOperationException("此队伍操作不包含名字可见性字段。");
        using var writer = new PacketWriter();
        writer.WriteBytes(RemainingData.AsSpan(0, start)).WriteString(visibility, 40)
            .WriteBytes(RemainingData.AsSpan(end));
        return this with { RemainingData = writer.ToArray() };
    }

    /// <summary>就地转换 displayName/prefix/suffix 文本组件；发生变化时返回重写后的新实例，否则返回 null。</summary>
    internal SetPlayerTeamPacket? WithConvertedComponents(Func<NbtTag, bool> convert)
    {
        if (Mode is not (0 or 2)) return null;
        var data = ReadData(out _, out _);
        if (data.Parameters is not { } parameters) return null;
        var changed = convert(parameters.DisplayName);
        changed |= convert(parameters.Prefix);
        changed |= convert(parameters.Suffix);
        if (!changed) return null;
        using var writer = new PacketWriter();
        writer.WriteNetworkNbt(parameters.DisplayName)
            .WriteByte(parameters.FriendlyFlags)
            .WriteString(parameters.NameTagVisibility, 40)
            .WriteString(parameters.CollisionRule, 40)
            .WriteVarInt(parameters.Color)
            .WriteNetworkNbt(parameters.Prefix)
            .WriteNetworkNbt(parameters.Suffix);
        if (Mode == 0)
        {
            writer.WriteVarInt(data.Players.Count);
            foreach (var player in data.Players) writer.WriteString(player);
        }
        return this with { RemainingData = writer.ToArray() };
    }

    private SetPlayerTeamData ReadData(out int visibilityStart, out int visibilityEnd)
    {
        Validate();
        var reader = new PacketReader(RemainingData);
        SetPlayerTeamParameters? parameters = null;
        visibilityStart = visibilityEnd = 0;
        if (Mode is 0 or 2)
        {
            var displayName = reader.ReadNetworkNbt();
            var friendlyFlags = reader.ReadByte();
            visibilityStart = reader.Position;
            var visibility = reader.ReadString(40);
            visibilityEnd = reader.Position;
            parameters = new(displayName, friendlyFlags, visibility, reader.ReadString(40),
                reader.ReadVarInt(), reader.ReadNetworkNbt(), reader.ReadNetworkNbt());
        }

        string[] players = [];
        if (Mode is 0 or 3 or 4)
        {
            var count = MinecraftPacketValidation.ReadCount(reader);
            players = new string[count];
            for (var index = 0; index < count; index++) players[index] = reader.ReadString();
        }
        MinecraftPacketValidation.RequireEnd(reader);
        return new(parameters, players);
    }

    private void Validate()
    {
        if (Mode > 4) throw new InvalidDataException("队伍包包含未知操作模式。");
    }
}
