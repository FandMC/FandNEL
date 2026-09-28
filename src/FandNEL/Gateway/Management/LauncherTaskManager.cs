using System.Collections.Concurrent;
using FandNEL.GameLauncher.Models;
using Serilog;

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
    int? ProgressPercent,
    string? GameName = null,
    string? RoleName = null,
    bool IsRental = false);

/// <summary>从准备资源开始追踪启动任务；失败保留原因，退出或取消后释放全部资源。</summary>
public sealed class LauncherTaskManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, Entry> _tasks = new();
    private readonly GatewayEventHub? _events;
    private readonly object _lifecycleLock = new();
    private Task? _disposeTask;

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
        var entry = new Entry(request?.GameId, request?.GameVersion, request?.GameId, null, false,
            initialProgress ?? new LaunchProgress(LaunchStage.Running, "游戏已启动。"));
        entry.Attach(handle.ProcessId);
        return Add(entry, (_, _) => Task.FromResult<IGameLaunchHandle>(handle), handle);
    }

    public Guid Start(string? gameId, string? gameVersion, string? gameName, string? roleName, bool isRental,
        Func<IProgress<LaunchProgress>, CancellationToken, Task<IGameLaunchHandle>> launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        return Add(new Entry(gameId, gameVersion, gameName, roleName, isRental,
            new LaunchProgress(LaunchStage.Preparing, "正在准备游戏启动。")), launch);
    }

    private Guid Add(Entry entry, Func<IProgress<LaunchProgress>, CancellationToken, Task<IGameLaunchHandle>> launch,
        IGameLaunchHandle? registeredHandle = null)
    {
        lock (_lifecycleLock)
        {
            if (_disposeTask is not null)
            {
                entry.Complete();
                throw new ObjectDisposedException(nameof(LauncherTaskManager));
            }
            if (!_tasks.TryAdd(entry.Id, entry))
                throw new InvalidOperationException("无法注册游戏启动任务。");
            _events?.Publish(GatewayEventKind.LauncherStarted, entry.Id.ToString());
            _ = Task.Run(() => RunAsync(entry, launch, registeredHandle));
        }
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
        if (!entry.SetProgress(progress))
            return false;
        _events?.Publish(GatewayEventKind.LauncherProgress, id.ToString(), progress.Message);
        return true;
    }

    public async Task<bool> StopAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(id, out var entry))
            return false;
        entry.RequestStop();
        _events?.Publish(GatewayEventKind.LauncherProgress, id.ToString());
        var cleanupFailure = await entry.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        Remove(entry);
        if (cleanupFailure is not null)
            throw new InvalidOperationException("清理游戏启动任务失败。", cleanupFailure);
        return true;
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        var entries = _tasks.Values.ToArray();
        foreach (var entry in entries)
        {
            entry.RequestStop();
            _events?.Publish(GatewayEventKind.LauncherProgress, entry.Id.ToString());
        }
        var failures = await Task.WhenAll(entries.Select(entry => entry.Completion)).WaitAsync(cancellationToken).ConfigureAwait(false);
        foreach (var entry in entries)
            Remove(entry);
        var cleanupFailures = failures.OfType<Exception>().ToArray();
        if (cleanupFailures.Length != 0)
            throw new AggregateException("清理游戏启动任务失败。", cleanupFailures);
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifecycleLock)
            return new ValueTask(_disposeTask ??= Task.Run(() => StopAllAsync()));
    }

    private async Task RunAsync(Entry entry,
        Func<IProgress<LaunchProgress>, CancellationToken, Task<IGameLaunchHandle>> launch,
        IGameLaunchHandle? registeredHandle)
    {
        var handle = registeredHandle;
        var exited = false;
        Exception? failure = null;
        Exception? cleanupFailure = null;
        try
        {
            entry.Token.ThrowIfCancellationRequested();
            handle ??= await launch(new InlineProgress(progress => UpdateProgress(entry.Id, progress)), entry.Token).ConfigureAwait(false);
            entry.Attach(handle.ProcessId);
            entry.Token.ThrowIfCancellationRequested();
            UpdateProgress(entry.Id, new LaunchProgress(LaunchStage.Running, "游戏已启动。"));
            await handle.WaitForExitAsync(entry.Token).ConfigureAwait(false);
            exited = true;
        }
        catch (OperationCanceledException) when (entry.Token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            if (handle is not null)
            {
                try
                {
                    if (!exited)
                        await handle.StopAsync().ConfigureAwait(false);
                }
                catch (Exception exception) { cleanupFailure = exception; }
                try { await handle.DisposeAsync().ConfigureAwait(false); }
                catch (Exception exception) { cleanupFailure ??= exception; }
            }

            var cancellationFailure = await entry.BeginCompletionAsync().ConfigureAwait(false);
            cleanupFailure ??= cancellationFailure;
            failure ??= cleanupFailure;
            if (failure is not null && (!entry.Token.IsCancellationRequested || cleanupFailure is not null))
            {
                entry.Fail(failure.Message);
                Log.Error(failure, "Game launch task {TaskId} failed", entry.Id);
                _events?.Publish(GatewayEventKind.LauncherFailed, entry.Id.ToString(), failure.Message);
            }
            else
            {
                Remove(entry);
            }
            entry.Complete(cleanupFailure);
        }
    }

    private void Remove(Entry entry)
    {
        if (_tasks.TryRemove(entry.Id, out _))
            _events?.Publish(GatewayEventKind.LauncherExited, entry.Id.ToString());
    }

    // Progress<T> 会把回调排入线程池，下载和启动阶段可能因此乱序。
    private sealed class InlineProgress(Action<LaunchProgress> report) : IProgress<LaunchProgress>
    {
        public void Report(LaunchProgress value) => report(value);
    }

    private sealed class Entry
    {
        public Entry(string? gameId, string? gameVersion, string? gameName, string? roleName, bool isRental,
            LaunchProgress progress)
        {
            Id = Guid.NewGuid();
            GameId = gameId;
            GameVersion = gameVersion;
            GameName = gameName;
            RoleName = roleName;
            IsRental = isRental;
            StartedAt = DateTimeOffset.UtcNow;
            _stage = progress.Stage;
            _message = progress.Message;
            _progressPercent = ToPercent(progress);
            Token = _cancellation.Token;
        }

        public Guid Id { get; }
        public string? GameId { get; }
        public string? GameVersion { get; }
        public string? GameName { get; }
        public string? RoleName { get; }
        public bool IsRental { get; }
        public DateTimeOffset StartedAt { get; }
        public CancellationToken Token { get; }
        public Task<Exception?> Completion => _completion.Task;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly TaskCompletionSource<Exception?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _lock = new();
        private LaunchStage _stage;
        private string _message;
        private int? _progressPercent;
        private int _processId;
        private bool _hasExited;
        private bool _completed;
        private bool _stopping;
        private Task _stopCallbacks = Task.CompletedTask;

        public bool SetProgress(LaunchProgress progress)
        {
            lock (_lock)
            {
                // 终态由工作任务在清理完成后发布，保留真正的异常原因。
                if (_completed || _stopping || progress.Stage is LaunchStage.Failed or LaunchStage.Completed)
                    return false;
                _stage = progress.Stage;
                _message = progress.Message;
                _progressPercent = ToPercent(progress);
                return true;
            }
        }

        public void Attach(int processId)
        {
            lock (_lock) _processId = processId;
        }

        public void RequestStop()
        {
            lock (_lock)
            {
                if (_completed || _stopping) return;
                _stopping = true;
                _stage = LaunchStage.Stopping;
                _message = "正在取消游戏启动或关闭游戏。";
                // 取消回调可能重入管理器或抛出异常，不能在持锁线程上同步执行。
                _stopCallbacks = _cancellation.CancelAsync();
            }
        }

        public void Fail(string message)
        {
            lock (_lock)
            {
                _stage = LaunchStage.Failed;
                _message = message;
                _hasExited = true;
            }
        }

        public async Task<Exception?> BeginCompletionAsync()
        {
            Task callbacks;
            lock (_lock)
            {
                _completed = true;
                _hasExited = true;
                callbacks = _stopCallbacks;
            }
            try
            {
                await callbacks.ConfigureAwait(false);
                return null;
            }
            catch (Exception exception) { return exception; }
        }

        public void Complete(Exception? cleanupFailure = null)
        {
            lock (_lock)
            {
                _completed = true;
                _hasExited = true;
                _cancellation.Dispose();
            }
            _completion.TrySetResult(cleanupFailure);
        }

        public LauncherTaskSnapshot Snapshot
        {
            get
            {
                lock (_lock)
                {
                    return new LauncherTaskSnapshot(
                        Id, GameId, GameVersion, _processId, _hasExited,
                        StartedAt, _stage, _message, _progressPercent, GameName, RoleName, IsRental);
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
