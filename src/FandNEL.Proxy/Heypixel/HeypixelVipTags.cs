using System.Text;
using FandNEL.Proxy.Packet.Minecraft.Nbt;
using FandNEL.Proxy.Packet.Minecraft.V1206;
using FandNEL.Proxy.Protocol;

namespace FandNEL.Proxy.Heypixel;

/// <summary>
/// Heypixel 用私有区字符 U+E0F8..U+E0FF 表示 VIP1..VIP8 图标（assets/minecraft/font/default.json
/// 把这些码位映射到 heypixel:tags/vip1.png..vip8.png，jar 内没有 vip9 的图片或码位）。
/// 没有该字体的第三方客户端只会显示空方框，这里把这些字符统一转换成 [VIP1]..[VIP8] 文本。
/// </summary>
internal static class HeypixelVipTags
{
    internal const char First = '\uE0F8';
    internal const int Count = 8;

    /// <summary>VIP1..VIP8 的显示文本；带对应颜色码，结尾的 §r 用于重置，避免后面的文字继续变色。</summary>
    private static readonly string[] Labels =
    [
        "[VIP1]",
        "§b[VIP2]§r",
        "§9[VIP3]§r",
        "§a[VIP4]§r",
        "§1[VIP5]§r",
        "§d[VIP6]§r",
        "§c[VIP7]§r",
        "§6[VIP8]§r",
    ];

    internal static bool IsTag(char value) => value >= First && value < First + Count;

    internal static bool Contains(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var character in text)
            if (IsTag(character))
                return true;
        return false;
    }

    /// <summary>把文本里的 VIP 图标字符替换为带颜色的 [VIP1]..[VIP8]；不含图标字符时原样返回。</summary>
    internal static string Convert(string text)
    {
        if (!Contains(text)) return text;
        var builder = new StringBuilder(text.Length + 16);
        foreach (var character in text)
            builder.Append(IsTag(character) ? Labels[character - First] : character.ToString());
        return builder.ToString();
    }

    /// <summary>递归转换文本组件里的所有字符串节点（就地修改）；返回是否发生变化。</summary>
    internal static bool Convert(NbtTag? component)
    {
        switch (component)
        {
            case NbtString text:
                if (!Contains(text.Value)) return false;
                text.Value = Convert(text.Value);
                return true;
            case NbtList list:
            {
                var changed = false;
                foreach (var item in list.Values) changed |= Convert(item);
                return changed;
            }
            case NbtCompound compound:
            {
                var changed = false;
                foreach (var pair in compound.Values) changed |= Convert(pair.Value);
                return changed;
            }
            default:
                return false;
        }
    }

    /// <summary>
    /// 客户端方向统一入口：把携带文本的包里的 VIP 图标字符转成 [VIPn] 后再转发。
    /// 解析失败时保持原样转发，交由客户端自行处理。
    /// </summary>
    internal static void Apply(PacketContext context)
    {
        try
        {
            switch (context.PacketId)
            {
                case MinecraftPacketIds.Clientbound.Team:
                {
                    var packet = SetPlayerTeamPacket.Read(context.Payload);
                    if (packet.WithConvertedComponents(Convert) is { } converted)
                        context.ReplacePayload(converted.Write());
                    break;
                }
                case MinecraftPacketIds.Clientbound.PlayerInfoUpdate:
                {
                    var packet = PlayerInfoUpdatePacket.Read(context.Payload);
                    if (packet.WithConvertedComponents(Convert) is { } converted)
                        context.ReplacePayload(converted.Write());
                    break;
                }
                case MinecraftPacketIds.Clientbound.SetObjective:
                {
                    var packet = SetObjectivePacket.Read(context.Payload);
                    if (packet.WithConvertedComponents(Convert) is { } converted)
                        context.ReplacePayload(converted.Write());
                    break;
                }
                case MinecraftPacketIds.Clientbound.SetScore:
                {
                    var packet = SetScorePacket.Read(context.Payload);
                    if (packet.WithConvertedComponents(Convert) is { } converted)
                        context.ReplacePayload(converted.Write());
                    break;
                }
                case MinecraftPacketIds.Clientbound.SystemChat:
                {
                    var packet = SystemChatPacket.Read(context.Payload);
                    if (Convert(packet.Content)) context.ReplacePayload(packet.Write());
                    break;
                }
                case MinecraftPacketIds.Clientbound.EntityMetadata:
                {
                    if (!EntityMetadataPacket.TryRead(context.Payload, out var packet)) break;
                    List<EntityMetadataEntry>? replaced = null;
                    for (var index = 0; index < packet.Entries.Count; index++)
                    {
                        var entry = packet.Entries[index];
                        var component = entry.SerializerId switch
                        {
                            (int)MetadataSerializer.Component => entry.ReadComponent(),
                            (int)MetadataSerializer.OptionalComponent => entry.ReadOptionalComponent(),
                            _ => null
                        };
                        if (component is null || !Convert(component)) continue;
                        replaced ??= [.. packet.Entries];
                        replaced[index] = entry.SerializerId == (int)MetadataSerializer.Component
                            ? EntityMetadataEntry.Component(entry.Index, component)
                            : EntityMetadataEntry.OptionalComponent(entry.Index, component);
                    }
                    if (replaced is not null)
                        context.ReplacePayload(new EntityMetadataPacket(packet.EntityId, replaced).Write());
                    break;
                }
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException
            or System.Text.DecoderFallbackException or OverflowException)
        {
            // 解析失败保持原样转发。
        }
    }
}
