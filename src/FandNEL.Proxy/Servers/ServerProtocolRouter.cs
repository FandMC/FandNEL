using System.Text.Json;
using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Servers.Germ;
using FandNEL.Proxy.Sessions;
using V108XIds = FandNEL.Proxy.Packet.Minecraft.V108X.MinecraftPacketIds;
using V1122Ids = FandNEL.Proxy.Packet.Minecraft.V1122.MinecraftPacketIds;
using V1200Ids = FandNEL.Proxy.Packet.Minecraft.V1200.MinecraftPacketIds;
using V1206Ids = FandNEL.Proxy.Packet.Minecraft.V1206.MinecraftPacketIds;
using V1218Ids = FandNEL.Proxy.Packet.Minecraft.V1218.MinecraftPacketIds;
using V12110Ids = FandNEL.Proxy.Packet.Minecraft.V12110.MinecraftPacketIds;

namespace FandNEL.Proxy.Servers;

/// <summary>Play 阶段自定义载荷的总入口：按通道分发到本次迁移的各个服务器协议。</summary>
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, V108XIds.Serverbound.PluginMessage, ProtocolVersion.V108X)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, V108XIds.Clientbound.PluginMessage, ProtocolVersion.V108X)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, V1122Ids.Serverbound.PluginMessage, ProtocolVersion.V1122)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, V1122Ids.Clientbound.PluginMessage, ProtocolVersion.V1122)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, V1200Ids.Serverbound.CustomPayload, ProtocolVersion.V1200)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, V1200Ids.Clientbound.CustomPayload, ProtocolVersion.V1200)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, V1206Ids.Serverbound.CustomPayload, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, V1206Ids.Clientbound.CustomPayload, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, V1218Ids.Serverbound.CustomPayload, ProtocolVersion.V1218)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, V1218Ids.Clientbound.CustomPayload, ProtocolVersion.V1218)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, V12110Ids.Serverbound.CustomPayload, ProtocolVersion.V12110)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, V12110Ids.Clientbound.CustomPayload, ProtocolVersion.V12110)]
public sealed class ServerProtocolRouter : IPacketHandler
{
    private const string RegisterChannel = "REGISTER";
    private const string ModernRegisterChannel = "minecraft:register";

    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        if (!ServerGameIds.IsEnabled(context.Connection))
            return ValueTask.CompletedTask;

        string channel;
        byte[] data;
        try
        {
            var reader = context.CreateReader();
            channel = reader.ReadString();
            data = reader.ReadBytes(reader.Remaining);
        }
        catch (Exception)
        {
            return ValueTask.CompletedTask;
        }

        return context.Direction == PacketDirection.ServerBound
            ? HandleServerboundAsync(context, channel, data, cancellationToken)
            : HandleClientboundAsync(context, channel, data, cancellationToken);
    }

    private static ValueTask HandleServerboundAsync(PacketContext context, string channel, byte[] data,
        CancellationToken cancellationToken)
    {
        switch (channel)
        {
            case AelyProtocol.CheckChannel:
                // Aely 的校验通道只由代理应答，客户端原样内容一律不放行。
                TryReadAelyPayload(data);
                context.Cancel();
                return ValueTask.CompletedTask;
            case ForgeSpoof.BrandOldChannel:
                return ForgeSpoof.HandleServerBrandAsync(context, data, cancellationToken);
            case ForgeSpoof.BrandChannel:
                return ForgeSpoof.HandleServerBrandModernAsync(context, data, cancellationToken);
            case ForgeSpoof.ForgeChannel:
                return ForgeSpoof.HandleServerForgeAsync(context, data, cancellationToken);
            case RegisterChannel:
                return ChannelRegister.HandleServerRegisterAsync(context, data, modernChannel: false, cancellationToken);
            case ModernRegisterChannel:
                return ChannelRegister.HandleServerRegisterAsync(context, data, modernChannel: true, cancellationToken);
            case DfdlProtocol.Channel:
                return DfdlProtocol.HandleServerboundAsync(context, data, cancellationToken);
            case GermProtocol.ClientChannel:
                return GermProtocol.HandleServerboundAsync(context, data, cancellationToken);
            default:
                return ValueTask.CompletedTask;
        }
    }

    private static ValueTask HandleClientboundAsync(PacketContext context, string channel, byte[] data,
        CancellationToken cancellationToken)
    {
        switch (channel)
        {
            case AelyProtocol.CheckChannel:
                return HandleAelyCheckAsync(context, data, cancellationToken);
            case ForgeSpoof.BrandOldChannel:
                return ForgeSpoof.HandleClientBrandAsync(context, data, cancellationToken);
            case ForgeSpoof.ForgeChannel:
                return ForgeSpoof.HandleClientForgeAsync(context, data, cancellationToken);
            case DfdlProtocol.Channel:
                return DfdlProtocol.HandleClientboundAsync(context, data, cancellationToken);
            case GermProtocol.ServerChannel:
                return GermProtocol.HandleClientboundAsync(context, data, cancellationToken);
            default:
                // 频道注册与其余通道保持原样转发。
                return ValueTask.CompletedTask;
        }
    }

    private static bool TryReadAelyPayload(byte[] data)
    {
        try
        {
            AelyProtocol.ReadPrefixedString(data);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async ValueTask HandleAelyCheckAsync(PacketContext context, byte[] data,
        CancellationToken cancellationToken)
    {
        context.Cancel();
        try
        {
            var payload = AelyProtocol.ReadPrefixedString(data);
            if (!payload.Contains("ClientHub_Code", StringComparison.Ordinal))
                return;
            using var document = JsonDocument.Parse(payload);
            var clientHubCode = document.RootElement.GetProperty("ClientHub_Code").GetString();
            if (string.IsNullOrEmpty(clientHubCode))
                return;

            var connection = context.Connection;
            var checkJson = AelyProtocol.BuildCheckJson(connection.PlayerName ?? string.Empty,
                connection.PlayerUuid?.ToString() ?? string.Empty, clientHubCode,
                ModInfoParser.Parse(connection.Options.ModInfo));
            await ServerPluginMessages.SendAsync(connection, AelyProtocol.CheckChannel,
                AelyProtocol.WritePrefixedString(checkJson), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 与参考实现一致：质询解析失败时静默丢弃该包。
        }
    }
}
