using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using FandNEL.Core.Entities.WPFLauncher.NetGame;
using FandNEL.GameLauncher.Models;
using FandNEL.GameLauncher.Services.Java;
using Serilog;

namespace FandNEL.GameLauncher.Services.Repair;

/// <summary>
/// 借助 BMCLAPI 镜像修复非 Windows 平台缺失的 vanilla 资源：网易客户端包按 Windows 分发，
/// Linux/macOS 需要补齐当前平台适用的 libraries、natives 本机库和 assets 资源对象。
/// 库或本机库修复失败会中止启动；assets 对象失败仅记录，不阻塞游戏。
/// </summary>
internal sealed class VanillaResourceRepairService(HttpClient http, LauncherPaths paths)
{
    private const string ManifestUrl = "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json";
    private const int AssetParallelism = 8;
    // BMCLAPI 要求请求携带可识别的 User-Agent（项目名 + 联系方式），空 UA 会被 403 拒绝。
    private const string UserAgent = "FandNEL/1.0 (+https://github.com/FandMC/FandNEL)";

    private static readonly (string From, string To)[] Rewrites =
    [
        ("https://launchermeta.mojang.com/", "https://bmclapi2.bangbang93.com/"),
        ("https://launcher.mojang.com/", "https://bmclapi2.bangbang93.com/"),
        ("https://libraries.minecraft.net/", "https://bmclapi2.bangbang93.com/maven/"),
        ("https://resources.download.minecraft.net/", "https://bmclapi2.bangbang93.com/assets/"),
        ("http://launchermeta.mojang.com/", "https://bmclapi2.bangbang93.com/"),
        ("http://launcher.mojang.com/", "https://bmclapi2.bangbang93.com/"),
        ("http://libraries.minecraft.net/", "https://bmclapi2.bangbang93.com/maven/"),
        ("http://resources.download.minecraft.net/", "https://bmclapi2.bangbang93.com/assets/")
    ];

    public async Task RepairAsync(EnumGameVersion gameVersion, IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        var versionName = MinecraftInstaller.VersionName(gameVersion);
        cancellationToken.ThrowIfCancellationRequested();
        using var versionJson = await LoadVanillaVersionAsync(versionName, cancellationToken).ConfigureAwait(false);
        var libraryFailures = await RepairLibrariesAsync(versionJson.RootElement, versionName, progress, cancellationToken).ConfigureAwait(false);
        await RepairAssetsAsync(versionJson.RootElement, versionName, progress, cancellationToken).ConfigureAwait(false);
        if (libraryFailures.Count > 0)
            throw new InvalidOperationException(
                $"BMCLAPI 资源修复未完成，缺少 {libraryFailures.Count} 个本平台必需的库：{string.Join("、", libraryFailures.Take(5))}…");
    }

    private async Task<JsonDocument> LoadVanillaVersionAsync(string versionName, CancellationToken cancellationToken)
    {
        using var manifest = await GetJsonDocumentAsync(ManifestUrl, cancellationToken).ConfigureAwait(false);
        JsonElement? entry = null;
        if (manifest.RootElement.TryGetProperty("versions", out var versions))
        {
            foreach (var version in versions.EnumerateArray())
            {
                if (version.TryGetProperty("id", out var id) && id.GetString() == versionName)
                {
                    entry = version.Clone();
                    break;
                }
            }
        }
        if (entry is null)
            throw new InvalidDataException($"BMCLAPI 版本清单中没有 {versionName}，无法执行资源修复。");
        var url = entry.Value.TryGetProperty("url", out var versionUrl) ? versionUrl.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidDataException($"BMCLAPI 版本清单缺少 {versionName} 的版本 JSON 地址。");
        return await GetJsonDocumentAsync(Rewrite(url), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>补齐当前平台适用的库与 natives 本机库，返回失败的库列表。</summary>
    private async Task<List<string>> RepairLibrariesAsync(JsonElement version, string versionName,
        IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        if (!version.TryGetProperty("libraries", out var libraries))
            return failures;
        var nativesDirectory = Path.Combine(paths.Minecraft, "versions", versionName, "natives");
        foreach (var library in libraries.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!RuleApplies(library))
                continue;
            var name = library.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() ?? string.Empty : string.Empty;
            try
            {
                var nativesJar = await EnsureLibraryArtifactsAsync(library, versionName, nativesDirectory, progress, cancellationToken)
                    .ConfigureAwait(false);
                if (nativesJar is not null)
                    await ExtractNativesAsync(nativesJar, nativesDirectory, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Log.Warning(exception, "BMCLAPI 库修复失败：{Library}", name);
                if (!failures.Contains(name))
                    failures.Add(name);
            }
        }
        return failures;
    }

    /// <summary>下载并校验库 artifact；返回需要解压的 natives jar 路径（如有）。</summary>
    private async Task<string?> EnsureLibraryArtifactsAsync(JsonElement library, string versionName,
        string nativesDirectory, IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        if (!library.TryGetProperty("downloads", out var downloads))
            return null;
        string? nativesJar = null;
        if (downloads.TryGetProperty("artifact", out var artifact) && artifact.ValueKind == JsonValueKind.Object)
        {
            nativesJar = await EnsureArtifactAsync(artifact, versionName, progress, cancellationToken).ConfigureAwait(false);
        }
        // 老版本（1.7-1.12）通过 classifiers + natives 映射提供本机库。
        if (downloads.TryGetProperty("classifiers", out var classifiers) && classifiers.ValueKind == JsonValueKind.Object)
        {
            var classifierName = ResolveClassifierName(library);
            if (classifierName is not null && classifiers.TryGetProperty(classifierName, out var classifier))
            {
                var jar = await EnsureArtifactAsync(classifier, versionName, progress, cancellationToken).ConfigureAwait(false);
                nativesJar = jar ?? nativesJar;
            }
        }
        return nativesJar;
    }

    private async Task<string?> EnsureArtifactAsync(JsonElement artifact, string versionName,
        IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        if (!artifact.TryGetProperty("path", out var pathProperty))
            return null;
        var relative = pathProperty.GetString();
        if (string.IsNullOrWhiteSpace(relative))
            return null;
        var target = LauncherPaths.Child(Path.Combine(paths.Minecraft, "libraries"), relative);
        var sha1 = artifact.TryGetProperty("sha1", out var sha1Property) ? sha1Property.GetString() : null;
        if (File.Exists(target) && (sha1 is null || await MatchesSha1Async(target, sha1, cancellationToken).ConfigureAwait(false)))
            return target;
        var url = artifact.TryGetProperty("url", out var urlProperty) && !string.IsNullOrWhiteSpace(urlProperty.GetString())
            ? Rewrite(urlProperty.GetString()!)
            : $"https://bmclapi2.bangbang93.com/maven/{relative}";
        progress?.Report(new LaunchProgress(LaunchStage.Downloading, $"正在修复 {Path.GetFileName(relative)}。"));
        await DownloadCheckedAsync(url, target, sha1, relative, progress, cancellationToken).ConfigureAwait(false);
        return target;
    }

    /// <summary>老版本 natives 映射（linux → natives-linux，osx → natives-macos），缺失时按官方命名探测。</summary>
    private static string? ResolveClassifierName(JsonElement library)
    {
        var os = PlatformRuntime.OsRuleName;
        var classifier = os == "osx" ? "natives-macos" : $"natives-{os}";
        if (library.TryGetProperty("natives", out var natives) && natives.ValueKind == JsonValueKind.Object &&
            natives.TryGetProperty(os, out var mapped) && mapped.ValueKind == JsonValueKind.String)
            return mapped.GetString();
        if (classifiersMissing(library, classifier))
            return classifier;
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 && classifiersMissing(library, $"{classifier}-arm64"))
            return $"{classifier}-arm64";
        return null;

        bool classifiersMissing(JsonElement element, string candidate)
        {
            return element.TryGetProperty("downloads", out var downloads) &&
                   downloads.TryGetProperty("classifiers", out var classifiers) &&
                   classifiers.TryGetProperty(candidate, out _);
        }
    }

    private static bool RuleApplies(JsonElement library)
    {
        if (!library.TryGetProperty("rules", out var rules))
            return true;
        var allowed = false;
        foreach (var rule in rules.EnumerateArray())
        {
            if (rule.TryGetProperty("features", out var features) && features.EnumerateObject().Any(feature => feature.Value.GetBoolean()))
                continue;
            if (rule.TryGetProperty("os", out var os) && os.ValueKind == JsonValueKind.Object)
            {
                if (os.TryGetProperty("name", out var osName) && osName.GetString() != PlatformRuntime.OsRuleName)
                    continue;
                if (os.TryGetProperty("arch", out var osArch))
                {
                    var currentArch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64"
                        : RuntimeInformation.ProcessArchitecture == Architecture.Arm ? "arm32"
                        : RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "x86" : "x64";
                    if (osArch.ValueKind == JsonValueKind.String && osArch.GetString() != currentArch)
                        continue;
                }
            }
            allowed = rule.GetProperty("action").GetString() == "allow";
        }
        return allowed;
    }

    private static async Task ExtractNativesAsync(string jarPath, string nativesDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(nativesDirectory);
        await using var stream = File.OpenRead(jarPath);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
                continue;
            var extension = Path.GetExtension(entry.FullName);
            if (extension is not (".so" or ".jnilib" or ".dylib"))
                continue;
            var target = LauncherPaths.Child(nativesDirectory, entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = entry.Open();
            await using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, true);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RepairAssetsAsync(JsonElement version, string versionName,
        IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        if (!version.TryGetProperty("assetIndex", out var assetIndex))
            return;
        var assetsRoot = Path.Combine(paths.Minecraft, "assets");
        var indexId = assetIndex.TryGetProperty("id", out var idProperty) && !string.IsNullOrWhiteSpace(idProperty.GetString())
            ? idProperty.GetString()! : versionName;
        var indexPath = Path.Combine(assetsRoot, "indexes", $"{indexId}.json");
        if (!File.Exists(indexPath))
        {
            var indexUrl = assetIndex.TryGetProperty("url", out var indexUrlProperty) ? indexUrlProperty.GetString() : null;
            if (string.IsNullOrWhiteSpace(indexUrl))
                throw new InvalidDataException("版本 JSON 缺少 assetIndex 地址，无法修复游戏资源。");
            var indexSha1 = assetIndex.TryGetProperty("sha1", out var indexSha1Property) ? indexSha1Property.GetString() : null;
            await DownloadCheckedAsync(Rewrite(indexUrl), indexPath, indexSha1, $"{indexId}.json", progress, cancellationToken)
                .ConfigureAwait(false);
        }

        using var index = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath, cancellationToken).ConfigureAwait(false));
        if (!index.RootElement.TryGetProperty("objects", out var objects))
            return;
        var objectsRoot = Path.Combine(assetsRoot, "objects");
        var missing = new List<(string Hash, string Target)>();
        foreach (var entry in objects.EnumerateObject())
        {
            if (!entry.Value.TryGetProperty("hash", out var hashProperty))
                continue;
            var hash = hashProperty.GetString();
            if (string.IsNullOrWhiteSpace(hash) || hash.Length < 3)
                continue;
            var target = Path.Combine(objectsRoot, hash[..2], hash);
            if (!File.Exists(target))
                missing.Add((hash, target));
        }
        // 不同资源名可能指向同一 hash，去重避免并发重复下载同一对象。
        missing = missing.DistinctBy(item => item.Hash).ToList();
        if (missing.Count == 0)
            return;
        Log.Information("BMCLAPI 需要修复 {Count} 个游戏资源对象。", missing.Count);
        var completed = 0;
        var failures = 0;
        using var gate = new SemaphoreSlim(AssetParallelism);
        await Task.WhenAll(missing.Select(item => Task.Run(async () =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var url = $"https://bmclapi2.bangbang93.com/assets/{item.Hash[..2]}/{item.Hash}";
                await DownloadCheckedAsync(url, item.Target, item.Hash, item.Hash, null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Interlocked.Increment(ref failures);
                Log.Warning(exception, "BMCLAPI 资源对象修复失败：{Hash}", item.Hash);
            }
            finally
            {
                gate.Release();
                var done = Interlocked.Increment(ref completed);
                progress?.Report(new LaunchProgress(LaunchStage.Downloading,
                    $"正在修复游戏资源 {done}/{missing.Count}", done, missing.Count, item.Hash));
            }
        }, cancellationToken))).ConfigureAwait(false);
        if (failures > 0)
            Log.Warning("BMCLAPI 资源对象修复有 {Count} 个失败；资源缺失通常只影响声音与语言文件，可再次启动重试。", failures);
    }

    private async Task DownloadCheckedAsync(string url, string target, string? sha1, string label,
        IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (sha1 is not null && File.Exists(target) && await MatchesSha1Async(target, sha1, cancellationToken).ConfigureAwait(false))
            return;
        var temporary = target + $".{Guid.NewGuid():N}.part";
        try
        {
            using var response = await SendAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            // 写入与 SHA1 校验复用同一个句柄（FileAccess.ReadWrite 才能在写入后回读校验）；
            // 写句柄是 FileShare.None，释放前二次打开会被自己拒绝。
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 128 * 1024, true))
            {
                await content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                if (sha1 is not null)
                {
                    output.Seek(0, SeekOrigin.Begin);
                    var actual = Convert.ToHexString(await SHA1.HashDataAsync(output, cancellationToken).ConfigureAwait(false));
                    if (!actual.Equals(sha1, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"资源 SHA1 校验失败：{label}");
                }
            }
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
        }
    }

    private async Task<JsonDocument> GetJsonDocumentAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private Task<HttpResponseMessage> SendAsync(string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        return http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static async Task<bool> MatchesSha1Async(string path, string sha1, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(path);
        return Convert.ToHexString(await SHA1.HashDataAsync(input, cancellationToken).ConfigureAwait(false))
            .Equals(sha1, StringComparison.OrdinalIgnoreCase);
    }

    private static string Rewrite(string url)
    {
        foreach (var (from, to) in Rewrites)
        {
            if (url.StartsWith(from, StringComparison.OrdinalIgnoreCase))
                return to + url[from.Length..];
        }
        return url;
    }
}
