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
/// 该层负责变更事件和面向 UI 的一致入口。
/// </summary>
public sealed class AccountManager : IAccountManager, IAccountService
{
    private readonly AccountService _accounts;
    private readonly GatewayEventHub? _events;

    public AccountManager(AccountService accounts, GatewayEventHub? events = null)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _events = events;
    }

    public AccountService Service => _accounts;

    public Task<IReadOnlyList<AccountSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        _accounts.ListAsync(cancellationToken);

    public async Task<AccountSession> LoginAsync(LoginRequest request, UiChallengeHandler challengeHandler,
        CancellationToken cancellationToken = default)
    {
        var session = await _accounts.LoginAsync(request, challengeHandler, cancellationToken).ConfigureAwait(false);
        _events?.Publish(GatewayEventKind.AccountAdded, session.UserId, "账号已登录并激活。");
        return session;
    }

    public Task<JavaAccountSession> GetSessionAsync(string userId, CancellationToken cancellationToken = default) =>
        _accounts.GetSessionAsync(userId, cancellationToken);

    public async Task RefreshAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _accounts.RefreshAsync(userId, cancellationToken).ConfigureAwait(false);
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
        _events?.Publish(GatewayEventKind.AccountRemoved, userId);
    }

    public async Task DeactivateAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _accounts.DeactivateAsync(userId, cancellationToken).ConfigureAwait(false);
        _events?.Publish(GatewayEventKind.AccountUpdated, userId);
    }
}
