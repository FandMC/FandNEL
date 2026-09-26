using System.Text.Json.Serialization;

namespace FandNEL.Gateway;

public enum GatewayPlatform
{
    Desktop = 0,
    Mobile = 1,
    Mixed = 2
}

public sealed record GatewayAccount
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("authorized")]
    public bool Authorized { get; set; }

    [JsonPropertyName("auto_login")]
    public bool AutoLogin { get; set; }

    [JsonPropertyName("channel")]
    public required string Channel { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("details")]
    public required string Details { get; init; }

    [JsonPropertyName("platform")]
    public GatewayPlatform Platform { get; init; } = GatewayPlatform.Desktop;

    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;
}

public sealed record AccountSummary(
    string Id,
    bool Authorized,
    bool AutoLogin,
    string Channel,
    string Type,
    GatewayPlatform Platform,
    string Alias);

public sealed record AccountSession(
    string UserId,
    string Channel,
    string Type,
    string Nickname,
    string Details);

