namespace FandNEL.Gateway;

public enum GatewayPlatform
{
    Desktop = 0,
    Mobile = 1,
    Mixed = 2
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

