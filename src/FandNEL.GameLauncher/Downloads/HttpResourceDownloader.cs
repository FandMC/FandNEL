using System.Net.Http;
using System.Security.Cryptography;
using FandNEL.GameLauncher.Models;

namespace FandNEL.GameLauncher.Downloads;

/// <summary>带临时文件和 SHA-256 校验的 HTTP 资源下载器。</summary>
public sealed class HttpResourceDownloader : IResourceDownloader
{
    private readonly HttpClient _httpClient;

    public HttpResourceDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task DownloadAsync(
        IReadOnlyList<DownloadRequest> resources,
        string destinationDirectory,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new ArgumentException("目标目录不能为空。", nameof(destinationDirectory));

        var root = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(root);
        foreach (var resource in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            resource.Validate();
            var targetPath = ResolveSafePath(root, resource.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            if (await IsReusableAsync(targetPath, resource, cancellationToken).ConfigureAwait(false))
            {
                progress?.Report(new LaunchProgress(
                    LaunchStage.Downloading,
                    $"已复用缓存资源 {resource.RelativePath}。",
                    new FileInfo(targetPath).Length,
                    resource.ExpectedLength,
                    resource.RelativePath));
                continue;
            }

            var temporaryPath = targetPath + $".fandnel-{Guid.NewGuid():N}.part";
            try
            {
                using var response = await _httpClient.GetAsync(
                    resource.Source,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (var output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                }

                var actualLength = new FileInfo(temporaryPath).Length;
                if (resource.ExpectedLength is { } expectedLength && actualLength != expectedLength)
                    throw new InvalidDataException($"资源长度不匹配：期望 {expectedLength}，实际 {actualLength}。");
                if (resource.Sha256 is not null)
                {
                    await using var verifyStream = File.OpenRead(temporaryPath);
                    var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(verifyStream, cancellationToken)).ToLowerInvariant();
                    if (!actualHash.Equals(resource.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"资源校验失败：{resource.RelativePath}。");
                }

                File.Move(temporaryPath, targetPath, overwrite: true);
                progress?.Report(new LaunchProgress(
                    LaunchStage.Downloading,
                    $"已下载 {resource.RelativePath}。",
                    actualLength,
                    response.Content.Headers.ContentLength,
                    resource.RelativePath));
            }
            catch
            {
                TryDelete(temporaryPath);
                throw;
            }
        }
    }

    private static string ResolveSafePath(string root, string relativePath)
    {
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("资源路径必须位于游戏目录内。", nameof(relativePath));
        return candidate;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { }
    }

    private static async Task<bool> IsReusableAsync(
        string path,
        DownloadRequest resource,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return false;
        var fileInfo = new FileInfo(path);
        if (resource.ExpectedLength is { } expectedLength && fileInfo.Length != expectedLength)
            return false;
        if (resource.Sha256 is null)
            return resource.ExpectedLength is not null;

        await using var stream = File.OpenRead(path);
        var actualHash = Convert.ToHexString(
            await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        return actualHash.Equals(resource.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
