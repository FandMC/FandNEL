using System.Collections.Concurrent;
using System.Text;
using FandNEL.Proxy.Models;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;
using Serilog;

namespace FandNEL.Proxy.Irc;

/// <summary>
/// 游戏内聊天 ↔ NeoEastSide 聊天室的桥。
/// 每个代理会话一个实例：拦截游戏内 /IRC 输入发到聊天室，
/// 并把聊天室消息以系统聊天包注入游戏（显示为 [ES 昵称] 内容）。
/// 同一账号支持多地登录，多开互不影响。
/// </summary>
public sealed class IrcChatBridge : IAsyncDisposable
{
    private const string UsageHint = "输入 /IRC 内容 即可发送消息到聊天室";
    private const int ChatMessageMaximumLength = 256;

    private readonly IrcChatOptions _options;
    private readonly IrcChatSession _session;
    private readonly ConcurrentDictionary<MinecraftConnection, byte> _connections = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recentLocalEcho = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _pumpGate = new();
    private Task? _pump;
    private string _displayName;
    private int _disposed;

    public IrcChatBridge(IrcChatOptions options, string hwid)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _displayName = options.Username;
        _session = new IrcChatSession(options, hwid);
    }

    /// <summary>把 IRC 拦截器注册进会话自己的协议注册表（由 ProxyOptions.ConfigureRegistry 调用）。</summary>
    public void AttachRegistry(PacketRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        foreach (var spec in IrcProtocol.Specs)
        {
            var versions = new[] { spec.Version };
            if (spec.ChatMessageId is { } chatMessageId)
            {
                registry.Register(ConnectionState.Play, PacketDirection.ServerBound, chatMessageId,
                    (context, _) => HandleChatPacketAsync(context, isCommandPacket: false), versions);
            }

            if (spec.ChatCommandId is { } chatCommandId)
            {
                registry.Register(ConnectionState.Play, PacketDirection.ServerBound, chatCommandId,
                    (context, _) => HandleChatPacketAsync(context, isCommandPacket: true), versions);
            }

            if (spec.SignedChatCommandId is { } signedCommandId)
            {
                registry.Register(ConnectionState.Play, PacketDirection.ServerBound, signedCommandId,
                    (context, _) => HandleChatPacketAsync(context, isCommandPacket: true), versions);
            }

            registry.Register(ConnectionState.Play, PacketDirection.ClientBound, spec.JoinGameId,
                (context, _) => HandleJoinGameAsync(context), versions);
        }

        Log.Information("IRC: interceptors attached for {VersionCount} protocol versions (account {Username})",
            IrcProtocol.Specs.Length, _options.Username);
    }

    /// <summary>绑定代理会话生命周期：会话停止或出错时自动停止轮询。</summary>
    public void BindSession(IProxySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Snapshot.State is ProxySessionState.Stopped or ProxySessionState.Faulted)
        {
            _ = DisposeAsync();
            return;
        }

        session.EventOccurred += OnSessionEvent;
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            _lifetime.Cancel();
        }
        catch
        {
            // 取消失败不影响后续释放。
        }

        _connections.Clear();
        _session.Dispose();
        // 这里不释放 CancellationTokenSource：轮询任务可能仍在等待中，交给 GC 更安全。
        return ValueTask.CompletedTask;
    }

    private void OnSessionEvent(object? sender, ProxyEventArgs args)
    {
        if (args.Value.Kind is not (ProxyEventKind.Stopped or ProxyEventKind.Faulted))
        {
            return;
        }

        if (sender is IProxySession session)
        {
            session.EventOccurred -= OnSessionEvent;
        }

        Log.Information("IRC: proxy session ended, bridge stopped");
        _ = DisposeAsync();
    }

    private ValueTask HandleJoinGameAsync(PacketContext context)
    {
        RegisterConnection(context.Connection);
        return ValueTask.CompletedTask;
    }

    private ValueTask HandleChatPacketAsync(PacketContext context, bool isCommandPacket)
    {
        string text;
        try
        {
            text = context.CreateReader().ReadString(ChatMessageMaximumLength);
        }
        catch (Exception)
        {
            // 读不出来的包不是我们的目标，原样放行。
            return ValueTask.CompletedTask;
        }

        if (!IrcProtocol.TryParseIrcCommand(text, isCommandPacket, out var content))
        {
            return ValueTask.CompletedTask;
        }

        // 是 /IRC 命令：吞掉原件（不再发给游戏服务器），转交聊天室。
        context.Cancel();
        HandleIrcCommand(context.Connection, content);
        return ValueTask.CompletedTask;
    }

    private void HandleIrcCommand(MinecraftConnection connection, string content)
    {
        RegisterConnection(connection);
        if (content.Length == 0)
        {
            _ = InjectAsync(connection, "用法：/IRC 内容（例：/IRC 大家好）");
            return;
        }

        _ = Task.Run(() => SendAndEchoAsync(connection, content));
    }

    private async Task SendAndEchoAsync(MinecraftConnection connection, string content)
    {
        try
        {
            var result = await _session.SendAsync(content, _lifetime.Token).ConfigureAwait(false);
            if (!result.Success)
            {
                Log.Warning("IRC: message rejected: {Reason}", result.Message);
                await InjectAsync(connection, $"§c发送失败：{result.Message}").ConfigureAwait(false);
                return;
            }

            // 本地毫秒级回显；服务器回环的同一条消息会被去重跳过，不会重复显示。
            // 广播给所有连接：局域网模式下同一代理可能挂着多个本地玩家。
            RememberLocalEcho(content);
            await BroadcastAsync(Format(_displayName, content)).ConfigureAwait(false);
            Log.Information("IRC: message sent: {Text}", content);
        }
        catch (OperationCanceledException)
        {
            // 会话已结束。
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "IRC: send failed");
            await InjectAsync(connection, "§c发送失败：" + exception.Message).ConfigureAwait(false);
        }
    }

    private void RegisterConnection(MinecraftConnection connection)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (!_connections.TryAdd(connection, 0))
        {
            return;
        }

        Log.Information("IRC: game client joined, chat bridge active");
        StartPump();
        _ = Task.Run(() => WelcomeAsync(connection));
    }

    private async Task WelcomeAsync(MinecraftConnection connection)
    {
        try
        {
            // 稍等一下再提示，避开进服瞬间的加载提示刷屏。
            await Task.Delay(TimeSpan.FromSeconds(2), _lifetime.Token).ConfigureAwait(false);
            await InjectAsync(connection, "§b[§cES§b]§f 已接入聊天室，" + UsageHint + "。").ConfigureAwait(false);
        }
        catch
        {
            // 会话结束或注入失败都不影响主流程。
        }
    }

    private void StartPump()
    {
        lock (_pumpGate)
        {
            if (_pump is not null || Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            _pump = Task.Run(() => PumpAsync(_lifetime.Token));
        }
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        // 首次轮询只用来取当前最新消息号（服务端会返回最近 50 条），
        // 直接展示会把历史消息一次性倒进游戏，这里选择只对齐游标。
        var primed = false;
        var nextUsageHint = DateTimeOffset.UtcNow.AddSeconds(15);
        var nextOnlineHint = DateTimeOffset.UtcNow.AddSeconds(6);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_connections.IsEmpty)
                {
                    // 没有游戏在线时不打扰服务器，只等下一轮。
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!await _session.EnsureLoginAsync(cancellationToken).ConfigureAwait(false))
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var poll = await _session.PollAsync(cancellationToken).ConfigureAwait(false);
                if (!poll.Success)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (poll.Disabled)
                {
                    Log.Warning("IRC: chat account disabled, bridge stopped");
                    await BroadcastAsync("§c聊天室账号已被禁用，IRC 功能已停止。").ConfigureAwait(false);
                    return;
                }

                if (primed)
                {
                    foreach (var message in poll.Messages)
                    {
                        if (string.IsNullOrWhiteSpace(message.Text))
                        {
                            continue;
                        }

                        if (message.IsIrc && WasRecentlyEchoed(message.Text))
                        {
                            // 自己的消息从服务器回环：只用它校准显示名，不再重复显示。
                            LearnDisplayName(message.Sender);
                            continue;
                        }

                        if (message.IsIrc)
                        {
                            LearnDisplayName(message.Sender);
                        }

                        await BroadcastAsync(Format(message.Sender, message.Text)).ConfigureAwait(false);
                    }
                }

                primed = true;

                var now = DateTimeOffset.UtcNow;
                if (_options.ShowUsageHint && now >= nextUsageHint)
                {
                    nextUsageHint = now.AddSeconds(15);
                    await BroadcastAsync(UsageHint).ConfigureAwait(false);
                }

                if (_options.ShowOnlineHint && now >= nextOnlineHint)
                {
                    nextOnlineHint = now.AddSeconds(6);
                    await BroadcastAsync($"聊天室在线人数：{poll.Online}").ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "IRC: unexpected poll loop error");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            try
            {
                await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task BroadcastAsync(string text)
    {
        foreach (var connection in _connections.Keys)
        {
            await InjectAsync(connection, text).ConfigureAwait(false);
        }
    }

    /// <summary>把一行文本注入游戏内聊天栏（系统聊天包）。</summary>
    private async Task InjectAsync(MinecraftConnection connection, string text)
    {
        try
        {
            // 只在双方都处于 Play 状态时注入：服务器切回 Configuration（重配置）时注入会让客户端认为收错包。
            if (connection.ClientState != ConnectionState.Play || connection.ServerState != ConnectionState.Play)
            {
                return;
            }

            var spec = IrcProtocol.TryGetSpec(connection.Version);
            if (spec is null)
            {
                return;
            }

            var payload = IrcProtocol.BuildSystemChatPayload(connection.Version, text);
            await connection.SendAsync(PacketDirection.ClientBound, spec.SystemChatId, payload, _lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 会话已结束。
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "IRC: failed to inject chat message, dropping connection");
            _connections.TryRemove(connection, out _);
        }
    }

    /// <summary>聊天室消息在游戏内的统一显示格式：§b[§cES §a昵称§b]§f 内容。</summary>
    private static string Format(string sender, string text)
    {
        var plain = text.Replace('\r', ' ').Replace('\n', ' ');
        return string.IsNullOrWhiteSpace(sender) ? plain : $"§b[§cES §a{sender.Trim()}§b]§f {plain}";
    }

    private void LearnDisplayName(string sender)
    {
        var name = sender.Trim();
        if (name.Length == 0 || string.Equals(name, _displayName, StringComparison.Ordinal))
        {
            return;
        }

        _displayName = name;
        Log.Information("IRC: display name resolved to {Name}", name);
    }

    private void RememberLocalEcho(string content)
    {
        var now = DateTimeOffset.UtcNow;
        _recentLocalEcho[content] = now;
        if (_recentLocalEcho.Count <= 300)
        {
            return;
        }

        foreach (var pair in _recentLocalEcho)
        {
            if (now - pair.Value > TimeSpan.FromSeconds(60))
            {
                _recentLocalEcho.TryRemove(pair.Key, out _);
            }
        }
    }

    /// <summary>服务器回环的消息是否就是本机刚回显过的那条（剥掉颜色码后按内容包含比较）。</summary>
    private bool WasRecentlyEchoed(string serverText)
    {
        var plain = StripColors(serverText);
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _recentLocalEcho)
        {
            if (now - pair.Value > TimeSpan.FromSeconds(60))
            {
                continue;
            }

            if (plain.Contains(pair.Key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>去掉 MC 颜色/格式控制码（§+单字符）。</summary>
    private static string StripColors(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('§'))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '§' && index + 1 < text.Length)
            {
                index++;
                continue;
            }

            builder.Append(text[index]);
        }

        return builder.ToString();
    }
}