using FandNEL.Accounts;
using FandNEL.Core.Protocol;

namespace FandNEL.Gateway.Management;

/// <summary>
/// FandNEL Gateway 运行态依赖容器。桌面 UI、后台任务和未来 Web 控制面共享同一套管理器。
/// </summary>
public sealed class GatewayRuntime : IAsyncDisposable
{
    public GatewayRuntime(AccountService accounts, WPFLauncher launcher, GameProxyService proxy)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(proxy);

        Events = new GatewayEventHub();
        Tokens = new TokenManager(Events);
        JavaUsers = new UserManager(launcher, accounts.DataDirectory, (userId, token) => Tokens.UpdateToken(userId, token));
        BedrockUsers = new CppUserManager(accounts.DataDirectory);
        JavaUsers.ReadUsersFromDisk();
        BedrockUsers.ReadUsersFromDisk();
        Accounts = new AccountManager(accounts, Tokens, JavaUsers, Events);
        Sessions = new ProxySessionManager(Events);
        Launchers = new LauncherTaskManager(Events);
        Catalog = new GameCatalogService(launcher, accounts);
        Games = new GameManager(Accounts, Catalog, proxy, Tokens, Sessions, Launchers, Events);
        WebSocket = new LocalGatewayWebSocketServer(this);
    }

    public GatewayEventHub Events { get; }
    public TokenManager Tokens { get; }
    public UserManager JavaUsers { get; }
    public CppUserManager BedrockUsers { get; }
    public AccountManager Accounts { get; }
    public ProxySessionManager Sessions { get; }
    public LauncherTaskManager Launchers { get; }
    public GameCatalogService Catalog { get; }
    public GameManager Games { get; }
    public LocalGatewayWebSocketServer WebSocket { get; }

    public Task StartAsync(CancellationToken cancellationToken = default) => WebSocket.StartAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await WebSocket.DisposeAsync().ConfigureAwait(false);
        await Launchers.DisposeAsync().ConfigureAwait(false);
        await Sessions.DisposeAsync().ConfigureAwait(false);
        await Games.DisposeAsync().ConfigureAwait(false);
        await JavaUsers.DisposeAsync().ConfigureAwait(false);
    }
}
