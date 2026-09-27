using System.Text;

namespace FandNEL.Proxy.Irc;

/// <summary>
/// IRC 桥接的轻量文件日志（%LOCALAPPDATA%/FandNEL/logs/irc.log）。
/// 游戏里没有反应时可先看这个文件确认登录、轮询与发送状态。
/// </summary>
internal static class IrcLog
{
    private const long MaximumBytes = 2 * 1024 * 1024;
    private static readonly object Gate = new();

    internal static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FandNEL",
        "logs",
        "irc.log");

    internal static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 超过 2MB 滚动一次，避免长时间挂机把日志写爆。
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaximumBytes)
                {
                    File.Move(LogPath, LogPath + ".old", overwrite: true);
                }

                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // 日志失败不影响主流程。
        }
    }
}