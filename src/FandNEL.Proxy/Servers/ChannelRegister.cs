using System.Text;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;

namespace FandNEL.Proxy.Servers;

/// <summary>客户端频道注册去重；Aely 与重生长城改用各自的固定频道表。</summary>
internal static class ChannelRegister
{
    internal static bool IsEnabled(MinecraftConnection connection) => ServerGameIds.IsEnabled(connection);

    /// <summary>客户端上行的频道注册：按游戏改写后重新发给服务端并丢弃原包。</summary>
    internal static async ValueTask HandleServerRegisterAsync(PacketContext context, byte[] data, bool modernChannel,
        CancellationToken cancellationToken)
    {
        // 只有旧版 REGISTER 通道会走 Aely/重生长城的固定列表；1.13+ 的 minecraft:register 一律按去重重发。
        if (!modernChannel)
        {
            if (AelyProtocol.IsEnabled(context.Connection))
            {
                await ForgeSpoof.SendRegisterAsync(context.Connection, AelyProtocol.ChannelList, cancellationToken)
                    .ConfigureAwait(false);
                context.Cancel();
                return;
            }
            if (CzsczlProtocol.IsEnabled(context.Connection))
            {
                await ForgeSpoof.SendRegisterAsync(context.Connection, CzsczlProtocol.ChannelList, cancellationToken)
                    .ConfigureAwait(false);
                context.Cancel();
                return;
            }
        }

        var channels = Encoding.UTF8.GetString(data)
            .Split('\0')
            .Where(static channel => !string.IsNullOrWhiteSpace(channel))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        await ForgeSpoof.SendRegisterAsync(context.Connection, Encode(channels), cancellationToken).ConfigureAwait(false);
        context.Cancel();
    }

    private static byte[] Encode(IEnumerable<string> channels)
    {
        using var stream = new MemoryStream();
        foreach (var channel in channels)
        {
            stream.Write(Encoding.UTF8.GetBytes(channel));
            stream.WriteByte(0);
        }
        return stream.ToArray();
    }
}
