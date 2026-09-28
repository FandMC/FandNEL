using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.V1206;
namespace FandNEL.Proxy.Protocol.Builtin;

[RegisterPacket(ConnectionState.Login, PacketDirection.ServerBound, 0)]
public sealed class LoginStartHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        if (context.Version == ProtocolVersion.V1206)
        {
            var packet = LoginStartPacket.Read(context.Payload);
            context.ReplacePayload((packet with
            {
                Name = context.Connection.Role.Name,
                PlayerUuid = Guid.TryParse(context.Connection.Role.Id, out var uuid) ? uuid : packet.PlayerUuid
            }).Write());
            return ValueTask.CompletedTask;
        }
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
        if (context.Version == ProtocolVersion.V1206)
        {
            var packet = EncryptionRequestPacket.Read(context.Payload);
            await context.Connection.AuthenticateAsync(packet.ServerId, packet.PublicKey, packet.VerifyToken,
                packet.ShouldAuthenticate, cancellationToken).ConfigureAwait(false);
            context.Cancel();
            return;
        }
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
        var threshold = SetCompressionPacket.Read(context.Payload).Threshold;
        context.Connection.EnableServerCompression(threshold);
        context.AfterForward(() =>
        {
            context.Connection.EnableClientCompression(threshold);
            return ValueTask.CompletedTask;
        });
        return ValueTask.CompletedTask;
    }
}

[RegisterPacket(ConnectionState.Login, PacketDirection.ClientBound, 2)]
public sealed class LoginSuccessHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        var connection = context.Connection;
        string username;
        if (context.Version == ProtocolVersion.V1206)
        {
            var packet = LoginSuccessPacket.Read(context.Payload);
            connection.PlayerUuid = packet.PlayerUuid;
            username = packet.Name;
        }
        else
        {
            var reader = context.CreateReader();
            if (context.Version >= ProtocolVersion.V1200)
                connection.PlayerUuid = reader.ReadUuid();
            else if (Guid.TryParse(reader.ReadString(36), out var uuid))
                connection.PlayerUuid = uuid;
            username = reader.ReadString(16);
        }
        connection.PlayerName = username;
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
