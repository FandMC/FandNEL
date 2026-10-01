using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1122;

/// <summary>1.12.2 加入游戏信息。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.JoinGame, ProtocolVersion.V1122)]
public sealed record JoinGamePacket(int EntityId, byte GameMode, int Dimension, byte Difficulty,
    byte MaxPlayers, string LevelType, bool ReducedDebugInfo)
{
    public static JoinGamePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var packet = new JoinGamePacket(reader.ReadInt(), reader.ReadByte(), reader.ReadInt(), reader.ReadByte(),
            reader.ReadByte(), reader.ReadString(32), reader.ReadBoolean());
        if (reader.Remaining != 0) throw new InvalidDataException("加入游戏包末尾包含多余字段。");
        return packet;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteInt(EntityId).WriteByte(GameMode).WriteInt(Dimension).WriteByte(Difficulty)
            .WriteByte(MaxPlayers).WriteString(LevelType, 32).WriteBoolean(ReducedDebugInfo).ToArray();
    }
}
