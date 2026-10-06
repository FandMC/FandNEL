using System.Text.Json;

namespace FandNEL.Proxy.Servers;

/// <summary>把通道配置里的 ModInfo 解析成“模组名 → MD5”，供 Aely 校验报文使用。</summary>
internal static class ModInfoParser
{
    internal static IReadOnlyDictionary<string, string> Parse(string? modInfo)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(modInfo))
            return result;
        try
        {
            using var document = JsonDocument.Parse(modInfo);
            switch (document.RootElement.ValueKind)
            {
                case JsonValueKind.Object:
                    ReadObject(document.RootElement, result);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in document.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                            ReadObject(item, result);
                    }
                    break;
            }
        }
        catch (Exception)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        return result;
    }

    private static void ReadObject(JsonElement element, Dictionary<string, string> result)
    {
        if (element.TryGetProperty("mods", out var mods) && mods.ValueKind == JsonValueKind.Array)
        {
            foreach (var mod in mods.EnumerateArray())
            {
                if (mod.ValueKind != JsonValueKind.Object)
                    continue;
                var id = GetString(mod, "id") ?? GetString(mod, "name");
                var md5 = GetString(mod, "md5") ?? GetString(mod, "hash");
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(md5))
                    result[id] = md5;
            }
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                result[property.Name] = property.Value.GetString()!;
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
