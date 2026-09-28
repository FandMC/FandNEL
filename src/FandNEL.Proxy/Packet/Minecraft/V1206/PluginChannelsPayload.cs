using System.Text;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>注册频道载荷：UTF-8 名称之间以空字符分隔，允许末尾分隔符。</summary>
public sealed record PluginChannelsPayload(string[] Channels)
{
    public const string RegisterChannel = "minecraft:register";
    public const string UnregisterChannel = "minecraft:unregister";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static PluginChannelsPayload Read(ReadOnlyMemory<byte> payload) =>
        new(StrictUtf8.GetString(payload.Span).Split('\0', StringSplitOptions.RemoveEmptyEntries));

    public byte[] Write()
    {
        if (Channels.Any(channel => string.IsNullOrEmpty(channel) || channel.Contains('\0')))
            throw new InvalidDataException("注册频道名称不能为空或包含空字符。");
        return Channels.Length == 0 ? [] : StrictUtf8.GetBytes(string.Join('\0', Channels) + '\0');
    }
}
