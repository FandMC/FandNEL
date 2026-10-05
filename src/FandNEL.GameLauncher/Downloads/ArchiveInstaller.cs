using System.Diagnostics;
using System.Security.Cryptography;
using FandNEL.GameLauncher.Models;
using SharpSevenZip;
using SharpSevenZip.Exceptions;

namespace FandNEL.GameLauncher.Downloads;

/// <summary>安装网易的 7z/zip 包；只有下载、校验和解压全部成功后才写入版本标记。</summary>
internal sealed class ArchiveInstaller(HttpClient http)
{
    public async Task InstallAsync(string url, string archivePath, string destination, string? md5,
        IProgress<LaunchProgress>? progress, CancellationToken cancellationToken, Task? precedingExtraction = null)
    {
        var marker = archivePath + ".installed";
        if (!string.IsNullOrEmpty(md5) && Directory.Exists(destination) && File.Exists(marker) &&
            await File.ReadAllTextAsync(marker, cancellationToken).ConfigureAwait(false) == md5)
        {
            if (precedingExtraction is not null)
                await precedingExtraction.ConfigureAwait(false);
            return;
        }

        await DownloadAsync(url, archivePath, md5, progress, cancellationToken).ConfigureAwait(false);
        // 下载可与前一个包的解压重叠，写入共享目录时仍保持原来的安装顺序。
        if (precedingExtraction is not null)
            await precedingExtraction.ConfigureAwait(false);
        await ExtractAsync(archivePath, destination, progress, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(md5))
            await File.WriteAllTextAsync(marker, md5, cancellationToken).ConfigureAwait(false);
    }

    public async Task DownloadAsync(string url, string destination, string? md5,
        IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        var uri = new Uri(url, UriKind.Absolute);
        if (uri.Scheme is not ("http" or "https"))
            throw new InvalidDataException("资源下载地址必须使用 HTTP 或 HTTPS。");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination) && !string.IsNullOrEmpty(md5) && await MatchesAsync(destination, md5, cancellationToken).ConfigureAwait(false))
            return;

        var temporary = destination + $".{Guid.NewGuid():N}.part";
        try
        {
            await FastHttpDownloader.DownloadAsync(
                http,
                uri,
                temporary,
                (completed, total) => progress?.Report(new LaunchProgress(
                    LaunchStage.Downloading,
                    $"正在下载 {Path.GetFileName(destination)}",
                    completed,
                    total)),
                cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(md5) && !await MatchesAsync(temporary, md5, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException($"资源 MD5 校验失败：{Path.GetFileName(destination)}");
            ReplaceDownloadedFile(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static void ReplaceDownloadedFile(string temporary, string destination)
    {
        if (File.Exists(destination))
        {
            // 旧版本的下载产物可能被解压工具标记为只读；清除属性后再替换。
            File.SetAttributes(destination, FileAttributes.Normal);
            try
            {
                File.Move(temporary, destination, overwrite: true);
                return;
            }
            catch (IOException)
            {
                // 某些 Windows 文件系统对 overwrite 的行为取决于目标文件属性，
                // 删除后重命名可以保持替换语义，同时仍会把真正的文件锁错误抛出。
            }
        }

        if (File.Exists(destination)) File.Delete(destination);
        File.Move(temporary, destination);
    }

    public static Task ExtractAsync(string archivePath, string destination, IProgress<LaunchProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("原生资源解压仅支持 Windows。");

            try
            {
                using var input = new CancellableArchiveStream(archivePath, cancellationToken);
                using var archive = new SharpSevenZipExtractor(input, leaveOpen: true)
                {
                    EventSynchronization = EventSynchronizationStrategy.AlwaysSynchronous
                };
                var checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var rootEntries = new HashSet<int>();
                foreach (var entry in archive.ArchiveFileData)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ValidateExtractionPath(destination, entry.FileName, entry.IsDirectory, checkedPaths))
                        rootEntries.Add(entry.Index);
                }

                Directory.CreateDirectory(destination);
                var archiveName = Path.GetFileName(archivePath);
                var entryName = archiveName;
                var progressClock = Stopwatch.StartNew();
                progress?.Report(new LaunchProgress(LaunchStage.Preparing, $"正在解压 {archiveName}", 0, 100));
                archive.FileExtractionStarted += (_, args) =>
                {
                    args.Cancel = cancellationToken.IsCancellationRequested;
                    entryName = args.FileInfo.FileName;
                };
                archive.Extracting += (_, args) =>
                {
                    if (progress is null || progressClock.ElapsedMilliseconds < 150)
                        return;
                    progressClock.Restart();
                    progress.Report(new LaunchProgress(LaunchStage.Preparing, $"正在解压 {entryName}",
                        args.PercentDone, 100, entryName));
                };
                // 整包交给原生 7z 解码，不能逐文件提取 solid 包，否则会重复解码同一压缩块。
                if (rootEntries.Count == 0)
                    archive.ExtractArchive(Path.GetFullPath(destination));
                else
                    archive.ExtractFiles(Path.GetFullPath(destination), archive.ArchiveFileData
                        .Where(entry => !rootEntries.Contains(entry.Index)).Select(entry => entry.Index).ToArray());
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new LaunchProgress(LaunchStage.Preparing, $"已解压 {archiveName}", 100, 100));
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (SharpSevenZipException exception)
            {
                throw new InvalidDataException($"资源解压失败：{Path.GetFileName(archivePath)}。", exception);
            }
        }, cancellationToken);

    private static bool ValidateExtractionPath(string destination, string entryName, bool isDirectory, HashSet<string> checkedPaths)
    {
        if (string.IsNullOrWhiteSpace(entryName) || entryName.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidDataException("压缩包包含无效资源路径。");
        // 部分包用 '.' 表示目标根目录；它不需要再次提取，但仍需检查链接。
        var isRootDirectory = isDirectory && entryName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) == ".";
        var path = isRootDirectory ? Path.GetFullPath(destination) : LauncherPaths.Child(destination, entryName);
        // 原生解压不能沿现有目录链接写出缓存根目录；同一父目录只检查一次。
        while (path is not null && checkedPaths.Add(path))
        {
            if ((File.Exists(path) || Directory.Exists(path)) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"资源解压路径包含目录或文件链接：{entryName}");
            path = Path.GetDirectoryName(path);
        }
        return isRootDirectory;
    }

    /// <summary>让原生解码器在读取下一段压缩数据时响应取消，而非等整包解压结束。</summary>
    private sealed class CancellableArchiveStream(string path, CancellationToken cancellationToken)
        : FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.SequentialScan)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return base.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return base.Read(buffer);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return base.Seek(offset, origin);
        }
    }

    internal static async Task<bool> MatchesAsync(string path, string md5, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(path);
        return Convert.ToHexString(await MD5.HashDataAsync(input, cancellationToken).ConfigureAwait(false))
            .Equals(md5, StringComparison.OrdinalIgnoreCase);
    }
}
