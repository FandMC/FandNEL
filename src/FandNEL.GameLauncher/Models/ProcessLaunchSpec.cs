namespace FandNEL.GameLauncher.Models;

/// <summary>传给进程工厂的完整启动参数。</summary>
public sealed record ProcessLaunchSpec(
    string FileName,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string?> Environment);

