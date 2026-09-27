using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Serilog;

namespace FandNEL.Proxy.Irc;

/// <summary>匿名聊天室 HTTP 会话：增量轮询和发送。</summary>
internal sealed class IrcChatSession : IDisposable
{
    private readonly HttpClient _http;
    private readonly IrcChatOptions _options;
    private readonly string _gameId;
    private readonly string _clientId = Guid.NewGuid().ToString("N");
    private long _lastId;
    private string? _lastFailureKey;

    internal IrcChatSession(IrcChatOptions options, string gameId)
    {
        _options = options;
        _gameId = string.IsNullOrWhiteSpace(gameId) ? "unknown" : gameId.Trim();
        _http = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl.Trim().TrimEnd('/') + "/"),
            Timeout = IrcConstants.HttpTimeout
        };
    }

    public void Dispose() => _http.Dispose();

    /// <summary>增量轮询。首次调用（lastId=0）服务端返回最近消息，是否展示由上层决定。</summary>
    internal async Task<IrcPollResult> PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, IrcConstants.PollPath,
                new IrcPollRequest(Interlocked.Read(ref _lastId)));
            var (status, payload) = await PostAsync<IrcPollResponse>(request, cancellationToken).ConfigureAwait(false);
            if (payload is not { Success: true })
            {
                var reason = payload?.Message ?? $"HTTP {(int)status}";
                WarnOnce("poll|" + reason, "IRC: poll rejected: " + reason);
                return IrcPollResult.Failure;
            }

            var messages = (payload.Messages ?? []).Where(message => message.Id > 0).ToList();
            var maximumId = Math.Max(Interlocked.Read(ref _lastId),
                messages.Count == 0 ? 0 : messages.Max(message => message.Id));
            Interlocked.Exchange(ref _lastId, maximumId);
            _lastFailureKey = null;
            return new IrcPollResult(true, messages, payload.Online);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WarnOnce("poll-ex|" + exception.Message, "IRC: poll request failed", exception);
            return IrcPollResult.Failure;
        }
    }

    /// <summary>发送一条消息；由服务端清理前缀并标记为 IRC 消息。</summary>
    internal async Task<IrcSendResult> SendAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, IrcConstants.SendPath,
                new IrcSendRequest(IrcConstants.OutboundMessagePrefix + text));
            var (status, payload) = await PostAsync<IrcSendResponse>(request, cancellationToken).ConfigureAwait(false);
            if (payload is { Success: true }) return new IrcSendResult(true, string.Empty);
            return new IrcSendResult(false, payload?.Message ?? $"HTTP {(int)status}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log.Warning(exception, "IRC: send request failed");
            return new IrcSendResult(false, exception.Message);
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation(IrcConstants.GameIdHeader, _gameId);
        request.Headers.TryAddWithoutValidation(IrcConstants.ClientIdHeader, _clientId);
        return request;
    }

    private async Task<(HttpStatusCode Status, T? Payload)> PostAsync<T>(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return (response.StatusCode, string.IsNullOrWhiteSpace(text)
            ? default
            : JsonSerializer.Deserialize<T>(text, IrcChatOptions.JsonOptions));
    }

    /// <summary>同一原因连续失败只告警一次，避免断网或凭据错误时刷日志。</summary>
    private void WarnOnce(string key, string message, Exception? exception = null)
    {
        if (string.Equals(_lastFailureKey, key, StringComparison.Ordinal))
        {
            Log.Debug("{Message}", message);
            return;
        }
        _lastFailureKey = key;
        if (exception is null) Log.Warning("{Message}", message);
        else Log.Warning(exception, "{Message}", message);
    }
}
