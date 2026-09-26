using System.Net;
using FandNEL.Core.Connection.Protocols;
using FandNEL.Core.Entities.WPFLauncher.NetGame.Mods;
using FandNEL.Core.Protocol;
using FandNEL.GameLauncher.Models;
using FandNEL.GameLauncher.Processes;

namespace FandNEL.GameLauncher.Services.Java;

/// <summary>Java 客户端完整编排：认证、资源、模组、启动参数和生命周期。</summary>
public sealed class JavaLauncherService(WPFLauncher launcher, LauncherPaths paths, HttpClient? httpClient = null)
{
    private readonly HttpClient _http = httpClient ?? new HttpClient();

    public async Task<JavaGameHandle> LaunchAsync(
        JavaLaunchRequest request,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        Directory.CreateDirectory(paths.Root);
        var installer = new MinecraftInstaller(launcher, paths, _http);
        var javaRuntime = new JavaRuntimeInstaller(paths, _http);
        EntityModsList? mods = null;
        AuthLibProtocol? authentication = null;
        JavaRpcService? rpc = null;
        IGameProcess? process = null;
        try
        {
            Report(progress, LaunchStage.Preparing, "正在准备 Java 运行时。");
            var java = await javaRuntime.PrepareAsync(request, progress, cancellationToken).ConfigureAwait(false);
            Report(progress, LaunchStage.Downloading, "正在准备 Minecraft 客户端资源。");
            await installer.PrepareClientAsync(request, progress, cancellationToken).ConfigureAwait(false);
            mods = await installer.PrepareModsAsync(request, progress, cancellationToken).ConfigureAwait(false);
            var runtime = installer.PrepareRuntime(request);

            var authPort = 0;
            rpc = new JavaRpcService(launcher, request, paths, _http);
            rpc.Start();
            var rpcPort = rpc.Port;
            authentication = new AuthLibProtocol(IPAddress.Loopback, authPort, System.Text.Json.JsonSerializer.Serialize(mods),
                MinecraftInstaller.VersionName(request.GameVersion), request.AccessToken,
                (userId, _) => Task.FromResult<string?>(userId == request.UserId ? request.UserToken : null));
            authentication.Start();
            authPort = authentication.Port;
            Report(progress, LaunchStage.StartingProcess, "正在启动 Minecraft。");
            var launchSpec = MinecraftCommandBuilder.Build(request, paths, java, runtime, authPort, rpcPort);
            process = await new SystemGameProcessFactory().StartAsync(launchSpec, cancellationToken).ConfigureAwait(false);
            var handle = new JavaGameHandle(process, authentication, rpc, mods, progress);
            Report(progress, LaunchStage.Running, $"Minecraft 已启动，进程 ID {handle.ProcessId}。");
            return handle;
        }
        catch
        {
            authentication?.Dispose();
            if (rpc is not null)
                await rpc.DisposeAsync().ConfigureAwait(false);
            if (process is not null)
                await process.DisposeAsync().ConfigureAwait(false);
            Report(progress, LaunchStage.Failed, "Minecraft 启动失败。");
            throw;
        }
    }

    private static void Report(IProgress<LaunchProgress>? progress, LaunchStage stage, string message) => progress?.Report(new(stage, message));
}

public sealed class JavaGameHandle : IAsyncDisposable
{
    private readonly IGameProcess _process;
    private readonly AuthLibProtocol _authentication;
    private readonly JavaRpcService _rpc;
    private readonly IProgress<LaunchProgress>? _progress;
    private int _disposed;

    internal JavaGameHandle(IGameProcess process, AuthLibProtocol authentication, JavaRpcService rpc, EntityModsList mods, IProgress<LaunchProgress>? progress)
    {
        _process = process;
        _authentication = authentication;
        _rpc = rpc;
        _progress = progress;
        Mods = mods;
    }

    public int ProcessId => _process.ProcessId;
    public bool HasExited => _process.HasExited;
    public EntityModsList Mods { get; }

    public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        await DisposeAsync().ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _progress?.Report(new LaunchProgress(LaunchStage.Stopping, "正在关闭 Minecraft。"));
        await _process.StopAsync(cancellationToken).ConfigureAwait(false);
        await DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _authentication.Dispose();
        await _rpc.DisposeAsync().ConfigureAwait(false);
        await _process.DisposeAsync().ConfigureAwait(false);
        _progress?.Report(new LaunchProgress(LaunchStage.Completed, "Minecraft 已退出。"));
    }
}
