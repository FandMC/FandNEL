using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FandNEL.Proxy.Irc;

/// <summary>聊天室消息。</summary>
internal sealed record IrcMessage(long Id, string Sender, string Text, bool IsIrc, long Time);

/// <summary>一次轮询的结果。</summary>
internal sealed record IrcPollResult(
    bool Success,
    string Message,
    IReadOnlyList<IrcMessage> Messages,
    int Online,
    bool Disabled,
    bool Unauthorized);

/// <summary>一次发言的结果。</summary>
internal sealed record IrcSendResult(bool Success, string Message);

/// <summary>
/// NeoEastSide 聊天室的 HTTP 会话：登录 → 增量轮询 → 发送。
/// 与 NeoEastSide 客户端使用同一套接口（Bearer 会话令牌 + X-HWID 头）。
/// </summary>
internal sealed class IrcChatSession : IDisposable
{
    private readonly HttpClient _http;
    private readonly IrcChatOptions _options;
    private readonly string _hwid;
    private string? _token;
    private long _lastId;

    internal IrcChatSession(IrcChatOptions options)
    {
        _options = options;
        var root = options.BaseUrl.Trim().TrimEnd('/');
        _http = new HttpClient
        {
            BaseAddress = new Uri(root + "/"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        _hwid = IrcHwid.Resolve(options.Hwid);
    }

    internal string Username => _options.Username;

    internal bool HasSession => _token is not null;

    /// <summary>确保已登录；失败返回 false，由调用方决定重试节奏。</summary>
    internal async Task<bool> EnsureLoginAsync(CancellationToken cancellationToken)
    {
        if (_token is not null)
        {
            return true;
        }

        try
        {
            using var response = await _http.PostAsJsonAsync(
                "api/auth/login",
                new { username = _options.Username, password = _options.Password },
                cancellationToken).ConfigureAwait(false);
            using var json = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            if (json.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True
                && json.RootElement.TryGetProperty("sessionToken", out var token) && token.ValueKind == JsonValueKind.String)
            {
                _token = token.GetString();
                if (!string.IsNullOrWhiteSpace(_token))
                {
                    IrcLog.Write($"已登录聊天室（{_options.Username}）。");
                    return true;
                }
            }

            IrcLog.Write($"聊天室登录失败：{ReadMessage(json)}");
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            IrcLog.Write("聊天室登录异常：" + exception.Message);
            return false;
        }
    }

    /// <summary>增量轮询。首次调用（lastId=0）服务端会返回最近 50 条，由上层决定是否展示。</summary>
    internal async Task<IrcPollResult> PollAsync(CancellationToken cancellationToken)
    {
        var token = _token;
        if (token is null)
        {
            return new IrcPollResult(false, "尚未登录聊天室。", [], 0, false, false);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat/poll")
            {
                Content = JsonContent.Create(new { lastId = Interlocked.Read(ref _lastId) })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("X-HWID", _hwid);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                InvalidateLogin();
                return new IrcPollResult(false, "登录已失效。", [], 0, false, true);
            }

            using var json = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            var root = json.RootElement;
            if (!root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
            {
                return new IrcPollResult(false, ReadMessage(json), [], 0, false, false);
            }

            var messages = new List<IrcMessage>();
            if (root.TryGetProperty("messages", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                var maximumId = Interlocked.Read(ref _lastId);
                foreach (var node in array.EnumerateArray())
                {
                    // 缺 id 的消息无法推进游标，直接跳过，避免整批丢失。
                    if (!node.TryGetProperty("id", out var idProperty) || !idProperty.TryGetInt64(out var id))
                    {
                        continue;
                    }

                    var sender = node.TryGetProperty("sender", out var senderProperty) ? senderProperty.GetString() ?? string.Empty : string.Empty;
                    var text = node.TryGetProperty("text", out var textProperty) ? textProperty.GetString() ?? string.Empty : string.Empty;
                    var isIrc = node.TryGetProperty("isIrc", out var ircProperty) && ircProperty.ValueKind == JsonValueKind.True;
                    var time = node.TryGetProperty("time", out var timeProperty) && timeProperty.TryGetInt64(out var timeValue) ? timeValue : 0;
                    messages.Add(new IrcMessage(id, sender, text, isIrc, time));
                    if (id > maximumId)
                    {
                        maximumId = id;
                    }
                }

                Interlocked.Exchange(ref _lastId, maximumId);
            }

            var online = root.TryGetProperty("online", out var onlineProperty) && onlineProperty.TryGetInt32(out var onlineValue)
                ? onlineValue
                : 0;
            var disabled = root.TryGetProperty("disabled", out var disabledProperty) && disabledProperty.ValueKind == JsonValueKind.True;
            return new IrcPollResult(true, string.Empty, messages, online, disabled, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            IrcLog.Write("聊天室轮询异常：" + exception.Message);
            return new IrcPollResult(false, exception.Message, [], 0, false, false);
        }
    }

    /// <summary>发送一条消息。文本会加上 /IRC 前缀，由服务端标记为 IRC 消息并清理前缀后入库。</summary>
    internal async Task<IrcSendResult> SendAsync(string text, CancellationToken cancellationToken)
    {
        if (!await EnsureLoginAsync(cancellationToken).ConfigureAwait(false))
        {
            return new IrcSendResult(false, "尚未登录聊天室。");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat/send")
            {
                Content = JsonContent.Create(new { text = "/IRC " + text, hwid = _hwid })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Headers.TryAddWithoutValidation("X-HWID", _hwid);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            using var json = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            if (json.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True)
            {
                return new IrcSendResult(true, string.Empty);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                InvalidateLogin();
            }

            return new IrcSendResult(false, ReadMessage(json));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            IrcLog.Write("发送到聊天室异常：" + exception.Message);
            return new IrcSendResult(false, exception.Message);
        }
    }

    internal void InvalidateLogin() => _token = null;

    public void Dispose() => _http.Dispose();

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return JsonDocument.Parse("{}");
        }

        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            // 网关错误页等非 JSON 响应：退化成空对象，让调用方走正常的失败分支。
            return JsonDocument.Parse("{}");
        }
    }

    private static string ReadMessage(JsonDocument json)
        => json.RootElement.TryGetProperty("message", out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? "请求失败。"
            : "请求失败。";
}