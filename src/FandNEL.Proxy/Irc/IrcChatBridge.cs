using FandNEL.Proxy.Models;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;
using Serilog;

namespace FandNEL.Proxy.Irc;

/// <summary>把 Minecraft 聊天命令和匿名 IRC 会话接起来。</summary>
public sealed class IrcChatBridge : IAsyncDisposable
{
    private readonly IrcChatOptions _options;
    private readonly IrcChatSession _session;
    private readonly IrcChatDelivery _delivery;
    private readonly IrcLocalEchoTracker _echoes = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _pumpGate = new();
    private Task? _pump;
    private string _displayName = IrcConstants.UnknownPlayerName;
    private IProxySession? _boundSession;
    private int _disposed;

    public IrcChatBridge(IrcChatOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _session = new IrcChatSession(options);
        _delivery = new IrcChatDelivery(_lifetime.Token);
    }

    /// <summary>把拦截器注册进会话协议注册表（由 ProxyOptions.ConfigureRegistry 调用）。</summary>
    public void AttachRegistry(PacketRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        foreach (var spec in IrcProtocol.Specs)
        {
            foreach (var (packetId, isCommand) in GetInboundPackets(spec))
            {
                registry.Register(ConnectionState.Play, PacketDirection.ServerBound, packetId,
                    (context, _) => HandleChatPacketAsync(context, isCommand), [spec.Version]);
            }

            registry.Register(ConnectionState.Play, PacketDirection.ClientBound, spec.JoinGameId,
                (context, _) =>
                {
                    RegisterConnection(context.Connection);
                    return ValueTask.CompletedTask;
                }, [spec.Version]);
        }
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

        _boundSession = session;
        session.EventOccurred += OnSessionEvent;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_boundSession is not null)
            _boundSession.EventOccurred -= OnSessionEvent;
        _lifetime.Cancel();
        _delivery.Clear();
        if (_pump is not null)
        {
            try { await _pump.ConfigureAwait(false); }
            catch (Exception exception) { Log.Debug(exception, "IRC: poll loop stopped with an error"); }
        }

        _session.Dispose();
        _lifetime.Dispose();
    }

    private static IEnumerable<(int PacketId, bool IsCommand)> GetInboundPackets(IrcProtocol.VersionSpec spec)
    {
        if (spec.ChatMessageId is { } chatMessageId)
            yield return (chatMessageId, false);
        if (spec.ChatCommandId is { } chatCommandId)
            yield return (chatCommandId, true);
        if (spec.SignedChatCommandId is { } signedChatCommandId)
            yield return (signedChatCommandId, true);
    }

    private void OnSessionEvent(object? sender, ProxyEventArgs args)
    {
        if (args.Value.Kind is not (ProxyEventKind.Stopped or ProxyEventKind.Faulted))
            return;
        Log.Information("IRC: proxy session ended, bridge stopped");
        _ = DisposeAsync();
    }

    private ValueTask HandleChatPacketAsync(PacketContext context, bool isCommandPacket)
    {
        string text;
        try
        {
            text = context.CreateReader().ReadString(IrcConstants.ChatMessageMaximumLength);
        }
        catch (Exception)
        {
            return ValueTask.CompletedTask;
        }

        if (!IrcProtocol.TryParseIrcCommand(text, isCommandPacket, out var content))
            return ValueTask.CompletedTask;

        context.Cancel();
        RegisterConnection(context.Connection);
        if (content.Length == 0)
            _ = _delivery.InjectAsync(context.Connection, IrcConstants.EmptyCommandHint);
        else
            _ = SendAndEchoAsync(context.Connection, content);
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
                await _delivery.InjectAsync(connection, IrcConstants.SendFailurePrefix + result.Message)
                    .ConfigureAwait(false);
                return;
            }

            _echoes.Remember(content);
            await _delivery.BroadcastAsync(Format(_displayName, content)).ConfigureAwait(false);
            Log.Information("IRC: message sent: {Text}", content);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception) when (connection.IsClosed)
        {
            _delivery.Remove(connection);
            Log.Debug("IRC: client disconnected before chat injection");
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "IRC: send failed");
            await _delivery.InjectAsync(connection, IrcConstants.SendFailurePrefix + exception.Message)
                .ConfigureAwait(false);
        }
    }

    private void RegisterConnection(MinecraftConnection connection)
    {
        UpdatePlayerName(connection.PlayerName);
        if (Volatile.Read(ref _disposed) != 0 || !_delivery.TryAdd(connection))
            return;

        Log.Information("IRC: game client joined, chat bridge active");
        lock (_pumpGate)
        {
            if (_pump is null && Volatile.Read(ref _disposed) == 0)
                _pump = new IrcChatPump(_options, _session, _delivery, RelayAsync).RunAsync(_lifetime.Token);
        }
        _ = WelcomeAsync(connection);
    }

    private async Task WelcomeAsync(MinecraftConnection connection)
    {
        try
        {
            await Task.Delay(IrcConstants.WelcomeDelay, _lifetime.Token).ConfigureAwait(false);
            await _delivery.InjectAsync(connection, IrcConstants.WelcomeMessagePrefix)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Log.Debug(exception, "IRC: welcome message was not delivered");
        }
    }

    private async Task RelayAsync(IReadOnlyList<IrcMessage> messages)
    {
        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Text))
                continue;
            if (message.IsIrc)
            {
                if (_echoes.Matches(message.Text))
                    continue;
            }

            await _delivery.BroadcastAsync(Format(message.Sender, message.Text)).ConfigureAwait(false);
        }
    }

    private void UpdatePlayerName(string? playerName)
    {
        var name = playerName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, _displayName, StringComparison.Ordinal))
            return;

        _displayName = name;
        _session.SetPlayerName(name);
        Log.Information("IRC: player identity resolved to {Name}", name);
    }

    private static string Format(string sender, string text)
    {
        var plain = IrcChatPayloads.SingleLine(text);
        return string.IsNullOrWhiteSpace(sender) ? plain : string.Format(IrcConstants.DisplayFormat, sender.Trim(), plain);
    }
}
