namespace FandNEL.GameLauncher.Processes;

public interface IGameProcess : IAsyncDisposable
{
    int ProcessId { get; }
    bool HasExited { get; }
    Task WaitForExitAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface IGameProcessFactory
{
    Task<IGameProcess> StartAsync(
        Models.ProcessLaunchSpec launchSpec,
        CancellationToken cancellationToken = default);
}

