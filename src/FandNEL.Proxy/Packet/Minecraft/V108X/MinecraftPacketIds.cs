namespace FandNEL.Proxy.Packet.Minecraft.V108X;

/// <summary>Minecraft 1.8.x（协议 47）的包标识。</summary>
public static class MinecraftPacketIds
{
    public static class Serverbound
    {
        public const int Animation = 0x0a;
        public const int PluginMessage = 0x17;
    }

    public static class Clientbound
    {
        public const int PluginMessage = 0x3f;
    }
}
