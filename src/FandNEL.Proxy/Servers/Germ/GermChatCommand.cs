using FandNEL.Proxy.Irc;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;

namespace FandNEL.Proxy.Servers.Germ;

/// <summary>聊天栏的 germ 按钮命令与输入框交互；优先级高于 IRC，避免被聊天桥接管。</summary>
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 0x01, ProtocolVersion.V108X, Priority = 10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, Packet.Minecraft.V1122.MinecraftPacketIds.Serverbound.ChatMessage, ProtocolVersion.V1122, Priority = 10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 0x05, ProtocolVersion.V1200, Priority = 10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 0x04, ProtocolVersion.V1200, Priority = 10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 0x06, ProtocolVersion.V1206, Priority = 10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 0x04, ProtocolVersion.V1206, Priority = 10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 0x05, ProtocolVersion.V1206, Priority = 10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 0x08, ProtocolVersion.V1218, Priority = 10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 0x06, ProtocolVersion.V1218, Priority = 10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 0x07, ProtocolVersion.V1218, Priority = 10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 0x08, ProtocolVersion.V12110, Priority = 10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 0x06, ProtocolVersion.V12110, Priority = 10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 0x07, ProtocolVersion.V12110, Priority = 10)]
public sealed class GermChatCommand : IPacketHandler
{
    private const string ButtonClickCommand = "germ-button-click";
    private const string ActivateInputCommand = "germ-activate-input";
    private const string ExpiredHint = "§c§l#操作失败#当前菜单已过期#";
    private const string ActivatedHint = "§a§l输入框已激活！请在聊天栏输入你要提交的文本！";

    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        if (!GermProtocol.IsEnabled(context.Connection))
            return ValueTask.CompletedTask;

        string text;
        try
        {
            text = context.CreateReader().ReadString(IrcConstants.ChatMessageMaximumLength);
        }
        catch (Exception)
        {
            return ValueTask.CompletedTask;
        }
        if (string.IsNullOrWhiteSpace(text))
            return ValueTask.CompletedTask;

        var state = context.Connection.ServerProtocols;
        if (state.ActiveInputField is not null)
        {
            _ = SubmitInputAsync(context, text, cancellationToken);
            context.Cancel();
            return ValueTask.CompletedTask;
        }

        var trimmed = text.Trim();
        if (!IsCommandPacket(context.Version, context.PacketId) && !trimmed.StartsWith('/') && !trimmed.StartsWith('.'))
            return ValueTask.CompletedTask;

        var body = trimmed.TrimStart('/', '.').Trim();
        var separator = body.IndexOf(' ');
        var name = separator < 0 ? body : body[..separator];
        var argument = separator < 0 ? string.Empty : body[(separator + 1)..].Trim();

        if (name.Equals(ButtonClickCommand, StringComparison.OrdinalIgnoreCase))
        {
            _ = HandleButtonClickAsync(context, argument, cancellationToken);
            context.Cancel();
        }
        else if (name.Equals(ActivateInputCommand, StringComparison.OrdinalIgnoreCase))
        {
            _ = HandleActivateInputAsync(context, argument, cancellationToken);
            context.Cancel();
        }
        return ValueTask.CompletedTask;
    }

    private static bool IsCommandPacket(ProtocolVersion version, int packetId) => version switch
    {
        ProtocolVersion.V1200 => packetId == 0x04,
        ProtocolVersion.V1206 => packetId is 0x04 or 0x05,
        ProtocolVersion.V1218 => packetId is 0x06 or 0x07,
        ProtocolVersion.V12110 => packetId is 0x06 or 0x07,
        _ => false
    };

    private static async Task HandleButtonClickAsync(PacketContext context, string key, CancellationToken cancellationToken)
    {
        var connection = context.Connection;
        var state = connection.ServerProtocols;
        if (key.Length == 0 || !state.GuiClickMap.TryGetValue(key, out var packets))
        {
            await SendHintAsync(context, ExpiredHint, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            foreach (var payload in packets)
                await ServerPluginMessages.SendAsync(connection, GermProtocol.ClientChannel, payload, cancellationToken)
                    .ConfigureAwait(false);
        }
        finally
        {
            state.GuiClickMap.Clear();
        }
    }

    private static async Task HandleActivateInputAsync(PacketContext context, string key, CancellationToken cancellationToken)
    {
        var state = context.Connection.ServerProtocols;
        if (key.Length == 0 || !state.GuiActiveInputMap.TryGetValue(key, out var field))
        {
            await SendHintAsync(context, ExpiredHint, cancellationToken).ConfigureAwait(false);
            return;
        }

        state.ActiveInputField = field;
        await SendHintAsync(context, ActivatedHint, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SubmitInputAsync(PacketContext context, string message, CancellationToken cancellationToken)
    {
        var connection = context.Connection;
        var state = connection.ServerProtocols;
        var field = state.ActiveInputField;
        if (field is null)
            return;
        state.GuiActiveInputMap.Clear();
        state.ActiveInputField = null;

        await SendHintAsync(context, $"§d§l正在尝试为[{field.GuiUuid}]提交[{message}]!", cancellationToken)
            .ConfigureAwait(false);
        var payloads = new[]
        {
            GermPackets.BuildGuiClick(field.GuiUuid, field.ButtonPath, 0),
            GermPackets.BuildScriptInput($"GUI${field.GuiUuid}@input", $"{{\"input\":\"{message}\"}}"),
            GermPackets.BuildGuiClose(field.GuiUuid)
        };
        foreach (var payload in payloads)
            await ServerPluginMessages.SendAsync(connection, GermProtocol.ClientChannel, payload, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task SendHintAsync(PacketContext context, string text, CancellationToken cancellationToken)
    {
        try
        {
            var payload = IrcChatPayloads.BuildSystemChat(context.Version, text);
            await context.Connection.SendAsync(PacketDirection.ClientBound,
                GermProtocol.GetSystemChatPacketId(context.Version), payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 客户端已经断开时忽略反馈消息的发送失败。
        }
    }
}
