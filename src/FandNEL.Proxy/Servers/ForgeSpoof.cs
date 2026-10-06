using System.Text;
using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;

namespace FandNEL.Proxy.Servers;

/// <summary>按 GameId 门控的 Forge 伪造：品牌伪装与 FML|HS 握手应答。</summary>
internal static class ForgeSpoof
{
    internal const string BrandOldChannel = "MC|Brand";
    internal const string BrandChannel = "minecraft:brand";
    internal const string ForgeChannel = "FML|HS";
    internal const string RegisterChannel = "REGISTER";
    internal const string ForgeBrandLegacy = "fml,forge";
    internal const string ForgeBrandModern = "forge";

    internal static bool IsEnabled(MinecraftConnection connection) => ServerGameIds.IsEnabled(connection);

    /// <summary>处理服务端下发的品牌包：Aely 丢弃并按条件补发固定频道表，其余游戏透传。</summary>
    internal static ValueTask HandleClientBrandAsync(PacketContext context, byte[] data, CancellationToken cancellationToken)
    {
        if (!AelyProtocol.IsEnabled(context.Connection))
            return ValueTask.CompletedTask;
        if (Encoding.UTF8.GetString(data).Contains("<- Spigot", StringComparison.Ordinal))
            _ = SendRegisterAsync(context.Connection, AelyProtocol.ChannelList, cancellationToken);
        context.Cancel();
        return ValueTask.CompletedTask;
    }

    /// <summary>处理客户端上行品牌包，伪装成 Forge 客户端。</summary>
    internal static async ValueTask HandleServerBrandAsync(PacketContext context, byte[] data, CancellationToken cancellationToken)
    {
        var state = context.Connection.ServerProtocols;
        var text = Encoding.UTF8.GetString(data);
        if (text.Contains(ForgeBrandLegacy, StringComparison.Ordinal))
            return;

        state.IsVanilla = true;
        if (text.Contains("lunarclient", StringComparison.Ordinal))
            state.LunarClient = true;
        if (AelyProtocol.IsEnabled(context.Connection))
        {
            context.Cancel();
            return;
        }

        await SendBrandAsync(context.Connection, BrandOldChannel, ForgeBrandLegacy, cancellationToken).ConfigureAwait(false);
        if (CzsczlProtocol.IsEnabled(context.Connection))
            await SendRegisterAsync(context.Connection, CzsczlProtocol.ChannelList, cancellationToken).ConfigureAwait(false);
        context.Cancel();
    }

    /// <summary>处理 1.13+ 的 minecraft:brand，统一伪装为 forge。</summary>
    internal static async ValueTask HandleServerBrandModernAsync(PacketContext context, byte[] data, CancellationToken cancellationToken)
    {
        var text = Encoding.UTF8.GetString(data);
        var state = context.Connection.ServerProtocols;
        if (!text.Contains("forge", StringComparison.Ordinal))
        {
            state.IsVanilla = true;
            if (text.Contains("lunarclient", StringComparison.Ordinal))
                state.LunarClient = true;
        }

        await SendBrandAsync(context.Connection, BrandChannel, ForgeBrandModern, cancellationToken).ConfigureAwait(false);
        context.Cancel();
    }

    /// <summary>客户端上行的 FML|HS：真实 Forge 客户端透传，原版客户端由 Czsczl 用固定模组列表替换。</summary>
    internal static async ValueTask HandleServerForgeAsync(PacketContext context, byte[] data, CancellationToken cancellationToken)
    {
        if (data.Length == 0)
            return;
        var state = context.Connection.ServerProtocols;
        if (data[0] == 1)
        {
            // 客户端主动发起 ClientHello，说明它是真正的 Forge 客户端，后续握手原样透传。
            state.IsForgeClient = true;
            return;
        }
        if (state.IsForgeClient)
            return;
        if (data[0] == 2 && CzsczlProtocol.IsEnabled(context.Connection))
        {
            await ServerPluginMessages.SendAsync(context.Connection, ForgeChannel,
                CzsczlProtocol.FmlModListData, cancellationToken).ConfigureAwait(false);
            context.Cancel();
        }
    }

    /// <summary>服务端下行的 FML|HS：为原版客户端补上握手应答。</summary>
    internal static async ValueTask HandleClientForgeAsync(PacketContext context, byte[] data, CancellationToken cancellationToken)
    {
        if (data.Length == 0)
            return;
        var state = context.Connection.ServerProtocols;
        switch (data[0])
        {
            case 0:
            {
                if (!TryReadServerHello(data, out var protocolVersion))
                    return;
                state.FmlProtocolVersion = protocolVersion;
                if (!state.IsVanilla)
                    return;
                if (CzsczlProtocol.IsEnabled(context.Connection))
                {
                    using var hello = new PacketWriter();
                    var payload = hello.WriteByte(1).WriteByte(protocolVersion).ToArray();
                    await ServerPluginMessages.SendAsync(context.Connection, ForgeChannel, payload, cancellationToken)
                        .ConfigureAwait(false);
                    await ServerPluginMessages.SendAsync(context.Connection, ForgeChannel,
                        CzsczlProtocol.FmlModListData, cancellationToken).ConfigureAwait(false);
                }
                context.Cancel();
                return;
            }
            case 2 when state.IsVanilla:
            {
                using var ack = new PacketWriter();
                var payload = ack.WriteByte(byte.MaxValue).WriteByte(2).ToArray();
                await ServerPluginMessages.SendAsync(context.Connection, ForgeChannel, payload, cancellationToken)
                    .ConfigureAwait(false);
                context.Cancel();
                return;
            }
            default:
                return;
        }
    }

    private static bool TryReadServerHello(byte[] data, out byte protocolVersion)
    {
        protocolVersion = 0;
        if (data.Length < 2)
            return false;
        protocolVersion = data[1];
        // 协议版本大于 1 时服务端会多带一个 OverrideDimension，这里只校验载荷边界。
        return protocolVersion <= 1 || data.Length >= 6;
    }

    private static async Task SendBrandAsync(MinecraftConnection connection, string channel, string brand,
        CancellationToken cancellationToken)
    {
        using var writer = new PacketWriter();
        var payload = writer.WriteString(brand).ToArray();
        await ServerPluginMessages.SendAsync(connection, channel, payload, cancellationToken).ConfigureAwait(false);
    }

    internal static Task SendRegisterAsync(MinecraftConnection connection, byte[] channels, CancellationToken cancellationToken) =>
        ServerPluginMessages.SendAsync(connection, RegisterChannel, channels, cancellationToken);
}
