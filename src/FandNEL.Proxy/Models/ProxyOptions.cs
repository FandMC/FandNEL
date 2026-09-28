using System.Net;
using FandNEL.Proxy.Heypixel;
using FandNEL.Proxy.Protocol;

namespace FandNEL.Proxy.Models;

/// <summary>创建 Minecraft 服务器代理会话所需的配置。</summary>
public sealed record ProxyOptions
{
    public const int DefaultListenPort = 20018;
    private string? _accessToken;

    public IPAddress ListenAddress { get; init; } = IPAddress.Loopback;
    /// <summary>起始监听端口；占用时逐个递增，0 等同于默认端口 20018。</summary>
    public int ListenPort { get; init; } = DefaultListenPort;
    public required ServerTarget Target { get; init; }
    public string? GameId { get; init; }
    public string? GameVersion { get; init; }
    public string? ModInfo { get; init; }
    public string? UserId { get; init; }
    public string? AccessToken
    {
        get => AccessTokenProvider is null ? _accessToken : AccessTokenProvider();
        init => _accessToken = value;
    }
    /// <summary>长期通道按需读取当前 token；提供者失效时不得回退到创建通道时的凭据。</summary>
    public Func<string>? AccessTokenProvider { get; init; }
    public PlayerRole Role { get; init; } = PlayerRole.Guest;
    public string? RentalServerId { get; init; }
    public Socks5Options? Socks5 { get; init; }
    public bool EnableLanBroadcast { get; init; }
    public string LanMotd { get; init; } = "FandNEL";
    public bool AddForgeHandshakeSuffix { get; init; } = true;
    public HeypixelOptions Heypixel { get; init; } = new();

    /// <summary>由应用层完成 Codexus 远程进服认证；只有成功返回后才发送加密响应。</summary>
    public Func<string, CancellationToken, Task>? JoinServerAsync { get; init; }

    /// <summary>注册此监听会话使用的协议扩展，不影响其他会话。</summary>
    public Action<PacketRegistry>? ConfigureRegistry { get; init; }

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>验证监听端点与代理连接配置。</summary>
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
        ArgumentNullException.ThrowIfNull(Heypixel);
        if (Heypixel.RequestTimeout <= TimeSpan.Zero || Heypixel.RequestTimeout > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(Heypixel.RequestTimeout));
    }
}
