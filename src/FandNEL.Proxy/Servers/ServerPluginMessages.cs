using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;
using V108XIds = FandNEL.Proxy.Packet.Minecraft.V108X.MinecraftPacketIds;
using V1122Ids = FandNEL.Proxy.Packet.Minecraft.V1122.MinecraftPacketIds;
using V1200Ids = FandNEL.Proxy.Packet.Minecraft.V1200.MinecraftPacketIds;
using V1206Ids = FandNEL.Proxy.Packet.Minecraft.V1206.MinecraftPacketIds;
using V1218Ids = FandNEL.Proxy.Packet.Minecraft.V1218.MinecraftPacketIds;
using V12110Ids = FandNEL.Proxy.Packet.Minecraft.V12110.MinecraftPacketIds;

namespace FandNEL.Proxy.Servers;

/// <summary>迁移协议向服务端回发的自定义载荷；统一按版本选择 Play 阶段的包标识。</summary>
internal static class ServerPluginMessages
{
    /// <summary>把一个自定义载荷发给服务端。</summary>
    internal static async Task SendAsync(MinecraftConnection connection, string channel, byte[] data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(data);
        var packetId = GetServerboundPacketId(connection.Version);
        using var writer = new PacketWriter();
        var payload = writer.WriteString(channel).WriteBytes(data).ToArray();
        await connection.SendAsync(PacketDirection.ServerBound, packetId, payload, cancellationToken)
            .ConfigureAwait(false);
    }

    private static int GetServerboundPacketId(ProtocolVersion version) => version switch
    {
        ProtocolVersion.V108X => V108XIds.Serverbound.PluginMessage,
        ProtocolVersion.V1122 => V1122Ids.Serverbound.PluginMessage,
        ProtocolVersion.V1200 => V1200Ids.Serverbound.CustomPayload,
        ProtocolVersion.V1206 => V1206Ids.Serverbound.CustomPayload,
        ProtocolVersion.V1218 => V1218Ids.Serverbound.CustomPayload,
        ProtocolVersion.V12110 => V12110Ids.Serverbound.CustomPayload,
        _ => throw new NotSupportedException($"[FandNEL] 尚未支持在协议 {version} 下回发自定义载荷。")
    };
}
