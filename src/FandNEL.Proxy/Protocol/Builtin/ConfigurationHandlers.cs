using V1206Ids = FandNEL.Proxy.Packet.Minecraft.V1206.MinecraftPacketIds;
using V1218Ids = FandNEL.Proxy.Packet.Minecraft.V1218.MinecraftPacketIds;
using V12110Ids = FandNEL.Proxy.Packet.Minecraft.V12110.MinecraftPacketIds;

namespace FandNEL.Proxy.Protocol.Builtin;

[RegisterPacket(ConnectionState.Configuration, PacketDirection.ClientBound, V1206Ids.Configuration.Finish)]
public sealed class FinishConfigurationHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        // FinishConfiguration 本身不改变状态；客户端仍需发送确认包。
        return ValueTask.CompletedTask;
    }
}

[RegisterPacket(ConnectionState.Configuration, PacketDirection.ServerBound, V1206Ids.Configuration.Finish)]
public sealed class AcknowledgeFinishConfigurationHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        // 确认包仍属于 Configuration；转发前不能让插件回复抢先使用 Play 包 ID。
        context.AfterForward(() =>
        {
            context.Connection.ClientState = ConnectionState.Play;
            context.Connection.ServerState = ConnectionState.Play;
            return ValueTask.CompletedTask;
        });
        return ValueTask.CompletedTask;
    }
}

[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, V1206Ids.Clientbound.StartConfiguration, ProtocolVersion.V1206, ProtocolVersion.V1210)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, V1218Ids.Clientbound.StartConfiguration, ProtocolVersion.V1218)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, V12110Ids.Clientbound.StartConfiguration, ProtocolVersion.V12110)]
public sealed class StartConfigurationHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        // 客户端确认包仍属于 Play，两个方向不能提前同时切状态。
        context.Connection.ServerState = ConnectionState.Configuration;
        context.Connection.ClientState = ConnectionState.Configuration;
        return ValueTask.CompletedTask;
    }
}

[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, V1206Ids.Serverbound.AcknowledgeConfiguration, ProtocolVersion.V1206, ProtocolVersion.V1210)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, V1218Ids.Serverbound.AcknowledgeConfiguration, ProtocolVersion.V1218)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, V12110Ids.Serverbound.AcknowledgeConfiguration, ProtocolVersion.V12110)]
[RegisterPacket(ConnectionState.Configuration, PacketDirection.ServerBound, V1206Ids.Serverbound.AcknowledgeConfiguration, ProtocolVersion.V1206, ProtocolVersion.V1210)]
[RegisterPacket(ConnectionState.Configuration, PacketDirection.ServerBound, V1218Ids.Serverbound.AcknowledgeConfiguration, ProtocolVersion.V1218)]
[RegisterPacket(ConnectionState.Configuration, PacketDirection.ServerBound, V12110Ids.Serverbound.AcknowledgeConfiguration, ProtocolVersion.V12110)]
public sealed class AcknowledgeConfigurationHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        context.Connection.ServerState = ConnectionState.Configuration;
        return ValueTask.CompletedTask;
    }
}
