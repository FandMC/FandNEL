using FandNEL.Proxy.Models;
using FandNEL.Proxy.Sessions;

namespace FandNEL.Proxy.Protocol;

/// <summary>负责创建和管理代理会话的主机。</summary>
public interface IProxyHost : IAsyncDisposable
{
    IReadOnlyCollection<IProxySession> Sessions { get; }
    Task<ProxySession> StartAsync(ProxyOptions options, CancellationToken cancellationToken = default);
    Task StopAllAsync(CancellationToken cancellationToken = default);
}
