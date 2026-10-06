using FandNEL.Proxy.Packet.Minecraft.V1206;
namespace FandNEL.Proxy.Protocol.Builtin;

[RegisterPacket(ConnectionState.Handshaking, PacketDirection.ServerBound, 0)]
public sealed class HandshakeHandler : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        var packet = HandshakePacket.Read(context.Payload);
        var version = (ProtocolVersion)packet.ProtocolVersion;
        var nextState = (ConnectionState)packet.NextState;
        if (nextState is not (ConnectionState.Status or ConnectionState.Login))
            throw new InvalidDataException($"不支持的 Minecraft 握手状态：{nextState}。");
        if (nextState == ConnectionState.Login && !Enum.IsDefined(version))
            throw new NotSupportedException($"尚未支持 Minecraft 协议 {(int)version}。");

        var connection = context.Connection;
        connection.Version = version;
        if (nextState == ConnectionState.Login)
            DetectForgeClient(connection, packet.ServerAddress);
        var suffix = connection.AddForgeHandshakeSuffix ? version switch
        {
            <= ProtocolVersion.V1122 => "\0FML\0",
            <= ProtocolVersion.V1180 => "\0FML2\0",
            <= ProtocolVersion.V1206 => "\0FML3\0",
            _ => "\0FORGE"
        } : string.Empty;
        context.ReplacePayload((packet with
        {
            ServerAddress = connection.Target.Host + suffix,
            ServerPort = checked((ushort)connection.Target.Port)
        }).Write());
        connection.ClientState = nextState;
        connection.ServerState = nextState;
        return ValueTask.CompletedTask;
    }

    /// <summary>按客户端握手地址里的 FML 标记识别真实 Forge 客户端，供迁移的 Forge 伪造分支判断。</summary>
    private static void DetectForgeClient(Sessions.MinecraftConnection connection, string? serverAddress)
    {
        var state = connection.ServerProtocols;
        if (string.IsNullOrEmpty(serverAddress))
        {
            state.IsVanilla = true;
            return;
        }

        var isForge = serverAddress.Contains("\0FML", StringComparison.Ordinal)
            || serverAddress.Contains("FORGE", StringComparison.Ordinal);
        state.IsForgeClient = isForge;
        state.IsVanilla = !isForge;
    }
}
