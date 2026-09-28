namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>协议 766 的元数据类型表；不能沿用其他协议版本的数值。</summary>
public enum MetadataSerializer
{
    Byte, VarInt, VarLong, Float, String, Component, OptionalComponent, ItemStack,
    Boolean, Rotations, BlockPosition, OptionalBlockPosition, Direction, OptionalUuid,
    BlockState, OptionalBlockState, CompoundTag, Particle, Particles, VillagerData,
    OptionalUnsignedInt, Pose, CatVariant, WolfVariant, FrogVariant, OptionalGlobalPosition,
    PaintingVariant, SnifferState, ArmadilloState, Vector3, Quaternion
}
