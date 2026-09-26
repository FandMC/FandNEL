using System.Net.Http.Headers;
using System.Text.Json;
using FandNEL.Core.Diagnostics;

namespace FandNEL.Core.Http;

/// <summary>
/// Small HttpClient adapter used by channel providers. It centralizes headers,
/// response buffering and optional redacted tracing without imposing a logging package.
/// </summary>
public sealed class HttpClientTransport : IHttpTransport
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly ITraceSink? _traceSink;
    private readonly int _traceBodyLimit;
    private int _disposed;

    public HttpClientTransport(
        HttpClient? client = null,
        ITraceSink? traceSink = null,
        int traceBodyLimit = 64 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(traceBodyLimit);

        _client = client ?? new HttpClient();
        _ownsClient = client is null;
        _traceSink = traceSink;
        _traceBodyLimit = traceBodyLimit;
    }

    public async ValueTask<HttpResponseData> SendAsync(
        HttpRequestSpec request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        ArgumentNullException.ThrowIfNull(request);
        using var message = request.ToMessage();
        if (_traceSink is not null)
        {
            await TraceRequestAsync(request, cancellationToken).ConfigureAwait(false);
        }

        using var response = await _client.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var headers = response.Headers
            .Concat(response.Content.Headers)
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.SelectMany(pair => pair.Value).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var result = new HttpResponseData(
            response.StatusCode,
            response.RequestMessage?.RequestUri ?? request.Uri,
            body,
            headers);
        if (_traceSink is not null)
        {
            await TraceResponseAsync(result, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _ownsClient)
        {
            _client.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask TraceRequestAsync(HttpRequestSpec request, CancellationToken cancellationToken)
    {
        var body = request.Body.Length > _traceBodyLimit
            ? request.Body[.._traceBodyLimit]
            : request.Body;
        var record = new
        {
            direction = "request",
            method = request.Method.Method,
            url = request.Uri.ToString(),
            headers = HttpTraceSanitizer.SanitizeHeaders(request.Headers),
            body = HttpTraceSanitizer.SanitizeBody(System.Text.Encoding.UTF8.GetString(body.Span))
        };
        return _traceSink!.WriteAsync(JsonSerializer.Serialize(record), cancellationToken);
    }

    private ValueTask TraceResponseAsync(HttpResponseData response, CancellationToken cancellationToken)
    {
        var body = response.Body.Length > _traceBodyLimit
            ? response.Body[.._traceBodyLimit]
            : response.Body;
        var record = new
        {
            direction = "response",
            status = (int)response.StatusCode,
            url = response.RequestUri.ToString(),
            headers = HttpTraceSanitizer.SanitizeHeaders(response.Headers.SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string>(pair.Key, value)))),
            body = HttpTraceSanitizer.SanitizeBody(System.Text.Encoding.UTF8.GetString(body.Span))
        };
        return _traceSink!.WriteAsync(JsonSerializer.Serialize(record), cancellationToken);
    }
}
