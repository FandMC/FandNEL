using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.GameEvent, ProtocolVersion.V1206)]
public sealed record GameEventPacket(byte Event, float Value)
{
    public const byte ChangeGameMode = 3;

    public static GameEventPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var packet = new GameEventPacket(reader.ReadByte(), reader.ReadFloat());
        MinecraftPacketValidation.RequireEnd(reader);
        return packet;
    }
}
