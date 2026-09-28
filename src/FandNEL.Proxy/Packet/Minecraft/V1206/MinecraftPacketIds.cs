namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>Minecraft 1.20.5/1.20.6（协议 766）的包标识。</summary>
public static class MinecraftPacketIds
{
    public static class Handshake
    {
        public const int Serverbound = 0;
    }

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
        public const int AcknowledgeConfiguration = 0x0c;
        public const int CustomPayload = 0x12;
        public const int Interact = 0x16;
        public const int Position = 0x1a;
        public const int PositionAndRotation = 0x1b;
        public const int Rotation = 0x1c;
        public const int OnGround = 0x1d;
        public const int SwingArm = 0x36;
        public const int UseItemOn = 0x38;
        public const int UseItem = 0x39;
    }

    public static class Clientbound
    {
        public const int SpawnEntity = 0x01;
        public const int CustomPayload = 0x19;
        public const int Disconnect = 0x1d;
        public const int GameEvent = 0x22;
        public const int JoinGame = 0x2b;
        public const int EntityMove = 0x2e;
        public const int EntityMoveAndRotation = 0x2f;
        public const int EntityRotation = 0x30;
        public const int PlayerInfoRemove = 0x3d;
        public const int PlayerInfoUpdate = 0x3e;
        public const int PlayerCorrection = 0x40;
        public const int RemoveEntities = 0x42;
        public const int ResetScore = 0x44;
        public const int ResourcePack = 0x46;
        public const int Respawn = 0x47;
        public const int DisplayObjective = 0x57;
        public const int EntityMetadata = 0x58;
        public const int SetObjective = 0x5e;
        public const int Team = 0x60;
        public const int SetScore = 0x61;
        public const int StartConfiguration = 0x69;
        public const int SystemChat = 0x6c;
        public const int EntityTeleport = 0x70;
    }

    public static class Configuration
    {
        public const int ServerboundCustomPayload = 0x02;
        public const int ClientboundCustomPayload = 0x01;
        public const int ClientboundDisconnect = 0x02;
        public const int Finish = 0x03;
        public const int ClientboundResourcePack = 0x09;
    }
}
