using System.Collections.Concurrent;
using FandNEL.Proxy.Models;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;

namespace FandNEL.Proxy.Services;

/// <summary>创建并追踪 TCP 代理会话。</summary>
public sealed class TcpProxyHost : IProxyHost
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly ConcurrentDictionary<Guid, ProxySession> _sessions = new();
    private bool _disposed;

    public IReadOnlyCollection<IProxySession> Sessions =>
        _sessions.Values.Cast<IProxySession>().ToArray();

    public async Task<ProxySession> StartAsync(ProxyOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var session = new ProxySession(options);
            session.EventOccurred += OnSessionEvent;
            try
            {
                await session.StartAsync(cancellationToken).ConfigureAwait(false);
                _sessions[session.Id] = session;
                return session;
            }
            catch
            {
                session.EventOccurred -= OnSessionEvent;
                await session.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopSessionsAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;
            _disposed = true;
            await StopSessionsAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task StopSessionsAsync()
    {
        await Task.WhenAll(_sessions.Values.Select(static session => session.DisposeAsync().AsTask())).ConfigureAwait(false);
        _sessions.Clear();
    }

    private void OnSessionEvent(object? sender, ProxyEventArgs args)
    {
        if (args.Value.Kind == ProxyEventKind.Stopped && sender is ProxySession session)
        {
            _sessions.TryRemove(session.Id, out _);
            session.EventOccurred -= OnSessionEvent;
        }
    }
}
