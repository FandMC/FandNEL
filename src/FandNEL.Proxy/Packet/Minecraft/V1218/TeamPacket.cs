using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1218;

/// <summary>队伍包的附加字段随 Method 变化，队名和 Method 明确解析，其余字段原样保留。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.Team, ProtocolVersion.V1218)]
public sealed record TeamPacket(string TeamName, byte Method, byte[] RemainingData)
{
    public static TeamPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        return new(reader.ReadString(), reader.ReadByte(), reader.ReadBytes(reader.Remaining));
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteString(TeamName).WriteByte(Method).WriteBytes(RemainingData).ToArray();
    }
}
