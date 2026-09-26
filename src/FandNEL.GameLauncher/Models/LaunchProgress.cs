namespace FandNEL.GameLauncher.Models;

public enum LaunchStage
{
    Preparing,
    Downloading,
    WritingConfiguration,
    StartingProcess,
    Running,
    Stopping,
    Completed,
    Failed
}

/// <summary>游戏启动阶段和下载进度。</summary>
public sealed record LaunchProgress(
    LaunchStage Stage,
    string Message,
    long CompletedBytes = 0,
    long? TotalBytes = null,
    string? CurrentResource = null);

