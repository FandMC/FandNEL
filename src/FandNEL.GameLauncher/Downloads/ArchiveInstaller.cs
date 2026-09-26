using System.Security.Cryptography;
using FandNEL.GameLauncher.Models;
using SharpCompress.Archives;

namespace FandNEL.GameLauncher.Downloads;

/// <summary>安装网易的 7z/zip 包；只有下载、校验和解压全部成功后才写入版本标记。</summary>
internal sealed class ArchiveInstaller(HttpClient http)
{
    public async Task InstallAsync(string url, string archivePath, string destination, string? md5,
        IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        var marker = archivePath + ".installed";
        if (!string.IsNullOrEmpty(md5) && Directory.Exists(destination) && File.Exists(marker) &&
            await File.ReadAllTextAsync(marker, cancellationToken).ConfigureAwait(false) == md5)
            return;

        await DownloadAsync(url, archivePath, md5, progress, cancellationToken).ConfigureAwait(false);
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
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (var output = File.Create(temporary))
            {
                var buffer = new byte[81920];
                long completed = 0;
                int length;
                while ((length = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
                    completed += length;
                    progress?.Report(new LaunchProgress(LaunchStage.Downloading, $"正在下载 {Path.GetFileName(destination)}", completed, response.Content.Headers.ContentLength));
                }
                if (response.Content.Headers.ContentLength is { } expected && completed != expected)
                    throw new InvalidDataException("资源下载不完整。");
            }
            if (!string.IsNullOrEmpty(md5) && !await MatchesAsync(temporary, md5, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException($"资源 MD5 校验失败：{Path.GetFileName(destination)}");
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public static Task ExtractAsync(string archivePath, string destination, IProgress<LaunchProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            Directory.CreateDirectory(destination);
            using var archive = ArchiveFactory.OpenArchive(archivePath);
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(entry.Key))
                    throw new InvalidDataException("压缩包包含无名称文件。");
                var target = LauncherPaths.Child(destination, entry.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = entry.OpenEntryStream();
                using var output = File.Create(target);
                input.CopyTo(output);
                progress?.Report(new LaunchProgress(LaunchStage.Preparing, $"正在解压 {entry.Key}"));
            }
        }, cancellationToken);

    internal static async Task<bool> MatchesAsync(string path, string md5, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(path);
        return Convert.ToHexString(await MD5.HashDataAsync(input, cancellationToken).ConfigureAwait(false))
            .Equals(md5, StringComparison.OrdinalIgnoreCase);
    }
}
