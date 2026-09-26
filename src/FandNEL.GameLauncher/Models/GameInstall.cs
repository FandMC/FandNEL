namespace FandNEL.GameLauncher.Models;

/// <summary>已准备好的游戏安装目录。</summary>
public sealed record GameInstall(string RootDirectory, string ExecutablePath, string WorkingDirectory);

