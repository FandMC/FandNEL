namespace FandNEL.Proxy.Heypixel;

/// <summary>Heypixel 目标服与运行参数；线上包字段由 Packet/Heypixel 管理。</summary>
internal static class HeypixelConstants
{
    internal const string GameId = "4661334467366178884";
    internal const string ConsentFormTitle = "游戏信息收集与反作弊告知书";
    internal static readonly TimeSpan CpsPollInterval = TimeSpan.FromMilliseconds(50);
    internal static readonly TimeSpan CpsSendInterval = TimeSpan.FromMilliseconds(250);
    internal static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan SentinelRefreshInterval = TimeSpan.FromSeconds(30);
}
