#nullable enable
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FandNEL.Core.Entities;

namespace FandNEL.Core.Services;

/// <summary>Codexus 远程认证和资源服务。凭证只放在请求头，不写入日志。</summary>
public sealed class WebNexusApi : IDisposable
{
    private static readonly Uri ServiceAddress = new("https://api.codexus.today/");
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly string _token;
    private readonly Action<string, int> _progress;

    public WebNexusApi(string nexusToken, Action<string, int>? progressCallback = null, HttpClient? client = null)
    {
        _token = nexusToken;
        _client = client ?? new HttpClient();
        _ownsClient = client is null;
        _progress = progressCallback ?? ((_, _) => { });
    }

    public Task<string> ComputeAuthenticationBodyAsync(string serverId, long gameId, string gameVersion, string modInfo, string channel, int userId, string handshakeKey, CancellationToken cancellationToken = default) =>
        PostAsync("/api/GameCipher/compute/authentication/body", new { serverId, gameId, gameVersion, modInfo, channel, userId, handshakeKey }, cancellationToken);

    public Task<string> ComputeHandshakeBodyAsync(int userId, string userToken, string base64Context, string channel, string gameVersion, CancellationToken cancellationToken = default) =>
        PostAsync("/api/GameCipher/compute/authentication/handshake", new { userId, userToken, base64Context, channel, gameVersion }, cancellationToken);

    public Task<string> PeAccountConvert(string body, CancellationToken cancellationToken = default) =>
        PostAsync("/api/PeGameCipher/account/convert", new { body }, cancellationToken);

    public Task<string> PeAuthentication(string clientKey, string displayName, string serverId, string gameType, uint userId, string userToken, CancellationToken cancellationToken = default) =>
        PostAsync("/api/PeGameCipher/authentication", new { clientKey, displayName, serverId, gameType, userId, userToken }, cancellationToken);

    public Task<string> PeHttpEncryptAsync(string body, CancellationToken cancellationToken = default) =>
        PostAsync("/api/PeGameCipher/crypto/encrypt", new { body }, cancellationToken);

    public Task<string> PeHttpDecryptAsync(string body, CancellationToken cancellationToken = default) =>
        PostAsync("/api/PeGameCipher/crypto/decrypt", new { body }, cancellationToken);

    public Task<string> PeCppGameClientInfoAsync(string os, long version, string entityId, long userId, string userToken, CancellationToken cancellationToken = default) =>
        PostAsync("/api/cpp-game-client-info", new { os, version, entityId, userId, userToken }, cancellationToken);

    public string PeMcpGetCheckNum(string dynamicPyCode, string dynamicCheckSalt, string gamePlayerId) =>
        PeMcpGetCheckNumAsync(dynamicPyCode, dynamicCheckSalt, gamePlayerId).GetAwaiter().GetResult();

    public async Task<string> PeMcpGetCheckNumAsync(string dynamicPyCode, string dynamicCheckSalt, string gamePlayerId, CancellationToken cancellationToken = default) =>
        Deserialize<BodyIn>(await PostAsync("/api/PeZeroKnowledgeProof/zkp/get/check-num", new { dynamicPyCode, dynamicCheckSalt, gamePlayerId }, cancellationToken).ConfigureAwait(false)).Body;

    public string PeMcpGetStartType(string signature, string userId) => PeMcpGetStartTypeAsync(signature, userId).GetAwaiter().GetResult();

    public async Task<string> PeMcpGetStartTypeAsync(string signature, string userId, CancellationToken cancellationToken = default) =>
        Deserialize<BodyIn>(await PostAsync("/api/PeZeroKnowledgeProof/zkp/get/start-type", new { signature, userId }, cancellationToken).ConfigureAwait(false)).Body;

    public IdCard GetRandomIdCard() => GetRandomIdCardAsync().GetAwaiter().GetResult();

    public async Task<IdCard> GetRandomIdCardAsync(CancellationToken cancellationToken = default) =>
        Deserialize<IdCard>(await GetAsync("/api/app/get/app/id-card", cancellationToken: cancellationToken).ConfigureAwait(false));

    public string ComputeCaptchaAsync(byte[] image) => ComputeCaptchaImageAsync(image).GetAwaiter().GetResult();

    public async Task<string> ComputeCaptchaImageAsync(byte[] image, CancellationToken cancellationToken = default) =>
        Deserialize<BodyIn>(await PostAsync("/api/app/compute/captcha", new { body = Convert.ToBase64String(image) }, cancellationToken).ConfigureAwait(false)).Body;

    public Task<string> GetAppVersionAsync(string appId, string appSecret, CancellationToken cancellationToken = default) =>
        PostAsync("/api/app/get/app/version", new { appId, appSecret }, cancellationToken);

    public Task<string> GetGatewayDownloadUrlAsync(string system, CancellationToken cancellationToken = default) =>
        GetAsync($"/api/App/getGatewayDownloadUrl?system={Uri.EscapeDataString(system)}", cancellationToken: cancellationToken);

    public byte[] DownloadFile(string url, string pluginId) => DownloadFileAsync(url, pluginId).GetAwaiter().GetResult();

    public async Task<byte[]> DownloadFileAsync(string url, string pluginId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, url);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var destination = new MemoryStream();
        var buffer = new byte[81920];
        var total = response.Content.Headers.ContentLength;
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            if (total is > 0) _progress(pluginId, (int)Math.Min(100, destination.Length * 100 / total.Value));
        }
        _progress(pluginId, 100);
        return destination.ToArray();
    }

    public async Task<string> GetAsync(string endpoint, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, endpoint);
        if (headers is not null)
            foreach (var header in headers) request.Headers.Add(header.Key, header.Value);
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> PostAsync<T>(string endpoint, T data, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, endpoint);
        request.Content = JsonContent.Create(data);
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint)
    {
        var uri = new Uri(ServiceAddress, endpoint);
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (uri.Scheme == ServiceAddress.Scheme && uri.Authority == ServiceAddress.Authority
            && !string.IsNullOrWhiteSpace(_token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return request;
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Codexus 服务请求失败（HTTP {(int)response.StatusCode}）。", null, response.StatusCode);
    }

    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json)
        ?? throw new InvalidDataException($"Codexus 服务未返回有效的 {typeof(T).Name}。");

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }
}
