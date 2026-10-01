using FandNEL.Proxy.Protocol;
using MinecraftPacketValidation = FandNEL.Proxy.Packet.Minecraft.V1206.MinecraftPacketValidation;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1200;

/// <summary>1.20.0 实体交互包，未知类型字段保持严格校验。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.Interact, ProtocolVersion.V1200)]
public sealed record InteractPacket(int EntityId, int Type, float? TargetX, float? TargetY, float? TargetZ,
    int? Hand, bool Sneaking)
{
    public static InteractPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var entityId = reader.ReadVarInt();
        var type = reader.ReadVarInt();
        if (type is < 0 or > 2) throw new NotSupportedException("未知实体交互类型，应保留原包。");
        float? x = null, y = null, z = null;
        if (type == 2) { x = reader.ReadFloat(); y = reader.ReadFloat(); z = reader.ReadFloat(); }
        int? hand = type is 0 or 2 ? reader.ReadVarInt() : null;
        var result = new InteractPacket(entityId, type, x, y, z, hand, reader.ReadBoolean());
        MinecraftPacketValidation.RequireEnd(reader);
        result.Validate();
        return result;
    }

    public byte[] Write()
    {
        Validate();
        using var writer = new PacketWriter().WriteVarInt(EntityId).WriteVarInt(Type);
        if (Type == 2)
            writer.WriteFloat(TargetX ?? throw new InvalidDataException("交互包缺少目标 X 坐标。"))
                .WriteFloat(TargetY ?? throw new InvalidDataException("交互包缺少目标 Y 坐标。"))
                .WriteFloat(TargetZ ?? throw new InvalidDataException("交互包缺少目标 Z 坐标。"));
        if (Type is 0 or 2)
            writer.WriteVarInt(Hand ?? throw new InvalidDataException("交互包缺少手部字段。"));
        return writer.WriteBoolean(Sneaking).ToArray();
    }

    private void Validate()
    {
        if (Type is < 0 or > 2) throw new NotSupportedException("未知实体交互类型，应保留原包。");
        if (TargetX.HasValue != (Type == 2) || TargetY.HasValue != (Type == 2) ||
            TargetZ.HasValue != (Type == 2) || Hand.HasValue != (Type is 0 or 2))
            throw new InvalidDataException("实体交互字段与交互类型不匹配。");
        MinecraftPacketValidation.RequireFinite(TargetX, TargetY, TargetZ);
        if (Hand.HasValue) MinecraftPacketValidation.RequireHand(Hand.Value);
    }
}
