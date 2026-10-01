using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.Respawn, ProtocolVersion.V1206)]
public sealed record RespawnPacket(CommonPlayerSpawnInfo SpawnInfo, byte DataToKeep)
{
    public static RespawnPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var packet = new RespawnPacket(CommonPlayerSpawnInfo.Read(reader), reader.ReadByte());
        MinecraftPacketValidation.RequireEnd(reader);
        return packet;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        SpawnInfo.Write(writer);
        return writer.WriteByte(DataToKeep).ToArray();
    }
}
