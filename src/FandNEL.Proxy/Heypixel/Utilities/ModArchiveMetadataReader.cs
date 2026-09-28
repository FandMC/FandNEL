using System.IO.Compression;
using System.Text;
using Serilog;

namespace FandNEL.Proxy.Heypixel;

public static class ModArchiveMetadataReader
{
    private const string ModsTomlPath = "META-INF/mods.toml";

    public static string? ReadModsToml(string jarPath)
    {
        if (!File.Exists(jarPath))
        {
            Log.Warning("Jar file not found: {JarPath}", jarPath);
            return null;
        }

        using ZipArchive archive = ZipFile.OpenRead(jarPath);
        string[] entryPaths =
        [
            ModsTomlPath,
            @"META-INF\mods.toml",
            "meta-inf/mods.toml",
            @"meta-inf\mods.toml"
        ];
        ZipArchiveEntry? entry = null;
        foreach (string entryPath in entryPaths)
        {
            entry = archive.Entries.FirstOrDefault(candidate =>
                string.Equals(candidate.FullName, entryPath, StringComparison.OrdinalIgnoreCase));
            if (entry is not null)
            {
                break;
            }
        }

        if (entry is null)
        {
            return null;
        }

        if (entry.Length > 1024 * 1024)
        {
            throw new InvalidDataException("Mod metadata exceeds its size limit.");
        }

        using Stream stream = entry.Open();
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static string? ParseModId(string modsToml)
    {
        bool insideModSection = false;
        foreach (string rawLine in modsToml.Split('\n'))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("[[mods]]"))
            {
                insideModSection = true;
                continue;
            }

            if (line.StartsWith("[[") && line != "[[mods]]")
            {
                insideModSection = false;
                continue;
            }

            if (!insideModSection || !line.Contains('='))
            {
                continue;
            }

            string[] parts = line.Split('=', 2);
            if (parts.Length != 2 || !string.Equals(parts[0].Trim(), "modId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = parts[1].Trim();
            int commentIndex = value.IndexOf('#');
            if (commentIndex >= 0)
            {
                value = value[..commentIndex].Trim();
            }

            return value.Trim('"', '\'');
        }

        return null;
    }

    public static string ReplaceFileName(string originalPath, string fileName)
    {
        int separator = Math.Max(originalPath.LastIndexOf('/'), originalPath.LastIndexOf('\\'));
        return separator >= 0 ? originalPath[..(separator + 1)] + fileName : fileName;
    }
}
