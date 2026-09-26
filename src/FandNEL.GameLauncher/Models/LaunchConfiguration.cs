namespace FandNEL.GameLauncher.Models;

/// <summary>可选的启动配置文件内容。</summary>
public sealed record LaunchConfiguration(
    string FilePath,
    string GameDirectory,
    string? GameId,
    string? GameVersion,
    string? ProxyHost,
    int? ProxyPort,
    IReadOnlyList<string> Arguments);

