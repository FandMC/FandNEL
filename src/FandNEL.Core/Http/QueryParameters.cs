namespace FandNEL.Core.Http;

/// <summary>
/// Query/form parameter collection with correct escaping and duplicate-key replacement.
/// </summary>
public sealed class QueryParameters
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

    public QueryParameters()
    {
    }

    public QueryParameters(string queryOrUri)
    {
        ArgumentNullException.ThrowIfNull(queryOrUri);
        var query = queryOrUri;
        var questionMark = query.IndexOf('?');
        if (questionMark >= 0)
        {
            query = query[(questionMark + 1)..];
        }

        if (query.StartsWith('?'))
        {
            query = query[1..];
        }

        foreach (var item in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = item.IndexOf('=');
            var key = separator < 0 ? item : item[..separator];
            var value = separator < 0 ? null : item[(separator + 1)..];
            Set(Uri.UnescapeDataString(key.Replace('+', ' ')), value is null ? null : Uri.UnescapeDataString(value.Replace('+', ' ')));
        }
    }

    public IEnumerable<KeyValuePair<string, string?>> Values => _values;

    public QueryParameters Set(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _values[key] = value;
        return this;
    }

    public QueryParameters Remove(string key)
    {
        _values.Remove(key);
        return this;
    }

    public bool TryGet(string key, out string? value) => _values.TryGetValue(key, out value);

    public string ToFormEncoded() => string.Join(
        '&',
        _values.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value ?? string.Empty)}"));

    public string ToQueryString() => _values.Count == 0 ? string.Empty : $"?{ToFormEncoded()}";

    public Uri Apply(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var builder = new UriBuilder(uri);
        var existing = new QueryParameters(uri.Query);
        foreach (var pair in _values)
        {
            existing.Set(pair.Key, pair.Value);
        }

        builder.Query = existing.ToFormEncoded();
        return builder.Uri;
    }
}
