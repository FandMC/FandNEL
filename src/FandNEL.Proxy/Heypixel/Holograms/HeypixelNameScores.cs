using System.Globalization;
using FandNEL.Proxy.Packet.Minecraft.Nbt;
using FandNEL.Proxy.Packet.Minecraft.V1206;

namespace FandNEL.Proxy.Heypixel;

/// <summary>保留多行玩家名下方的原版计分项，不改变侧栏或 TAB 的数据包。</summary>
internal sealed class HeypixelNameScores
{
    private readonly Dictionary<string, SetObjectivePacket> _objectives = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Objective, string Owner), SetScorePacket> _scores = [];
    private string? _belowName;

    internal void Clear()
    {
        _objectives.Clear();
        _scores.Clear();
        _belowName = null;
    }

    internal bool Process(int packetId, ReadOnlyMemory<byte> payload)
    {
        switch (packetId)
        {
            case MinecraftPacketIds.Clientbound.DisplayObjective:
                var display = DisplayObjectivePacket.Read(payload);
                if (display.Slot != 2) return false;
                _belowName = _objectives.ContainsKey(display.ObjectiveName) ? display.ObjectiveName : null;
                return true;
            case MinecraftPacketIds.Clientbound.SetObjective:
                var objective = SetObjectivePacket.Read(payload);
                var selected = _belowName == objective.Name;
                if (objective.Mode == 1)
                {
                    _objectives.Remove(objective.Name);
                    foreach (var key in _scores.Keys.Where(key => key.Objective == objective.Name).ToArray()) _scores.Remove(key);
                    if (selected) _belowName = null;
                }
                else if (objective.Mode == 0 || _objectives.ContainsKey(objective.Name))
                    _objectives[objective.Name] = objective;
                return selected;
            case MinecraftPacketIds.Clientbound.SetScore:
                var score = SetScorePacket.Read(payload);
                if (!_objectives.ContainsKey(score.ObjectiveName)) return false;
                _scores[(score.ObjectiveName, score.Owner)] = score;
                return _belowName == score.ObjectiveName;
            case MinecraftPacketIds.Clientbound.ResetScore:
                var reset = ResetScorePacket.Read(payload);
                if (reset.ObjectiveName is { } name)
                    return _scores.Remove((name, reset.Owner)) && name == _belowName;
                var changed = false;
                foreach (var key in _scores.Keys.Where(key => key.Owner == reset.Owner).ToArray())
                {
                    _scores.Remove(key);
                    changed |= key.Objective == _belowName;
                }
                return changed;
            default:
                return false;
        }
    }

    internal NbtTag? GetText(string profileName)
    {
        if (_belowName is null || !_objectives.TryGetValue(_belowName, out var objective)) return null;
        _scores.TryGetValue((_belowName, profileName), out var score);
        var value = Format(score?.Value ?? 0, score?.NumberFormat ?? objective.NumberFormat);
        // 下方分数使用无样式的默认格式；不继承玩家队伍的颜色或装饰。
        return new NbtCompound().Set("text", new NbtString(string.Empty))
            .Set("extra", new NbtList(NbtTagType.Compound,
                [HeypixelHologramText.AsCompound(value), new NbtCompound().Set("text", new NbtString(" ")),
                    HeypixelHologramText.AsCompound(objective.DisplayName!)]));
    }

    private static NbtTag Format(int score, ScoreNumberFormat? format)
    {
        if (format?.Kind == ScoreNumberFormatKind.Blank) return new NbtString(string.Empty);
        if (format?.Kind == ScoreNumberFormatKind.Fixed) return format.Content!.DeepClone();
        var value = new NbtCompound().Set("text", new NbtString(score.ToString(CultureInfo.InvariantCulture)));
        if (format?.Content is NbtCompound style)
        {
            string[] keys = ["color", "bold", "italic", "underlined", "strikethrough", "obfuscated", "font", "insertion", "clickEvent", "hoverEvent"];
            foreach (var key in keys)
                if (style.TryGet(key, out var field)) value.Set(key, field.DeepClone());
        }
        return value;
    }

}
