namespace FandNEL.Gateway.Management;

/// <summary>Gateway 运行态事件类型。事件只描述状态变化，不携带账号密钥。</summary>
public enum GatewayEventKind
{
    AccountAdded,
    AccountRemoved,
    AccountUpdated,
    TokenUpdated,
    TokenExpired,
    ProxyStarted,
    ProxyStopped,
    ProxyFaulted,
    LauncherStarted,
    LauncherProgress,
    LauncherExited,
    LauncherFailed
}

public sealed record GatewayEvent(
    GatewayEventKind Kind,
    DateTimeOffset OccurredAt,
    string? EntityId = null,
    string? Message = null);

public sealed class GatewayEventArgs(GatewayEvent value) : EventArgs
{
    public GatewayEvent Value { get; } = value;
}

/// <summary>
/// 进程内的轻量事件总线。UI 和管理器都可以订阅，发布失败不会反向破坏数据面。
/// </summary>
public sealed class GatewayEventHub
{
    public event EventHandler<GatewayEventArgs>? Published;

    public void Publish(GatewayEventKind kind, string? entityId = null, string? message = null)
    {
        var args = new GatewayEventArgs(new GatewayEvent(kind, DateTimeOffset.UtcNow, entityId, message));
        try
        {
            Published?.Invoke(this, args);
        }
        catch
        {
            // 观察者异常不能影响账号、代理或启动任务。
        }
    }
}
