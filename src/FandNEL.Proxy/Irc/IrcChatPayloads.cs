using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.V1206;
using System.Text.Json;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Packet.Minecraft.Nbt;

namespace FandNEL.Proxy.Irc;

/// <summary>把统一的 IRC 文本转换为各 Minecraft 版本的系统聊天包载荷。</summary>
internal static class IrcChatPayloads
{
    internal static byte[] BuildSystemChat(ProtocolVersion version, string text)
    {
        var safe = SingleLine(text);
        if (version == ProtocolVersion.V1206)
            return new SystemChatPacket(new NbtCompound().Set(IrcConstants.TextComponentField, new NbtString(safe)), false).Write();
        using var writer = new PacketWriter();
        if (version >= ProtocolVersion.V1206)
            return BuildNetworkText(writer, safe);

        writer.WriteString(JsonSerializer.Serialize(new IrcJsonTextComponent(safe)));
        if (version >= ProtocolVersion.V1200) writer.WriteBoolean(false); // 1.20：是否动作栏
        else if (version >= ProtocolVersion.V1165)
        {
            writer.WriteByte(IrcConstants.ChatMessagePosition); // 消息位置：聊天栏
            writer.WriteBytes(new byte[IrcConstants.ChatMessageUuidByteLength]); // 普通聊天包需要 UUID
        }
        else if (version >= ProtocolVersion.V108X)
        {
            writer.WriteByte(IrcConstants.ChatMessagePosition); // 消息位置：聊天栏
        }
        return writer.ToArray();
    }

    private static byte[] BuildNetworkText(PacketWriter writer, string text)
    {
        var component = new NbtCompound().Set(IrcConstants.TextComponentField, new NbtString(text));
        writer.WriteNetworkNbtCompound(component);
        writer.WriteBoolean(false); // 是否动作栏
        return writer.ToArray();
    }

    /// <summary>聊天栏是单行显示：换行/回车统一压成空格，空串给一个空格。</summary>
    internal static string SingleLine(string? text)
    {
        if (string.IsNullOrEmpty(text)) return " ";
        return text.Contains('\r') || text.Contains('\n')
            ? text.Replace('\r', ' ').Replace('\n', ' ')
            : text;
    }
}
