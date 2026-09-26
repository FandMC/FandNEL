using System.Text.Json;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Protocol;

/// <summary>
/// Common response envelope used by the Gateway and launcher APIs.
/// </summary>
public record ApiResponseBase
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("details")]
    public string Details { get; init; } = string.Empty;

    [JsonIgnore]
    public bool IsSuccess => Code is 0 or 100 or 200;
}

public record ApiResponse<T> : ApiResponseBase
{
    [JsonPropertyName("entity")]
    public T? Data { get; init; }

    public T RequireData()
    {
        if (!IsSuccess)
        {
            throw new ApiResponseException(Code, Message, Details);
        }

        return Data is null ? throw new ApiResponseException(Code, "响应缺少实体数据。", Details) : Data;
    }
}

public sealed record ApiCollectionResponse<T> : ApiResponseBase
{
    [JsonPropertyName("entities")]
    public T[] Items { get; init; } = [];

    [JsonPropertyName("total")]
    [JsonConverter(typeof(FlexibleInt32JsonConverter))]
    public int Total { get; init; }

    public IReadOnlyList<T> RequireItems()
    {
        if (!IsSuccess)
        {
            throw new ApiResponseException(Code, Message, Details);
        }

        return Items;
    }
}

public sealed class ApiResponseException : Exception
{
    public ApiResponseException(int code, string message, string? details = null)
        : base(string.IsNullOrWhiteSpace(details) ? message : $"{message} ({details})")
    {
        Code = code;
        Details = details ?? string.Empty;
    }

    public int Code { get; }

    public string Details { get; }
}

public sealed class FlexibleInt32JsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetInt32(out var value) => value,
            JsonTokenType.String when int.TryParse(reader.GetString(), out var value) => value,
            JsonTokenType.Null => 0,
            _ => throw new JsonException("Expected a number or numeric string.")
        };

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

public sealed class FlexibleStringJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.True => bool.TrueString,
            JsonTokenType.False => bool.FalseString,
            JsonTokenType.Null => string.Empty,
            _ => throw new JsonException("Expected a scalar JSON value.")
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
