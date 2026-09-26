using FandNEL.GameLauncher.Models;

namespace FandNEL.GameLauncher.Services;

/// <summary>准备本地游戏目录并解析可执行文件路径。</summary>
public sealed class FileGameInstallService : IGameInstallService
{
    public Task<GameInstall> PrepareAsync(GameLaunchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var root = Path.GetFullPath(request.InstallDirectory);
        Directory.CreateDirectory(root);
        var executable = Path.IsPathRooted(request.ExecutablePath)
            ? Path.GetFullPath(request.ExecutablePath)
            : Path.GetFullPath(Path.Combine(root, request.ExecutablePath));
        var workingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
            ? Path.GetDirectoryName(executable) ?? root
            : Path.GetFullPath(Path.Combine(root, request.WorkingDirectory));
        Directory.CreateDirectory(workingDirectory);
        return Task.FromResult(new GameInstall(root, executable, workingDirectory));
    }
}

