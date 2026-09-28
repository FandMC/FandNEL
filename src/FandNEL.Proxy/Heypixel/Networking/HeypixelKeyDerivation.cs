using System.Globalization;
using FandNEL.Proxy.Packet.Heypixel;

namespace FandNEL.Proxy.Heypixel;

/// <summary>异步调用现有派生接口；昵称按查询参数编码，错误不携带身份或密钥。</summary>
internal static class HeypixelKeyDerivation
{
    private static readonly HttpClient Client = new();

    internal static Uri CreateRequestUri(Uri endpoint, Guid profile, string userId, string playerName)
    {
        if (endpoint.Scheme != Uri.UriSchemeHttps && !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback))
            throw new ArgumentException("Heypixel 密钥服务必须使用 HTTPS。", nameof(endpoint));
        if (!long.TryParse(userId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericUserId))
            throw new InvalidDataException("Heypixel 用户 ID 不是有效整数。");
        if (string.IsNullOrWhiteSpace(playerName))
            throw new InvalidDataException("Heypixel 登录响应缺少玩家名称。");
        var builder = new UriBuilder(endpoint)
        {
            Query = $"profile={profile:D}&user={numericUserId.ToString(CultureInfo.InvariantCulture)}&name={Uri.EscapeDataString(playerName)}"
        };
        return builder.Uri;
    }

    internal static async Task<Uuid128> DeriveAsync(HeypixelOptions options, Guid profile,
        string userId, string playerName, CancellationToken cancellationToken, HttpClient? client = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.RequestTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            CreateRequestUri(options.DeriveKeyEndpoint, profile, userId, playerName));
        try
        {
            using var response = await (client ?? Client).SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new InvalidDataException($"Heypixel 密钥派生失败（HTTP {(int)response.StatusCode}）。");
            await response.Content.LoadIntoBufferAsync(256, timeout.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            try { return Uuid128.Parse(body); }
            catch (UuidFormatException) { throw new InvalidDataException("Heypixel 密钥服务返回了无效 UUID。"); }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Heypixel 密钥派生超时，请稍后重试。");
        }
        catch (HttpRequestException)
        {
            throw new IOException("Heypixel 密钥服务暂时无法访问。");
        }
    }
}
