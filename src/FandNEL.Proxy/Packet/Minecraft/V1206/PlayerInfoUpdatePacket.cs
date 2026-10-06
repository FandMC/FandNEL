using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.Nbt;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public sealed record PlayerInfoEntry(Guid Id, string? ProfileName, int? GameMode, NbtTag? DisplayName);

/// <summary>读取玩家列表状态，不改写档案、聊天签名或 TAB 字段；原始载荷完整保留。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.PlayerInfoUpdate, ProtocolVersion.V1206)]
public sealed class PlayerInfoUpdatePacket
{
    private readonly byte[] _payload;

    private PlayerInfoUpdatePacket(byte actions, IReadOnlyList<PlayerInfoEntry> entries, byte[] payload)
    {
        Actions = actions;
        Entries = entries;
        _payload = payload;
    }

    public byte Actions { get; }
    public IReadOnlyList<PlayerInfoEntry> Entries { get; }

    public static PlayerInfoUpdatePacket Read(ReadOnlyMemory<byte> payload)
    {
        var bytes = payload.ToArray();
        var reader = new PacketReader(bytes);
        var actions = reader.ReadByte();
        if ((actions & ~0x3f) != 0) throw new InvalidDataException("玩家信息包包含未知操作。");
        var count = MinecraftPacketValidation.ReadCount(reader);
        if (count > reader.Remaining / 16) throw new InvalidDataException("玩家信息数量超过剩余 UUID 载荷。");
        var entries = new PlayerInfoEntry[count];
        for (var index = 0; index < count; index++)
        {
            var id = reader.ReadUuid();
            string? name = null;
            int? gameMode = null;
            NbtTag? displayName = null;
            if ((actions & 0x01) != 0)
            {
                name = reader.ReadString(16);
                var properties = MinecraftPacketValidation.ReadCount(reader);
                if (properties > reader.Remaining / 3) throw new InvalidDataException("玩家档案属性数量超过剩余载荷。");
                for (var property = 0; property < properties; property++)
                {
                    reader.ReadString();
                    reader.ReadString();
                    if (reader.ReadBoolean()) reader.ReadString();
                }
            }
            if ((actions & 0x02) != 0 && reader.ReadBoolean())
            {
                reader.ReadUuid();
                reader.ReadLong();
                reader.ReadByteArray(512);
                reader.ReadByteArray(4096);
            }
            if ((actions & 0x04) != 0) gameMode = reader.ReadVarInt();
            if ((actions & 0x08) != 0) reader.ReadBoolean();
            if ((actions & 0x10) != 0) reader.ReadVarInt();
            if ((actions & 0x20) != 0 && reader.ReadBoolean()) displayName = reader.ReadNetworkNbt();
            entries[index] = new(id, name, gameMode, displayName);
        }
        MinecraftPacketValidation.RequireEnd(reader);
        return new(actions, entries, bytes);
    }

    public byte[] Write() => (byte[])_payload.Clone();

    /// <summary>就地转换 TAB 条目 displayName 文本组件；发生变化时返回重写后的新实例，否则返回 null。</summary>
    internal PlayerInfoUpdatePacket? WithConvertedComponents(Func<NbtTag, bool> convert)
    {
        if ((Actions & 0x20) == 0) return null;
        var bytes = _payload;
        var reader = new PacketReader(bytes);
        reader.ReadByte();
        var count = MinecraftPacketValidation.ReadCount(reader);
        List<(int Start, int End, NbtTag Tag)> replacements = [];
        for (var index = 0; index < count; index++)
        {
            reader.ReadUuid();
            if ((Actions & 0x01) != 0)
            {
                reader.ReadString(16);
                var properties = MinecraftPacketValidation.ReadCount(reader);
                for (var property = 0; property < properties; property++)
                {
                    reader.ReadString();
                    reader.ReadString();
                    if (reader.ReadBoolean()) reader.ReadString();
                }
            }
            if ((Actions & 0x02) != 0 && reader.ReadBoolean())
            {
                reader.ReadUuid();
                reader.ReadLong();
                reader.ReadByteArray(512);
                reader.ReadByteArray(4096);
            }
            if ((Actions & 0x04) != 0) reader.ReadVarInt();
            if ((Actions & 0x08) != 0) reader.ReadBoolean();
            if ((Actions & 0x10) != 0) reader.ReadVarInt();
            if ((Actions & 0x20) != 0 && reader.ReadBoolean())
            {
                var start = reader.Position;
                var displayName = reader.ReadNetworkNbt();
                var end = reader.Position;
                if (convert(displayName)) replacements.Add((start, end, displayName));
            }
        }
        MinecraftPacketValidation.RequireEnd(reader);
        if (replacements.Count == 0) return null;
        using var writer = new PacketWriter();
        var copied = 0;
        foreach (var (start, end, tag) in replacements)
        {
            writer.WriteBytes(bytes.AsSpan(copied, start - copied));
            writer.WriteNetworkNbt(tag);
            copied = end;
        }
        writer.WriteBytes(bytes.AsSpan(copied));
        return new PlayerInfoUpdatePacket(Actions, Entries, writer.ToArray());
    }
}
