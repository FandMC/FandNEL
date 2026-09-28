using FandNEL.Proxy.Packet.Heypixel;
using FandNEL.Proxy.Packet.Minecraft.V1206;
using FandNEL.Proxy.Protocol;

namespace FandNEL.Proxy.Heypixel;

/// <summary>自定义频道协商和分派；由所属连接串行调用。</summary>
internal sealed class HeypixelPluginMessages
{
    private int _registrationCount;

    internal async Task HandleAsync(HeypixelConnection connection, PacketContext context)
    {
        var packet = CustomPayloadPacket.Read(context.Payload);
        if (packet.Data.Length > HeypixelPacketIds.MaximumPayloadLength)
            throw new InvalidDataException("Heypixel plugin message exceeds its size limit.");
        if (context.Direction == PacketDirection.ServerBound)
        {
            if (packet.Channel == PluginChannelsPayload.RegisterChannel) context.Cancel();
            else if (packet.Channel == BrandPayload.Channel)
                context.ReplacePayload(new CustomPayloadPacket(packet.Channel, new BrandPayload("forge").Write()).Write());
            return;
        }
        switch (packet.Channel)
        {
            case PluginChannelsPayload.RegisterChannel:
                var registration = MergeChannels(packet.Data);
                if (++_registrationCount == 2)
                {
                    await connection.SendPluginAsync(PluginChannelsPayload.RegisterChannel, registration).ConfigureAwait(false);
                    await connection.SendPluginAsync(HeypixelChannels.Skin, new HeypixelSkinSyncPacket().Write()).ConfigureAwait(false);
                }
                break;
            case HeypixelChannels.Event:
                await connection.RespondAsync(packet.Data).ConfigureAwait(false);
                break;
            case HeypixelChannels.Form:
                var form = FloodgateFormPacket.Read(packet.Data);
                if (form.Content.Contains(HeypixelConstants.ConsentFormTitle, StringComparison.Ordinal))
                    await connection.SendPluginAsync(packet.Channel,
                        new FloodgateFormResponsePacket(form.FormId, "0").Write()).ConfigureAwait(false);
                break;
        }
    }

    internal static byte[] MergeChannels(ReadOnlyMemory<byte> payload) => new PluginChannelsPayload(
        PluginChannelsPayload.Read(payload).Channels.Concat(HeypixelChannels.Required)
            .Distinct(StringComparer.Ordinal).ToArray()).Write();
}
