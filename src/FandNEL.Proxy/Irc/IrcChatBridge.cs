using System.Collections.Concurrent;
using FandNEL.Proxy.Models;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;
using Serilog;

namespace FandNEL.Proxy.Irc;

/// <summary>
/// 游戏内聊天 ↔ 聊天室的桥：拦截 /IRC 转发聊天室，聊天室消息以系统聊天包注入游戏
/// （显示为 §b[§cES §a游戏ID§b]§f 内容）。每个代理会话一个实例，匿名客户端通过短期 clientId 区分在线状态。
/// </summary>
public sealed class IrcChatBridge : IAsyncDisposable
{
    private readonly IrcChatOptions _options;
    private readonly IrcChatSession _session;
    private readonly ConcurrentDictionary<MinecraftConnection, byte> _connections = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recentLocalEcho = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _pumpGate = new();
    private Task? _pump;
    private string _displayName;
    private int _disposed;

    public IrcChatBridge(IrcChatOptions options, string gameId)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _displayName = string.IsNullOrWhiteSpace(gameId) ? "unknown" : gameId.Trim();
        _session = new IrcChatSession(options, _displayName);
    }

    /// <summary>把拦截器注册进会话协议注册表（由 ProxyOptions.ConfigureRegistry 调用）。</summary>
    public void AttachRegistry(PacketRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        foreach (var spec in IrcProtocol.Specs)
        {
            var versions = new[] { spec.Version };
            // 聊天包与命令包都是 /IRC 的来源：1.19+ 的普通命令、以及带签名的命令包。
            (int? Id, bool IsCommand)[] inbound = [(spec.ChatMessageId, false), (spec.ChatCommandId, true), (spec.SignedChatCommandId, true)];
            foreach (var (id, isCommand) in inbound)
                if (id is { } packetId)
                    registry.Register(ConnectionState.Play, PacketDirection.ServerBound, packetId,
                        (context, _) => HandleChatPacketAsync(context, isCommand), versions);
            registry.Register(ConnectionState.Play, PacketDirection.ClientBound, spec.JoinGameId,
                (context, _) => { RegisterConnection(context.Connection); return ValueTask.CompletedTask; }, versions);
        }
        Log.Information("IRC: interceptors attached for {VersionCount} protocol versions (game {GameId})",
            IrcProtocol.Specs.Count, _displayName);
    }

    /// <summary>绑定代理会话生命周期：会话停止或出错时自动停止轮询。</summary>
    public void BindSession(IProxySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Snapshot.State is ProxySessionState.Stopped or ProxySessionState.Faulted) { _ = DisposeAsync(); return; }
        session.EventOccurred += OnSessionEvent;
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return ValueTask.CompletedTask;
        try { _lifetime.Cancel(); }
        catch (Exception exception) { Log.Debug(exception, "IRC: cancelling lifetime failed"); } // 取消失败不影响后续释放
        _connections.Clear();
        _session.Dispose();
        return ValueTask.CompletedTask; // 不释放 CancellationTokenSource：轮询任务可能仍在等待，交给 GC 更安全
    }

    private void OnSessionEvent(object? sender, ProxyEventArgs args)
    {
        if (args.Value.Kind is not (ProxyEventKind.Stopped or ProxyEventKind.Faulted)) return;
        if (sender is IProxySession session) session.EventOccurred -= OnSessionEvent;
        Log.Information("IRC: proxy session ended, bridge stopped");
        _ = DisposeAsync();
    }

    private ValueTask HandleChatPacketAsync(PacketContext context, bool isCommandPacket)
    {
        string text;
        try { text = context.CreateReader().ReadString(IrcConstants.ChatMessageMaximumLength); }
        catch (Exception) { return ValueTask.CompletedTask; } // 读不出来的包不是我们的目标，原样放行
        if (!IrcProtocol.TryParseIrcCommand(text, isCommandPacket, out var content)) return ValueTask.CompletedTask;

        // 是 /IRC：吞掉原件（不再发给游戏服务器），转交聊天室。
        context.Cancel();
        RegisterConnection(context.Connection);
        if (content.Length == 0) _ = InjectAsync(context.Connection, IrcConstants.EmptyCommandHint);
        else _ = Task.Run(() => SendAndEchoAsync(context.Connection, content));
        return ValueTask.CompletedTask;
    }

    private async Task SendAndEchoAsync(MinecraftConnection connection, string content)
    {
        try
        {
            var result = await _session.SendAsync(content, _lifetime.Token).ConfigureAwait(false);
            if (!result.Success)
            {
                Log.Warning("IRC: message rejected: {Reason}", result.Message);
                await InjectAsync(connection, IrcConstants.SendFailurePrefix + result.Message).ConfigureAwait(false);
                return;
            }
            // 本地毫秒级回显；服务器回环的同一条消息会被去重跳过。广播给所有连接（局域网多开）。
            RememberLocalEcho(content);
            await BroadcastAsync(Format(_displayName, content)).ConfigureAwait(false);
            Log.Information("IRC: message sent: {Text}", content);
        }
        catch (OperationCanceledException) { } // 会话已结束
        catch (Exception) when (connection.IsClosed)
        {
            // 玩家主动退出时，轮询/提示任务可能与 channel 关闭并发；这是正常生命周期，不记录堆栈。
            _connections.TryRemove(connection, out _);
            Log.Debug("IRC: client disconnected before chat injection");
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "IRC: send failed");
            await InjectAsync(connection, IrcConstants.SendFailurePrefix + exception.Message).ConfigureAwait(false);
        }
    }

    private void RegisterConnection(MinecraftConnection connection)
    {
        if (Volatile.Read(ref _disposed) != 0 || !_connections.TryAdd(connection, 0)) return;
        Log.Information("IRC: game client joined, chat bridge active");
        lock (_pumpGate)
            if (_pump is null && Volatile.Read(ref _disposed) == 0) _pump = Task.Run(() => PumpAsync(_lifetime.Token));
        _ = Task.Run(() => WelcomeAsync(connection));
    }

    private async Task WelcomeAsync(MinecraftConnection connection)
    {
        try
        {
            // 稍等一下再提示，避开进服瞬间的加载提示刷屏。
            await Task.Delay(IrcConstants.WelcomeDelay, _lifetime.Token).ConfigureAwait(false);
            await InjectAsync(connection, IrcConstants.WelcomeMessagePrefix + IrcConstants.UsageHint + "。").ConfigureAwait(false);
        }
        catch (Exception) { } // 会话结束或注入失败都不影响主流程
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        // 首次轮询只用来对齐游标（服务端会返回最近 50 条，直接展示会把历史消息倒进游戏）。
        var primed = false;
        var nextUsageHint = DateTimeOffset.UtcNow.Add(IrcConstants.UsageHintInterval);
        var nextOnlineHint = DateTimeOffset.UtcNow.Add(IrcConstants.OnlineHintInterval);
        while (!cancellationToken.IsCancellationRequested)
        {
            var wait = _options.PollInterval;
            try
            {
                if (_connections.IsEmpty)
                    wait = IrcConstants.NoConnectionsDelay; // 无人在线不打扰服务器
                else
                {
                    var poll = await _session.PollAsync(cancellationToken).ConfigureAwait(false);
                    if (!poll.Success) wait = IrcConstants.PollFailureDelay;
                    else
                    {
                        if (primed) await RelayAsync(poll.Messages).ConfigureAwait(false);
                        primed = true;
                        var now = DateTimeOffset.UtcNow;
                        if (_options.ShowUsageHint && now >= nextUsageHint)
                        { nextUsageHint = now.Add(IrcConstants.UsageHintInterval); await BroadcastAsync(IrcConstants.UsageHint).ConfigureAwait(false); }
                        if (_options.ShowOnlineHint && now >= nextOnlineHint)
                        { nextOnlineHint = now.Add(IrcConstants.OnlineHintInterval); await BroadcastAsync(string.Format(IrcConstants.OnlineHintFormat, poll.Online)).ConfigureAwait(false); }
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Log.Warning(exception, "IRC: unexpected poll loop error");
                wait = IrcConstants.UnexpectedPollFailureDelay;
            }
            if (cancellationToken.IsCancellationRequested) break;
            await DelayAsync(wait, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>把一批聊天室消息注入游戏；自己的服务器回环只用来校准显示名，不再重复显示。</summary>
    private async Task RelayAsync(IReadOnlyList<IrcMessage> messages)
    {
        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Text)) continue;
            if (message.IsIrc)
            {
                LearnDisplayName(message.Sender);
                if (WasRecentlyEchoed(message.Text)) continue;
            }
            await BroadcastAsync(Format(message.Sender, message.Text)).ConfigureAwait(false);
        }
    }

    private async Task BroadcastAsync(string text)
    {
        foreach (var connection in _connections.Keys) await InjectAsync(connection, text).ConfigureAwait(false);
    }

    /// <summary>把一行文本注入游戏内聊天栏（系统聊天包）。</summary>
    private async Task InjectAsync(MinecraftConnection connection, string text)
    {
        try
        {
            // 只在双方都处于 Play 状态时注入：服务器切回 Configuration（重配置）时注入会让客户端收错包。
            if (connection.ClientState != ConnectionState.Play || connection.ServerState != ConnectionState.Play) return;
            if (connection.IsClosed)
            {
                _connections.TryRemove(connection, out _);
                return;
            }
            if (IrcProtocol.TryGetSpec(connection.Version) is not { } spec) return;
            var payload = IrcChatPayloads.BuildSystemChat(connection.Version, text);
            await connection.SendAsync(PacketDirection.ClientBound, spec.SystemChatId, payload, _lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { } // 会话已结束
        catch (Exception exception) when (connection.IsClosed)
        {
            _connections.TryRemove(connection, out _);
            Log.Debug(exception, "IRC: client disconnected before chat injection");
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
        var plain = IrcChatPayloads.SingleLine(text);
        return string.IsNullOrWhiteSpace(sender) ? plain : string.Format(IrcConstants.DisplayFormat, sender.Trim(), plain);
    }

    private void LearnDisplayName(string sender)
    {
        var name = sender.Trim();
        if (name.Length == 0 || string.Equals(name, _displayName, StringComparison.Ordinal)) return;
        _displayName = name;
        Log.Information("IRC: display name resolved to {Name}", name);
    }

    private void RememberLocalEcho(string content)
    {
        var now = DateTimeOffset.UtcNow;
        _recentLocalEcho[content] = now;
        if (_recentLocalEcho.Count <= IrcConstants.MaximumRecentEchoes) return;
        var stale = _recentLocalEcho.Where(pair => now - pair.Value > IrcConstants.RecentEchoLifetime).Select(pair => pair.Key).ToArray();
        foreach (var key in stale) _recentLocalEcho.TryRemove(key, out _);
    }

    /// <summary>服务器回环的消息是否就是本机刚回显过的那条（剥掉颜色码后按内容包含比较）。</summary>
    private bool WasRecentlyEchoed(string serverText)
    {
        var plain = IrcConstants.ColorCode().Replace(serverText, string.Empty);
        var now = DateTimeOffset.UtcNow;
        return _recentLocalEcho.Any(pair => now - pair.Value <= IrcConstants.RecentEchoLifetime
            && plain.Contains(pair.Key, StringComparison.Ordinal));
    }

    /// <summary>可取消的等待；被取消时静默返回，由轮询循环的条件结束整个任务。</summary>
    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try { await Task.Delay(delay, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { } // 会话已结束
    }
}
