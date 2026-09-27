using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace FandNEL.Proxy.Irc;

/// <summary>
/// IRC 发言用的设备标识（HWID）。
/// 与 NeoEastSide 客户端同一算法（注册表 MachineGuid 的 SHA256 前 16 字节大写十六进制），
/// 同一台机器上已用 NeoEastSide 激活过的卡密可以直接沿用，无需重新激活。
/// </summary>
internal static class IrcHwid
{
    /// <summary>配置里写了就用配置，否则按本机生成。</summary>
    internal static string Resolve(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        var machineGuid = ReadMachineGuid();
        if (string.IsNullOrWhiteSpace(machineGuid))
        {
            // 读不到注册表时退化为机器名，保证同一台机器稳定即可。
            machineGuid = Environment.MachineName;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(machineGuid));
        return Convert.ToHexString(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// 读取 MachineGuid。FandNEL.Proxy 的目标框架是 net9.0（跨平台），
    /// 不引用 Windows 专用程序集，这里用反射访问注册表；失败时返回空串。
    /// </summary>
    private static string ReadMachineGuid()
    {
        try
        {
            var registryType = Type.GetType("Microsoft.Win32.Registry, Microsoft.Win32.Registry");
            var localMachine = registryType?
                .GetField("LocalMachine", BindingFlags.Public | BindingFlags.Static)?
                .GetValue(null);
            var openSubKey = localMachine?.GetType().GetMethod("OpenSubKey", new[] { typeof(string) });
            var key = openSubKey?.Invoke(localMachine, new object?[] { @"SOFTWARE\Microsoft\Cryptography" });
            if (key is null)
            {
                return string.Empty;
            }

            try
            {
                var getValue = key.GetType().GetMethod("GetValue", new[] { typeof(string) });
                return getValue?.Invoke(key, new object?[] { "MachineGuid" })?.ToString() ?? string.Empty;
            }
            finally
            {
                (key as IDisposable)?.Dispose();
            }
        }
        catch
        {
            return string.Empty;
        }
    }
}