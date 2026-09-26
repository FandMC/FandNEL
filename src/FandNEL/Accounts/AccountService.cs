using System.IO;
using FandNEL.Core.Authentication;
using FandNEL.Core.Protocol;
using FandNEL.Core.Security;
using FandNEL.Core.Storage;
using FandNEL.Gateway;
using FandNEL.Protocol.Authentication;
using UiChallengeHandler = FandNEL.Protocol.Authentication.ILoginChallengeHandler;

namespace FandNEL.Accounts;

/// <summary>只持久化加密的 sauth；游戏 token 在 OTP 激活成功后进入内存。</summary>
public sealed class AccountService : IAccountService, IAsyncDisposable
{
    private readonly WPFLauncher _launcher;
    private readonly AccountLoginService _login;
    private readonly IEncryptedStore<List<GatewayAccount>> _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, JavaAccountSession> _sessions = new();
    private readonly Dictionary<string, DateTimeOffset> _updated = new();
    private List<GatewayAccount>? _accounts;

    public string DataDirectory { get; }
    public WPFLauncher Launcher => _launcher;

    public AccountService(WPFLauncher launcher, string dataDirectory)
    {
        _launcher = launcher;
        _login = new AccountLoginService(launcher);
        Directory.CreateDirectory(dataDirectory);
        DataDirectory = dataDirectory;
        _store = new EncryptedJsonFileStore<List<GatewayAccount>>(
            Path.Combine(dataDirectory, "accounts.json.dpapi"), new WindowsDpapiSecretProtector());
    }

    public async Task<IReadOnlyList<AccountSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return (await LoadAsync(cancellationToken).ConfigureAwait(false)).Select(a => new AccountSummary(
                a.Id, a.Authorized || _sessions.ContainsKey(a.Id), false, a.Channel, a.Type, a.Platform, a.Alias)).ToArray();
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
            var accounts = (await LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
            string alias = accounts.Find(a => a.Id == session.UserId)?.Alias ?? session.DisplayName;
            accounts.RemoveAll(a => a.Id == session.UserId);
            accounts.Add(new GatewayAccount
            {
                Id = session.UserId, Channel = session.Channel, Type = "cookie", Details = session.Cookie,
                Alias = alias, Authorized = true, Platform = request.Platform
            });
            await _store.WriteAsync(accounts, cancellationToken).ConfigureAwait(false);
            _accounts = accounts;
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
            if (_sessions.TryGetValue(userId, out var current))
            {
                if (DateTimeOffset.UtcNow - _updated[userId] < TimeSpan.FromMinutes(25)) return current;
                var updated = await _launcher.AuthenticationUpdateAsync(userId, current.Token).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(updated?.Token))
                {
                    _sessions.Remove(userId);
                    throw new InvalidOperationException("账号会话已失效，请重新激活。");
                }
                current = current with { Token = updated.Token };
                SaveSession(current);
                return current;
            }
            GatewayAccount account = (await LoadAsync(cancellationToken).ConfigureAwait(false))
                .Find(a => a.Id == userId) ?? throw new InvalidOperationException("请先选择账号。");
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

    public Task RenameAsync(string userId, string alias, CancellationToken cancellationToken = default) =>
        MutateAsync(accounts => (accounts.Find(a => a.Id == userId)
            ?? throw new KeyNotFoundException("账号不存在。")).Alias = alias.Trim(), cancellationToken);

    public async Task RemoveAsync(string userId, CancellationToken cancellationToken = default)
    {
        await MutateAsync(accounts =>
        {
            accounts.RemoveAll(a => a.Id == userId);
            _sessions.Remove(userId);
            _updated.Remove(userId);
        }, cancellationToken).ConfigureAwait(false);
    }

    private void SaveSession(JavaAccountSession session)
    {
        _sessions[session.UserId] = session;
        _updated[session.UserId] = DateTimeOffset.UtcNow;
    }

    private async Task<List<GatewayAccount>> LoadAsync(CancellationToken cancellationToken) =>
        _accounts ??= await _store.ReadAsync(cancellationToken).ConfigureAwait(false) ?? [];

    private async Task MutateAsync(Action<List<GatewayAccount>> mutation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var accounts = (await LoadAsync(cancellationToken).ConfigureAwait(false)).Select(a => a with { }).ToList();
            mutation(accounts);
            await _store.WriteAsync(accounts, cancellationToken).ConfigureAwait(false);
            _accounts = accounts;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync().ConfigureAwait(false);
        _sessions.Clear();
        _gate.Dispose();
    }

}
