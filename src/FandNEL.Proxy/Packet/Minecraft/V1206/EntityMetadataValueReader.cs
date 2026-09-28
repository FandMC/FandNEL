using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

// 协议 766 元数据和粒子边界；未知类型明确拒绝，调用方决定是否原包透传。
internal static class EntityMetadataValueReader
{
    internal static void SkipValue(PacketReader reader, MetadataSerializer serializer)
    {
        switch (serializer)
        {
            case MetadataSerializer.Byte:
            case MetadataSerializer.Boolean:
                reader.Skip(1);
                break;
            case MetadataSerializer.VarInt:
            case MetadataSerializer.Direction:
            case MetadataSerializer.BlockState:
            case MetadataSerializer.OptionalBlockState:
            case MetadataSerializer.OptionalUnsignedInt:
            case MetadataSerializer.Pose:
            case MetadataSerializer.CatVariant:
            case MetadataSerializer.WolfVariant:
            case MetadataSerializer.FrogVariant:
            case MetadataSerializer.PaintingVariant:
            case MetadataSerializer.SnifferState:
            case MetadataSerializer.ArmadilloState:
                reader.ReadVarInt();
                break;
            case MetadataSerializer.VarLong:
                reader.ReadVarLong();
                break;
            case MetadataSerializer.Float:
                reader.Skip(sizeof(float));
                break;
            case MetadataSerializer.String:
                reader.ReadString();
                break;
            case MetadataSerializer.Component:
            case MetadataSerializer.CompoundTag:
                reader.ReadNetworkNbt();
                break;
            case MetadataSerializer.OptionalComponent:
                if (reader.ReadBoolean()) reader.ReadNetworkNbt();
                break;
            case MetadataSerializer.ItemStack:
                MinecraftItemReader.Skip(reader);
                break;
            case MetadataSerializer.Rotations:
            case MetadataSerializer.Vector3:
                reader.Skip(3 * sizeof(float));
                break;
            case MetadataSerializer.Quaternion:
                reader.Skip(4 * sizeof(float));
                break;
            case MetadataSerializer.BlockPosition:
                reader.Skip(sizeof(long));
                break;
            case MetadataSerializer.OptionalBlockPosition:
                if (reader.ReadBoolean()) reader.Skip(sizeof(long));
                break;
            case MetadataSerializer.OptionalUuid:
                if (reader.ReadBoolean()) reader.Skip(16);
                break;
            case MetadataSerializer.OptionalGlobalPosition:
                if (reader.ReadBoolean())
                {
                    reader.ReadString();
                    reader.Skip(sizeof(long));
                }
                break;
            case MetadataSerializer.VillagerData:
                reader.ReadVarInt();
                reader.ReadVarInt();
                reader.ReadVarInt();
                break;
            case MetadataSerializer.Particle:
                SkipParticle(reader);
                break;
            case MetadataSerializer.Particles:
                var count = MinecraftItemReader.ReadCount(reader);
                for (var i = 0; i < count; i++) SkipParticle(reader);
                break;
            default:
                throw new NotSupportedException($"未知协议 766 元数据 serializer：{(int)serializer}。");
        }
    }

    private static void SkipParticle(PacketReader reader)
    {
        var particle = reader.ReadVarInt();
        switch (particle)
        {
            case 1: // block
            case 2: // block_marker
            case 28: // falling_dust
            case 99: // shriek
            case 105: // dust_pillar
                reader.ReadVarInt();
                break;
            case 13: // dust
                reader.Skip(4 * sizeof(float));
                break;
            case 14: // dust_color_transition
                reader.Skip(7 * sizeof(float));
                break;
            case 20: // entity_effect
            case 35: // sculk_charge
                reader.Skip(sizeof(int));
                break;
            case 44: // item
                MinecraftItemReader.Skip(reader);
                break;
            case 45: // vibration
                var source = reader.ReadVarInt();
                if (source == 0) reader.Skip(sizeof(long));
                else if (source == 1)
                {
                    reader.ReadVarInt();
                    reader.Skip(sizeof(float));
                }
                else throw new NotSupportedException("未知振动粒子位置类型。");
                reader.ReadVarInt();
                break;
            case < 0 or > 108:
                throw new NotSupportedException($"未知协议 766 粒子：{particle}。");
        }
    }
}
