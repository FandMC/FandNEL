using FandNEL.Proxy.Sessions;

namespace FandNEL.Proxy.Servers;

/// <summary>本次迁移的目标服务器游戏 ID；只有这些游戏会启用下方的通用伪造与服务器协议。</summary>
internal static class ServerGameIds
{
    /// <summary>艾尔莉雅（Aely）。</summary>
    internal const string Aely = "4622580075322428635";

    /// <summary>重生长城（Czsczl）。</summary>
    internal const string Czsczl = "4646072985456603598";

    /// <summary>东陆反作弊（DFDL）。</summary>
    internal const string Dfdl = "4619737516439909914";

    /// <summary>OMG。</summary>
    internal const string Omg = "4662907052731024438";

    /// <summary>溯源（SuYuan）。</summary>
    internal const string SuYuan = "4671863192181219211";

    internal static bool IsEnabled(MinecraftConnection connection)
    {
        var gameId = connection.Options.GameId;
        return gameId is Aely or Czsczl or Dfdl or Omg or SuYuan;
    }

    internal static bool IsGame(MinecraftConnection connection, string gameId) =>
        string.Equals(connection.Options.GameId, gameId, StringComparison.Ordinal);
}
