using FandNEL.Proxy.Protocol;

namespace FandNEL.Proxy.Irc;

/// <summary>各协议版本的 IRC 包 ID 和输入命令解析。</summary>
internal static class IrcProtocol
{
    internal sealed record VersionSpec(
        ProtocolVersion Version, int JoinGameId, int SystemChatId,
        int? ChatMessageId, int? ChatCommandId, int? SignedChatCommandId);

    internal static IReadOnlyList<VersionSpec> Specs { get; } =
    [
        new(ProtocolVersion.V1076, 0x01, 0x02, 0x01, null, null),
        new(ProtocolVersion.V108X, 0x01, 0x02, 0x01, null, null),
        new(ProtocolVersion.V1122, 0x23, 0x0F, 0x02, null, null),
        new(ProtocolVersion.V1165, 0x24, 0x0E, 0x03, null, null),
        new(ProtocolVersion.V1180, 0x26, 0x0F, 0x03, null, null),
        new(ProtocolVersion.V1200, 0x28, 0x64, 0x05, 0x04, null),
        new(ProtocolVersion.V1206, 0x2B, 0x6C, 0x06, 0x04, 0x05),
        new(ProtocolVersion.V1210, 0x2B, 0x6C, 0x06, 0x04, 0x05),
        new(ProtocolVersion.V1218, 0x2B, 0x72, 0x08, 0x06, 0x07),
        new(ProtocolVersion.V12110, 0x30, 0x77, 0x08, 0x06, 0x07)
    ];

    internal static VersionSpec? TryGetSpec(ProtocolVersion version) =>
        Specs.FirstOrDefault(spec => spec.Version == version);

    /// <summary>解析 /IRC 输入：1.18 及更早走聊天包，1.19+ 走命令包。</summary>
    internal static bool TryParseIrcCommand(string? text, bool isCommandPacket, out string content)
    {
        content = string.Empty;
        var keyword = isCommandPacket ? IrcConstants.CommandName : IrcConstants.ChatCommandPrefix;
        if (text is null || !text.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)) return false;
        if (text.Length > keyword.Length && !char.IsWhiteSpace(text[keyword.Length])) return false;
        content = text.Length > keyword.Length ? text[keyword.Length..].Trim() : string.Empty;
        return true;
    }
}
