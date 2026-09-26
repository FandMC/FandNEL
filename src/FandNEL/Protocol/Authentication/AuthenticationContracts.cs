using FandNEL.Gateway;

namespace FandNEL.Protocol.Authentication;

public sealed record LoginRequest(
    string Channel,
    string Account,
    string Password,
    GatewayPlatform Platform = GatewayPlatform.Desktop);

public sealed record AuthenticationResult(
    string UserId,
    string Nickname,
    string Type,
    string Details);

public interface ILoginChallengeHandler
{
    Task<string?> RequestCaptchaAsync(string channel, ReadOnlyMemory<byte> image, CancellationToken cancellationToken);
    Task<bool> RequestVerificationAsync(string channel, Uri verificationUri, CancellationToken cancellationToken);
}

public interface IChannelAuthenticator
{
    string Channel { get; }

    Task<AuthenticationResult> AuthenticateAsync(
        LoginRequest request,
        ILoginChallengeHandler challengeHandler,
        CancellationToken cancellationToken);
}

public sealed class UnsupportedChannelAuthenticator(string channel) : IChannelAuthenticator
{
    public string Channel { get; } = channel;

    public Task<AuthenticationResult> AuthenticateAsync(
        LoginRequest request,
        ILoginChallengeHandler challengeHandler,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException($"渠道 {Channel} 的认证器尚未注册");
}

