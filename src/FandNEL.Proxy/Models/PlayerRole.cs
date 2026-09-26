namespace FandNEL.Proxy.Models;

/// <summary>当前代理会话使用的角色信息。</summary>
public sealed record PlayerRole(string Name, string? Id = null)
{
    public static PlayerRole Guest { get; } = new("Guest");
}
