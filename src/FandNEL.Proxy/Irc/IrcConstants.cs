using System.Text.RegularExpressions;

namespace FandNEL.Proxy.Irc;

/// <summary>IRC 桥接的默认配置、接口路径和运行参数，避免在各职责中散落协议常量。</summary>
internal static partial class IrcConstants
{
    internal const string DefaultBaseUrl = "https://api.codexus.today/api/irc";

    internal const string PollPath = "api/chat/poll";
    internal const string SendPath = "api/chat/send";
    internal const string GameIdHeader = "X-Game-ID";
    internal const string ClientIdHeader = "X-Client-ID";
    internal const string OutboundMessagePrefix = "/IRC ";
    internal const string CommandName = "irc";
    internal const string ChatCommandPrefix = "/irc";
    internal const string TextComponentField = "text";

    internal const int DefaultPollIntervalMilliseconds = 1200;
    internal const int MinimumPollIntervalMilliseconds = 500;
    internal const int MaximumPollIntervalMilliseconds = 10000;
    internal const int ChatMessageMaximumLength = 256;
    internal const int MaximumRecentEchoes = 300;
    internal const int ChatMessagePosition = 1;
    internal const int ChatMessageUuidByteLength = 16;

    internal static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan WelcomeDelay = TimeSpan.FromSeconds(2);
    internal static readonly TimeSpan OnlineHintInterval = TimeSpan.FromSeconds(6);
    internal static readonly TimeSpan UsageHintInterval = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan NoConnectionsDelay = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan PollFailureDelay = TimeSpan.FromSeconds(3);
    internal static readonly TimeSpan UnexpectedPollFailureDelay = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan RecentEchoLifetime = TimeSpan.FromSeconds(60);

    internal const string UsageHint = "输入 /IRC 内容 即可发送消息到聊天室";
    internal const string EmptyCommandHint = "用法：/IRC 内容（例：/IRC 大家好）";
    internal const string WelcomeMessagePrefix = "§b[§cES§b]§f 已接入聊天室，";
    internal const string SendFailurePrefix = "§c发送失败：";
    internal const string OnlineHintFormat = "聊天室在线人数：{0}";
    internal const string DisplayFormat = "§b[§a{0}§b]§f {1}";

    [GeneratedRegex("§.", RegexOptions.Compiled)]
    internal static partial Regex ColorCode();
}
