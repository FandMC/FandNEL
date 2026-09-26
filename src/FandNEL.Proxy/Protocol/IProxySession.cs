using FandNEL.Proxy.Models;

namespace FandNEL.Proxy.Protocol;

/// <summary>单个 Minecraft 服务器代理会话。</summary>
public interface IProxySession : IAsyncDisposable
{
    Guid Id { get; }
    ProxySessionSnapshot Snapshot { get; }
    event EventHandler<ProxyEventArgs>? EventOccurred;
    Task UpdateServerAsync(ServerTarget target, CancellationToken cancellationToken = default);
    Task UpdateRoleAsync(PlayerRole role, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
