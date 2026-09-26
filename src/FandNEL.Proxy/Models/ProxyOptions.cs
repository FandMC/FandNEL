using System.Net;
using FandNEL.Proxy.Protocol;

namespace FandNEL.Proxy.Models;

/// <summary>创建 Minecraft 服务器代理会话所需的配置。</summary>
public sealed record ProxyOptions
{
    public IPAddress ListenAddress { get; init; } = IPAddress.Loopback;
    public int ListenPort { get; init; }
    public required ServerTarget Target { get; init; }
    public string? GameId { get; init; }
    public string? GameVersion { get; init; }
    public string? ModInfo { get; init; }
    public string? UserId { get; init; }
    public string? AccessToken { get; init; }
    public PlayerRole Role { get; init; } = PlayerRole.Guest;
    public string? RentalServerId { get; init; }
    public Socks5Options? Socks5 { get; init; }
    public bool EnableLanBroadcast { get; init; }
    public string LanMotd { get; init; } = "FandNEL";
    public bool AddForgeHandshakeSuffix { get; init; } = true;

    /// <summary>由应用层完成 Codexus 远程进服认证；只有成功返回后才发送加密响应。</summary>
    public Func<string, CancellationToken, Task>? JoinServerAsync { get; init; }

    /// <summary>注册此监听会话使用的协议扩展，不影响其他会话。</summary>
    public Action<PacketRegistry>? ConfigureRegistry { get; init; }

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>监听端口为 0 时请求系统分配动态端口。</summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(ListenAddress);
        ArgumentNullException.ThrowIfNull(Target);
        ArgumentNullException.ThrowIfNull(Role);
        if (ListenPort is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(ListenPort), "监听端口必须介于 0 和 65535 之间。");
        if (string.IsNullOrWhiteSpace(Role.Name))
            throw new ArgumentException("角色名不能为空。", nameof(Role));
        if (LanMotd.Contains('[') || LanMotd.Contains(']'))
            throw new ArgumentException("局域网名称不能包含方括号。", nameof(LanMotd));
        if (ConnectTimeout <= TimeSpan.Zero || ConnectTimeout > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(ConnectTimeout));
        Target.Validate();
        Socks5?.Validate();
    }
}
