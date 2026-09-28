using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public sealed record ProfileProperty(string Name, string Value, string? Signature);

public sealed record LoginSuccessPacket(Guid PlayerUuid, string Name, IReadOnlyList<ProfileProperty> Properties, bool StrictErrorHandling)
{
    public static LoginSuccessPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var uuid = reader.ReadUuid();
        var name = reader.ReadString(16);
        var count = reader.ReadVarInt();
        if (count < 0 || count > reader.Remaining / 3) throw new InvalidDataException("登录属性数量非法。");
        var properties = new ProfileProperty[count];
        for (var index = 0; index < count; index++)
            properties[index] = new ProfileProperty(reader.ReadString(), reader.ReadString(), reader.ReadBoolean() ? reader.ReadString() : null);
        var strict = reader.ReadBoolean();
        if (reader.Remaining != 0) throw new InvalidDataException("Login Success 包末尾存在多余数据。");
        return new(uuid, name, properties, strict);
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        writer.WriteUuid(PlayerUuid).WriteString(Name, 16).WriteVarInt(Properties.Count);
        foreach (var property in Properties)
        {
            writer.WriteString(property.Name).WriteString(property.Value).WriteBoolean(property.Signature is not null);
            if (property.Signature is not null) writer.WriteString(property.Signature);
        }
        return writer.WriteBoolean(StrictErrorHandling).ToArray();
    }
}
