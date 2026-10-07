namespace FandNEL.GameLauncher.Models;

/// <summary>启动器运行平台信息，统一 Windows/Linux/macOS 的分支判断。</summary>
public static class PlatformRuntime
{
    /// <summary>Minecraft 版本规则使用的 OS 名称：windows / linux / osx。</summary>
    public static string OsRuleName =>
        OperatingSystem.IsWindows() ? "windows" :
        OperatingSystem.IsMacOS() ? "osx" : "linux";

    /// <summary>Adoptium 下载 API 的 OS 标识。</summary>
    public static string AdoptiumOs => OperatingSystem.IsMacOS() ? "mac" : OperatingSystem.IsWindows() ? "windows" : "linux";

    /// <summary>Adoptium 下载 API 的架构标识。</summary>
    public static string AdoptiumArch => Environment.Is64BitProcess
        ? (System.Runtime.InteropServices.Architecture.Arm64 == System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ? "aarch64" : "x64")
        : "x86";

    /// <summary>Java 可执行文件名：Windows 用 javaw.exe 避免控制台窗口，其他平台只有 java。</summary>
    public static string JavaExecutableName => OperatingSystem.IsWindows() ? "javaw.exe" : "java";

    /// <summary>当前平台是否由本启动器自动安装 Java 运行时。</summary>
    public static bool SupportsManagedJavaRuntime =>
        OperatingSystem.IsWindows() ? Environment.Is64BitOperatingSystem
        : System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
            is System.Runtime.InteropServices.Architecture.X64 or System.Runtime.InteropServices.Architecture.Arm64;
}
