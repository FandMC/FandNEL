using System.Security.Cryptography;
using FandNEL.Core.Entities.WPFLauncher.NetGame;
using FandNEL.Core.Entities.WPFLauncher.NetGame.Mods;
using FandNEL.Core.Entities.WPFLauncher.NetGame.Texture;
using FandNEL.Core.Protocol;
using FandNEL.GameLauncher.Downloads;
using FandNEL.GameLauncher.Models;

namespace FandNEL.GameLauncher.Services.Java;

/// <summary>沿用 Codexus 的网易客户端包、Forge 库和模组分发协议。</summary>
internal sealed class MinecraftInstaller(WPFLauncher launcher, LauncherPaths paths, HttpClient http)
{
    private readonly ArchiveInstaller _archives = new(http);

    public async Task PrepareClientAsync(JavaLaunchRequest request, IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        var baseResponse = await launcher.GetMinecraftClientLibsAsync(request.UserId, request.UserToken).ConfigureAwait(false);
        if (baseResponse.Code != 0 || baseResponse.Data is null)
            throw new InvalidOperationException($"获取基础游戏资源失败：{baseResponse.Message}");
        var basePackage = baseResponse.Data;
        await _archives.InstallAsync(basePackage.Url, Path.Combine(paths.Cache, "GameBase.7z"), paths.GameBase,
            basePackage.Md5, progress, cancellationToken).ConfigureAwait(false);

        var response = await launcher.GetMinecraftClientLibsAsync(request.UserId, request.UserToken, request.GameVersion).ConfigureAwait(false);
        if (response.Code != 0 || response.Data is null)
            throw new InvalidOperationException($"获取游戏版本资源失败：{response.Message}");
        var versionPackage = response.Data;
        var packageName = request.GameVersion.ToString();
        await _archives.InstallAsync(versionPackage.Url, Path.Combine(paths.Cache, packageName + ".7z"), paths.GameBase,
            versionPackage.Md5, progress, cancellationToken).ConfigureAwait(false);
        await _archives.InstallAsync(versionPackage.CoreLibUrl, Path.Combine(paths.Cache, packageName + "_Lib.7z"), paths.Cache,
            versionPackage.CoreLibMd5, progress, cancellationToken).ConfigureAwait(false);
        InstallLibraries(Path.Combine(paths.Cache, packageName + "_libs"), VersionName(request.GameVersion));
    }

    public async Task<EntityModsList> PrepareModsAsync(JavaLaunchRequest request, IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        var mods = new EntityModsList();
        var response = await launcher.GetGameCoreModListAsync(request.UserId, request.UserToken, request.GameVersion,
            request.GameType == EnumGType.ServerGame).ConfigureAwait(false);
        if (response.Code != 0 || response.Data?.IidList is null)
            throw new InvalidOperationException($"获取核心模组列表失败：{response.Message}");
        var details = await launcher.GetGameCoreModDetailsListAsync(request.UserId, request.UserToken, response.Data.IidList).ConfigureAwait(false);
        if (details.Code != 0 || details.Data is null)
            throw new InvalidOperationException($"获取核心模组资源失败：{details.Message}");

        foreach (var component in details.Data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resource = component.SubEntities.FirstOrDefault() ?? throw new InvalidDataException("核心模组缺少下载资源。");
            var jarName = $"{Path.GetFileNameWithoutExtension(resource.ResName)}@{component.MTypeId}@{component.EntityId}.jar";
            var jarPath = LauncherPaths.Child(paths.CoreMods(request.GameId), jarName);
            if (!File.Exists(jarPath) || !await ArchiveInstaller.MatchesAsync(jarPath, resource.JarMd5, cancellationToken).ConfigureAwait(false))
            {
                var cache = Path.Combine(paths.Cache, "Downloads", LauncherPaths.Key(resource.ResUrl));
                await _archives.DownloadAsync(resource.ResUrl, cache + ".archive", resource.ResMd5, progress, cancellationToken).ConfigureAwait(false);
                await ArchiveInstaller.ExtractAsync(cache + ".archive", cache, progress, cancellationToken).ConfigureAwait(false);
                var matches = Directory.EnumerateFiles(cache, "*.jar", SearchOption.AllDirectories).ToArray();
                string? source = null;
                foreach (var candidate in matches)
                {
                    if (await ArchiveInstaller.MatchesAsync(candidate, resource.JarMd5, cancellationToken).ConfigureAwait(false))
                    {
                        source = candidate;
                        break;
                    }
                }
                if (source is null)
                    throw new InvalidDataException($"核心模组包中没有校验匹配的 JAR：{resource.ResName}");
                CopyFile(source, jarPath);
            }
            // 认证清单使用平台下发的逻辑 ID，不使用本地重命名后的文件名。
            foreach (var sub in component.SubEntities)
                mods.Mods.Add(new EntityModsInfo
                {
                    ModPath = $"{component.ItemId}@{component.MTypeId}@0.jar",
                    Id = $"{component.ItemId}@{component.MTypeId}@0.jar",
                    Iid = component.ItemId,
                    Md5 = sub.JarMd5.ToUpperInvariant(),
                    Name = string.Empty,
                    Version = string.Empty
                });
        }

        var assets = await launcher.GetNetGameComponentDownloadListAsync(request.UserId, request.UserToken, request.GameId).ConfigureAwait(false);
        if (assets.Code != 0 || assets.Data is null)
        {
            if (request.GameType == EnumGType.NetGame)
                throw new InvalidOperationException($"获取服务器资源失败：{assets.Message}");
            return mods;
        }
        var asset = assets.Data.SubEntities.FirstOrDefault() ?? throw new InvalidDataException("服务器资源包为空。");
        var assetDirectory = paths.GameAssets(request.GameId);
        await _archives.InstallAsync(asset.ResUrl, assetDirectory + ".7z", assetDirectory,
            asset.ResMd5, progress, cancellationToken).ConfigureAwait(false);
        var serverMods = Path.Combine(assetDirectory, ".minecraft", "mods");
        if (Directory.Exists(serverMods))
        {
            foreach (var jar in Directory.EnumerateFiles(serverMods, "*.jar", SearchOption.AllDirectories))
            {
                await using var input = File.OpenRead(jar);
                var name = Path.GetFileName(jar);
                mods.Mods.Add(new EntityModsInfo
                {
                    ModPath = name,
                    Id = name,
                    Iid = name.Split('@')[0],
                    Md5 = Convert.ToHexString(await MD5.HashDataAsync(input, cancellationToken).ConfigureAwait(false)),
                    Name = string.Empty,
                    Version = string.Empty
                });
            }
        }
        return mods;
    }

    public string PrepareRuntime(JavaLaunchRequest request)
    {
        var runtime = paths.Runtime(request);
        Directory.CreateDirectory(runtime);
        var mods = Path.Combine(runtime, "mods");
        // 保留旧模组快照，避免更新残留与新版同时装载，也不删除用户添加的文件。
        if (Directory.Exists(mods))
            Directory.Move(mods, Path.Combine(runtime, $"mods.previous-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(mods);
        CopyDirectory(Path.Combine(paths.GameAssets(request.GameId), ".minecraft"), runtime);
        if (request.LoadCoreMods)
            CopyDirectory(paths.CoreMods(request.GameId), mods);
        CopyDirectory(paths.CustomMods, mods);

        var native = Path.Combine(paths.Resources, "api-ms-win-crt-utility-l1-1-1.dll");
        if (File.Exists(native))
            CopyFile(native, Path.Combine(paths.Minecraft, "versions", VersionName(request.GameVersion), "natives", "runtime", Path.GetFileName(native)));
        return runtime;
    }

    private void InstallLibraries(string sourceDirectory, string version)
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"版本库目录不存在：{sourceDirectory}");
        foreach (var source in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(source);
            string? target = null;
            if (name == version + ".jar" || name == version + ".json")
                target = Path.Combine(paths.Minecraft, "versions", version, name);
            else if (name.StartsWith("forge-" + version + "-", StringComparison.Ordinal))
                target = Library("net/minecraftforge/forge", "forge-");
            else if (name.StartsWith("launchwrapper-", StringComparison.Ordinal))
                target = Library("net/minecraft/launchwrapper", "launchwrapper-");
            else if (name.StartsWith("MercuriusUpdater-", StringComparison.Ordinal))
                target = Library("net/minecraftforge/MercuriusUpdater", "MercuriusUpdater-");
            else if (name.StartsWith("modlauncher-", StringComparison.Ordinal))
                target = Library(name.StartsWith("modlauncher-10.2.", StringComparison.Ordinal) ? "net/minecraftforge/modlauncher" : "cpw/mods/modlauncher", "modlauncher-");
            if (target is not null)
                CopyFile(source, target);

            string Library(string group, string prefix) => Path.Combine(paths.Minecraft, "libraries", group,
                Path.GetFileNameWithoutExtension(name)[prefix.Length..], name);
        }
    }

    internal static string VersionName(EnumGameVersion version) => version.ToString()[2..].Replace('_', '.');

    private static void CopyFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
            return;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            CopyFile(file, LauncherPaths.Child(destination, Path.GetRelativePath(source, file)));
    }
}
