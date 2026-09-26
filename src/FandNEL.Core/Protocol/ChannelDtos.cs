using System.Text.Json.Serialization;

namespace FandNEL.Core.Protocol;

public static class LoginChannels
{
    public const string Netease = "netease";
    public const string Pc4399 = "4399pc";
    public const string Mixin4399 = "4399com";
}

public sealed record ChannelDevice
{
    [JsonPropertyName("device-id")]
    public string Identifier { get; init; } = string.Empty;

    [JsonPropertyName("device-id-sm")]
    public string SecondaryIdentifier { get; init; } = string.Empty;

    [JsonPropertyName("device-udid")]
    public string Udid { get; init; } = string.Empty;

    [JsonPropertyName("device-state")]
    public string? State { get; init; }
}

public sealed record Pc4399LoginResponse
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("msg")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public Pc4399LoginData Data { get; init; } = new();
}

public sealed record Pc4399LoginData
{
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("login_tip")]
    public string LoginTip { get; init; } = string.Empty;

    [JsonPropertyName("sdk_login_data")]
    public string SessionData { get; init; } = string.Empty;
}

public sealed record Mixin4399OAuthResponse
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("result")]
    public Mixin4399OAuthResult Result { get; init; } = new();
}

public sealed record Mixin4399OAuthResult
{
    [JsonPropertyName("login_url")]
    public string LoginUrl { get; init; } = string.Empty;

    [JsonPropertyName("login_url_backup")]
    public string BackupLoginUrl { get; init; } = string.Empty;
}

public sealed record Mixin4399UserResponse
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("result")]
    public Mixin4399User? User { get; init; }
}

public sealed record Mixin4399User
{
    [JsonPropertyName("uid")]
    public long UserId { get; init; }

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("nck")]
    public string Nickname { get; init; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;
}

public sealed record NeteaseSessionData
{
    [JsonPropertyName("sa_data")]
    public string SaData { get; init; } = string.Empty;

    [JsonPropertyName("sauth_json")]
    public string SauthJson { get; init; } = string.Empty;

    [JsonPropertyName("sdkuid")]
    public string SdkUserId { get; init; } = string.Empty;

    [JsonPropertyName("sessionid")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("token")]
    public string? Token { get; init; }

    [JsonPropertyName("otp_token")]
    public string? OtpToken { get; init; }
}
