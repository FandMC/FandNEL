using FandNEL.GameLauncher.Models;

namespace FandNEL.GameLauncher.Services;

public interface IGameLauncher
{
    Task<GameLaunchHandle> LaunchAsync(
        GameLaunchRequest request,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IGameInstallService
{
    Task<GameInstall> PrepareAsync(
        GameLaunchRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILaunchConfigWriter
{
    Task WriteAsync(LaunchConfiguration configuration, CancellationToken cancellationToken = default);
}

public sealed record ProxyEndpoint(string Host, int Port)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new ArgumentException("代理地址不能为空。", nameof(Host));
        if (Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(Port));
    }
}

public interface IProxyLease : IAsyncDisposable
{
    ProxyEndpoint Endpoint { get; }
}

public interface IProxyEndpointProvider
{
    Task<IProxyLease> AcquireAsync(
        ProxyEndpointRequest request,
        CancellationToken cancellationToken = default);
}

