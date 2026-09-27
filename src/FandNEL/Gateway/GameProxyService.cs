using System.Net;
using FandNEL.Accounts;
using FandNEL.Core.Connection;
using FandNEL.Proxy.Irc;
using FandNEL.Proxy.Models;
using FandNEL.Proxy.Services;

namespace FandNEL.Gateway;

/// <summary>把账户激活、Codexus 远程认证和无 SDK Proxy 连接接在一起。</summary>
public sealed class GameProxyService(AccountService accounts) : IAsyncDisposable
{
    private readonly DotNettyProxyHost _host = new();

    public IReadOnlyCollection<FandNEL.Proxy.Protocol.IProxySession> Sessions => _host.Sessions;

    public async Task<GameProxyLease> StartAsync(GameProxyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var session = await accounts.GetSessionAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (!int.TryParse(session.UserId, out var numericUserId))
            throw new InvalidOperationException("网易账号 ID 不是有效的数字。");

        // 游戏内 IRC（/IRC 与 NeoEastSide 聊天室互通）：配置默认生成在 %LOCALAPPDATA%/FandNEL/irc.json。
        var ircOptions = IrcChatOptions.Load();
        var irc = ircOptions.IsUsable ? new IrcChatBridge(ircOptions) : null;

        try
        {
            var proxy = await _host.StartAsync(new ProxyOptions
            {
                ListenAddress = request.ListenAddress,
                ListenPort = request.ListenPort,
                Target = request.Target,
                GameId = request.GameId,
                GameVersion = request.GameVersion,
                ModInfo = request.ModInfo,
                UserId = request.UserId,
                AccessToken = session.Token,
                Role = request.Role,
                RentalServerId = request.RentalServerId,
                Socks5 = request.Socks5,
                EnableLanBroadcast = request.EnableLanBroadcast,
                LanMotd = request.LanMotd,
                AddForgeHandshakeSuffix = request.AddForgeHandshakeSuffix,
                JoinServerAsync = (serverId, ct) => NetEaseConnection.AuthenticateAsync(
                    new JavaJoinRequest(serverId, request.GameId, request.GameVersion, request.ModInfo,
                        request.NexusToken, numericUserId, session.Token), ct),
                ConfigureRegistry = irc is null ? null : irc.AttachRegistry
            }, cancellationToken).ConfigureAwait(false);

            irc?.BindSession(proxy);

            var endpoint = proxy.Snapshot.LocalEndpoint
                ?? throw new InvalidOperationException("Proxy 未返回本地监听端点。");
            return new GameProxyLease(proxy, new GameLauncher.Services.ProxyEndpoint(endpoint.Address.ToString(), endpoint.Port));
        }
        catch
        {
            if (irc is not null)
                await irc.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}

public sealed record GameProxyRequest
{
    public required string UserId { get; init; }
    /// <summary>可选的远程服务凭据。本地流程不依赖 Nexus 账号。</summary>
    public string NexusToken { get; init; } = string.Empty;
    public required string GameId { get; init; }
    public required string GameVersion { get; init; }
    public string ModInfo { get; init; } = "[]";
    public required ServerTarget Target { get; init; }
    public PlayerRole Role { get; init; } = PlayerRole.Guest;
    public IPAddress ListenAddress { get; init; } = IPAddress.Loopback;
    public int ListenPort { get; init; }
    public string? RentalServerId { get; init; }
    public Socks5Options? Socks5 { get; init; }
    public bool EnableLanBroadcast { get; init; }
    public string LanMotd { get; init; } = "FandNEL";
    public bool AddForgeHandshakeSuffix { get; init; } = true;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(GameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(GameVersion);
        Target.Validate();
    }
}

public sealed class GameProxyLease : GameLauncher.Services.IProxyLease
{
    private readonly FandNEL.Proxy.Protocol.IProxySession _session;
    private int _disposed;

    internal GameProxyLease(FandNEL.Proxy.Protocol.IProxySession session, GameLauncher.Services.ProxyEndpoint endpoint)
    {
        _session = session;
        Endpoint = endpoint;
    }

    public GameLauncher.Services.ProxyEndpoint Endpoint { get; }

    /// <summary>当前 lease 对应的代理会话，供 Gateway 运行态管理器追踪生命周期。</summary>
    public FandNEL.Proxy.Protocol.IProxySession Session => _session;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _session.DisposeAsync().ConfigureAwait(false);
    }
}
