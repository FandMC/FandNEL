namespace FandNEL.Proxy.Packet.Heypixel;

/// <summary>Heypixel 自定义消息的线协议标识，不包含 Minecraft 原版包号。</summary>
public static class HeypixelPacketIds
{
    public const int InboundDiscriminator = 250;
    public const int Challenge = 101;
    public const int Report = 1;
    public const int KeepAlive = 2;
    public const int Cps = 3;
    public const int UseItemOn = 5;
    public const int MaximumPayloadLength = 1 << 20;
}
