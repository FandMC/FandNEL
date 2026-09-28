using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.Nbt;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public sealed record ResourcePackPushPacket(Guid Id, string Url, string Hash, bool Required, NbtTag? Prompt)
{
    public static ResourcePackPushPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new ResourcePackPushPacket(reader.ReadUuid(), reader.ReadString(), reader.ReadString(40),
            reader.ReadBoolean(), reader.ReadBoolean() ? reader.ReadNetworkNbt() : null);
        MinecraftPacketValidation.RequireEnd(reader);
        return result;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        writer.WriteUuid(Id).WriteString(Url).WriteString(Hash, 40).WriteBoolean(Required).WriteBoolean(Prompt is not null);
        if (Prompt is not null) writer.WriteNetworkNbt(Prompt);
        return writer.ToArray();
    }
}
