using FandNEL.Proxy.Protocol;
using MinecraftPacketValidation = FandNEL.Proxy.Packet.Minecraft.V1206.MinecraftPacketValidation;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1218;

public sealed record KnownPack(string Namespace, string Id, string Version);

[RegisterPacketModel(ConnectionState.Configuration, PacketDirection.ServerBound, MinecraftPacketIds.Configuration.ServerboundKnownPacks, ProtocolVersion.V1218)]
public sealed record KnownPacksPacket(IReadOnlyList<KnownPack> Packs)
{
    private const int MaximumPacks = 1024;

    public static KnownPacksPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var count = reader.ReadVarInt();
        if (count is < 0 or > MaximumPacks) throw new InvalidDataException("已知资源包数量非法。");
        var packs = new List<KnownPack>(count);
        for (var i = 0; i < count; i++)
            packs.Add(new KnownPack(reader.ReadString(), reader.ReadString(), reader.ReadString()));
        MinecraftPacketValidation.RequireEnd(reader);
        return new(packs);
    }

    public byte[] Write()
    {
        if (Packs.Count > MaximumPacks) throw new InvalidDataException("已知资源包数量超过限制。");
        using var writer = new PacketWriter();
        writer.WriteVarInt(Packs.Count);
        foreach (var pack in Packs)
            writer.WriteString(pack.Namespace).WriteString(pack.Id).WriteString(pack.Version);
        return writer.ToArray();
    }
}
