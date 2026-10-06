using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Servers.Germ;

/// <summary>germ 报文编解码：Data = [int32 germId][字段]，字符串为 VarInt 长度前缀。</summary>
internal static class GermPackets
{
    internal const int IdScriptDefault = 2;
    internal const int IdGuiResponse = 3;
    internal const int IdGuiOpen = 4;
    internal const int IdQueryCache = 6;
    internal const int IdGuiClose = 11;
    internal const int IdClickDown = 12;
    internal const int IdClick = 13;
    internal const int IdClientLink = 16;
    internal const int IdFragment = -1;
    internal const int IdPassThrough1 = 23;
    internal const int IdActionResponse = 25;
    internal const int IdScriptInput = 26;
    internal const int IdPassThrough2 = 39;
    internal const int IdSuYuanVerify = 64;

    /// <summary>编码一个上行 germ 包。</summary>
    internal static byte[] Encode(int germId, Action<PacketWriter>? fields = null)
    {
        using var writer = new PacketWriter();
        writer.WriteInt(germId);
        fields?.Invoke(writer);
        return writer.ToArray();
    }

    internal static byte[] BuildGuiOpen(string gui) => Encode(IdGuiOpen, writer =>
    {
        writer.WriteInt(0);
        writer.WriteInt(0);
        writer.WriteString(gui);
        writer.WriteString(gui);
        writer.WriteString(gui);
    });

    internal static byte[] BuildGuiClose(string state) => Encode(IdGuiClose, writer => writer.WriteString(state));

    internal static byte[] BuildGuiClick(string top, string node, int action) => Encode(IdClick, writer =>
    {
        writer.WriteString(top);
        writer.WriteString(node);
        writer.WriteInt(action);
    });

    internal static byte[] BuildGuiClickDown(string type, string top, string node, int action) =>
        Encode(IdClickDown, writer =>
        {
            writer.WriteString(type);
            writer.WriteString(top);
            writer.WriteString(node);
            writer.WriteInt(action);
            writer.WriteInt(0);
            writer.WriteInt(0);
        });

    internal static byte[] BuildScriptDefault(string gui, string part, string command) =>
        Encode(IdScriptDefault, writer =>
        {
            writer.WriteString(gui);
            writer.WriteString(part);
            writer.WriteString(command);
        });

    internal static byte[] BuildScriptInput(string name, string value) => Encode(IdScriptInput, writer =>
    {
        writer.WriteString(name);
        writer.WriteString(value);
    });

    internal static byte[] BuildActionResponse(string json) =>
        Encode(IdActionResponse, writer => writer.WriteString(json));

    internal static byte[] BuildClientLink(string json) =>
        Encode(IdClientLink, writer => writer.WriteString(json));

    /// <summary>读取 germ 包的 32 位包标识；载荷不足时返回 null。</summary>
    internal static int? TryReadId(byte[] data)
    {
        if (data is null || data.Length < sizeof(int))
            return null;
        try
        {
            var reader = new PacketReader(data);
            return reader.ReadInt();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
