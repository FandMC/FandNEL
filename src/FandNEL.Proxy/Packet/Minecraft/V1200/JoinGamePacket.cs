using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1200;

/// <summary>1.20.0 加入游戏包；实体 ID 后的版本字段保留原始载荷。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.JoinGame, ProtocolVersion.V1200)]
public sealed record JoinGamePacket(int EntityId, byte[] RemainingData)
{
    public static JoinGamePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        return new(reader.ReadInt(), reader.ReadBytes(reader.Remaining));
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteInt(EntityId).WriteBytes(RemainingData).ToArray();
    }
}
