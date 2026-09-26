namespace FandNEL.Proxy.Protocol.Builtin;

[RegisterPacket(ConnectionState.Handshaking, PacketDirection.ServerBound, 0)]
public sealed class HandshakeHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        var reader = context.CreateReader();
        var version = (ProtocolVersion)reader.ReadVarInt();
        _ = reader.ReadString(255);
        _ = reader.ReadUnsignedShort();
        var nextState = (ConnectionState)reader.ReadVarInt();
        if (nextState is not (ConnectionState.Status or ConnectionState.Login))
            throw new InvalidDataException($"不支持的 Minecraft 握手状态：{nextState}。");
        if (nextState == ConnectionState.Login && !Enum.IsDefined(version))
            throw new NotSupportedException($"尚未支持 Minecraft 协议 {(int)version}。");

        var connection = context.Connection;
        connection.Version = version;
        var suffix = connection.AddForgeHandshakeSuffix ? version switch
        {
            <= ProtocolVersion.V1122 => "\0FML\0",
            <= ProtocolVersion.V1180 => "\0FML2\0",
            <= ProtocolVersion.V1206 => "\0FML3\0",
            _ => "\0FORGE"
        } : string.Empty;
        using var writer = new PacketWriter();
        writer.WriteVarInt((int)version).WriteString(connection.Target.Host + suffix, 255)
            .WriteUnsignedShort(checked((ushort)connection.Target.Port)).WriteVarInt((int)nextState);
        context.ReplaceRange(0, reader.Position, writer.ToArray());
        connection.ClientState = nextState;
        connection.ServerState = nextState;
        return ValueTask.CompletedTask;
    }
}
