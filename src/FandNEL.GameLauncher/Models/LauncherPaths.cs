using System.Security.Cryptography;
using System.Text;

namespace FandNEL.GameLauncher.Models;

/// <summary>每个启动器实例独立的缓存和资源路径。</summary>
public sealed class LauncherPaths
{
    public LauncherPaths(string rootDirectory, string? resourceDirectory = null)
    {
        Root = Path.GetFullPath(rootDirectory);
        Resources = Path.GetFullPath(resourceDirectory ?? Path.Combine(Root, "resources"));
    }

    public string Root { get; }
    public string Resources { get; }
    public string Cache => Path.Combine(Root, ".game_cache");
    public string GameBase => Path.Combine(Cache, "Game", "Base");
    public string Minecraft => Path.Combine(GameBase, ".minecraft");
    public string Java => Path.Combine(Cache, "Java");
    public string Skins => Path.Combine(Cache, "Skins");
    public string CustomMods => Path.Combine(Resources, "mods");

    public string GameAssets(string gameId) => Path.Combine(Cache, "Game", Key(gameId));
    public string CoreMods(string gameId) => Path.Combine(Cache, "GameMods", Key(gameId));
    public string Runtime(JavaLaunchRequest request) =>
        Path.Combine(Cache, "Game", "Runtime", Key($"{request.UserId}:{request.GameId}:{request.RoleName}"), ".minecraft");

    internal static string Key(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    internal static string Child(string root, string relativePath)
    {
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(prefix, relativePath));
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"资源路径超出目标目录：{relativePath}");
        return path;
    }
}
