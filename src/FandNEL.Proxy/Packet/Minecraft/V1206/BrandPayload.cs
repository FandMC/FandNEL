using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public sealed record BrandPayload(string Brand)
{
    public const string Channel = "minecraft:brand";

    public static BrandPayload Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new BrandPayload(reader.ReadString());
        MinecraftPacketValidation.RequireEnd(reader);
        return result;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteString(Brand).ToArray();
    }
}
