using System.Diagnostics;
using FandNEL.GameLauncher.Models;

namespace FandNEL.GameLauncher.Processes;

/// <summary>使用 System.Diagnostics.Process 启动游戏并管理进程生命周期。</summary>
public sealed class SystemGameProcessFactory : IGameProcessFactory
{
    public Task<IGameProcess> StartAsync(ProcessLaunchSpec launchSpec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(launchSpec);
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(launchSpec.FileName))
            throw new FileNotFoundException("游戏可执行文件不存在。", launchSpec.FileName);

        var startInfo = new ProcessStartInfo
        {
            FileName = launchSpec.FileName,
            WorkingDirectory = launchSpec.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in launchSpec.Arguments)
            startInfo.ArgumentList.Add(argument);
        foreach (var pair in launchSpec.Environment)
        {
            if (pair.Value is null)
                startInfo.Environment.Remove(pair.Key);
            else
                startInfo.Environment[pair.Key] = pair.Value;
        }

        var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("无法启动游戏进程。");
        return Task.FromResult<IGameProcess>(new SystemGameProcess(process));
    }

    private sealed class SystemGameProcess(Process process) : IGameProcess
    {
        private int _disposed;

        public int ProcessId => process.Id;
        public bool HasExited => process.HasExited;

        public Task WaitForExitAsync(CancellationToken cancellationToken = default) =>
            process.WaitForExitAsync(cancellationToken);

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (process.HasExited)
                return;
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                return;
            }
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

