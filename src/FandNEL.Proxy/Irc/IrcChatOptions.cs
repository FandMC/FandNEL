using System.Text.Json;
using System.Text.Json.Serialization;

namespace FandNEL.Proxy.Irc;

/// <summary>IRC 聊天室桥接配置：默认值集中在 IrcConstants，不读写配置文件。</summary>
public sealed record IrcChatOptions
{
    public bool Enabled { get; init; } = true;
    public string BaseUrl { get; init; } = IrcConstants.DefaultBaseUrl;
    /// <summary>是否定期提示聊天室在线人数。</summary>
    public bool ShowOnlineHint { get; init; } = true;
    /// <summary>是否定期提示「/IRC 内容」用法。</summary>
    public bool ShowUsageHint { get; init; } = true;
    public int PollIntervalMilliseconds { get; init; } = IrcConstants.DefaultPollIntervalMilliseconds;

    [JsonIgnore]
    public bool IsUsable => Enabled && !string.IsNullOrWhiteSpace(BaseUrl);

    [JsonIgnore]
    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(Math.Clamp(
        PollIntervalMilliseconds,
        IrcConstants.MinimumPollIntervalMilliseconds,
        IrcConstants.MaximumPollIntervalMilliseconds));

    /// <summary>聊天室接口 JSON 约定（camelCase、大小写不敏感）。</summary>
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
