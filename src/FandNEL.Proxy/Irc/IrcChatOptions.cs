using System.Text.Json;
using Serilog;
using System.Text.Json.Serialization;

namespace FandNEL.Proxy.Irc;

/// <summary>
/// IRC 聊天室桥接配置。首次运行会自动在 %LOCALAPPDATA%/FandNEL/irc.json 写入一份默认配置：
/// 服务地址已填好，账号留空——填上账号（或改用 <see cref="Enabled"/> 关闭）重启 FandNEL 生效。
/// 账号不写入源码/仓库，避免公开仓库携带凭据。
/// </summary>
public sealed record IrcChatOptions
{
    /// <summary>是否启用游戏内 IRC。关闭后代理行为与未接入时完全一致。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>聊天室服务地址。</summary>
    public string BaseUrl { get; init; } = "https://neoeastside.636.ltd";

    /// <summary>聊天室账号（脱盒用户名，游戏内显示的名字就是它）。留空则不启用。</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>聊天室账号密码。留空则不启用。</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>是否每 6 秒在游戏内提示聊天室在线人数。</summary>
    public bool ShowOnlineHint { get; init; } = true;

    /// <summary>是否每 15 秒在游戏内提示「/IRC 内容」用法。</summary>
    public bool ShowUsageHint { get; init; } = true;

    /// <summary>聊天室轮询间隔（毫秒）。</summary>
    public int PollIntervalMilliseconds { get; init; } = 1200;

    /// <summary>配置是否可用（启用且账号信息完整）。</summary>
    [JsonIgnore]
    public bool IsUsable => Enabled
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Password);

    /// <summary>实际轮询间隔（限制在 0.5~10 秒之间）。</summary>
    [JsonIgnore]
    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(Math.Clamp(PollIntervalMilliseconds, 500, 10000));

    public static string ConfigPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FandNEL",
        "irc.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>读取配置；文件不存在时写入默认配置，方便用户直接找到并修改。</summary>
    public static IrcChatOptions Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var defaults = new IrcChatOptions();
                Save(defaults);
                Log.Information("IRC: default config written to {Path}", ConfigPath);
                return defaults;
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<IrcChatOptions>(json, SerializerOptions) ?? new IrcChatOptions();
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "IRC: failed to read config, falling back to defaults");
            return new IrcChatOptions();
        }
    }

    public static void Save(IrcChatOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(options, SerializerOptions));
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "IRC: failed to write config");
        }
    }
}