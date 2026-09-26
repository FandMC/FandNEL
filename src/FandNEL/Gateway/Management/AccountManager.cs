using FandNEL.Accounts;
using FandNEL.Core.Authentication;
using FandNEL.Gateway;
using FandNEL.Protocol.Authentication;
using UiChallengeHandler = FandNEL.Protocol.Authentication.ILoginChallengeHandler;

namespace FandNEL.Gateway.Management;

public interface IAccountManager
{
    Task<IReadOnlyList<AccountSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<AccountSession> LoginAsync(LoginRequest request, UiChallengeHandler challengeHandler,
        CancellationToken cancellationToken = default);
    Task<JavaAccountSession> GetSessionAsync(string userId, CancellationToken cancellationToken = default);
    Task RefreshAsync(string userId, CancellationToken cancellationToken = default);
    Task RenameAsync(string userId, string alias, CancellationToken cancellationToken = default);
    Task RemoveAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Gateway 账号管理门面。持久化和网易激活仍由 AccountService 负责，
/// 该层负责 token 同步、变更事件和面向 UI 的一致入口。
/// </summary>
public sealed class AccountManager : IAccountManager, IAccountService
{
    private readonly AccountService _accounts;
    private readonly TokenManager _tokens;
    private readonly UserManager _javaUsers;
    private readonly GatewayEventHub? _events;

    public AccountManager(AccountService accounts, TokenManager tokens, UserManager javaUsers,
        GatewayEventHub? events = null)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _javaUsers = javaUsers ?? throw new ArgumentNullException(nameof(javaUsers));
        _events = events;
    }

    public AccountService Service => _accounts;

    public Task<IReadOnlyList<AccountSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        _accounts.ListAsync(cancellationToken);

    public async Task<AccountSession> LoginAsync(LoginRequest request, UiChallengeHandler challengeHandler,
        CancellationToken cancellationToken = default)
    {
        var session = await _accounts.LoginAsync(request, challengeHandler, cancellationToken).ConfigureAwait(false);
        // AccountSession.Details 是持久化的 sauth；游戏 token 必须从激活后的运行态读取。
        var activated = await _accounts.GetSessionAsync(session.UserId, cancellationToken).ConfigureAwait(false);
        _tokens.UpdateToken(activated.UserId, activated.Token);
        _javaUsers.AddUser(new ManagedUser
        {
            UserId = session.UserId,
            Authorized = true,
            AutoLogin = false,
            Channel = session.Channel,
            Type = session.Type,
            Details = session.Details,
            Platform = GatewayPlatform.Desktop,
            Alias = session.Nickname
        });
        _javaUsers.AddUserToMaintain(activated.UserId, activated.Token);
        _events?.Publish(GatewayEventKind.AccountAdded, session.UserId, "账号已登录并激活。");
        return session;
    }

    public async Task<JavaAccountSession> GetSessionAsync(string userId, CancellationToken cancellationToken = default)
    {
        var session = await _accounts.GetSessionAsync(userId, cancellationToken).ConfigureAwait(false);
        _tokens.UpdateToken(session.UserId, session.Token);
        _javaUsers.AddUserToMaintain(session.UserId, session.Token);
        return session;
    }

    public async Task RefreshAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _accounts.RefreshAsync(userId, cancellationToken).ConfigureAwait(false);
        var session = await _accounts.GetSessionAsync(userId, cancellationToken).ConfigureAwait(false);
        _tokens.UpdateToken(session.UserId, session.Token);
        _javaUsers.AddUserToMaintain(session.UserId, session.Token);
        _events?.Publish(GatewayEventKind.AccountUpdated, userId);
    }

    public async Task RenameAsync(string userId, string alias, CancellationToken cancellationToken = default)
    {
        await _accounts.RenameAsync(userId, alias, cancellationToken).ConfigureAwait(false);
        _events?.Publish(GatewayEventKind.AccountUpdated, userId);
    }

    public async Task RemoveAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _accounts.RemoveAsync(userId, cancellationToken).ConfigureAwait(false);
        _tokens.RemoveToken(userId);
        _javaUsers.RemoveUser(userId);
        _events?.Publish(GatewayEventKind.AccountRemoved, userId);
    }
}
