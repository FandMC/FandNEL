namespace FandNEL.GameLauncher.Models;

/// <summary>游戏进程及附属资源的统一生命周期。</summary>
public interface IGameLaunchHandle : IAsyncDisposable
{
    int ProcessId { get; }
    bool HasExited { get; }
    Task WaitForExitAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
