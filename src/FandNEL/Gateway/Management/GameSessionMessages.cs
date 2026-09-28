using System.Text.Json;
using System.Text.Json.Serialization;
using FandNEL.GameLauncher.Models;
using FandNEL.Proxy.Models;

namespace FandNEL.Gateway.Management;

/// <summary>Games 列表与启动进度共用的控制面字段。</summary>
internal static class GameSessionMessages
{
    public static string Serialize(IEnumerable<ProxySessionSnapshot> proxies, IEnumerable<LauncherTaskSnapshot> launches) =>
        JsonSerializer.Serialize(proxies.Select(FromProxy).Concat(launches.Select(FromLauncher)));

    public static string SerializeProgress(LauncherTaskSnapshot task)
    {
        var entry = FromLauncher(task);
        return JsonSerializer.Serialize(new ProgressMessage(entry.Id, entry.StatusText, entry.ProgressValue));
    }

    private static SessionMessage FromProxy(ProxySessionSnapshot session) => new(
        $"interceptor-{session.Id}",
        // name 用于配置页导航，必须是会话 GUID；游戏 ID 由配置响应的 game_id 提供。
        session.Id.ToString(), session.Id.ToString(), session.Target.Host, session.Role.Name,
        session.GameVersion ?? string.Empty,
        session.State switch
        {
            ProxySessionState.Running => "Running",
            ProxySessionState.Faulted => "Failed",
            ProxySessionState.Stopping => "Stopping",
            _ => "Stopped"
        },
        "Interceptor", "Java", !string.IsNullOrWhiteSpace(session.RentalServerId),
        session.LocalEndpoint?.ToString() ?? string.Empty, session.State == ProxySessionState.Running ? 100 : 0);

    private static SessionMessage FromLauncher(LauncherTaskSnapshot task) => new(
        $"game-{task.Id}", task.Id.ToString(), task.Id.ToString(),
        string.IsNullOrWhiteSpace(task.GameName) ? task.GameId ?? string.Empty : task.GameName,
        task.RoleName ?? string.Empty, task.GameVersion ?? string.Empty,
        task.Stage == LaunchStage.Running ? "Running" : task.Stage == LaunchStage.Failed ? $"启动失败：{task.Message}" : task.Message,
        task.Stage == LaunchStage.Running ? "Game" : "ModDownload", "Java", task.IsRental, string.Empty,
        task.ProgressPercent ?? 0);

    private sealed record SessionMessage(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("guid")] string Guid,
        [property: JsonPropertyName("server_name")] string ServerName,
        [property: JsonPropertyName("character_name")] string CharacterName,
        [property: JsonPropertyName("server_version")] string ServerVersion,
        [property: JsonPropertyName("status_text")] string StatusText,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("game_type")] string GameType,
        [property: JsonPropertyName("is_rental")] bool IsRental,
        [property: JsonPropertyName("local_address")] string LocalAddress,
        [property: JsonPropertyName("progress_value")] int ProgressValue);

    private sealed record ProgressMessage(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("percent")] int Percent);
}
