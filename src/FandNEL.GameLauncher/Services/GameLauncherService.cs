using FandNEL.GameLauncher.Downloads;
using FandNEL.GameLauncher.Models;
using FandNEL.GameLauncher.Processes;

namespace FandNEL.GameLauncher.Services;

/// <summary>编排安装、资源、Proxy 和游戏进程的启动服务。</summary>
public sealed class GameLauncherService : IGameLauncher
{
    private readonly IGameInstallService _installService;
    private readonly IResourceDownloader _resourceDownloader;
    private readonly IGameProcessFactory _processFactory;
    private readonly IProxyEndpointProvider? _proxyProvider;
    private readonly ILaunchConfigWriter? _configWriter;

    public GameLauncherService(
        IGameInstallService installService,
        IResourceDownloader resourceDownloader,
        IGameProcessFactory processFactory,
        IProxyEndpointProvider? proxyProvider = null,
        ILaunchConfigWriter? configWriter = null)
    {
        _installService = installService ?? throw new ArgumentNullException(nameof(installService));
        _resourceDownloader = resourceDownloader ?? throw new ArgumentNullException(nameof(resourceDownloader));
        _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
        _proxyProvider = proxyProvider;
        _configWriter = configWriter;
    }

    public async Task<GameLaunchHandle> LaunchAsync(
        GameLaunchRequest request,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        IProxyLease? proxyLease = null;
        IGameProcess? process = null;
        try
        {
            Report(progress, LaunchStage.Preparing, "正在准备游戏目录。");
            var install = await _installService.PrepareAsync(request, cancellationToken).ConfigureAwait(false);
            if (request.Resources.Count > 0)
            {
                Report(progress, LaunchStage.Downloading, $"正在下载 {request.Resources.Count} 个资源。");
                await _resourceDownloader.DownloadAsync(request.Resources, install.RootDirectory, progress, cancellationToken).ConfigureAwait(false);
            }

            if (!File.Exists(install.ExecutablePath))
                throw new FileNotFoundException("游戏可执行文件不存在。", install.ExecutablePath);

            if (request.Proxy is not null)
            {
                if (_proxyProvider is null)
                    throw new InvalidOperationException("请求使用 Proxy，但没有配置 IProxyEndpointProvider。");
                proxyLease = await _proxyProvider.AcquireAsync(request.Proxy, cancellationToken).ConfigureAwait(false);
                proxyLease.Endpoint.Validate();
            }

            var arguments = request.Arguments.ToArray();
            var configPath = await WriteConfigurationAsync(request, install, proxyLease, arguments, progress, cancellationToken).ConfigureAwait(false);
            if (configPath is not null)
                arguments = arguments.Append(configPath).ToArray();

            Report(progress, LaunchStage.StartingProcess, $"正在启动 {Path.GetFileName(install.ExecutablePath)}。");
            process = await _processFactory.StartAsync(
                new ProcessLaunchSpec(install.ExecutablePath, install.WorkingDirectory, arguments, request.Environment),
                cancellationToken).ConfigureAwait(false);
            var handle = new GameLaunchHandle(process, proxyLease);
            Report(progress, LaunchStage.Running, $"游戏已启动，进程 ID 为 {handle.ProcessId}。");
            return handle;
        }
        catch
        {
            if (process is not null)
                await process.DisposeAsync().ConfigureAwait(false);
            if (proxyLease is not null)
                await proxyLease.DisposeAsync().ConfigureAwait(false);
            Report(progress, LaunchStage.Failed, "游戏启动失败。");
            throw;
        }
    }

    private async Task<string?> WriteConfigurationAsync(
        GameLaunchRequest request,
        GameInstall install,
        IProxyLease? proxyLease,
        IReadOnlyList<string> arguments,
        IProgress<LaunchProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (request.ConfigPath is null)
            return null;
        if (_configWriter is null)
            throw new InvalidOperationException("请求写入启动配置，但没有配置 ILaunchConfigWriter。");

        var path = ResolveChildPath(install.RootDirectory, request.ConfigPath);
        var endpoint = proxyLease?.Endpoint;
        var configuration = new LaunchConfiguration(
            path,
            install.RootDirectory,
            request.GameId,
            request.GameVersion,
            endpoint?.Host,
            endpoint?.Port,
            arguments);
        Report(progress, LaunchStage.WritingConfiguration, "正在写入游戏启动配置。");
        await _configWriter.WriteAsync(configuration, cancellationToken).ConfigureAwait(false);
        return path;
    }

    private static void Report(IProgress<LaunchProgress>? progress, LaunchStage stage, string message) =>
        progress?.Report(new LaunchProgress(stage, message));

    private static string ResolveChildPath(string rootDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            throw new ArgumentException("启动配置路径必须是游戏目录中的相对路径。", nameof(relativePath));
        var root = Path.GetFullPath(rootDirectory);
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("启动配置路径必须位于游戏目录内。", nameof(relativePath));
        return path;
    }
}
