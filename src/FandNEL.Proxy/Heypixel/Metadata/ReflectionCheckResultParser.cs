using System.Text;
using System.Text.Json;
using Serilog;

namespace FandNEL.Proxy.Heypixel;

public static class ReflectionCheckResultParser
{
    private static readonly string[] SensitiveKeywords =
    [
        "password", "token", "session", "credential", "secret",
        "auth", "key", "private", "uuid", "hwid"
    ];

    private static readonly string[] SensitiveClassNames =
    [
        "SessionManager", "LoginManager", "NetworkManager",
        "AuthenticationService", "PlayerData", "UserProfile"
    ];

    public static ReflectionCheckReport Parse(string json)
    {
        ReflectionCheckReport report = new();
        List<JsonElement>? elements = JsonSerializer.Deserialize<List<JsonElement>>(json);
        if (elements is not { Count: > 0 })
        {
            report.FinalAction = "EMPTY";
            report.AccessPath = "null";
            return report;
        }

        StringBuilder accessPath = new();
        for (int index = 0; index < elements.Count; index++)
        {
            ReflectionCheckEntry entry = ParseEntry(elements[index]);
            report.ChainSteps.Add(
                $"Step {index + 1}: {entry.Action} - {entry.ClassName ?? "from previous"}.{entry.FieldName ?? entry.EnumName}");

            if (index == elements.Count - 1)
            {
                report.FinalAction = entry.Action;
            }

            if (entry.InstanceFromPrevious)
            {
                report.ChainDepth++;
            }
            else
            {
                accessPath.Clear();
                accessPath.Append(entry.ClassName);
            }

            if (!string.IsNullOrEmpty(entry.FieldName))
            {
                accessPath.Append('.').Append(entry.FieldName);
            }
            else if (!string.IsNullOrEmpty(entry.EnumName))
            {
                accessPath.Append('.').Append(entry.EnumName);
            }
        }

        report.AccessPath = accessPath.ToString();
        AnalyzeSecurity(report, elements);
        return report;
    }

    public static void PrintDiagnostics(ReflectionCheckReport report)
    {
        Log.Debug("Heypixel: reflection check parsed ({StepCount} steps, suspicious: {Suspicious})",
            report.ChainSteps.Count, report.HasSecurityRisk);
    }

    private static ReflectionCheckEntry ParseEntry(JsonElement element)
    {
        ReflectionCheckEntry entry = new();
        if (element.TryGetProperty("action", out JsonElement action))
        {
            entry.Action = action.GetString();
        }

        if (element.TryGetProperty("className", out JsonElement className))
        {
            entry.ClassName = className.GetString();
        }

        if (element.TryGetProperty("fieldName", out JsonElement fieldName))
        {
            entry.FieldName = fieldName.GetString();
        }

        if (element.TryGetProperty("enumName", out JsonElement enumName))
        {
            entry.EnumName = enumName.GetString();
        }

        if (element.TryGetProperty("instanceFromPrevious", out JsonElement fromPrevious))
        {
            entry.InstanceFromPrevious = fromPrevious.ValueKind switch
            {
                JsonValueKind.String => fromPrevious.GetString() == "true",
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new ArgumentException("Invalid JsonValueKind")
            };
        }

        return entry;
    }

    private static void AnalyzeSecurity(ReflectionCheckReport report, IEnumerable<JsonElement> elements)
    {
        if (report.AccessPath is null)
        {
            return;
        }

        string normalizedPath = report.AccessPath.ToLowerInvariant();
        List<string> warnings = SensitiveKeywords
            .Where(normalizedPath.Contains)
            .Select(keyword => "Contains danger keyword: " + keyword)
            .ToList();

        if (report.ChainDepth > 2)
        {
            warnings.Add($"Deep chained calls (Deep: {report.ChainDepth})");
        }

        foreach (JsonElement element in elements)
        {
            if (!element.TryGetProperty("className", out JsonElement classNameElement))
            {
                continue;
            }

            string? className = classNameElement.GetString();
            foreach (string sensitiveClassName in SensitiveClassNames)
            {
                if (className?.Contains(sensitiveClassName, StringComparison.Ordinal) == true)
                {
                    warnings.Add("Dangerous class: " + sensitiveClassName);
                }
            }
        }

        if (!normalizedPath.StartsWith("com.heypixel.heypixelmod"))
        {
            warnings.Add("Accessing non-Heypixel classes, this is a security risk");
        }

        if (warnings.Count == 0)
        {
            return;
        }

        report.HasSecurityRisk = true;
        report.SecurityWarning = string.Join("; ", warnings);
    }
}
