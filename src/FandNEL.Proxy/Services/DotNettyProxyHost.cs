using System.Collections.Concurrent;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using DotNetty.Transport.Bootstrapping;
using FandNEL.Proxy.Models;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;

namespace FandNEL.Proxy.Services;

/// <summary>使用 DotNetty 事件循环承载代理会话。</summary>
public sealed class DotNettyProxyHost : IProxyHost
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly Lazy<Task> _disposeTask;
    private readonly ConcurrentDictionary<Guid, ProxySession> _sessions = new();
    private readonly MultithreadEventLoopGroup _acceptorGroup = new(1);
    private readonly MultithreadEventLoopGroup _workerGroup = new();
    private bool _disposed;

    public DotNettyProxyHost()
    {
        _disposeTask = new Lazy<Task>(DisposeCoreAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyCollection<IProxySession> Sessions => _sessions.Values.Cast<IProxySession>().ToArray();

    public async Task<ProxySession> StartAsync(ProxyOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var session = new ProxySession(options, _workerGroup);
            session.EventOccurred += OnSessionEvent;
            try
            {
                await session.StartAsync(_acceptorGroup, cancellationToken).ConfigureAwait(false);
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
            await Task.WhenAll(_sessions.Values.Select(static session => session.DisposeAsync().AsTask())).ConfigureAwait(false);
            _sessions.Clear();
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public ValueTask DisposeAsync() => new(_disposeTask.Value);

    private async Task DisposeCoreAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            await Task.WhenAll(_sessions.Values.Select(static session => session.DisposeAsync().AsTask())).ConfigureAwait(false);
            _sessions.Clear();
        }
        finally
        {
            _lifecycle.Release();
        }

        await _acceptorGroup.ShutdownGracefullyAsync().ConfigureAwait(false);
        await _workerGroup.ShutdownGracefullyAsync().ConfigureAwait(false);
        _lifecycle.Dispose();
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
