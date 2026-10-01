namespace FandNEL.Proxy.Packet.Minecraft.Metadata;

/// <summary>实体元数据 serializer registry。ID 按协议注册顺序定义。</summary>
public enum MetadataSerializer
{
    Byte = 0, Int = 1, Long = 2, Float = 3, String = 4, Component = 5,
    OptionalComponent = 6, ItemStack = 7, Boolean = 8, Rotations = 9,
    BlockPosition = 10, OptionalBlockPosition = 11, Direction = 12, OptionalUuid = 13,
    BlockState = 14, OptionalBlockState = 15,
    // 1.21.8 only; 1.21.10 removes CompoundTag from the registry.
    CompoundTag = 16,
    ParticleV1218 = 17, ParticlesV1218 = 18, VillagerDataV1218 = 19,
    OptionalUnsignedIntV1218 = 20, PoseV1218 = 21, CatVariantV1218 = 22,
    CowVariantV1218 = 23, WolfVariantV1218 = 24, WolfSoundVariantV1218 = 25,
    FrogVariantV1218 = 26, PigVariantV1218 = 27, ChickenVariantV1218 = 28,
    OptionalGlobalPositionV1218 = 29, PaintingVariantV1218 = 30, SnifferStateV1218 = 31,
    ArmadilloStateV1218 = 32, Vector3V1218 = 33, QuaternionV1218 = 34,
    ParticleV12110 = 16, ParticlesV12110 = 17, VillagerDataV12110 = 18,
    OptionalUnsignedIntV12110 = 19, PoseV12110 = 20, CatVariantV12110 = 21,
    CowVariantV12110 = 22, WolfVariantV12110 = 23, WolfSoundVariantV12110 = 24,
    FrogVariantV12110 = 25, PigVariantV12110 = 26, ChickenVariantV12110 = 27,
    OptionalGlobalPositionV12110 = 28, PaintingVariantV12110 = 29, SnifferStateV12110 = 30,
    ArmadilloStateV12110 = 31, CopperGolemStateV12110 = 32,
    WeatheringCopperStateV12110 = 33, Vector3V12110 = 34, QuaternionV12110 = 35,
    ResolvableProfileV12110 = 36
}

public enum MetadataProtocol
{
    V1218,
    V12110
}
