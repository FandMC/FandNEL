namespace FandNEL.GameLauncher.Models;

/// <summary>一次游戏启动任务的输入。</summary>
public sealed record GameLaunchRequest
{
    public required string InstallDirectory { get; init; }
    public required string ExecutablePath { get; init; }
    public string? WorkingDirectory { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    public IReadOnlyList<DownloadRequest> Resources { get; init; } = Array.Empty<DownloadRequest>();
    public IReadOnlyDictionary<string, string?> Environment { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    public ProxyEndpointRequest? Proxy { get; init; }
    public string? ConfigPath { get; init; }
    public string? GameId { get; init; }
    public string? GameVersion { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InstallDirectory))
            throw new ArgumentException("游戏目录不能为空。", nameof(InstallDirectory));
        if (string.IsNullOrWhiteSpace(ExecutablePath))
            throw new ArgumentException("游戏可执行文件不能为空。", nameof(ExecutablePath));
        if (ConfigPath is not null && Path.IsPathRooted(ConfigPath))
            throw new ArgumentException("启动配置路径必须位于游戏目录内。", nameof(ConfigPath));
        foreach (var resource in Resources)
            resource.Validate();
        Proxy?.Validate();
    }
}

/// <summary>向 Proxy 适配器申请本地端点的参数，不依赖 Proxy 项目。</summary>
public sealed record ProxyEndpointRequest(
    string? GameId = null,
    string? UserId = null,
    string? RentalServerId = null)
{
    public void Validate()
    {
        if (GameId is not null && GameId.Length > 256)
            throw new ArgumentException("游戏标识过长。", nameof(GameId));
        if (UserId is not null && UserId.Length > 256)
            throw new ArgumentException("用户标识过长。", nameof(UserId));
        if (RentalServerId is not null && RentalServerId.Length > 256)
            throw new ArgumentException("租赁服务器标识过长。", nameof(RentalServerId));
    }
}

