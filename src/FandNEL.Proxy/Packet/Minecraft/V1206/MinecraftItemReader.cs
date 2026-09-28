using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.V1206;

/// <summary>读取协议 766 ItemStack 的组件边界；字段内容由调用方原样保留。</summary>
public static class MinecraftItemReader
{
    private const int MaximumDepth = 64;

    // 1.20.5/1.20.6 的组件 ID；不同版本不可复用此顺序。
    // Food 等跨版本字段以 ViaVersion 的 TYPE1_20_5 为准：
    // https://github.com/ViaVersion/ViaVersion/blob/master/api/src/main/java/com/viaversion/viaversion/api/minecraft/item/data/FoodProperties1_20_5.java
    private enum Component
    {
        CustomData, MaxStackSize, MaxDamage, Damage, Unbreakable, CustomName, ItemName, Lore,
        Rarity, Enchantments, CanPlaceOn, CanBreak, AttributeModifiers, CustomModelData,
        HideAdditionalTooltip, HideTooltip, RepairCost, CreativeSlotLock, EnchantmentGlintOverride,
        IntangibleProjectile, Food, FireResistant, Tool, StoredEnchantments, DyedColor, MapColor,
        MapId, MapDecorations, MapPostProcessing, ChargedProjectiles, BundleContents, PotionContents,
        SuspiciousStewEffects, WritableBookContent, WrittenBookContent, Trim, DebugStickState,
        EntityData, BucketEntityData, BlockEntityData, Instrument, OminousBottleAmplifier, Recipes,
        LodestoneTracker, FireworkExplosion, Fireworks, Profile, NoteBlockSound, BannerPatterns,
        BaseColor, PotDecorations, Container, BlockState, Bees, Lock, ContainerLoot
    }

    public static void Skip(PacketReader reader, int depth = 0)
    {
        CheckDepth(depth);
        var amount = reader.ReadVarInt();
        if (amount <= 0) return;
        reader.ReadVarInt();
        var added = ReadCount(reader);
        var removed = ReadCount(reader);
        for (var i = 0; i < added; i++) SkipComponent(reader, (Component)reader.ReadVarInt(), depth);
        for (var i = 0; i < removed; i++) reader.ReadVarInt();
    }

    private static void SkipComponent(PacketReader reader, Component component, int depth)
    {
        switch (component)
        {
            case Component.CustomData:
            case Component.CustomName:
            case Component.ItemName:
            case Component.IntangibleProjectile:
            case Component.MapDecorations:
            case Component.DebugStickState:
            case Component.EntityData:
            case Component.BucketEntityData:
            case Component.BlockEntityData:
            case Component.Recipes:
            case Component.Lock:
            case Component.ContainerLoot:
                reader.ReadNetworkNbt();
                return;
            case Component.MaxStackSize:
            case Component.MaxDamage:
            case Component.Damage:
            case Component.Rarity:
            case Component.CustomModelData:
            case Component.RepairCost:
            case Component.MapId:
            case Component.MapPostProcessing:
            case Component.OminousBottleAmplifier:
            case Component.BaseColor:
                reader.ReadVarInt();
                return;
            case Component.Unbreakable:
            case Component.EnchantmentGlintOverride:
                reader.ReadBoolean();
                return;
            case Component.HideAdditionalTooltip:
            case Component.HideTooltip:
            case Component.CreativeSlotLock:
            case Component.FireResistant:
                return;
            case Component.Lore:
                Array(reader, r => r.ReadNetworkNbt());
                return;
            case Component.Enchantments:
            case Component.StoredEnchantments:
                Array(reader, r => { r.ReadVarInt(); r.ReadVarInt(); });
                reader.ReadBoolean();
                return;
            case Component.CanPlaceOn:
            case Component.CanBreak:
                Array(reader, SkipBlockPredicate);
                reader.ReadBoolean();
                return;
            case Component.AttributeModifiers:
                Array(reader, r =>
                {
                    r.ReadVarInt();
                    r.Skip(16);
                    r.ReadString();
                    r.ReadDouble();
                    r.ReadVarInt();
                    r.ReadVarInt();
                });
                reader.ReadBoolean();
                return;
            case Component.Food:
                reader.ReadVarInt();
                reader.ReadFloat();
                reader.ReadBoolean();
                reader.ReadFloat();
                // 766 尚无 1.21 的 usingConvertsTo 字段。
                Array(reader, r => { SkipPotionEffect(r, depth); r.ReadFloat(); });
                return;
            case Component.Tool:
                Array(reader, r =>
                {
                    SkipHolderSet(r);
                    Optional(r, value => value.ReadFloat());
                    Optional(r, value => value.ReadBoolean());
                });
                reader.ReadFloat();
                reader.ReadVarInt();
                return;
            case Component.DyedColor:
                reader.ReadInt();
                reader.ReadBoolean();
                return;
            case Component.MapColor:
                reader.ReadInt();
                return;
            case Component.ChargedProjectiles:
            case Component.BundleContents:
            case Component.Container:
                Array(reader, r => Skip(r, depth + 1));
                return;
            case Component.PotionContents:
                Optional(reader, r => r.ReadVarInt());
                Optional(reader, r => r.ReadInt());
                Array(reader, r => SkipPotionEffect(r, depth));
                return;
            case Component.SuspiciousStewEffects:
                Array(reader, r => { r.ReadVarInt(); r.ReadVarInt(); });
                return;
            case Component.WritableBookContent:
                Array(reader, SkipFilteredString);
                return;
            case Component.WrittenBookContent:
                SkipFilteredString(reader);
                reader.ReadString();
                reader.ReadVarInt();
                Array(reader, r => { r.ReadNetworkNbt(); Optional(r, value => value.ReadNetworkNbt()); });
                reader.ReadBoolean();
                return;
            case Component.Trim:
                Holder(reader, r =>
                {
                    r.ReadString();
                    r.ReadVarInt();
                    r.ReadFloat();
                    Array(r, value => { value.ReadVarInt(); value.ReadString(); });
                    r.ReadNetworkNbt();
                });
                Holder(reader, r => { r.ReadString(); r.ReadVarInt(); r.ReadNetworkNbt(); r.ReadBoolean(); });
                reader.ReadBoolean();
                return;
            case Component.Instrument:
                Holder(reader, r =>
                {
                    Holder(r, sound => { sound.ReadString(); Optional(sound, value => value.ReadFloat()); });
                    r.ReadVarInt();
                    r.ReadFloat();
                });
                return;
            case Component.LodestoneTracker:
                Optional(reader, r => { r.ReadString(); r.ReadPosition(); });
                reader.ReadBoolean();
                return;
            case Component.FireworkExplosion:
                SkipFireworkExplosion(reader);
                return;
            case Component.Fireworks:
                reader.ReadVarInt();
                Array(reader, SkipFireworkExplosion);
                return;
            case Component.Profile:
                Optional(reader, r => r.ReadString());
                Optional(reader, r => r.Skip(16));
                Array(reader, r => { r.ReadString(); r.ReadString(); Optional(r, value => value.ReadString()); });
                return;
            case Component.NoteBlockSound:
                reader.ReadString();
                return;
            case Component.BannerPatterns:
                Array(reader, r =>
                {
                    Holder(r, pattern => { pattern.ReadString(); pattern.ReadString(); });
                    r.ReadVarInt();
                });
                return;
            case Component.PotDecorations:
                Array(reader, r => r.ReadVarInt());
                return;
            case Component.BlockState:
                Array(reader, r => { r.ReadString(); r.ReadString(); });
                return;
            case Component.Bees:
                Array(reader, r => { r.ReadNetworkNbt(); r.ReadVarInt(); r.ReadVarInt(); });
                return;
            default:
                throw new NotSupportedException($"未知协议 766 物品组件：{(int)component}。");
        }
    }

    private static void SkipBlockPredicate(PacketReader reader)
    {
        Optional(reader, SkipHolderSet);
        Optional(reader, r => Array(r, property =>
        {
            property.ReadString();
            if (property.ReadBoolean()) property.ReadString();
            else
            {
                Optional(property, value => value.ReadString());
                Optional(property, value => value.ReadString());
            }
        }));
        Optional(reader, r => r.ReadNetworkNbt());
    }

    private static void SkipPotionEffect(PacketReader reader, int depth)
    {
        reader.ReadVarInt();
        SkipPotionEffectData(reader, depth);
    }

    private static void SkipPotionEffectData(PacketReader reader, int depth)
    {
        CheckDepth(depth);
        reader.ReadVarInt();
        reader.ReadVarInt();
        reader.Skip(3);
        if (reader.ReadBoolean()) SkipPotionEffectData(reader, depth + 1);
    }

    private static void SkipFilteredString(PacketReader reader)
    {
        reader.ReadString();
        Optional(reader, r => r.ReadString());
    }

    private static void SkipFireworkExplosion(PacketReader reader)
    {
        reader.ReadVarInt();
        reader.Skip(checked(ReadCount(reader) * sizeof(int)));
        reader.Skip(checked(ReadCount(reader) * sizeof(int)));
        reader.Skip(2);
    }

    private static void SkipHolderSet(PacketReader reader)
    {
        var count = ReadCount(reader);
        if (count == 0) reader.ReadString();
        else for (var i = 0; i < count - 1; i++) reader.ReadVarInt();
    }

    private static void Holder(PacketReader reader, Action<PacketReader> readInline)
    {
        var id = reader.ReadVarInt();
        if (id < 0) throw new InvalidDataException("注册表 Holder ID 不能为负数。");
        if (id == 0) readInline(reader);
    }

    private static void Optional(PacketReader reader, Action<PacketReader> readValue)
    {
        if (reader.ReadBoolean()) readValue(reader);
    }

    private static void Array(PacketReader reader, Action<PacketReader> readElement)
    {
        var count = ReadCount(reader);
        for (var i = 0; i < count; i++) readElement(reader);
    }

    public static int ReadCount(PacketReader reader) => MinecraftPacketValidation.ReadCount(reader);

    private static void CheckDepth(int depth)
    {
        if (depth > MaximumDepth) throw new InvalidDataException("物品或效果嵌套超过限制。");
    }
}
