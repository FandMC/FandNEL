using FandNEL.Core.Entities.WPFLauncher.NetGame;
using FandNEL.GameLauncher.Downloads;
using FandNEL.GameLauncher.Models;

namespace FandNEL.GameLauncher.Services.Java;

internal sealed class JavaRuntimeInstaller(LauncherPaths paths, HttpClient http)
{
    public async Task<string> PrepareAsync(JavaLaunchRequest request, IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.JavaExecutable))
        {
            var custom = Path.GetFullPath(request.JavaExecutable);
            if (!File.Exists(custom))
                throw new FileNotFoundException("指定的 Java 不存在。", custom);
            return custom;
        }
        if (!PlatformRuntime.SupportsManagedJavaRuntime)
            throw new PlatformNotSupportedException("当前平台无法自动安装 Java 运行时；请指定 Java 路径。");

        var major = request.GameVersion >= EnumGameVersion.V_1_20_6 ? 21 : request.GameVersion >= EnumGameVersion.V_1_16 ? 17 : 8;
        var directory = Path.Combine(paths.Java, major == 8 ? "jre8" : $"jdk{major}");
        var executable = Path.Combine(directory, "bin", PlatformRuntime.JavaExecutableName);
        if (File.Exists(executable))
            return executable;
        var installer = new ArchiveInstaller(http);
        if (OperatingSystem.IsWindows())
        {
            if (major == 21)
            {
                const string url = "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jre/hotspot/normal/eclipse";
                var archive = Path.Combine(paths.Java, "jre21.zip");
                var extracted = Path.Combine(paths.Java, "jre21-download");
                await installer.InstallAsync(url, archive, extracted, null, progress, cancellationToken).ConfigureAwait(false);
                var candidate = Directory.EnumerateFiles(extracted, "javaw.exe", SearchOption.AllDirectories)
                    .FirstOrDefault(path => Path.GetFileName(Path.GetDirectoryName(path)) == "bin")
                    ?? throw new InvalidDataException("Java 21 包不包含 javaw.exe。");
                var extractedRoot = Path.GetDirectoryName(Path.GetDirectoryName(candidate))!;
                CopyRuntimeTree(extractedRoot, directory);
            }
            else
            {
                await installer.InstallAsync("https://x19.gdl.netease.com/jre-v64-220420.7z",
                    Path.Combine(paths.Java, "Jre.7z"), paths.Java, null, progress, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            // Linux/macOS 无法使用网易的 Windows 运行时包，改用 Adoptium 的官方 JRE。
            var os = PlatformRuntime.AdoptiumOs;
            var arch = PlatformRuntime.AdoptiumArch;
            var url = $"https://api.adoptium.net/v3/binary/latest/{major}/ga/{os}/{arch}/jre/hotspot/normal/eclipse";
            var archive = Path.Combine(paths.Java, $"jre{major}-{os}-{arch}.tar.gz");
            var extracted = Path.Combine(paths.Java, $"jre{major}-{os}-{arch}-download");
            // JRE 归档无官方 MD5，已完整下载过就不再重下；截断的包会在 tar 解压时报错提示。
            if (!File.Exists(archive))
                await installer.DownloadAsync(url, archive, null, progress, cancellationToken).ConfigureAwait(false);
            await ArchiveInstaller.ExtractTarGzAsync(archive, extracted, progress, cancellationToken).ConfigureAwait(false);
            var candidate = Directory.EnumerateFiles(extracted, "java", SearchOption.AllDirectories)
                .FirstOrDefault(path => Path.GetFileName(Path.GetDirectoryName(path)) == "bin")
                ?? throw new InvalidDataException($"JRE 包不包含 java 可执行文件（{os}/{arch}）。");
            var extractedRoot = Path.GetDirectoryName(Path.GetDirectoryName(candidate))!;
            CopyRuntimeTree(extractedRoot, directory);
            FixExecutableBits(directory);
        }
        if (!File.Exists(executable))
            throw new FileNotFoundException($"运行时包未包含 Java {major}。", executable);
        return executable;
    }

    private static void CopyRuntimeTree(string extractedRoot, string directory)
    {
        foreach (var file in Directory.EnumerateFiles(extractedRoot, "*", SearchOption.AllDirectories))
        {
            var destination = LauncherPaths.Child(directory, Path.GetRelativePath(extractedRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    /// <summary>7z/tar 工具可能丢失可执行位，统一恢复 JRE bin 与 jspawnhelper 的执行权限。</summary>
    private static void FixExecutableBits(string runtimeRoot)
    {
        if (OperatingSystem.IsWindows())
            return;
        var bin = Path.Combine(runtimeRoot, "bin");
        if (!Directory.Exists(bin))
            return;
        const UnixFileMode executableMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        foreach (var file in Directory.EnumerateFiles(bin, "*", SearchOption.AllDirectories))
        {
            try { File.SetUnixFileMode(file, executableMode); }
            catch (NotSupportedException) { }
        }
        // JDK 17+ 的 jspawnhelper 位于 lib/，JVM 以可执行方式调用它，权限丢失会导致无法启动。
        foreach (var name in new[] { Path.Combine(runtimeRoot, "lib", "jspawnhelper"), Path.Combine(bin, "jspawnhelper") })
        {
            if (File.Exists(name))
                File.SetUnixFileMode(name, executableMode);
        }
    }
}
