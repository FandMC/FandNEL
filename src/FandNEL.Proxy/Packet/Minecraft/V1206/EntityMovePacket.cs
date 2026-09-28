using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>相对位移采用 1/4096 格为单位；仅旋转的包位移为零。</summary>
public sealed record EntityMovePacket(int PacketId, int EntityId, short DeltaX, short DeltaY, short DeltaZ,
    byte? Yaw, byte? Pitch, bool OnGround)
{
    public static EntityMovePacket Read(int packetId, ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var entityId = reader.ReadVarInt();
        var (position, rotation) = Fields(packetId);
        short dx = 0, dy = 0, dz = 0;
        byte? yaw = null, pitch = null;
        if (position)
        {
            dx = unchecked((short)reader.ReadUnsignedShort());
            dy = unchecked((short)reader.ReadUnsignedShort());
            dz = unchecked((short)reader.ReadUnsignedShort());
        }
        if (rotation) { yaw = reader.ReadByte(); pitch = reader.ReadByte(); }
        var result = new EntityMovePacket(packetId, entityId, dx, dy, dz, yaw, pitch, reader.ReadBoolean());
        MinecraftPacketValidation.RequireEnd(reader);
        return result;
    }

    public byte[] Write()
    {
        var (position, rotation) = Fields(PacketId);
        if ((!position && (DeltaX != 0 || DeltaY != 0 || DeltaZ != 0)) ||
            Yaw.HasValue != rotation || Pitch.HasValue != rotation)
            throw new InvalidDataException("实体移动包字段与包类型不匹配。");
        using var writer = new PacketWriter();
        writer.WriteVarInt(EntityId);
        if (position) writer.WriteUnsignedShort(unchecked((ushort)DeltaX)).WriteUnsignedShort(unchecked((ushort)DeltaY))
            .WriteUnsignedShort(unchecked((ushort)DeltaZ));
        if (rotation) writer.WriteByte(Yaw!.Value).WriteByte(Pitch!.Value);
        return writer.WriteBoolean(OnGround).ToArray();
    }

    private static (bool Position, bool Rotation) Fields(int packetId) => packetId switch
    {
        MinecraftPacketIds.Clientbound.EntityMove => (true, false),
        MinecraftPacketIds.Clientbound.EntityMoveAndRotation => (true, true),
        MinecraftPacketIds.Clientbound.EntityRotation => (false, true),
        _ => throw new ArgumentOutOfRangeException(nameof(packetId), packetId, "不是实体相对移动包。")
    };
}
