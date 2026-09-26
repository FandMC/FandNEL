using FandNEL.Core.Security;

namespace FandNEL.Core.Authentication;

public sealed record ChannelLoginRequest
{
    public required string Channel { get; init; }

    public required string Account { get; init; }

    /// <summary>
    /// The plain value exists only at the provider boundary. Providers should copy it
    /// into <see cref="SecureBytes"/> immediately and clear any temporary buffers.
    /// </summary>
    public required string Password { get; init; }

    public string? DeviceId { get; init; }
}

public sealed record AuthenticationResult
{
    public required string UserId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public required string Channel { get; init; }

    public string LoginType { get; init; } = "password";

    public required string SessionDetails { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

public abstract record LoginChallenge
{
    public required string Channel { get; init; }

    public string Prompt { get; init; } = string.Empty;
}

public sealed record CaptchaChallenge : LoginChallenge
{
    public required ReadOnlyMemory<byte> Image { get; init; }

    public string? CaptchaId { get; init; }
}

public sealed record BrowserVerificationChallenge : LoginChallenge
{
    public required Uri VerificationUri { get; init; }

    public bool CanOpenEmbedded { get; init; } = true;
}

public interface ILoginChallengeHandler
{
    ValueTask<string?> ResolveAsync(
        LoginChallenge challenge,
        CancellationToken cancellationToken = default);
}

public interface IChannelLoginProvider
{
    string Channel { get; }

    ValueTask<AuthenticationResult> AuthenticateAsync(
        ChannelLoginRequest request,
        ILoginChallengeHandler challengeHandler,
        CancellationToken cancellationToken = default);
}

public interface IChannelLoginRegistry
{
    IReadOnlyCollection<string> Channels { get; }

    bool TryGet(string channel, out IChannelLoginProvider? provider);
}

public sealed class ChannelLoginRegistry : IChannelLoginRegistry
{
    private readonly IReadOnlyDictionary<string, IChannelLoginProvider> _providers;

    public ChannelLoginRegistry(IEnumerable<IChannelLoginProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToDictionary(item => item.Channel, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Channels => _providers.Keys.ToArray();

    public bool TryGet(string channel, out IChannelLoginProvider? provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        if (_providers.TryGetValue(channel, out var match))
        {
            provider = match;
            return true;
        }

        provider = null;
        return false;
    }
}
