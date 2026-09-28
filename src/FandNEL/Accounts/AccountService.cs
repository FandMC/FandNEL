using System.IO;
using FandNEL.Core.Authentication;
using FandNEL.Core.Protocol;
using FandNEL.Gateway;
using FandNEL.Gateway.Management;
using FandNEL.Protocol.Authentication;
using UiChallengeHandler = FandNEL.Protocol.Authentication.ILoginChallengeHandler;

namespace FandNEL.Accounts;

/// <summary>使用 users.json 保存 Java 账号；游戏 token 在 OTP 激活成功后进入内存。</summary>
public sealed class AccountService : IAccountService, IAsyncDisposable
{
    private readonly WPFLauncher _launcher;
    private readonly AccountLoginService _login;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, JavaAccountSession> _sessions = new();

    public string DataDirectory { get; }
    public WPFLauncher Launcher => _launcher;
    public UserManager Users { get; }

    public AccountService(WPFLauncher launcher, string dataDirectory)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _login = new AccountLoginService(launcher);
        Directory.CreateDirectory(dataDirectory);
        DataDirectory = dataDirectory;
        Users = new UserManager(launcher, dataDirectory);
    }

    public async Task<IReadOnlyList<AccountSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Users.GetUsersNoDetails().Select(a => new AccountSummary(
                a.UserId, a.Authorized, false, a.Channel, a.Type, a.Platform, a.Alias)).ToArray();
        }
        finally { _gate.Release(); }
    }

    public async Task<AccountSession> LoginAsync(LoginRequest request, UiChallengeHandler challengeHandler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challengeHandler);
        if (request.Platform == GatewayPlatform.Mobile)
            throw new NotSupportedException("此界面用于 Java 版；移动版请使用 Core.G79 接口。");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            JavaAccountSession session = request.Channel == "cookie"
                ? await _login.LoginWithCookieAsync(request.Password, cancellationToken).ConfigureAwait(false)
                : await _login.LoginAsync(request.Channel, request.Account, request.Password,
                    cancellationToken).ConfigureAwait(false);
            string alias = Users.GetUserById(session.UserId)?.Alias ?? session.DisplayName;
            await SaveUserAsync(new ManagedUser
            {
                UserId = session.UserId, Channel = session.Channel, Type = "cookie", Details = session.Cookie,
                Alias = alias, Authorized = true, Platform = request.Platform
            }, cancellationToken).ConfigureAwait(false);
            SaveSession(session);
            return new AccountSession(session.UserId, session.Channel, "cookie", alias, session.Cookie);
        }
        finally { _gate.Release(); }
    }

    public async Task<JavaAccountSession> GetSessionAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var account = Users.GetUserById(userId)
                ?? throw new InvalidOperationException("请先选择账号。");
            var available = Users.GetAvailableUser(userId);
            if (available is not null && _sessions.TryGetValue(userId, out var current))
            {
                available = await Users.RefreshAvailableUserAsync(userId, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("账号会话已失效，请重新激活。");
                cancellationToken.ThrowIfCancellationRequested();
                current = current with { Token = available.AccessToken };
                _sessions[userId] = current;
                return current;
            }
            _sessions.Remove(userId);
            if (account.Type != "cookie" || !account.Details.TrimStart().StartsWith('{'))
                throw new InvalidOperationException("旧账号记录缺少完整 sauth，请重新登录一次。");
            var activated = await _login.LoginWithCookieAsync(account.Details, cancellationToken).ConfigureAwait(false);
            if (activated.UserId != userId) throw new InvalidOperationException("激活账号与保存记录不一致。");
            SaveSession(activated);
            return activated;
        }
        finally { _gate.Release(); }
    }

    public async Task RefreshAsync(string userId, CancellationToken cancellationToken = default) =>
        _ = await GetSessionAsync(userId, cancellationToken).ConfigureAwait(false);

    public async Task RenameAsync(string userId, string alias, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var user = Users.GetUserById(userId) ?? throw new KeyNotFoundException("账号不存在。");
            await SaveUserAsync(new ManagedUser
            {
                UserId = user.UserId, Authorized = user.Authorized, AutoLogin = user.AutoLogin,
                Channel = user.Channel, Type = user.Type, Details = user.Details,
                Platform = user.Platform, Alias = alias.Trim()
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var previous = Users.GetUserById(userId);
            Users.RemoveUser(userId);
            _sessions.Remove(userId);
            try { await Users.SaveUsersToDiskAsync(cancellationToken).ConfigureAwait(false); }
            catch
            {
                if (previous is not null)
                {
                    previous.Authorized = false;
                    Users.AddUser(previous);
                }
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DeactivateAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _sessions.Remove(userId);
            Users.RemoveAvailableUser(userId);
            await Users.SaveUsersToDiskAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private void SaveSession(JavaAccountSession session)
    {
        _sessions[session.UserId] = session;
        var user = Users.GetUserById(session.UserId);
        if (user is not null) user.Authorized = true;
        Users.AddUserToMaintain(session.UserId, session.Token);
    }

    private async Task SaveUserAsync(ManagedUser user, CancellationToken cancellationToken)
    {
        var previous = Users.GetUserById(user.UserId);
        Users.AddUser(user, saveToDisk: false);
        try
        {
            await Users.SaveUsersToDiskAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (previous is null) Users.RemoveUser(user.UserId);
            else Users.AddUser(previous);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Users.DisposeAsync().ConfigureAwait(false);
        _sessions.Clear();
        _gate.Dispose();
    }

}
