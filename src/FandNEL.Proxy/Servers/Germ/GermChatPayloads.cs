using System.Text.Json;
using System.Text.Json.Serialization;
using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.Nbt;
using FandNEL.Proxy.Packet.Minecraft.V1206;
using FandNEL.Proxy.Protocol;

namespace FandNEL.Proxy.Servers.Germ;

/// <summary>把 germ 按钮渲染成各版本可点击的聊天组件。</summary>
internal static class GermChatPayloads
{
    internal const string ButtonClickPrefix = "/germ-button-click ";
    internal const string InputClickPrefix = "/germ-activate-input ";
    private const int ChatPosition = 1;

    /// <summary>构造聊天里的按钮行：金色方括号名称 + 悬停提示 + 运行命令。</summary>
    internal static byte[] BuildButtons(ProtocolVersion version, IReadOnlyList<GermComponent> buttons)
    {
        var extra = new List<GermJsonComponent>();
        foreach (var button in buttons)
        {
            extra.Add(new GermJsonComponent
            {
                Text = $"[§6{button.ButtonName}§r]",
                HoverEvent = new GermJsonHoverEvent
                {
                    Action = "show_text",
                    Value = "执行操作 => " + (button.ButtonTips ?? button.ButtonName)
                },
                ClickEvent = new GermJsonClickEvent
                {
                    Action = "run_command",
                    Value = ButtonClickPrefix + button.ButtonPath
                }
            });
            extra.Add(new GermJsonComponent { Text = " " });
        }

        if (version >= ProtocolVersion.V1206)
            return new SystemChatPacket(BuildNbt(extra), false).Write();

        var json = JsonSerializer.Serialize(new GermJsonComponent { Text = string.Empty, Extra = extra });
        if (version == ProtocolVersion.V1200)
            return new Packet.Minecraft.V1200.SystemChatPacket(json, false).Write();
        if (version == ProtocolVersion.V1122)
            return new Packet.Minecraft.V1122.ClientboundChatMessagePacket(json, ChatPosition).Write();
        using var writer = new PacketWriter();
        writer.WriteString(json).WriteByte(ChatPosition);
        return writer.ToArray();
    }

    /// <summary>1.20.5 及以上用 NBT 表达聊天组件。</summary>
    private static NbtCompound BuildNbt(IReadOnlyList<GermJsonComponent> components)
    {
        var extra = new List<NbtTag>();
        foreach (var component in components)
        {
            var tag = new NbtCompound().Set("text", new NbtString(component.Text ?? string.Empty));
            if (component.HoverEvent is not null)
                tag.Set("hoverEvent", new NbtCompound()
                    .Set("action", new NbtString(component.HoverEvent.Action))
                    .Set("contents", new NbtString(component.HoverEvent.Value ?? string.Empty)));
            if (component.ClickEvent is not null)
                tag.Set("clickEvent", new NbtCompound()
                    .Set("action", new NbtString(component.ClickEvent.Action))
                    .Set("value", new NbtString(component.ClickEvent.Value ?? string.Empty)));
            extra.Add(tag);
        }
        return new NbtCompound().Set("text", new NbtString(string.Empty)).Set("extra", new NbtList(NbtTagType.Compound, extra));
    }

    private sealed class GermJsonComponent
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("extra")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<GermJsonComponent>? Extra { get; set; }

        [JsonPropertyName("hoverEvent")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GermJsonHoverEvent? HoverEvent { get; set; }

        [JsonPropertyName("clickEvent")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GermJsonClickEvent? ClickEvent { get; set; }
    }

    private sealed class GermJsonHoverEvent
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "show_text";

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    private sealed class GermJsonClickEvent
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "run_command";

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }
}
