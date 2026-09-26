using System.Net;
using System.Text;

namespace FandNEL.Core.Http;

/// <summary>
/// Detached HTTP response data. The underlying HttpResponseMessage is disposed
/// by the transport before this object is returned.
/// </summary>
public sealed class HttpResponseData
{
    public HttpResponseData(
        HttpStatusCode statusCode,
        Uri requestUri,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null)
    {
        StatusCode = statusCode;
        RequestUri = requestUri;
        Body = body;
        Headers = headers ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    }

    public HttpStatusCode StatusCode { get; }

    public Uri RequestUri { get; }

    public ReadOnlyMemory<byte> Body { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

    public bool IsSuccessStatusCode => (int)StatusCode is >= 200 and <= 299;

    public string TextBody => Encoding.UTF8.GetString(Body.Span);

    public void EnsureSuccessStatusCode()
    {
        if (!IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"HTTP {(int)StatusCode} ({StatusCode}) returned by {RequestUri}.",
                inner: null,
                StatusCode);
        }
    }

    public bool TryGetHeader(string name, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (Headers.TryGetValue(name, out var values) && values.Count > 0)
        {
            value = values[0];
            return true;
        }

        value = null;
        return false;
    }
}
