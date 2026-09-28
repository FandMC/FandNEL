using FandNEL.Proxy.Packet.Minecraft.Nbt;
using FandNEL.Proxy.Packet.Minecraft.V1206;

namespace FandNEL.Proxy.Heypixel;

/// <summary>为目标服的多行悬浮字维护客户端显示实体；不向服务端创建实体。</summary>
internal sealed partial class HeypixelHolograms
{
    private readonly Dictionary<int, Entity> _entities = [];
    private readonly Dictionary<int, Entity> _overlays = [];
    private readonly HashSet<int> _serverIds = [];
    private readonly HeypixelNameScores _nameScores = new();
    private int _nextVirtualId = -1;
    private string? _dimension;

    internal sealed record Output(int PacketId, byte[] Payload);
    internal sealed class Update
    {
        internal byte[]? Payload { get; set; }
        internal List<Output> Before { get; } = [];
        internal List<Output> After { get; } = [];
    }

    internal Update Process(int packetId, ReadOnlyMemory<byte> payload)
    {
        var update = new Update();
        switch (packetId)
        {
            case MinecraftPacketIds.Clientbound.SpawnEntity:
                Spawn(SpawnEntityPacket.Read(payload), update);
                break;
            case MinecraftPacketIds.Clientbound.PlayerInfoUpdate:
                UpdatePlayers(PlayerInfoUpdatePacket.Read(payload), update);
                break;
            case MinecraftPacketIds.Clientbound.PlayerInfoRemove:
                RemovePlayers(PlayerInfoRemovePacket.Read(payload), update);
                break;
            case MinecraftPacketIds.Clientbound.Team:
                UpdateTeam(SetPlayerTeamPacket.Read(payload), update);
                break;
            case MinecraftPacketIds.Clientbound.GameEvent:
                var gameEvent = GameEventPacket.Read(payload);
                if (gameEvent.Event == GameEventPacket.ChangeGameMode && gameEvent.Value is >= 0 and <= 3)
                {
                    _localGameMode = (int)gameEvent.Value;
                    RefreshPlayers(update);
                }
                break;
            case MinecraftPacketIds.Clientbound.EntityMove:
            case MinecraftPacketIds.Clientbound.EntityMoveAndRotation:
            case MinecraftPacketIds.Clientbound.EntityRotation:
                var move = EntityMovePacket.Read(packetId, payload);
                if (_entities.TryGetValue(move.EntityId, out var moving))
                {
                    moving.X += move.DeltaX / 4096d;
                    moving.Y += move.DeltaY / 4096d;
                    moving.Z += move.DeltaZ / 4096d;
                    MoveOverlay(moving, update);
                }
                break;
            case MinecraftPacketIds.Clientbound.EntityTeleport:
                var teleport = EntityTeleportPacket.Read(payload);
                if (_entities.TryGetValue(teleport.EntityId, out var teleported))
                {
                    (teleported.X, teleported.Y, teleported.Z) = (teleport.X, teleport.Y, teleport.Z);
                    MoveOverlay(teleported, update);
                }
                break;
            case MinecraftPacketIds.Clientbound.RemoveEntities:
                Remove(RemoveEntitiesPacket.Read(payload), update);
                break;
            case MinecraftPacketIds.Clientbound.JoinGame:
                return ProcessJoin(JoinGamePacket.Read(payload));
            case MinecraftPacketIds.Clientbound.Respawn:
                var spawnInfo = RespawnPacket.Read(payload).SpawnInfo;
                var dimension = spawnInfo.DimensionName;
                var gameModeChanged = _localGameMode != spawnInfo.GameMode;
                _localGameMode = spawnInfo.GameMode;
                // 同维度重生保留 ClientLevel，现有盔甲架与显示实体仍然存在。
                if (!string.Equals(_dimension, dimension, StringComparison.Ordinal)) Reset(dimension, update);
                else if (gameModeChanged) RefreshPlayers(update);
                break;
            default:
                if (_nameScores.Process(packetId, payload)) RefreshPlayers(update);
                break;
        }
        return update;
    }

    internal Update ProcessJoin(JoinGamePacket packet, Guid? localUuid = null, string? localName = null)
    {
        var update = new Update();
        var spawnInfo = packet.ReadSpawnInfo();
        Reset(spawnInfo.DimensionName, update);
        ResetPlayers(packet.EntityId, localUuid, localName);
        _localGameMode = spawnInfo.GameMode;
        _nameScores.Clear();
        return update;
    }

    private void Reset(string dimension, Update update)
    {
        if (_overlays.Count > 0)
            update.After.Add(new(MinecraftPacketIds.Clientbound.RemoveEntities, new RemoveEntitiesPacket(_overlays.Keys.ToArray()).Write()));
        _entities.Clear();
        _overlays.Clear();
        _serverIds.Clear();
        _nextVirtualId = -1;
        _dimension = dimension;
    }

    internal Update ProcessMetadata(EntityMetadataPacket packet)
    {
        var update = new Update();
        Metadata(packet, update);
        return update;
    }

    private void Spawn(SpawnEntityPacket packet, Update update)
    {
        // 若服务器也用了当前虚拟 ID，先撤销显示实体，再为旧悬浮字分配新 ID。
        Entity? displaced = null;
        if (_overlays.TryGetValue(packet.EntityId, out displaced))
            RemoveOverlay(displaced, update.Before);
        if (_entities.Remove(packet.EntityId, out var previous))
            RemoveOverlay(previous, update.Before);
        _serverIds.Add(packet.EntityId);
        if (packet.EntityType is MinecraftEntityTypes.ArmorStand or MinecraftEntityTypes.TextDisplay or MinecraftEntityTypes.Player)
        {
            var entity = new Entity(packet.EntityType, packet.X, packet.Y, packet.Z) { Uuid = packet.Uuid };
            _entities[packet.EntityId] = entity;
            if (packet.EntityType == MinecraftEntityTypes.Player && packet.EntityId != _localEntityId)
                RefreshPlayer(entity, update);
        }
        if (displaced?.Text is not null)
            ShowOverlay(displaced, update);
    }

    private void Metadata(EntityMetadataPacket packet, Update update)
    {
        if (!_entities.TryGetValue(packet.EntityId, out var entity)) return;
        if (entity.Type == MinecraftEntityTypes.Player)
        {
            UpdatePlayerMetadata(entity, packet, update);
            return;
        }
        if (entity.Type == MinecraftEntityTypes.TextDisplay)
        {
            var textEntry = packet.Entries.LastOrDefault(entry => entry.Index == EntityMetadataIndices.Text && entry.SerializerId == (int)MetadataSerializer.Component);
            if (textEntry is null) return;
            var text = HeypixelHologramText.Normalize(textEntry.ReadComponent(), out var changed, out _);
            if (changed)
                update.Payload = ReplaceEntry(packet, EntityMetadataEntry.Component(EntityMetadataIndices.Text, text)).Write();
            return;
        }

        var relevant = false;
        foreach (var entry in packet.Entries)
        {
            if (entry.Index == EntityMetadataIndices.CustomName && entry.SerializerId == (int)MetadataSerializer.OptionalComponent)
            {
                entity.Name = entry.ReadOptionalComponent();
                relevant = true;
            }
            else if (entry.Index == EntityMetadataIndices.NameVisible && entry.SerializerId == (int)MetadataSerializer.Boolean)
            {
                entity.NameVisible = entry.ReadBoolean();
                relevant = true;
            }
            else if (entry.Index == EntityMetadataIndices.ArmorStandFlags && entry.SerializerId == (int)MetadataSerializer.Byte)
            {
                entity.Flags = (ArmorStandFlags)entry.ReadByte();
                relevant = true;
            }
        }
        if (!relevant) return;
        var multiline = false;
        entity.Text = entity.Name is null ? null : HeypixelHologramText.Normalize(entity.Name, out _, out multiline);
        if (entity.NameVisible && multiline)
        {
            update.Payload = ReplaceEntry(packet, EntityMetadataEntry.Boolean(EntityMetadataIndices.NameVisible, false)).Write();
            entity.NameSuppressed = true;
            ShowOverlay(entity, update);
        }
        else
        {
            RemoveOverlay(entity, update.After);
            if (entity.NameSuppressed)
                update.Payload = ReplaceEntry(packet, EntityMetadataEntry.Boolean(EntityMetadataIndices.NameVisible, entity.NameVisible)).Write();
            entity.NameSuppressed = false;
        }
    }

    private static EntityMetadataPacket ReplaceEntry(EntityMetadataPacket packet, EntityMetadataEntry replacement) =>
        new(packet.EntityId, packet.Entries.Where(entry => entry.Index != replacement.Index).Append(replacement).ToArray());

    private void ShowOverlay(Entity entity, Update update)
    {
        if (entity.Text is null) return;
        if (entity.OverlayId is null)
        {
            while (_serverIds.Contains(_nextVirtualId) || _overlays.ContainsKey(_nextVirtualId))
                _nextVirtualId = checked(_nextVirtualId - 1);
            var id = _nextVirtualId;
            _nextVirtualId = checked(_nextVirtualId - 1);
            entity.OverlayId = id;
            _overlays.Add(id, entity);
            var spawn = new SpawnEntityPacket(id, Guid.NewGuid(), MinecraftEntityTypes.TextDisplay,
                entity.X, entity.NameY, entity.Z, 0, 0, 0, 0, 0, 0, 0);
            update.After.Add(new(MinecraftPacketIds.Clientbound.SpawnEntity, spawn.Write()));
        }
        else MoveOverlay(entity, update);
        var metadata = new EntityMetadataPacket(entity.OverlayId.Value,
        [
            EntityMetadataEntry.Byte(EntityMetadataIndices.DisplayBillboard, (byte)DisplayBillboard.Center),
            EntityMetadataEntry.Component(EntityMetadataIndices.Text, entity.Text),
            EntityMetadataEntry.VarInt(EntityMetadataIndices.TextLineWidth, 32767), // 只按服务端的显式换行分行。
            EntityMetadataEntry.Byte(EntityMetadataIndices.TextFlags,
                (byte)(entity.Type == MinecraftEntityTypes.Player && entity.IsCrouching
                    ? TextDisplayFlags.DefaultBackground
                    : TextDisplayFlags.SeeThrough | TextDisplayFlags.DefaultBackground))
        ]);
        if (entity.Type == MinecraftEntityTypes.Player)
            metadata = new EntityMetadataPacket(metadata.EntityId, [.. metadata.Entries,
                EntityMetadataEntry.Create(EntityMetadataIndices.DisplayViewRange, MetadataSerializer.Float,
                    writer => writer.WriteFloat(entity.IsScore ? 10f / 64f : entity.IsCrouching ? 0.5f : 1f))]);
        update.After.Add(new(MinecraftPacketIds.Clientbound.EntityMetadata, metadata.Write()));
        if (entity.BelowName is { } score)
        {
            AlignScore(entity, score);
            ShowOverlay(score, update);
        }
    }

    private static void MoveOverlay(Entity entity, Update update)
    {
        if (entity.OverlayId is not { } id) return;
        update.After.Add(new(MinecraftPacketIds.Clientbound.EntityTeleport,
            new EntityTeleportPacket(id, entity.X, entity.NameY, entity.Z, 0, 0, false).Write()));
        if (entity.BelowName is { } score)
        {
            AlignScore(entity, score);
            MoveOverlay(score, update);
        }
    }

    private static void AlignScore(Entity entity, Entity score)
    {
        (score.X, score.Y, score.Z) = (entity.X, entity.Y, entity.Z);
        score.PlayerFlags = entity.PlayerFlags;
        score.Pose = entity.Pose;
    }

    private void RemoveOverlay(Entity entity, List<Output> output)
    {
        if (entity.BelowName is { } score) RemoveOverlay(score, output);
        if (entity.OverlayId is not { } id) return;
        _overlays.Remove(id);
        entity.OverlayId = null;
        output.Add(new(MinecraftPacketIds.Clientbound.RemoveEntities, new RemoveEntitiesPacket([id]).Write()));
    }

    private void Remove(RemoveEntitiesPacket packet, Update update)
    {
        var ids = new List<int>(packet.EntityIds);
        foreach (var id in packet.EntityIds)
        {
            _serverIds.Remove(id);
            if (!_entities.Remove(id, out var entity)) continue;
            foreach (var overlayId in new[] { entity.OverlayId, entity.BelowName?.OverlayId })
                if (overlayId is { } virtualId)
                {
                    _overlays.Remove(virtualId);
                    ids.Add(virtualId);
                }
        }
        if (ids.Count != packet.EntityIds.Length)
            update.Payload = new RemoveEntitiesPacket(ids.ToArray()).Write();
    }

    private sealed class Entity(int type, double x, double y, double z)
    {
        internal int Type { get; } = type;
        internal Guid Uuid { get; init; }
        internal PlayerProfile? Profile { get; set; }
        internal Entity? BelowName { get; set; }
        internal bool IsScore { get; init; }
        internal double X { get; set; } = x;
        internal double Y { get; set; } = y;
        internal double Z { get; set; } = z;
        internal NbtTag? Name { get; set; }
        internal NbtTag? Text { get; set; }
        internal bool NameVisible { get; set; }
        internal bool NameSuppressed { get; set; }
        internal ArmorStandFlags Flags { get; set; }
        internal byte PlayerFlags { get; set; }
        internal int Pose { get; set; }
        internal bool IsCrouching => (PlayerFlags & 0x02) != 0;
        internal int? OverlayId { get; set; }
        internal double NameY => Y + (Type == MinecraftEntityTypes.Player
            ? Pose switch { 1 or 3 or 4 => 0.6, 2 or 7 => 0.2, 5 => 1.5, _ => 1.8 }
            : Flags.HasFlag(ArmorStandFlags.Marker) ? 0 : Flags.HasFlag(ArmorStandFlags.Small) ? 0.9875 : 1.975)
            + 0.5 + (BelowName is null ? 0 : 0.25875);
    }
}
