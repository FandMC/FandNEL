using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.Nbt;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public enum ScoreNumberFormatKind { Blank, Styled, Fixed }

public sealed record ScoreNumberFormat(ScoreNumberFormatKind Kind, NbtTag? Content)
{
    internal static ScoreNumberFormat? ReadOptional(PacketReader reader)
    {
        if (!reader.ReadBoolean()) return null;
        var kind = (ScoreNumberFormatKind)reader.ReadVarInt();
        return kind switch
        {
            ScoreNumberFormatKind.Blank => new(kind, null),
            ScoreNumberFormatKind.Styled => new(kind, reader.ReadNetworkNbtCompound()),
            ScoreNumberFormatKind.Fixed => new(kind, reader.ReadNetworkNbt()),
            _ => throw new InvalidDataException("计分板数字格式无效。")
        };
    }

    internal static void WriteOptional(PacketWriter writer, ScoreNumberFormat? format)
    {
        if (format is null)
        {
            writer.WriteBoolean(false);
            return;
        }
        writer.WriteBoolean(true).WriteVarInt((int)format.Kind);
        switch (format.Kind)
        {
            case ScoreNumberFormatKind.Blank:
                break;
            case ScoreNumberFormatKind.Styled:
                writer.WriteNetworkNbtCompound((NbtCompound)format.Content!);
                break;
            case ScoreNumberFormatKind.Fixed:
                writer.WriteNetworkNbt(format.Content!);
                break;
            default:
                throw new InvalidDataException("计分板数字格式无效。");
        }
    }
}

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.DisplayObjective, ProtocolVersion.V1206)]
public sealed record DisplayObjectivePacket(int Slot, string ObjectiveName)
{
    public static DisplayObjectivePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var packet = new DisplayObjectivePacket(reader.ReadVarInt(), reader.ReadString());
        if (packet.Slot is < 0 or > 18) throw new InvalidDataException("计分板显示槽位无效。");
        MinecraftPacketValidation.RequireEnd(reader);
        return packet;
    }
}

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SetObjective, ProtocolVersion.V1206)]
public sealed record SetObjectivePacket(string Name, byte Mode, NbtTag? DisplayName, int? RenderType, ScoreNumberFormat? NumberFormat)
{
    public static SetObjectivePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var name = reader.ReadString();
        var mode = reader.ReadByte();
        if (mode > 2) throw new InvalidDataException("计分板目标操作无效。");
        NbtTag? displayName = null;
        int? renderType = null;
        ScoreNumberFormat? numberFormat = null;
        if (mode != 1)
        {
            displayName = reader.ReadNetworkNbt();
            renderType = reader.ReadVarInt();
            if (renderType is not (0 or 1)) throw new InvalidDataException("计分板渲染类型无效。");
            numberFormat = ScoreNumberFormat.ReadOptional(reader);
        }
        MinecraftPacketValidation.RequireEnd(reader);
        return new(name, mode, displayName, renderType, numberFormat);
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        writer.WriteString(Name).WriteByte(Mode);
        if (Mode != 1)
        {
            writer.WriteNetworkNbt(DisplayName!).WriteVarInt(RenderType!.Value);
            ScoreNumberFormat.WriteOptional(writer, NumberFormat);
        }
        return writer.ToArray();
    }

    /// <summary>就地转换 displayName 文本组件；发生变化时返回可转发的新实例，否则返回 null。</summary>
    internal SetObjectivePacket? WithConvertedComponents(Func<NbtTag, bool> convert) =>
        DisplayName is not null && convert(DisplayName) ? this : null;
}

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SetScore, ProtocolVersion.V1206)]
public sealed record SetScorePacket(string Owner, string ObjectiveName, int Value, NbtTag? DisplayName, ScoreNumberFormat? NumberFormat)
{
    public static SetScorePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var owner = reader.ReadString();
        var objective = reader.ReadString();
        var value = reader.ReadVarInt();
        var displayName = reader.ReadBoolean() ? reader.ReadNetworkNbt() : null;
        var numberFormat = ScoreNumberFormat.ReadOptional(reader);
        MinecraftPacketValidation.RequireEnd(reader);
        return new(owner, objective, value, displayName, numberFormat);
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        writer.WriteString(Owner).WriteString(ObjectiveName).WriteVarInt(Value);
        writer.WriteBoolean(DisplayName is not null);
        if (DisplayName is not null) writer.WriteNetworkNbt(DisplayName);
        ScoreNumberFormat.WriteOptional(writer, NumberFormat);
        return writer.ToArray();
    }

    /// <summary>就地转换 displayName 文本组件；发生变化时返回可转发的新实例，否则返回 null。</summary>
    internal SetScorePacket? WithConvertedComponents(Func<NbtTag, bool> convert) =>
        DisplayName is not null && convert(DisplayName) ? this : null;
}

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.ResetScore, ProtocolVersion.V1206)]
public sealed record ResetScorePacket(string Owner, string? ObjectiveName)
{
    public static ResetScorePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var owner = reader.ReadString();
        var objective = reader.ReadBoolean() ? reader.ReadString() : null;
        MinecraftPacketValidation.RequireEnd(reader);
        return new(owner, objective);
    }
}
