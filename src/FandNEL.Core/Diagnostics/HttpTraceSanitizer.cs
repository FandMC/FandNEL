using System.Text.Json;
using System.Text.RegularExpressions;

namespace FandNEL.Core.Diagnostics;

public static partial class HttpTraceSanitizer
{
    private static readonly string[] SensitiveHeaderNames =
    [
        "authorization", "proxy-authorization", "cookie", "set-cookie", "x-api-key",
        "x-auth-token", "x-csrf-token", "token", "access_token", "refresh_token",
        "password", "secret", "credential", "sauth"
    ];

    public static IReadOnlyDictionary<string, string> SanitizeHeaders(
        IEnumerable<KeyValuePair<string, string>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            result[header.Key] = IsSensitiveHeader(header.Key) ? "[REDACTED]" : header.Value;
        }

        return result;
    }

    public static string SanitizeBody(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return SanitizeJson(document.RootElement).GetRawText();
        }
        catch (JsonException)
        {
            return SensitiveValueRegex().Replace(body, "$1=[REDACTED]");
        }
    }

    private static bool IsSensitiveHeader(string name) =>
        SensitiveHeaderNames.Any(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));

    private static JsonElement SanitizeJson(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteSanitized(writer, element, propertyName: null);
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static void WriteSanitized(Utf8JsonWriter writer, JsonElement element, string? propertyName)
    {
        if (propertyName is not null && IsSensitiveHeader(propertyName))
        {
            writer.WriteStringValue("[REDACTED]");
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteSanitized(writer, property.Value, property.Name);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteSanitized(writer, item, propertyName: null);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    [GeneratedRegex("(?i)(\\\"?(?:password|token|secret|authorization|cookie)\\\"?\\s*[:=]\\s*)(?:\\\"[^\\\"]*\\\"|[^&\\s,}]+)")]
    private static partial Regex SensitiveValueRegex();
}
