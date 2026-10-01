using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1122;

/// <summary>1.12.2 客户端发送的聊天消息。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ServerBound, MinecraftPacketIds.Serverbound.ChatMessage, ProtocolVersion.V1122)]
public sealed record ServerboundChatMessagePacket(string Message)
{
    public static ServerboundChatMessagePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new ServerboundChatMessagePacket(reader.ReadString(256));
        ChatPacketValidation.RequireEnd(reader);
        return result;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteString(Message, 256).ToArray();
    }
}

/// <summary>1.12.2 服务端发送的聊天 JSON 与显示位置。</summary>
[RegisterPacketModel(ConnectionState.Play, PacketDirection.ClientBound, MinecraftPacketIds.Clientbound.ChatMessage, ProtocolVersion.V1122)]
public sealed record ClientboundChatMessagePacket(string Json, byte Position)
{
    public static ClientboundChatMessagePacket Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new PacketReader(payload);
        var result = new ClientboundChatMessagePacket(reader.ReadString(), reader.ReadByte());
        ChatPacketValidation.RequireEnd(reader);
        return result;
    }

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteString(Json).WriteByte(Position).ToArray();
    }
}

internal static class ChatPacketValidation
{
    internal static void RequireEnd(PacketReader reader)
    {
        if (reader.Remaining != 0) throw new InvalidDataException("聊天包末尾包含多余字段。");
    }
}
