using FandNEL.Accounts;
using FandNEL.Core.Entities.WPFLauncher.NetGame;
using FandNEL.Core.Protocol;
using FandNEL.Proxy.Protocol;

namespace FandNEL.Gateway.Management;

/// <summary>
/// Gateway 业务编排入口：目录查询、角色操作、账号激活、Proxy 会话和启动任务
/// 使用同一组运行态管理器。它不引用旧 Codexus.Gateway。
/// </summary>
public sealed class GameManager : IAsyncDisposable
{
    private readonly GameCatalogService _catalog;
    private readonly GameProxyService _proxy;

    public GameManager(
        AccountManager accounts,
        GameCatalogService catalog,
        GameProxyService proxy,
        TokenManager tokens,
        ProxySessionManager sessions,
        LauncherTaskManager launchers,
        GatewayEventHub events)
    {
        Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
        Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        Sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        Launchers = launchers ?? throw new ArgumentNullException(nameof(launchers));
        Events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public AccountManager Accounts { get; }
    public TokenManager Tokens { get; }
    public ProxySessionManager Sessions { get; }
    public LauncherTaskManager Launchers { get; }
    public GatewayEventHub Events { get; }

    public Task<IReadOnlyList<GameEntry>> ListGamesAsync(string userId, GameKind kind, string keyword = "",
        int offset = 0, CancellationToken cancellationToken = default) =>
        _catalog.ListAsync(userId, kind, keyword, offset, cancellationToken);

    public Task<GameDetails> GetGameDetailsAsync(string userId, GameEntry game, string? password = null,
        CancellationToken cancellationToken = default) =>
        _catalog.DetailsAsync(userId, game, password, cancellationToken);

    public Task<IReadOnlyList<GameRole>> ListRolesAsync(string userId, GameEntry game,
        CancellationToken cancellationToken = default) =>
        _catalog.RolesAsync(userId, game, cancellationToken);

    public Task CreateRoleAsync(string userId, GameEntry game, string name,
        CancellationToken cancellationToken = default) =>
        _catalog.CreateRoleAsync(userId, game, name, cancellationToken);

    public async Task<GameProxyLease> StartProxyAsync(GameProxyRequest request,
        CancellationToken cancellationToken = default)
    {
        var lease = await _proxy.StartAsync(request, cancellationToken).ConfigureAwait(false);
        Sessions.Register(lease.Session);
        return lease;
    }

    public ValueTask DisposeAsync() => _proxy.DisposeAsync();
}
