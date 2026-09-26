namespace FandNEL.Proxy.Protocol.Builtin;

[RegisterPacket(ConnectionState.Configuration, PacketDirection.ClientBound, 3)]
public sealed class FinishConfigurationHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        // FinishConfiguration 本身不改变状态；客户端仍需发送确认包。
        return ValueTask.CompletedTask;
    }
}

[RegisterPacket(ConnectionState.Configuration, PacketDirection.ServerBound, 3)]
public sealed class AcknowledgeFinishConfigurationHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        context.Connection.ClientState = ConnectionState.Play;
        context.Connection.ServerState = ConnectionState.Play;
        return ValueTask.CompletedTask;
    }
}

[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, 105, ProtocolVersion.V1206, ProtocolVersion.V1210)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, 111, ProtocolVersion.V1218)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, 116, ProtocolVersion.V12110)]
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

[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 12, ProtocolVersion.V1206, ProtocolVersion.V1210)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, 15, ProtocolVersion.V1218, ProtocolVersion.V12110)]
[RegisterPacket(ConnectionState.Configuration, PacketDirection.ServerBound, 12, ProtocolVersion.V1206, ProtocolVersion.V1210)]
[RegisterPacket(ConnectionState.Configuration, PacketDirection.ServerBound, 15, ProtocolVersion.V1218, ProtocolVersion.V12110)]
public sealed class AcknowledgeConfigurationHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        context.Connection.ServerState = ConnectionState.Configuration;
        return ValueTask.CompletedTask;
    }
}
