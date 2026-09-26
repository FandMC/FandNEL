using System.Collections.Concurrent;
using FandNEL.Proxy.Models;
using FandNEL.Proxy.Protocol;

namespace FandNEL.Gateway.Management;

/// <summary>追踪所有 Java/Bedrock Proxy 会话，并在会话停止时自动移除。</summary>
public sealed class ProxySessionManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, IProxySession> _sessions = new();
    private readonly GatewayEventHub? _events;
    private int _disposed;

    public ProxySessionManager(GatewayEventHub? events = null)
    {
        _events = events;
    }

    public int Count => _sessions.Count;

    public IReadOnlyCollection<IProxySession> Sessions => _sessions.Values.ToArray();

    public IReadOnlyList<ProxySessionSnapshot> GetSnapshots() =>
        _sessions.Values.Select(static session => session.Snapshot).ToArray();

    public void Register(IProxySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!_sessions.TryAdd(session.Id, session))
            return;
        session.EventOccurred += OnSessionEvent;
        _events?.Publish(GatewayEventKind.ProxyStarted, session.Id.ToString());
    }

    public bool TryGet(Guid id, out IProxySession? session) => _sessions.TryGetValue(id, out session);

    public async Task<bool> StopAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(id, out var session))
            return false;
        await session.StopAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        var sessions = _sessions.Values.ToArray();
        foreach (var session in sessions)
            await session.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        await StopAllAsync().ConfigureAwait(false);
        foreach (var session in _sessions.Values)
            session.EventOccurred -= OnSessionEvent;
        _sessions.Clear();
    }

    private void OnSessionEvent(object? sender, ProxyEventArgs args)
    {
        var value = args.Value;
        if (value.Kind is ProxyEventKind.Stopped or ProxyEventKind.Faulted)
        {
            if (_sessions.TryRemove(value.SessionId, out var session))
                session.EventOccurred -= OnSessionEvent;
            _events?.Publish(value.Kind == ProxyEventKind.Stopped
                ? GatewayEventKind.ProxyStopped
                : GatewayEventKind.ProxyFaulted, value.SessionId.ToString(), value.Message);
        }
    }
}
