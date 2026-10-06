using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;
using Serilog;

namespace FandNEL.Proxy.Servers.Germ;

/// <summary>OMG 与SuYuan共用的 germ 上下行处理。</summary>
internal static class GermProtocol
{
    /// <summary>服务端下发使用的通道。</summary>
    internal const string ServerChannel = "germplugin";

    /// <summary>代理上行使用的通道。</summary>
    internal const string ClientChannel = "germmod";

    private const int MaximumResponseLength = 999_999;

    internal static bool IsEnabled(MinecraftConnection connection) =>
        ServerGameIds.IsGame(connection, ServerGameIds.Omg) || ServerGameIds.IsGame(connection, ServerGameIds.SuYuan);

    /// <summary>服务端 → 客户端：缓存 GUI 内容并在查询时把按钮渲染到聊天栏。</summary>
    internal static ValueTask HandleClientboundAsync(PacketContext context, byte[] data, CancellationToken cancellationToken)
    {
        var germId = GermPackets.TryReadId(data);
        if (germId is null)
            return ValueTask.CompletedTask;

        switch (germId.Value)
        {
            case GermPackets.IdPassThrough1:
            case GermPackets.IdPassThrough2:
                return ValueTask.CompletedTask;
            case GermPackets.IdFragment:
                return HandleFragmentAsync(context, data, cancellationToken);
            case GermPackets.IdGuiResponse:
                CacheGuiResponse(context, data);
                return ValueTask.CompletedTask;
            case GermPackets.IdQueryCache:
                return HandleQueryAsync(context, data, cancellationToken);
            case GermPackets.IdSuYuanVerify:
                if (!ServerGameIds.IsGame(context.Connection, ServerGameIds.SuYuan))
                    return ValueTask.CompletedTask;
                return HandleVerifyAsync(context, data, cancellationToken);
            default:
                return ValueTask.CompletedTask;
        }
    }

    /// <summary>客户端 → 服务端：溯源代发 ClientLink，OMG 只吞掉客户端自行上报的 ClientLink。</summary>
    internal static async ValueTask HandleServerboundAsync(PacketContext context, byte[] data, CancellationToken cancellationToken)
    {
        var germId = GermPackets.TryReadId(data);
        if (germId is null)
            return;

        if (ServerGameIds.IsGame(context.Connection, ServerGameIds.Omg))
        {
            if (germId.Value == GermPackets.IdClientLink)
                context.Cancel();
            return;
        }

        if (!ServerGameIds.IsGame(context.Connection, ServerGameIds.SuYuan))
            return;

        if (germId.Value != GermPackets.IdClientLink)
            return;

        var payload = GermPackets.BuildClientLink(BuildClientLinkJson(context.Connection));
        await ServerPluginMessages.SendAsync(context.Connection, ClientChannel, payload, cancellationToken)
            .ConfigureAwait(false);
        context.Cancel();
    }

    /// <summary>溯源引擎自检：应答 verify 后吞掉该包。</summary>
    private static async ValueTask HandleVerifyAsync(PacketContext context, byte[] data, CancellationToken cancellationToken)
    {
        var text = ReadPrefixedString(data, sizeof(int));
        if (text.Contains("verify", StringComparison.Ordinal) && text.Contains("install_engine", StringComparison.Ordinal))
        {
            var payload = GermPackets.BuildActionResponse("{\"name\":\"verify\",\"data\":\"has\"}");
            await ServerPluginMessages.SendAsync(context.Connection, ClientChannel, payload, cancellationToken)
                .ConfigureAwait(false);
        }
        context.Cancel();
    }

    private static ValueTask HandleFragmentAsync(PacketContext context, byte[] data, CancellationToken cancellationToken)
    {
        var state = context.Connection.ServerProtocols;
        GermFragment fragment;
        byte[]? chunk;
        try
        {
            var reader = new PacketReader(data);
            reader.ReadInt();
            fragment = state.GermFragment ??= new GermFragment();
            fragment.Start = reader.ReadBoolean();
            fragment.FullLength = reader.ReadInt();
            fragment.End = reader.ReadBoolean();
            chunk = reader.ReadByteArray();
        }
        catch (Exception)
        {
            state.GermFragment = null;
            return ValueTask.CompletedTask;
        }

        byte[]? complete;
        try
        {
            complete = fragment.Append(chunk);
        }
        catch (Exception exception)
        {
            Log.Debug(exception, "Germ 分片组装失败，连接 {ConnectionId}", context.Connection.Id);
            state.GermFragment = null;
            return ValueTask.CompletedTask;
        }

        if (complete is null)
            return ValueTask.CompletedTask;
        state.GermFragment = null;
        return HandleClientboundAsync(context, complete, cancellationToken);
    }

    private static void CacheGuiResponse(PacketContext context, byte[] data)
    {
        var isSuYuan = ServerGameIds.IsGame(context.Connection, ServerGameIds.SuYuan);
        try
        {
            var reader = new PacketReader(data);
            reader.ReadInt();
            var top = reader.ReadString();
            var node = reader.ReadString();
            if (isSuYuan)
                reader.ReadByte();
            var content = reader.ReadString(MaximumResponseLength);
            if (top == "gui")
                context.Connection.ServerProtocols.GermGuiCache[node] = new GermGuiResponse(top, node, content);
        }
        catch (Exception exception)
        {
            Log.Debug(exception, "Germ GUI 响应解析失败，连接 {ConnectionId}", context.Connection.Id);
        }
    }

    private static ValueTask HandleQueryAsync(PacketContext context, byte[] data, CancellationToken cancellationToken)
    {
        string key;
        try
        {
            var reader = new PacketReader(data);
            reader.ReadInt();
            key = reader.ReadString();
        }
        catch (Exception)
        {
            return ValueTask.CompletedTask;
        }

        var state = context.Connection.ServerProtocols;
        if (!state.GermGuiCache.TryGetValue(key, out var response))
            return ValueTask.CompletedTask;
        var isSuYuan = ServerGameIds.IsGame(context.Connection, ServerGameIds.SuYuan);
        if (isSuYuan && key == "区服选择主界面" && state.GermGuiCache.TryGetValue("rpg区服选择子界面1", out var fallback))
            response = fallback;
        if (!TryRenderButtons(context, response, cancellationToken))
            return ValueTask.CompletedTask;
        context.Cancel();
        return ValueTask.CompletedTask;
    }

    /// <summary>关闭原生 GUI、给服务端回发 GUIOpen，并把按钮渲染到聊天栏。</summary>
    private static bool TryRenderButtons(PacketContext context, GermGuiResponse response, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(response.Top) || response.Top != "gui"
            || string.IsNullOrWhiteSpace(response.Node) || string.IsNullOrWhiteSpace(response.Content))
            return false;

        var connection = context.Connection;
        var buttons = GermMenu.Parse(response.Content, response.Node, connection.PlayerName ?? string.Empty);
        if (buttons.Count == 0)
            return false;

        var state = connection.ServerProtocols;
        foreach (var button in buttons)
            state.GuiClickMap[button.ButtonPath] = button.ClickAction.ToArray();

        _ = ServerPluginMessages.SendAsync(connection, ClientChannel, GermPackets.BuildGuiClose(response.Node), cancellationToken);
        _ = ServerPluginMessages.SendAsync(connection, ClientChannel, GermPackets.BuildGuiOpen(response.Node), cancellationToken);
        _ = SendButtonsChatAsync(context, buttons, cancellationToken);
        return true;
    }

    private static async Task SendButtonsChatAsync(PacketContext context, IReadOnlyList<GermComponent> buttons,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = GermChatPayloads.BuildButtons(context.Version, buttons);
            await context.Connection.SendAsync(PacketDirection.ClientBound, GetSystemChatPacketId(context.Version),
                payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Log.Debug(exception, "Germ 按钮聊天消息发送失败，连接 {ConnectionId}", context.Connection.Id);
        }
    }

    internal static int GetSystemChatPacketId(ProtocolVersion version) => version switch
    {
        ProtocolVersion.V108X => 0x02,
        ProtocolVersion.V1122 => Packet.Minecraft.V1122.MinecraftPacketIds.Clientbound.ChatMessage,
        ProtocolVersion.V1200 => Packet.Minecraft.V1200.MinecraftPacketIds.Clientbound.SystemChat,
        ProtocolVersion.V1206 => Packet.Minecraft.V1206.MinecraftPacketIds.Clientbound.SystemChat,
        ProtocolVersion.V1218 => Packet.Minecraft.V1218.MinecraftPacketIds.Clientbound.SystemChat,
        ProtocolVersion.V12110 => Packet.Minecraft.V12110.MinecraftPacketIds.Clientbound.SystemChat,
        _ => throw new NotSupportedException($"[FandNEL] 尚未支持在协议 {version} 下发送 germ 聊天消息。")
    };

    private static string ReadPrefixedString(byte[] data, int offset)
    {
        if (offset >= data.Length)
            return string.Empty;
        try
        {
            var reader = new PacketReader(data);
            for (var i = 0; i < offset; i++)
                reader.ReadByte();
            return reader.ReadString();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>溯源要求上行 ClientLink 带上伪造的设备与账号信息。</summary>
    private static string BuildClientLinkJson(MinecraftConnection connection)
    {
        var name = connection.PlayerName ?? string.Empty;
        return JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["netease"] = "yd." + Md5Hex("M-I-r-a-c-l-e:EMAIL:" + name)[..16] + "@163.com",
            ["modIdentity"] = "39e6df31a6ad4d08a8c750a9ca232113",
            ["ip"] = GenerateIp(name),
            ["qq"] = GenerateQq(name),
            ["machine"] = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    Sha256Hex(name + ":T").ToUpperInvariant() + Sha256Hex(name + ":M").ToUpperInvariant()
                    + Sha256Hex(name + ":L").ToUpperInvariant() + Sha256Hex(name + ":B").ToUpperInvariant())) + "=",
            ["engineVersion"] = "4.4.0",
            ["joinIp"] = null!
        });
    }

    private static string GenerateIp(string name)
    {
        try
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
            var value = BitConverter.ToUInt32(hash, 0);
            return $"{(value >> 24) & 0xFF}.{(value >> 16) & 0xFF}.{(value >> 8) & 0xFF}.{value & 0xFF}";
        }
        catch (Exception)
        {
            return "192.168.1.1";
        }
    }

    private static string GenerateQq(string name)
    {
        var hash = Sha256Hex(name);
        var value = Convert.ToInt64(hash[..13], 16);
        return (value % 1_000_000_000_000L).ToString("D10");
    }

    private static string Md5Hex(string value) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
