using FandNEL.Accounts;
using FandNEL.Core.Protocol;
using FandNEL.GameLauncher.Models;
using FandNEL.GameLauncher.Services.Java;

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
        JavaUsers = accounts.Users;
        JavaUsers.TokenUpdated += OnJavaTokenUpdated;
        JavaUsers.TokenRemoved += OnJavaTokenRemoved;
        BedrockUsers = new CppUserManager(accounts.DataDirectory);
        BedrockUsers.ReadUsersFromDisk();
        Accounts = new AccountManager(accounts, Events);
        Sessions = new ProxySessionManager(Events);
        Launchers = new LauncherTaskManager(Events);
        JavaLauncher = new JavaLauncherService(launcher, new LauncherPaths(accounts.DataDirectory));
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
    public JavaLauncherService JavaLauncher { get; }
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
        JavaUsers.TokenUpdated -= OnJavaTokenUpdated;
        JavaUsers.TokenRemoved -= OnJavaTokenRemoved;
    }

    private void OnJavaTokenUpdated(string userId, string token) => Tokens.UpdateToken(userId, token);
    private void OnJavaTokenRemoved(string userId) => Tokens.RemoveToken(userId);
}
