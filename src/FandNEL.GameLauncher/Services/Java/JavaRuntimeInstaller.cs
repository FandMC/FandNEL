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
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitOperatingSystem)
            throw new PlatformNotSupportedException("网易自动安装的 Java 运行时仅支持 Windows x64；其他平台请指定 Java 路径。");

        var major = request.GameVersion >= EnumGameVersion.V_1_20_6 ? 21 : request.GameVersion >= EnumGameVersion.V_1_16 ? 17 : 8;
        var directory = Path.Combine(paths.Java, major == 8 ? "jre8" : $"jdk{major}");
        var executable = Path.Combine(directory, "bin", "javaw.exe");
        if (File.Exists(executable))
            return executable;
        var installer = new ArchiveInstaller(http);
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
            foreach (var file in Directory.EnumerateFiles(extractedRoot, "*", SearchOption.AllDirectories))
            {
                var destination = LauncherPaths.Child(directory, Path.GetRelativePath(extractedRoot, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: true);
            }
        }
        else
        {
            await installer.InstallAsync("https://x19.gdl.netease.com/jre-v64-220420.7z",
                Path.Combine(paths.Java, "Jre.7z"), paths.Java, null, progress, cancellationToken).ConfigureAwait(false);
        }
        if (!File.Exists(executable))
            throw new FileNotFoundException($"运行时包未包含 Java {major}。", executable);
        return executable;
    }
}
