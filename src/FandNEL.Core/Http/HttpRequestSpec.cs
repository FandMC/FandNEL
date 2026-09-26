using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FandNEL.Core.Http;

/// <summary>
/// Immutable description of an HTTP request. The transport owns the network
/// resources, while this type only owns the request bytes.
/// </summary>
public sealed record HttpRequestSpec
{
    public HttpRequestSpec(
        HttpMethod method,
        Uri uri,
        ReadOnlyMemory<byte> body = default,
        string? contentType = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(uri);
        if (body.Length > 0 && string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("A content type is required when a request body is present.", nameof(contentType));
        }

        Method = method;
        Uri = uri;
        Body = body;
        ContentType = contentType;
        Headers = headers is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
    }

    public HttpMethod Method { get; }

    public Uri Uri { get; }

    public ReadOnlyMemory<byte> Body { get; }

    public string? ContentType { get; }

    public IReadOnlyDictionary<string, string> Headers { get; }

    public static HttpRequestSpec Get(Uri uri, IReadOnlyDictionary<string, string>? headers = null) =>
        new(HttpMethod.Get, uri, headers: headers);

    public static HttpRequestSpec PostJson<T>(
        Uri uri,
        T value,
        JsonSerializerOptions? options = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        var body = JsonSerializer.SerializeToUtf8Bytes(value, options);
        return new HttpRequestSpec(HttpMethod.Post, uri, body, "application/json; charset=utf-8", headers);
    }

    public static HttpRequestSpec PostForm(
        Uri uri,
        IEnumerable<KeyValuePair<string, string?>> values,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        var builder = new QueryParameters();
        foreach (var pair in values)
        {
            builder.Set(pair.Key, pair.Value);
        }

        var body = Encoding.UTF8.GetBytes(builder.ToFormEncoded());
        return new HttpRequestSpec(HttpMethod.Post, uri, body, "application/x-www-form-urlencoded", headers);
    }

    internal HttpRequestMessage ToMessage()
    {
        var message = new HttpRequestMessage(Method, Uri);
        foreach (var header in Headers)
        {
            if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                throw new FormatException($"Invalid HTTP header: {header.Key}");
            }
        }

        if (Body.Length > 0)
        {
            var content = new ByteArrayContent(Body.ToArray());
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(ContentType!);
            message.Content = content;
        }

        return message;
    }
}
