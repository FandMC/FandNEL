using System.Collections.Concurrent;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;
using Serilog;

namespace FandNEL.Proxy.Irc;

/// <summary>管理当前代理中的 Minecraft 连接，并统一处理 IRC 消息注入。</summary>
internal sealed class IrcChatDelivery(CancellationToken cancellationToken)
{
    private readonly ConcurrentDictionary<MinecraftConnection, byte> _connections = new();

    internal bool IsEmpty => _connections.IsEmpty;

    internal bool TryAdd(MinecraftConnection connection) => _connections.TryAdd(connection, 0);

    internal void Remove(MinecraftConnection connection) => _connections.TryRemove(connection, out _);

    internal void Clear() => _connections.Clear();

    internal async Task BroadcastAsync(string text)
    {
        foreach (var connection in _connections.Keys)
            await InjectAsync(connection, text).ConfigureAwait(false);
    }

    internal async Task InjectAsync(MinecraftConnection connection, string text)
    {
        try
        {
            if (connection.ClientState != ConnectionState.Play || connection.ServerState != ConnectionState.Play)
                return;
            if (connection.IsClosed)
            {
                Remove(connection);
                return;
            }
            if (IrcProtocol.TryGetSpec(connection.Version) is not { } spec)
                return;

            var payload = IrcChatPayloads.BuildSystemChat(connection.Version, text);
            await connection.SendAsync(PacketDirection.ClientBound, spec.SystemChatId, payload, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (connection.IsClosed)
        {
            Remove(connection);
            Log.Debug(exception, "IRC: client disconnected before chat injection");
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "IRC: failed to inject chat message, dropping connection");
            Remove(connection);
        }
    }
}
