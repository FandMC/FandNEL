using FandNEL.Proxy.Packet.Minecraft.V1206;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;

namespace FandNEL.Proxy.Heypixel;

[RegisterPacket(ConnectionState.Handshaking, PacketDirection.ServerBound, MinecraftPacketIds.Handshake.Serverbound, Priority = -10)]
[RegisterPacket(ConnectionState.Login, PacketDirection.ClientBound, MinecraftPacketIds.Login.ClientboundSuccess, ProtocolVersion.V1206, Priority = -10)]
[RegisterPacket(ConnectionState.Configuration, PacketDirection.ClientBound, MinecraftPacketIds.Configuration.ClientboundCustomPayload, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Configuration, PacketDirection.ServerBound, MinecraftPacketIds.Configuration.ServerboundCustomPayload, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Configuration, PacketDirection.ServerBound, MinecraftPacketIds.Configuration.Finish, ProtocolVersion.V1206, Priority = -10)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.JoinGame, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.GameEvent, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.CustomPayload, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.PlayerCorrection, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.EntityMetadata, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.Team, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.PlayerInfoUpdate, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.PlayerInfoRemove, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.DisplayObjective, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SetObjective, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SetScore, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.ResetScore, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SystemChat, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.StartConfiguration, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.SpawnEntity, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.EntityMove, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.EntityMoveAndRotation, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.EntityRotation, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.EntityTeleport, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.RemoveEntities, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.Respawn, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.CustomPayload, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.Position, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.PositionAndRotation, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.Rotation, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.OnGround, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.SwingArm, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.UseItem, ProtocolVersion.V1206)]
[RegisterPacket(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.UseItemOn, ProtocolVersion.V1206)]
public sealed class HeypixelProtocol : IPacketHandler
{
    public ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken)
    {
        if (!IsEnabled(context.Connection)) return ValueTask.CompletedTask;
        if (context.State == ConnectionState.Handshaking)
        {
            if (context.Connection.ClientState == ConnectionState.Login && context.Version != ProtocolVersion.V1206)
                throw new NotSupportedException("[FandNEL] 布吉岛需要 Minecraft 1.20.1 经 ViaForge 转换为 1.20.6（协议 766）后连接。");
            return ValueTask.CompletedTask;
        }
        if (context.State == ConnectionState.Login)
        {
            context.AfterForward(context.Connection.Heypixel.InitializeAsync);
            return ValueTask.CompletedTask;
        }
        return context.Connection.Heypixel.HandleAsync(context);
    }

    internal static bool IsEnabled(MinecraftConnection connection) =>
        connection.Options.Heypixel.Enabled && connection.Options.GameId == HeypixelConstants.GameId;
}
