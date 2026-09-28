using FandNEL.Core.Entities.WPFLauncher.NetGame;
using FandNEL.Core.Entities.WPFLauncher.NetGame.Texture;

namespace FandNEL.GameLauncher.Models;

/// <summary>网易 Java 客户端的启动上下文；账号凭据仅在本次启动中使用。</summary>
public sealed record JavaLaunchRequest
{
    public required string UserId { get; init; }
    public required string UserToken { get; init; }
    public Func<string>? UserTokenProvider { get; init; }
    public required string GameId { get; init; }
    public required string RoleName { get; init; }
    public required EnumGameVersion GameVersion { get; init; }
    public required string ServerHost { get; init; }
    public required int ServerPort { get; init; }
    public EnumGType GameType { get; init; } = EnumGType.NetGame;
    public int MaxMemoryMb { get; init; } = 4096;
    public bool LoadCoreMods { get; init; } = true;
    public string ProtocolVersion { get; init; } = string.Empty;
    public string? JavaExecutable { get; init; }

    public string GetUserToken()
    {
        var token = UserTokenProvider is { } provider ? provider() : UserToken;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("未找到对应用户的网易游戏令牌。");
        return token;
    }

    public void Validate()
    {
        if (!uint.TryParse(UserId, out _))
            throw new ArgumentException("用户 ID 必须是无符号整数。", nameof(UserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(UserToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(GameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(RoleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(ServerHost);
        if (!Enum.IsDefined(GameVersion) || GameVersion is EnumGameVersion.NONE or EnumGameVersion.V_CPP or EnumGameVersion.V_X64_CPP or EnumGameVersion.V_RTX)
            throw new ArgumentOutOfRangeException(nameof(GameVersion), "必须选择受支持的 Java 版本。");
        if (ServerPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(ServerPort));
        if (MaxMemoryMb < 512)
            throw new ArgumentOutOfRangeException(nameof(MaxMemoryMb), "游戏内存不能低于 512 MB。");
    }
}
