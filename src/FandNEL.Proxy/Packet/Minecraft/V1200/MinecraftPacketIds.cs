namespace FandNEL.Proxy.Packet.Minecraft.V1200;

/// <summary>Minecraft 1.20/1.20.1 协议包标识（协议 763）。</summary>
public static class MinecraftPacketIds
{
    public static class Serverbound
    {
        public const int CustomPayload = 0x0d;
        public const int Interact = 0x10;
        public const int Position = 0x14;
        public const int PositionAndRotation = 0x15;
        public const int Rotation = 0x16;
        public const int OnGround = 0x17;
        public const int SwingArm = 0x2f;
        public const int UseItemOn = 0x31;
        public const int UseItem = 0x32;
    }

    public static class Clientbound
    {
        public const int SpawnEntity = 0x01;
        public const int SpawnPlayer = 0x03;
        public const int CustomPayload = 0x17;
        public const int JoinGame = 0x28;
        public const int EntityMove = 0x2b;
        public const int EntityMoveAndRotation = 0x2c;
        public const int EntityRotation = 0x2d;
        public const int SystemChat = 0x64;
        public const int EntityTeleport = 0x68;
    }
}
