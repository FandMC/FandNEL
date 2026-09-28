using FandNEL.Proxy.Packet.Minecraft.Nbt;
using FandNEL.Proxy.Packet.Minecraft.V1206;

namespace FandNEL.Proxy.Heypixel;

internal sealed partial class HeypixelHolograms
{
    private readonly Dictionary<Guid, PlayerProfile> _profiles = [];
    private readonly Dictionary<string, PlayerTeam> _teams = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _playerTeams = new(StringComparer.Ordinal);
    private int _localEntityId;
    private Guid? _localUuid;
    private string? _localName;
    private int _localGameMode;

    private void ResetPlayers(int entityId, Guid? uuid, string? name)
    {
        _profiles.Clear();
        _teams.Clear();
        _playerTeams.Clear();
        (_localEntityId, _localUuid, _localName) = (entityId, uuid, name);
        _localGameMode = 0;
    }

    private void UpdatePlayers(PlayerInfoUpdatePacket packet, Update update)
    {
        foreach (var entry in packet.Entries)
        {
            if (entry.ProfileName is { } name)
                _profiles[entry.Id] = new PlayerProfile(name);
            if (entry.Id == _localUuid)
            {
                if (entry.ProfileName is not null) _localName = entry.ProfileName;
                if (entry.GameMode is { } localMode) _localGameMode = localMode;
            }
        }
        // DisplayName 只属于 TAB 列表；头顶名称使用 GameProfile 名字和队伍前后缀。
        if ((packet.Actions & 0x05) != 0) RefreshPlayers(update);
    }

    private void RemovePlayers(PlayerInfoRemovePacket packet, Update update)
    {
        foreach (var id in packet.PlayerIds) _profiles.Remove(id);
        // 从 TAB 移除资料不等于销毁实体；NPC 会用此方式隐藏列表，保留实体已绑定的资料。
    }

    private void UpdateTeam(SetPlayerTeamPacket packet, Update update)
    {
        // 与 HeypixelConnection 保持一致：目标服的移除成员包不转发给客户端。
        if (packet.Mode == 4) return;
        var data = packet.ReadData();
        if (packet.Mode is 0 or 1)
        {
            _teams.Remove(packet.Name);
            foreach (var member in _playerTeams.Where(pair => pair.Value == packet.Name).Select(pair => pair.Key).ToArray())
                _playerTeams.Remove(member);
        }
        if (data.Parameters is { } parameters && (packet.Mode == 0 || _teams.ContainsKey(packet.Name)))
        {
            var prefix = HeypixelHologramText.Normalize(parameters.Prefix, out _, out var prefixLines);
            var suffix = HeypixelHologramText.Normalize(parameters.Suffix, out _, out var suffixLines);
            var team = new PlayerTeam(parameters, prefix, suffix, prefixLines || suffixLines);
            _teams[packet.Name] = team;
            // 保留前后缀、碰撞、颜色等原始字段，只关闭客户端原有的单行头顶名称。
            if (team.Multiline && parameters.NameTagVisibility != "never")
                update.Payload = packet.WithNameTagVisibility("never").Write();
        }
        if (packet.Mode is 0 or 3 && _teams.ContainsKey(packet.Name))
            foreach (var member in data.Players) _playerTeams[member] = packet.Name;
        RefreshPlayers(update);
    }

    private void RefreshPlayers(Update update)
    {
        foreach (var (id, entity) in _entities)
            if (entity.Type == MinecraftEntityTypes.Player && id != _localEntityId)
                RefreshPlayer(entity, update);
    }

    private void UpdatePlayerMetadata(Entity entity, EntityMetadataPacket packet, Update update)
    {
        var relevant = false;
        foreach (var entry in packet.Entries)
        {
            if (entry.Index == EntityMetadataIndices.SharedFlags && entry.SerializerId == (int)MetadataSerializer.Byte)
            {
                entity.PlayerFlags = entry.ReadByte();
                relevant = true;
            }
            else if (entry.Index == EntityMetadataIndices.Pose && entry.SerializerId == (int)MetadataSerializer.Pose)
            {
                entity.Pose = new FandNEL.Proxy.Packet.IO.PacketReader(entry.RawValue).ReadVarInt();
                relevant = true;
            }
        }
        if (relevant && packet.EntityId != _localEntityId) RefreshPlayer(entity, update);
    }

    private void RefreshPlayer(Entity entity, Update update)
    {
        if (_profiles.TryGetValue(entity.Uuid, out var current)) entity.Profile = current;
        var profile = entity.Profile;
        entity.Text = null;
        if (entity.Uuid != _localUuid && profile is not null
            && _playerTeams.TryGetValue(profile.Name, out var teamName)
            && _teams.TryGetValue(teamName, out var team) && team.Multiline && CanSeePlayer(entity, teamName, team))
            entity.Text = PlayerText(profile.Name, team);
        if (entity.Text is null)
        {
            RemoveOverlay(entity, update.After);
            entity.BelowName = null;
            return;
        }
        var score = _nameScores.GetText(profile!.Name);
        if (score is null)
        {
            if (entity.BelowName is { } previous) RemoveOverlay(previous, update.After);
            entity.BelowName = null;
        }
        else
        {
            entity.BelowName ??= new Entity(MinecraftEntityTypes.Player, entity.X, entity.Y, entity.Z) { IsScore = true };
            entity.BelowName.Text = score;
        }
        ShowOverlay(entity, update);
    }

    private bool CanSeePlayer(Entity entity, string teamName, PlayerTeam team)
    {
        var localName = _localUuid is { } id && _profiles.TryGetValue(id, out var profile) ? profile.Name : _localName;
        var hasLocalTeam = localName is not null && _playerTeams.TryGetValue(localName, out _);
        var sameTeam = hasLocalTeam && _playerTeams[localName!] == teamName;
        var visible = team.Parameters.NameTagVisibility switch
        {
            "always" => true,
            "hideForOtherTeams" => !hasLocalTeam || sameTeam,
            "hideForOwnTeam" => !hasLocalTeam || !sameTeam,
            _ => false
        };
        if (!visible) return false;
        return _localGameMode == 3 || (entity.PlayerFlags & 0x20) == 0
            || (sameTeam && (team.Parameters.FriendlyFlags & 0x02) != 0);
    }

    private static NbtCompound PlayerText(string name, PlayerTeam team)
    {
        var text = new NbtCompound().Set("text", new NbtString(string.Empty))
            .Set("extra", new NbtList(NbtTagType.Compound,
                [HeypixelHologramText.AsCompound(team.Prefix), new NbtCompound().Set("text", new NbtString(name)),
                    HeypixelHologramText.AsCompound(team.Suffix)]));
        // 队伍颜色作为父样式，前后缀中显式指定的样式仍有优先级。
        string[] colors = ["black", "dark_blue", "dark_green", "dark_aqua", "dark_red", "dark_purple", "gold", "gray",
            "dark_gray", "blue", "green", "aqua", "red", "light_purple", "yellow", "white"];
        var color = team.Parameters.Color;
        if (color is >= 0 and < 16) text.Set("color", new NbtString(colors[color]));
        else if (color is >= 16 and <= 20)
        {
            string[] styles = ["obfuscated", "bold", "strikethrough", "underlined", "italic"];
            text.Set(styles[color - 16], new NbtByte(1));
        }
        return text;
    }

    private sealed record PlayerProfile(string Name);
    private sealed record PlayerTeam(SetPlayerTeamParameters Parameters, NbtTag Prefix, NbtTag Suffix, bool Multiline);
}
