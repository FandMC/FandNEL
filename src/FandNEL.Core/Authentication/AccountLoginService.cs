using System.Text.Json;
using FandNEL.Core.Entities.WPFLauncher;
using FandNEL.Core.Protocol;

namespace FandNEL.Core.Authentication;

/// <summary>Core 账号登录门面：负责将 sauth 兑换为可用于进服的网易游戏令牌。</summary>
public sealed class AccountLoginService : IDisposable
{
    private readonly WPFLauncher _launcher;
    private readonly bool _ownsLauncher;

    public AccountLoginService(WPFLauncher? launcher = null)
    {
        _launcher = launcher ?? new WPFLauncher();
        _ownsLauncher = launcher is null;
    }

    public async Task<JavaAccountSession> LoginWithCookieAsync(string cookie, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cookie);
        var result = await _launcher.LoginWithCookieAsync(cookie, cancellationToken).ConfigureAwait(false);
        var user = result.Item1;
        var sauth = ExtractCookie(cookie);
        return new JavaAccountSession(
            user.EntityId,
            user.Token,
            sauth,
            result.Item2,
            user.Account);
    }

    public async Task<JavaAccountSession> LoginWithNeteaseEmailAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _launcher.LoginWithEmailAsync(email, password).ConfigureAwait(false);
        var cookie = WPFLauncher.GenerateCookie(user, _launcher.MPay.GetDevice());
        return await LoginWithCookieAsync(cookie.Json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>使用 Codexus 原有渠道协议登录，再统一走网易 OTP 激活。</summary>
    public async Task<JavaAccountSession> LoginAsync(
        string channel,
        string account,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        cancellationToken.ThrowIfCancellationRequested();

        string cookie;
        switch (channel.Trim().ToLowerInvariant())
        {
            case "netease":
                return await LoginWithNeteaseEmailAsync(account, password, cancellationToken).ConfigureAwait(false);
            case "4399pc":
                using (var client = new Pc4399())
                    cookie = await client.LoginWithPasswordAsync(account, password, null, null).ConfigureAwait(false);
                break;
            case "4399com":
            case "4399":
                using (var client = new Com4399())
                    cookie = await client.LoginAndAuthorize(account, password).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException($"不支持的登录渠道：{channel}");
        }

        return await LoginWithCookieAsync(cookie, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_ownsLauncher) _launcher.Dispose();
    }

    private static string ExtractCookie(string cookie)
    {
        try
        {
            return JsonSerializer.Deserialize<EntityX19CookieRequest>(cookie)?.Json ?? cookie;
        }
        catch (JsonException)
        {
            return cookie;
        }
    }
}

public sealed record JavaAccountSession(
    string UserId,
    string Token,
    string Cookie,
    string Channel,
    string DisplayName);
