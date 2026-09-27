using System.Text;
using System.Text.Json;
using FandNEL.Proxy.Protocol;

namespace FandNEL.Proxy.Irc;

/// <summary>
/// IRC 用到的各协议版本包 ID 与载荷构造。
/// 包 ID 取自原版协议（版本口径与 FandNEL 内置处理器一致），
/// 并已用 1.20.6 / 1.21 / 1.21.8 / 1.21.10 的 start_configuration 包 ID 交叉验证。
/// </summary>
internal static class IrcProtocol
{
    /// <summary>单个协议版本需要用到的 IRC 相关包。</summary>
    internal sealed record VersionSpec(
        ProtocolVersion Version,
        int JoinGameId,
        int SystemChatId,
        int? ChatMessageId,
        int? ChatCommandId,
        int? SignedChatCommandId);

    internal static readonly VersionSpec[] Specs =
    [
        new(ProtocolVersion.V1076, JoinGameId: 0x01, SystemChatId: 0x02, ChatMessageId: 0x01, ChatCommandId: null, SignedChatCommandId: null),
        new(ProtocolVersion.V108X, JoinGameId: 0x01, SystemChatId: 0x02, ChatMessageId: 0x01, ChatCommandId: null, SignedChatCommandId: null),
        new(ProtocolVersion.V1122, JoinGameId: 0x23, SystemChatId: 0x0F, ChatMessageId: 0x02, ChatCommandId: null, SignedChatCommandId: null),
        new(ProtocolVersion.V1165, JoinGameId: 0x24, SystemChatId: 0x0E, ChatMessageId: 0x03, ChatCommandId: null, SignedChatCommandId: null),
        new(ProtocolVersion.V1180, JoinGameId: 0x26, SystemChatId: 0x0F, ChatMessageId: 0x03, ChatCommandId: null, SignedChatCommandId: null),
        new(ProtocolVersion.V1200, JoinGameId: 0x28, SystemChatId: 0x64, ChatMessageId: 0x05, ChatCommandId: 0x04, SignedChatCommandId: null),
        new(ProtocolVersion.V1206, JoinGameId: 0x2B, SystemChatId: 0x6C, ChatMessageId: 0x06, ChatCommandId: 0x04, SignedChatCommandId: 0x05),
        new(ProtocolVersion.V1210, JoinGameId: 0x2B, SystemChatId: 0x6C, ChatMessageId: 0x06, ChatCommandId: 0x04, SignedChatCommandId: 0x05),
        new(ProtocolVersion.V1218, JoinGameId: 0x2B, SystemChatId: 0x72, ChatMessageId: 0x08, ChatCommandId: 0x06, SignedChatCommandId: 0x07),
        new(ProtocolVersion.V12110, JoinGameId: 0x30, SystemChatId: 0x77, ChatMessageId: 0x08, ChatCommandId: 0x06, SignedChatCommandId: 0x07)
    ];

    internal static VersionSpec? TryGetSpec(ProtocolVersion version)
    {
        foreach (var spec in Specs)
        {
            if (spec.Version == version)
            {
                return spec;
            }
        }

        return null;
    }

    /// <summary>
    /// 解析疑似 /IRC 的输入。
    /// 1.18 及更早版本由聊天包携带 "/IRC 内容"（带斜杠）；
    /// 1.19+ 客户端把 "/irc 内容" 作为命令包发送，内容不含首个斜杠。
    /// </summary>
    internal static bool TryParseIrcCommand(string text, bool isCommandPacket, out string content)
    {
        content = string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var keyword = isCommandPacket ? "irc" : "/irc";
        if (!text.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 防止把 "/ircabc" 这类正常输入误判成命令。
        if (text.Length > keyword.Length && text[keyword.Length] != ' ')
        {
            return false;
        }

        content = text.Length > keyword.Length ? text[keyword.Length..].Trim() : string.Empty;
        return true;
    }

    /// <summary>把一行文本包成指定版本的系统聊天包载荷（服务端 → 客户端方向）。</summary>
    internal static byte[] BuildSystemChatPayload(ProtocolVersion version, string text)
    {
        var safe = Sanitize(text);
        using var writer = new PacketWriter();
        if (version >= ProtocolVersion.V1206)
        {
            // 1.20.5+：匿名 NBT 文本组件 + 是否动作栏。
            WriteNbtTextComponent(writer, safe);
            writer.WriteBoolean(false);
        }
        else if (version >= ProtocolVersion.V1200)
        {
            // 1.20 / 1.20.1：JSON 文本组件 + 是否动作栏。
            writer.WriteString(JsonComponent(safe));
            writer.WriteBoolean(false);
        }
        else if (version >= ProtocolVersion.V1165)
        {
            // 1.16.5 / 1.18：JSON 文本组件 + 显示位置（1=系统）+ 发送者 UUID。
            writer.WriteString(JsonComponent(safe));
            writer.WriteByte(1);
            writer.WriteBytes(new byte[16]);
        }
        else if (version >= ProtocolVersion.V108X)
        {
            // 1.8.x / 1.12.2：JSON 文本组件 + 显示位置（1=系统）。
            writer.WriteString(JsonComponent(safe));
            writer.WriteByte(1);
        }
        else
        {
            // 1.7.6：只有 JSON 文本组件（该版本尚未引入显示位置字段）。
            writer.WriteString(JsonComponent(safe));
        }

        return writer.ToArray();
    }

    private static string JsonComponent(string text) => JsonSerializer.Serialize(new { text });

    /// <summary>匿名 NBT（1.20.2+ 网络格式）：根节点不带名字，写 TAG_Compound { text: TAG_String }。</summary>
    private static void WriteNbtTextComponent(PacketWriter writer, string text)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(0x0A); // TAG_Compound
        stream.WriteByte(0x08); // TAG_String
        WriteNbtString(stream, "text");
        WriteNbtString(stream, text);
        stream.WriteByte(0x00); // TAG_End
        writer.WriteBytes(stream.ToArray());
    }

    private static void WriteNbtString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
        {
            // NBT 字符串上限 65535 字节；聊天室消息远小于该值，这里只做兜底截断。
            bytes = Encoding.UTF8.GetBytes(value[..Math.Min(value.Length, 1000)]);
        }

        stream.WriteByte((byte)(bytes.Length >> 8));
        stream.WriteByte((byte)(bytes.Length & 0xFF));
        stream.Write(bytes);
    }

    /// <summary>换行会破坏单行聊天显示，统一压成空格。</summary>
    private static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return " ";
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            builder.Append(character is '\r' or '\n' ? ' ' : character);
        }

        return builder.ToString();
    }
}