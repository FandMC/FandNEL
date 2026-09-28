using System.Text.Json;
using System.Threading.Channels;
using FandNEL.GameLauncher.Models;
using Serilog;

namespace FandNEL.Gateway.Management;

/// <summary>合并高频下载事件，向当前 WebSocket 推送既有 Games 列表和进度协议。</summary>
internal sealed class LauncherTaskNotifications : IAsyncDisposable
{
    private readonly GatewayRuntime _runtime;
    private readonly Func<string, string, Task> _send;
    private readonly CancellationTokenSource _stop;
    private readonly Channel<bool> _changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly Task _worker;
    private int _ready;

    public LauncherTaskNotifications(GatewayRuntime runtime, Func<string, string, Task> send, CancellationToken cancellationToken)
    {
        _runtime = runtime;
        _send = send;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runtime.Events.Published += OnEvent;
        _worker = RunAsync();
    }

    public void Enable()
    {
        if (Interlocked.Exchange(ref _ready, 1) == 0 && _runtime.Launchers.Count > 0)
            _changes.Writer.TryWrite(true);
    }

    private void OnEvent(object? sender, GatewayEventArgs args)
    {
        if (Volatile.Read(ref _ready) != 0 && args.Value.Kind is (GatewayEventKind.LauncherStarted or GatewayEventKind.LauncherProgress
            or GatewayEventKind.LauncherFailed or GatewayEventKind.LauncherExited))
            _changes.Writer.TryWrite(true);
    }

    private async Task RunAsync()
    {
        var failures = new HashSet<Guid>();
        var running = new HashSet<Guid>();
        try
        {
            while (await _changes.Reader.WaitToReadAsync(_stop.Token).ConfigureAwait(false))
            {
                // 下载和解压可能逐块报告进度；只保留最新快照，最多每 250 ms 推送一次。
                await Task.Delay(250, _stop.Token).ConfigureAwait(false);
                while (_changes.Reader.TryRead(out _)) { }
                var tasks = _runtime.Launchers.GetSnapshots();
                foreach (var task in tasks)
                {
                    await _send("launch_progress", GameSessionMessages.SerializeProgress(task)).ConfigureAwait(false);
                    if (task.Stage == LaunchStage.Failed && failures.Add(task.Id))
                        await _send("error_notification", $"启动游戏失败: {task.Message}").ConfigureAwait(false);
                    if (task.Stage == LaunchStage.Running && running.Add(task.Id))
                        await _send("launch_game/success", JsonSerializer.Serialize(new { process_id = task.ProcessId })).ConfigureAwait(false);
                }
                failures.IntersectWith(tasks.Select(task => task.Id));
                running.IntersectWith(tasks.Select(task => task.Id));
                await _send("query_game_session", GameSessionMessages.Serialize(_runtime.Sessions.GetSnapshots(), tasks)).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception exception)
        {
            // 断开控制页只结束通知订阅，不影响下载或已启动的游戏。
            Log.Debug(exception, "Java launch notification stream ended");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _runtime.Events.Published -= OnEvent;
        await _stop.CancelAsync().ConfigureAwait(false);
        await _worker.ConfigureAwait(false);
        _stop.Dispose();
    }
}
