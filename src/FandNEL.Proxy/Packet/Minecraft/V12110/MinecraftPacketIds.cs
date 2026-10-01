namespace FandNEL.Proxy.Packet.Minecraft.V12110;

/// <summary>Minecraft 1.21.10（协议 773）的包标识。</summary>
public static class MinecraftPacketIds
{
    public static class Login
    {
        public const int ClientboundDisconnect = 0;
        public const int ClientboundEncryptionRequest = 1;
        public const int ClientboundSuccess = 2;
        public const int ClientboundSetCompression = 3;
        public const int ServerboundStart = 0;
        public const int ServerboundEncryptionResponse = 1;
        public const int ServerboundAcknowledged = 3;
    }

    public static class Serverbound
    {
        public const int AcknowledgeConfiguration = 0x0f;
        public const int CustomPayload = 0x15;
        public const int Interact = 0x19;
        public const int Position = 0x1d;
        public const int PositionAndRotation = 0x1e;
        public const int Rotation = 0x1f;
        public const int OnGround = 0x20;
        public const int SwingArm = 0x3c;
        public const int UseItemOn = 0x3f;
        public const int UseItem = 0x40;
    }

    public static class Clientbound
    {
        public const int SpawnEntity = 0x01;
        public const int CustomPayload = 0x18;
        public const int JoinGame = 0x30;
        public const int EntityPositionSync = 0x23;
        public const int EntityMove = 0x33;
        public const int EntityMoveAndRotation = 0x34;
        public const int EntityRotation = 0x36;
        public const int EntityMetadata = 0x61;
        public const int Team = 0x6b;
        public const int StartConfiguration = 0x74;
        public const int SystemChat = 0x77;
    }

    public static class Configuration
    {
        public const int ServerboundClientInformation = 0;
        public const int ClientboundCustomPayload = 1;
        public const int ServerboundCustomPayload = 2;
        public const int ClientboundDisconnect = 2;
        public const int Finish = 3;
        public const int ServerboundKnownPacks = 7;
        public const int ClientboundResourcePack = 9;
    }
}
