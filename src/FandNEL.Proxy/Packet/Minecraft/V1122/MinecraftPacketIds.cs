namespace FandNEL.Proxy.Packet.Minecraft.V1122;

/// <summary>Minecraft 1.12.2（协议 340）的包标识。</summary>
public static class MinecraftPacketIds
{
    public static class Serverbound
    {
        public const int ChatMessage = 0x02;
        public const int PluginMessage = 0x09;
        public const int PlayerPosition = 0x0e;
    }

    public static class Clientbound
    {
        public const int ChatMessage = 0x0f;
        public const int ChunkData = 0x20;
        public const int JoinGame = 0x23;
        public const int PluginMessage = 0x18;
    }
}
