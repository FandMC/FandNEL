namespace FandNEL.Proxy.Servers.Germ;

/// <summary>把服务端下发的 germ YAML 解析成聊天按钮；仅迁移 isDefault=true 的按钮路径。</summary>
internal static class GermMenu
{
    /// <summary>解析 YAML 中指定 node 下的可点按钮。</summary>
    internal static List<GermComponent> Parse(string yaml, string node, string playerName)
    {
        var components = new List<GermComponent>();
        Dictionary<object, object> document;
        try
        {
            document = GermYaml.Parse(yaml);
        }
        catch (Exception)
        {
            return components;
        }

        if (document.Count == 0 || !document.TryGetValue(node, out var value) || value is not Dictionary<object, object> entries)
            return components;

        foreach (var entry in entries)
        {
            if (entry.Key is string key && !IsReservedKey(key) && entry.Value is Dictionary<object, object> content)
                ProcessContent(content, key, node, playerName, components);
        }
        return components;
    }

    private static void ProcessContent(Dictionary<object, object>? content, string key, string node, string playerName,
        List<GermComponent> components)
    {
        if (content is null)
            return;
        var type = ReadType(content);
        var actions = ExtractBtnAction(content);
        if (actions.Length == 0)
        {
            if (content.ContainsKey("relativeParts"))
            {
                if (content["relativeParts"] is not Dictionary<object, object> parts)
                    return;
                foreach (var partKey in parts.Keys)
                    ProcessContent(parts[partKey] as Dictionary<object, object>, $"{key}${partKey}", node, playerName, components);
            }
            if (type != 10 && type != 3)
                return;
        }

        string? tips = null;
        if (content.TryGetValue("tooltip", out var tooltip))
        {
            if (tooltip is string tooltipText && !string.IsNullOrWhiteSpace(tooltipText))
                tips = tooltipText;
            else if (tooltip is List<object> { Count: > 0 } lines)
                tips = string.Join("\n", lines);
        }

        var name = GetDisplayLineText(content, key);
        if (name.Contains("%s_if(", StringComparison.Ordinal))
            name = key;
        if (string.IsNullOrWhiteSpace(name))
            name = tips;
        if (name is not null)
            name = name.Replace("&", "§");
        if (tips is not null)
            tips = tips.Replace("&", "§");

        var component = new GermComponent
        {
            Type = GermComponentType.Button,
            GuiUuid = node,
            ButtonName = name ?? string.Empty,
            ButtonTips = tips,
            ButtonPath = key,
            ClickAction =
            [
                GermPackets.BuildGuiOpen(node),
                GermPackets.BuildGuiClick(node, key, 0),
                GermPackets.BuildGuiClickDown(node, node, key, 1)
            ]
        };

        foreach (var action in actions)
        {
            if (string.IsNullOrWhiteSpace(action))
                continue;
            var command = action.Replace("%player_name%", playerName ?? string.Empty);
            if (command == "open<->null")
                continue;
            component.ClickAction.Add(GermPackets.BuildScriptDefault(node, key, command));
        }

        component.ClickAction.Add(GermPackets.BuildGuiClickDown(node, node, key, 0));
        components.Add(component);
    }

    private static bool IsReservedKey(string key) =>
        key.Equals("options", StringComparison.OrdinalIgnoreCase) || key.Equals("_bg", StringComparison.OrdinalIgnoreCase);

    /// <summary>clickDos 支持字符串或字符串列表，大小写变体一并识别。</summary>
    private static string[] ExtractBtnAction(Dictionary<object, object> content)
    {
        if (!content.TryGetValue("clickDos", out var value)
            && !content.TryGetValue("clickdos", out value)
            && !content.TryGetValue("CLICKDOS", out value))
            return [];

        return value switch
        {
            string text => [text],
            List<object> list => list.Where(static item => item is not null).Select(static item => item!.ToString()!).ToArray(),
            _ => []
        };
    }

    private static int ReadType(Dictionary<object, object> content)
    {
        if (content.TryGetValue("type", out var value))
        {
            try
            {
                return (int)value;
            }
            catch (Exception)
            {
            }
        }
        return -1;
    }

    private static IReadOnlyList<string> ReadTexts(Dictionary<object, object> content)
    {
        var texts = new List<string>();
        if (content.TryGetValue("texts", out var value) && value is List<object> list)
        {
            foreach (var item in list)
            {
                if (item is string text)
                    texts.Add(text);
            }
        }
        return texts;
    }

    private static string GetDisplayLineText(Dictionary<object, object> content, string normalName)
    {
        var texts = ReadTexts(content);
        if (texts.Count > 0)
            return string.Join(" ", texts);

        return normalName switch
        {
            "subject_bedwar" => "起床战争",
            "subject_skywar" => "空岛战争",
            "subject_leisure" => "休闲游戏",
            "subject_fight" => "竞技游戏",
            "subject_survive" => "生存",
            "subject_fight_team" => "战争",
            _ => normalName
        };
    }
}
