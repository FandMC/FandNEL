using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public sealed record UseItemOnPacket(int Hand, BlockPosition Location, int Face,
    float CursorPositionX, float CursorPositionY, float CursorPositionZ, bool InsideBlock, int Sequence)
{
    public static UseItemOnPacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new UseItemOnPacket(reader.ReadVarInt(), reader.ReadPosition(), reader.ReadVarInt(),
            reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadBoolean(), reader.ReadVarInt());
        MinecraftPacketValidation.RequireEnd(reader);
        result.Validate();
        return result;
    }

    public byte[] Write()
    {
        Validate();
        using var writer = new PacketWriter();
        return writer.WriteVarInt(Hand).WritePosition(Location).WriteVarInt(Face)
            .WriteFloat(CursorPositionX).WriteFloat(CursorPositionY).WriteFloat(CursorPositionZ)
            .WriteBoolean(InsideBlock).WriteVarInt(Sequence).ToArray();
    }

    private void Validate()
    {
        MinecraftPacketValidation.RequireHand(Hand);
        if (Face is < 0 or > 5) throw new InvalidDataException("方块交互包包含未知朝向。");
        MinecraftPacketValidation.RequireFinite(CursorPositionX, CursorPositionY, CursorPositionZ);
    }
}
