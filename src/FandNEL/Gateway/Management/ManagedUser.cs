using System.Text.Json.Serialization;
using FandNEL.Gateway;

namespace FandNEL.Gateway.Management;

public sealed class ManagedUser
{
    [JsonPropertyName("id")]
    public required string UserId { get; set; }

    [JsonPropertyName("authorized")]
    public bool Authorized { get; set; }

    [JsonPropertyName("auto_login")]
    public bool AutoLogin { get; set; }

    [JsonPropertyName("channel")]
    public required string Channel { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("details")]
    public required string Details { get; set; }

    [JsonPropertyName("platform")]
    public GatewayPlatform Platform { get; set; }

    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;
}

public sealed class ManagedAvailableUser
{
    [JsonPropertyName("id")]
    public required string UserId { get; set; }

    [JsonPropertyName("token")]
    public required string AccessToken { get; set; }

    [JsonPropertyName("last_login_time")]
    public long LastLoginTime { get; set; }
}
