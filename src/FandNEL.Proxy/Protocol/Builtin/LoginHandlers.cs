namespace FandNEL.Proxy.Protocol.Builtin;

[RegisterPacket(ConnectionState.Login, PacketDirection.ServerBound, 0)]
public sealed class LoginStartHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        var reader = context.CreateReader();
        _ = reader.ReadString(16);
        using var writer = new PacketWriter();
        writer.WriteString(context.Connection.Role.Name, 16);
        if (context.Version >= ProtocolVersion.V1206)
        {
            var uuid = reader.ReadBytes(16);
            if (Guid.TryParse(context.Connection.Role.Id, out var roleUuid))
                uuid = roleUuid.ToByteArray(bigEndian: true);
            writer.WriteBytes(uuid);
        }
        else if (context.Version == ProtocolVersion.V1200)
        {
            var hasUuid = reader.ReadBoolean();
            writer.WriteBoolean(hasUuid);
            if (hasUuid)
            {
                var uuid = reader.ReadBytes(16);
                if (Guid.TryParse(context.Connection.Role.Id, out var roleUuid))
                    uuid = roleUuid.ToByteArray(bigEndian: true);
                writer.WriteBytes(uuid);
            }
        }
        context.ReplaceRange(0, reader.Position, writer.ToArray());
        return ValueTask.CompletedTask;
    }
}

[RegisterPacket(ConnectionState.Login, PacketDirection.ClientBound, 1)]
public sealed class EncryptionRequestHandler : IPacketHandler
{
    public async ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        var reader = context.CreateReader();
        var serverId = reader.ReadString(20);
        var publicKey = context.Version == ProtocolVersion.V1076
            ? reader.ReadBytes(reader.ReadUnsignedShort()) : reader.ReadByteArray(4096);
        var verifyToken = context.Version == ProtocolVersion.V1076
            ? reader.ReadBytes(reader.ReadUnsignedShort()) : reader.ReadByteArray(4096);
        var shouldAuthenticate = context.Version < ProtocolVersion.V1206 || reader.ReadBoolean();
        await context.Connection.AuthenticateAsync(serverId, publicKey, verifyToken, shouldAuthenticate, cancellationToken)
            .ConfigureAwait(false);
        context.Cancel();
    }
}

[RegisterPacket(ConnectionState.Login, PacketDirection.ClientBound, 3)]
public sealed class CompressionHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        if (context.Version == ProtocolVersion.V1076)
            return ValueTask.CompletedTask;
        context.Connection.EnableServerCompression(context.CreateReader().ReadVarInt());
        // 与 Codexus 一致，只压缩远端链路；本地客户端不接收此控制包。
        context.Cancel();
        return ValueTask.CompletedTask;
    }
}

[RegisterPacket(ConnectionState.Login, PacketDirection.ClientBound, 2)]
public sealed class LoginSuccessHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        var reader = context.CreateReader();
        var connection = context.Connection;
        if (context.Version >= ProtocolVersion.V1200)
            connection.PlayerUuid = new Guid(reader.ReadBytes(16), bigEndian: true);
        else if (Guid.TryParse(reader.ReadString(36), out var uuid))
            connection.PlayerUuid = uuid;
        var username = reader.ReadString(16);
        if (context.Version >= ProtocolVersion.V1206)
            connection.ServerState = ConnectionState.Configuration;
        else
            connection.ClientState = connection.ServerState = ConnectionState.Play;
        // 登录属性、签名和版本相关尾字段不重编码，完整保留服务端载荷。
        connection.NotifyJoined(username);
        return ValueTask.CompletedTask;
    }
}

[RegisterPacket(ConnectionState.Login, PacketDirection.ServerBound, 3,
    ProtocolVersion.V1206, ProtocolVersion.V1210, ProtocolVersion.V1218, ProtocolVersion.V12110)]
public sealed class LoginAcknowledgedHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        context.Connection.ClientState = ConnectionState.Configuration;
        context.Connection.ServerState = ConnectionState.Configuration;
        return ValueTask.CompletedTask;
    }
}
