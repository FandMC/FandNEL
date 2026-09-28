namespace FandNEL.Proxy.Heypixel;

public sealed record HeypixelClientIdentity(
    string UserId,
    string UserToken,
    IReadOnlyList<string> ModDirectories)
{
    public override string ToString() => nameof(HeypixelClientIdentity);
}
