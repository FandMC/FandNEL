using System.Collections.Concurrent;
using FandNEL.GameLauncher.Models;

namespace FandNEL.Gateway.Management;

public sealed record LauncherTaskSnapshot(
    Guid Id,
    string? GameId,
    string? GameVersion,
    int ProcessId,
    bool HasExited,
    DateTimeOffset StartedAt,
    LaunchStage Stage,
    string Message,
    int? ProgressPercent);

/// <summary>追踪游戏启动句柄和进度，进程退出后自动移除。</summary>
public sealed class LauncherTaskManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, Entry> _tasks = new();
    private readonly GatewayEventHub? _events;
    private int _disposed;

    public LauncherTaskManager(GatewayEventHub? events = null)
    {
        _events = events;
    }

    public int Count => _tasks.Count;

    public IReadOnlyList<LauncherTaskSnapshot> GetSnapshots() =>
        _tasks.Values.Select(static item => item.Snapshot).ToArray();

    public Guid Register(GameLaunchHandle handle, GameLaunchRequest? request = null,
        LaunchProgress? initialProgress = null)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var entry = new Entry(Guid.NewGuid(), handle, request, initialProgress);
        if (!_tasks.TryAdd(entry.Id, entry))
            throw new InvalidOperationException("无法注册游戏启动任务。");
        _events?.Publish(GatewayEventKind.LauncherStarted, entry.Id.ToString());
        _ = ObserveExitAsync(entry);
        return entry.Id;
    }

    public bool TryGet(Guid id, out LauncherTaskSnapshot? snapshot)
    {
        if (_tasks.TryGetValue(id, out var entry))
        {
            snapshot = entry.Snapshot;
            return true;
        }

        snapshot = null;
        return false;
    }

    public bool UpdateProgress(Guid id, LaunchProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (!_tasks.TryGetValue(id, out var entry))
            return false;
        entry.SetProgress(progress);
        _events?.Publish(GatewayEventKind.LauncherProgress, id.ToString(), progress.Message);
        return true;
    }

    public async Task<bool> StopAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(id, out var entry))
            return false;
        await entry.Handle.StopAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in _tasks.Values)
            await entry.Handle.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        await StopAllAsync().ConfigureAwait(false);
        _tasks.Clear();
    }

    private async Task ObserveExitAsync(Entry entry)
    {
        try
        {
            await entry.Handle.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _events?.Publish(GatewayEventKind.LauncherFailed, entry.Id.ToString(), exception.Message);
        }
        finally
        {
            _tasks.TryRemove(entry.Id, out _);
            _events?.Publish(GatewayEventKind.LauncherExited, entry.Id.ToString());
        }
    }

    private sealed class Entry
    {
        public Entry(Guid id, GameLaunchHandle handle, GameLaunchRequest? request, LaunchProgress? progress)
        {
            Id = id;
            Handle = handle;
            GameId = request?.GameId;
            GameVersion = request?.GameVersion;
            StartedAt = DateTimeOffset.UtcNow;
            _stage = progress?.Stage ?? LaunchStage.Running;
            _message = progress?.Message ?? "游戏已启动。";
            _progressPercent = ToPercent(progress);
        }

        public Guid Id { get; }
        public GameLaunchHandle Handle { get; }
        public string? GameId { get; }
        public string? GameVersion { get; }
        public DateTimeOffset StartedAt { get; }
        private readonly object _lock = new();
        private LaunchStage _stage;
        private string _message;
        private int? _progressPercent;

        public void SetProgress(LaunchProgress progress)
        {
            lock (_lock)
            {
                _stage = progress.Stage;
                _message = progress.Message;
                _progressPercent = ToPercent(progress);
            }
        }

        public LauncherTaskSnapshot Snapshot
        {
            get
            {
                lock (_lock)
                {
                    return new LauncherTaskSnapshot(
                        Id, GameId, GameVersion, Handle.ProcessId, Handle.HasExited,
                        StartedAt, _stage, _message, _progressPercent);
                }
            }
        }

        private static int? ToPercent(LaunchProgress? progress)
        {
            if (progress?.TotalBytes is not > 0)
                return progress?.Stage is LaunchStage.Running or LaunchStage.Completed ? 100 : null;
            return (int)Math.Clamp(progress.CompletedBytes * 100L / progress.TotalBytes.Value, 0L, 100L);
        }
    }
}
