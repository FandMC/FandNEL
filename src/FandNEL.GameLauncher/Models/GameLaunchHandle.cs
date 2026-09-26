using FandNEL.GameLauncher.Processes;
using FandNEL.GameLauncher.Services;

namespace FandNEL.GameLauncher.Models;

/// <summary>已启动游戏及其代理资源的生命周期句柄。</summary>
public sealed class GameLaunchHandle : IAsyncDisposable
{
    private readonly IGameProcess _process;
    private readonly IProxyLease? _proxyLease;
    private int _released;

    internal GameLaunchHandle(IGameProcess process, IProxyLease? proxyLease)
    {
        _process = process;
        _proxyLease = proxyLease;
        _ = MonitorExitAsync();
    }

    public int ProcessId => _process.ProcessId;
    public bool HasExited => _process.HasExited;
    public ProxyEndpoint? ProxyEndpoint => _proxyLease?.Endpoint;

    public Task WaitForExitAsync(CancellationToken cancellationToken = default) =>
        WaitAndReleaseAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _process.StopAsync(cancellationToken).ConfigureAwait(false);
        await ReleaseProxyAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        await _process.DisposeAsync().ConfigureAwait(false);
    }

    private async Task MonitorExitAsync()
    {
        try
        {
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await ReleaseProxyAsync().ConfigureAwait(false);
            }
            catch
            {
                // 进程监视是后台清理任务，不能产生未观察到的异常。
            }
        }
    }

    private async Task WaitAndReleaseAsync(CancellationToken cancellationToken)
    {
        await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        await ReleaseProxyAsync().ConfigureAwait(false);
    }

    private async Task ReleaseProxyAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
            return;
        if (_proxyLease is not null)
            await _proxyLease.DisposeAsync().ConfigureAwait(false);
    }
}
