namespace FandNEL.Proxy.Heypixel;

/// <summary>Heypixel 原生协议配置，只在目标游戏的连接上生效。</summary>
public sealed record HeypixelOptions
{
    public bool Enabled { get; init; } = true;
    public Uri DeriveKeyEndpoint { get; init; } = new("https://service.codexus.today/third-party/heypixel/derive-key-v3");
    public IReadOnlyList<string> ModDirectories { get; init; } = [];
    /// <summary>替代旧插件的全局 setHook 标志，限制为当前代理会话。</summary>
    public bool UseLocalSessionKey { get; init; }
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
