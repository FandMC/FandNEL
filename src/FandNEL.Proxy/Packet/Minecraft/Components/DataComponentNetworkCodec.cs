using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Minecraft.Components;

/// <summary>
/// 1.21.8/1.21.10 DataComponentPatch 编解码器。
/// 组件值按网络协议解析边界后保留原始 bytes；这样尚未被业务使用的组件仍可安全转发。
/// </summary>
public static class DataComponentNetworkCodec
{
    private const int MaxComponents = 96;
    private const int MaxCollection = 1_048_576;
    private const int MaxDepth = 64;

    public static DataComponentPatch ReadPatch(ReadOnlyMemory<byte> payload, DataComponentProtocol protocol, out int consumed)
    {
        var reader = new PacketReader(payload);
        var patch = ReadPatch(reader, protocol, payload);
        consumed = reader.Position;
        return patch;
    }

    public static DataComponentPatch ReadPatch(DataComponentProtocol protocol, ReadOnlyMemory<byte> payload, out int consumed)
        => ReadPatch(payload, protocol, out consumed);

    public static void WritePatch(PacketWriter writer, DataComponentPatch patch)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(patch);
        if (patch.Added.Count > MaxComponents || patch.Removed.Count > MaxComponents)
            throw new InvalidDataException("物品组件补丁数量超过限制。");

        writer.WriteVarInt(patch.Added.Count).WriteVarInt(patch.Removed.Count);
        foreach (var (typeId, component) in patch.Added)
        {
            if (typeId < 0 || typeId > (int)DataComponentType.ShulkerColor || component.TypeId != typeId)
                throw new InvalidDataException($"物品组件类型不一致：{typeId}。");
            writer.WriteVarInt(typeId).WriteBytes(component.RawPayload.Span);
        }
        foreach (var typeId in patch.Removed)
        {
            if (typeId is < 0 or > (int)DataComponentType.ShulkerColor)
                throw new InvalidDataException($"物品组件类型非法：{typeId}。");
            writer.WriteVarInt(typeId);
        }
    }

    /// <summary>仅推进游标，用于元数据中的嵌套 ItemStack；不会分配原始 payload。</summary>
    public static void SkipPatch(PacketReader reader, DataComponentProtocol protocol = DataComponentProtocol.V1218, int depth = 0)
    {
        CheckDepth(depth);
        var added = ReadCount(reader, "nested component", MaxComponents);
        var removed = ReadCount(reader, "nested removed component", MaxComponents);
        for (var i = 0; i < added; i++)
        {
            var id = reader.ReadVarInt();
            if (id < 0 || id > (int)DataComponentType.ShulkerColor)
                throw new NotSupportedException($"协议 {protocol} 未知嵌套组件 id：{id}。");
            SkipComponent(reader, id, protocol, depth + 1);
        }
        for (var i = 0; i < removed; i++)
        {
            var id = reader.ReadVarInt();
            if (id < 0 || id > (int)DataComponentType.ShulkerColor)
                throw new NotSupportedException($"协议 {protocol} 未知移除组件 id：{id}。");
        }
    }

    /// <summary>读取一个已知组件并返回该组件的原始网络 payload。</summary>
    public static RawDataComponent ReadComponent(ReadOnlyMemory<byte> payload, int typeId,
        DataComponentProtocol protocol, out int consumed)
    {
        var reader = new PacketReader(payload);
        var start = reader.Position;
        SkipComponent(reader, typeId, protocol, 0);
        consumed = reader.Position - start;
        return new RawDataComponent(typeId, payload.Slice(start, consumed));
    }

    public static RawDataComponent ReadComponent(ReadOnlyMemory<byte> payload, DataComponentType type,
        DataComponentProtocol protocol, out int consumed) => ReadComponent(payload, (int)type, protocol, out consumed);

    public static ItemStackData ReadItemStack(ReadOnlyMemory<byte> payload, DataComponentProtocol protocol, out int consumed)
    {
        var reader = new PacketReader(payload);
        var count = reader.ReadVarInt();
        if (count <= 0)
        {
            consumed = reader.Position;
            return new ItemStackData(0, 0, null, payload[..consumed]);
        }

        var itemId = reader.ReadVarInt();
        var patchStart = reader.Position;
        var patch = ReadPatch(payload[patchStart..], protocol, out var patchLength);
        reader.Skip(patchLength);
        consumed = reader.Position;
        return new ItemStackData(count, itemId, patch, payload[patchStart..consumed]);
    }

    public static void WriteItemStack(PacketWriter writer, ItemStackData item, DataComponentProtocol protocol = DataComponentProtocol.V1218)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsEmpty) { writer.WriteVarInt(0); return; }
        writer.WriteVarInt(item.Count).WriteVarInt(item.ItemId);
        if (item.Components is not null) WritePatch(writer, item.Components);
        else if (!item.RawPayload.IsEmpty) writer.WriteBytes(item.RawPayload.Span);
        else writer.WriteVarInt(0).WriteVarInt(0);
    }

    private static DataComponentPatch ReadPatch(PacketReader reader, DataComponentProtocol protocol, ReadOnlyMemory<byte> source)
    {
        var addedCount = ReadCount(reader, "added component", MaxComponents);
        var removedCount = ReadCount(reader, "removed component", MaxComponents);
        var patch = new DataComponentPatch();

        for (var i = 0; i < addedCount; i++)
        {
            var typeId = reader.ReadVarInt();
            if (typeId is < 0 or > (int)DataComponentType.ShulkerColor)
                throw new NotSupportedException($"协议 {protocol} 未知物品组件 id：{typeId}。无法确定未知组件长度，应保留物品原包。");
            var start = reader.Position;
            SkipComponent(reader, typeId, protocol, 0);
            var bytes = source.Slice(start, reader.Position - start);
            patch.Set(new RawDataComponent(typeId, bytes));
        }

        for (var i = 0; i < removedCount; i++)
        {
            var typeId = reader.ReadVarInt();
            if (typeId is < 0 or > (int)DataComponentType.ShulkerColor)
                throw new NotSupportedException($"协议 {protocol} 未知移除组件 id：{typeId}。");
            patch.Remove(typeId);
        }
        return patch;
    }

    private static void SkipComponent(PacketReader r, int typeId, DataComponentProtocol protocol, int depth)
    {
        CheckDepth(depth);
        switch ((DataComponentType)typeId)
        {
            case DataComponentType.CustomData:
            case DataComponentType.CustomName:
            case DataComponentType.ItemName:
            case DataComponentType.IntangibleProjectile:
            case DataComponentType.MapDecorations:
            case DataComponentType.DebugStickState:
                r.ReadNetworkNbt(); return;
            case DataComponentType.EntityData:
                if (protocol == DataComponentProtocol.V12110) r.ReadVarInt();
                r.ReadNetworkNbt(); return;
            case DataComponentType.BucketEntityData:
                r.ReadNetworkNbt(); return;
            case DataComponentType.BlockEntityData:
                if (protocol == DataComponentProtocol.V12110) r.ReadVarInt();
                r.ReadNetworkNbt(); return;
            case DataComponentType.Recipes:
            case DataComponentType.Lock:
            case DataComponentType.ContainerLoot:
                r.ReadNetworkNbt(); return;
            case DataComponentType.MaxStackSize:
            case DataComponentType.MaxDamage:
            case DataComponentType.Damage:
            case DataComponentType.Rarity:
            case DataComponentType.RepairCost:
            case DataComponentType.MapId:
            case DataComponentType.MapPostProcessing:
            case DataComponentType.OminousBottleAmplifier:
            case DataComponentType.BaseColor:
            case DataComponentType.VillagerVariant:
            case DataComponentType.WolfVariant:
            case DataComponentType.WolfSoundVariant:
            case DataComponentType.WolfCollar:
            case DataComponentType.FoxVariant:
            case DataComponentType.SalmonSize:
            case DataComponentType.ParrotVariant:
            case DataComponentType.TropicalFishPattern:
            case DataComponentType.TropicalFishBaseColor:
            case DataComponentType.TropicalFishPatternColor:
            case DataComponentType.MooshroomVariant:
            case DataComponentType.RabbitVariant:
            case DataComponentType.PigVariant:
            case DataComponentType.CowVariant:
            case DataComponentType.FrogVariant:
            case DataComponentType.HorseVariant:
            case DataComponentType.LlamaVariant:
            case DataComponentType.AxolotlVariant:
            case DataComponentType.CatVariant:
            case DataComponentType.CatCollar:
            case DataComponentType.SheepColor:
            case DataComponentType.ShulkerColor:
                r.ReadVarInt(); return;
            case DataComponentType.Unbreakable:
            case DataComponentType.CreativeSlotLock:
            case DataComponentType.Glider:
                return;
            case DataComponentType.ItemModel:
            case DataComponentType.DamageResistant:
            case DataComponentType.TooltipStyle:
            case DataComponentType.NoteBlockSound:
            case DataComponentType.ProvidesBannerPatterns:
                r.ReadString(); return;
            case DataComponentType.Lore:
                List(r, x => x.ReadNetworkNbt(), 256); return;
            case DataComponentType.Enchantments:
            case DataComponentType.StoredEnchantments:
                List(r, x => { x.ReadVarInt(); x.ReadVarInt(); }, 256);
                return;
            case DataComponentType.CanPlaceOn:
            case DataComponentType.CanBreak:
                List(r, x => SkipBlockPredicate(x, protocol, depth + 1), 64); return;
            case DataComponentType.AttributeModifiers:
                List(r, x =>
                {
                    x.ReadVarInt(); x.ReadString(); x.ReadDouble();
                    x.ReadVarInt(); x.ReadVarInt(); var display = x.ReadVarInt();
                    if (display == 2) x.ReadNetworkNbt();
                }, 256); return;
            case DataComponentType.CustomModelData:
                List(r, x => x.ReadFloat(), MaxCollection); List(r, x => x.ReadBoolean(), MaxCollection);
                List(r, x => x.ReadString(), MaxCollection); List(r, x => x.ReadInt(), MaxCollection); return;
            case DataComponentType.TooltipDisplay:
                r.ReadBoolean(); List(r, x => x.ReadVarInt(), 96); return;
            case DataComponentType.EnchantmentGlintOverride:
                r.ReadBoolean(); return;
            case DataComponentType.Food:
                r.ReadVarInt(); r.ReadFloat(); r.ReadBoolean(); return;
            case DataComponentType.Consumable:
                r.ReadFloat(); r.ReadVarInt(); SkipHolder(r, SkipSoundEvent, depth);
                r.ReadBoolean(); List(r, x => SkipConsumeEffect(x, protocol, depth + 1), 64); return;
            case DataComponentType.UseRemainder:
                SkipItemStack(r, protocol, depth + 1); return;
            case DataComponentType.UseCooldown:
                r.ReadFloat(); Optional(r, x => x.ReadString()); return;
            case DataComponentType.Tool:
                List(r, x => { SkipHolderSet(x); Optional(x, y => y.ReadFloat()); Optional(x, y => y.ReadBoolean()); }, 256);
                r.ReadFloat(); r.ReadVarInt(); r.ReadBoolean(); return;
            case DataComponentType.Weapon:
                r.ReadVarInt(); r.ReadFloat(); return;
            case DataComponentType.Enchantable:
                r.ReadVarInt(); return;
            case DataComponentType.Equippable:
                r.ReadVarInt(); SkipHolder(r, SkipSoundEvent, depth); Optional(r, x => x.ReadString());
                Optional(r, x => x.ReadString()); Optional(r, SkipHolderSet);
                r.Skip(5); SkipHolder(r, SkipSoundEvent, depth); return;
            case DataComponentType.Repairable:
                SkipHolderSet(r); return;
            case DataComponentType.DeathProtection:
                List(r, x => SkipConsumeEffect(x, protocol, depth + 1), 64); return;
            case DataComponentType.BlocksAttacks:
                r.Skip(2 * sizeof(float)); List(r, x => { x.ReadFloat(); Optional(x, SkipHolderSet); x.Skip(2 * sizeof(float)); }, 64);
                r.Skip(3 * sizeof(float)); Optional(r, x => x.ReadString());
                Optional(r, x => SkipHolder(x, SkipSoundEvent, depth)); Optional(r, x => SkipHolder(x, SkipSoundEvent, depth)); return;
            case DataComponentType.DyedColor:
            case DataComponentType.MapColor:
                r.Skip(sizeof(int)); return;
            case DataComponentType.ChargedProjectiles:
            case DataComponentType.BundleContents:
            case DataComponentType.Container:
                List(r, x => SkipItemStack(x, protocol, depth + 1), 256); return;
            case DataComponentType.PotionContents:
                Optional(r, x => x.ReadVarInt()); Optional(r, x => x.ReadInt());
                List(r, x => SkipPotionEffect(x, depth + 1), 64); Optional(r, x => x.ReadString()); return;
            case DataComponentType.PotionDurationScale:
                r.ReadFloat(); return;
            case DataComponentType.LodestoneTracker:
                Optional(r, x => { x.ReadString(); x.Skip(sizeof(long)); }); r.ReadBoolean(); return;
            case DataComponentType.SuspiciousStewEffects:
                List(r, x => { x.ReadVarInt(); x.ReadVarInt(); }, 64); return;
            case DataComponentType.WritableBookContent:
                List(r, SkipFilteredString, 100); return;
            case DataComponentType.WrittenBookContent:
                SkipFilteredString(r); r.ReadString(); r.ReadVarInt(); List(r, x => { x.ReadNetworkNbt(); Optional(x, y => y.ReadNetworkNbt()); }, 100); r.ReadBoolean(); return;
            case DataComponentType.Trim:
                SkipHolder(r, SkipTrimMaterial, depth); SkipHolder(r, SkipTrimPattern, depth); return;
            case DataComponentType.Instrument:
                if (r.ReadBoolean()) SkipHolder(r, SkipInstrument, depth); else r.ReadString(); return;
            case DataComponentType.ProvidesTrimMaterial:
                switch (r.ReadByte()) { case 0: r.ReadString(); break; case 1: SkipHolder(r, SkipTrimMaterial, depth); break; default: throw new InvalidDataException("未知 trim material 模式。"); } return;
            case DataComponentType.JukeboxPlayable:
                switch (r.ReadByte()) { case 0: r.ReadString(); break; case 1: SkipHolder(r, SkipJukeboxSong, depth); break; default: throw new InvalidDataException("未知 jukebox song 模式。"); } return;
            case DataComponentType.FireworkExplosion:
                SkipFireworkExplosion(r); return;
            case DataComponentType.Fireworks:
                r.ReadVarInt(); List(r, SkipFireworkExplosion, 256); return;
            case DataComponentType.Profile:
                if (protocol == DataComponentProtocol.V12110) SkipResolvableProfile(r); else { Optional(r, x => x.ReadString()); Optional(r, x => x.Skip(16)); List(r, x => { x.ReadString(); x.ReadString(); Optional(x, y => y.ReadString()); }, 64); } return;
            case DataComponentType.BannerPatterns:
                List(r, x => { SkipHolder(x, y => { y.ReadString(); y.ReadString(); }, depth); x.ReadVarInt(); }, 256); return;
            case DataComponentType.PotDecorations:
                List(r, x => x.ReadVarInt(), 4); return;
            case DataComponentType.BlockState:
                List(r, x => { x.ReadString(); x.ReadString(); }, 256); return;
            case DataComponentType.Bees:
                List(r, x => { if (protocol == DataComponentProtocol.V12110) x.ReadVarInt(); x.ReadNetworkNbt(); x.ReadVarInt(); x.ReadVarInt(); }, 256); return;
            case DataComponentType.BreakSound:
                SkipHolder(r, SkipSoundEvent, depth); return;
            case DataComponentType.PaintingVariant:
                SkipHolder(r, SkipPaintingVariant, depth); return;
            case DataComponentType.ChickenVariant:
                switch (r.ReadByte()) { case 0: r.ReadString(); break; case 1: r.ReadVarInt(); break; default: throw new InvalidDataException("未知鸡变种模式。"); } return;
            default:
                throw new NotSupportedException($"未实现物品组件 payload：{typeId}（协议 {protocol}）。应保留物品原包。");
        }
    }

    private static void SkipItemStack(PacketReader r, DataComponentProtocol protocol, int depth)
    {
        CheckDepth(depth); var count = r.ReadVarInt(); if (count <= 0) return; r.ReadVarInt(); SkipPatch(r, protocol, depth + 1);
    }

    private static void SkipBlockPredicate(PacketReader r, DataComponentProtocol protocol, int depth)
    {
        Optional(r, SkipHolderSet); Optional(r, x => List(x, y => { y.ReadString(); if (y.ReadBoolean()) y.ReadString(); else { y.ReadString(); y.ReadString(); } }, 64));
        Optional(r, x => x.ReadNetworkNbt());
        List(r, x => { var id = x.ReadVarInt(); SkipComponent(x, id, protocol, depth); }, 64);
        List(r, x => x.ReadVarInt(), 64);
    }

    private static void SkipConsumeEffect(PacketReader r, DataComponentProtocol protocol, int depth)
    {
        switch (r.ReadVarInt())
        {
            case 0: List(r, x => SkipPotionEffect(x, depth + 1), 64); r.ReadFloat(); break;
            case 1: SkipHolderSet(r); break;
            case 2: break;
            case 3: r.ReadFloat(); break;
            case 4: SkipSoundEvent(r); break;
            default: throw new NotSupportedException("未知 consume effect 类型。");
        }
    }

    private static void SkipPotionEffect(PacketReader r, int depth)
    {
        r.ReadVarInt();
        SkipPotionEffectDetails(r, depth);
    }

    private static void SkipPotionEffectDetails(PacketReader r, int depth)
    {
        CheckDepth(depth); r.ReadVarInt(); r.ReadVarInt(); r.ReadBoolean(); r.ReadBoolean(); r.ReadBoolean();
        Optional(r, x => SkipPotionEffectDetails(x, depth + 1));
    }

    private static void SkipHolderSet(PacketReader r)
    {
        var count = r.ReadVarInt(); if (count == 0) { r.ReadString(); return; }
        if (count < 0 || count > MaxCollection + 1) throw new InvalidDataException("HolderSet 数量非法。");
        for (var i = 0; i < count - 1; i++) r.ReadVarInt();
    }

    private static void SkipHolder(PacketReader r, Action<PacketReader> inline, int depth)
    {
        var marker = r.ReadVarInt(); if (marker < 0) throw new InvalidDataException("Holder 标记不能为负数。"); if (marker == 0) inline(r);
    }

    private static void SkipSoundEvent(PacketReader r) { r.ReadString(); Optional(r, x => x.ReadFloat()); }
    private static void SkipInstrument(PacketReader r) { SkipHolder(r, SkipSoundEvent, 0); r.ReadFloat(); r.ReadFloat(); r.ReadNetworkNbt(); }
    private static void SkipJukeboxSong(PacketReader r) { SkipHolder(r, SkipSoundEvent, 0); r.ReadNetworkNbt(); r.ReadFloat(); r.ReadVarInt(); }
    private static void SkipTrimMaterial(PacketReader r) { r.ReadString(); List(r, x => { x.ReadString(); x.ReadString(); }, 256); r.ReadNetworkNbt(); }
    private static void SkipTrimPattern(PacketReader r) { r.ReadString(); r.ReadNetworkNbt(); r.ReadBoolean(); }
    private static void SkipPaintingVariant(PacketReader r) { r.ReadVarInt(); r.ReadVarInt(); r.ReadString(); Optional(r, x => x.ReadNetworkNbt()); Optional(r, x => x.ReadNetworkNbt()); }
    private static void SkipFilteredString(PacketReader r) { r.ReadString(); Optional(r, x => x.ReadString()); }
    private static void SkipFireworkExplosion(PacketReader r) { r.ReadVarInt(); SkipArray(r, sizeof(int)); SkipArray(r, sizeof(int)); r.Skip(2); }

    private static void SkipResolvableProfile(PacketReader r)
    {
        var kind = r.ReadVarInt();
        if (kind == 0) { Optional(r, x => x.ReadString(16)); Optional(r, x => x.Skip(16)); }
        else if (kind == 1) { r.Skip(16); r.ReadString(16); }
        else throw new InvalidDataException($"未知 ResolvableProfile 类型：{kind}。");
        List(r, x => { x.ReadString(64); x.ReadString(); Optional(x, y => y.ReadString(1024)); }, 16);
        Optional(r, x => x.ReadString()); Optional(r, x => x.ReadString()); Optional(r, x => x.ReadString());
    }

    private static int ReadCount(PacketReader r, string name, int max)
    {
        var count = r.ReadVarInt(); if (count < 0 || count > max) throw new InvalidDataException($"{name} 数量非法：{count}。"); return count;
    }

    private static void List(PacketReader r, Action<PacketReader> read, int max) { var count = ReadCount(r, "组件列表", max); for (var i = 0; i < count; i++) read(r); }
    private static void Optional(PacketReader r, Action<PacketReader> read) { if (r.ReadBoolean()) read(r); }
    private static void SkipArray(PacketReader r, int elementSize) { var count = ReadCount(r, "数组", 256); r.Skip(checked(count * elementSize)); }
    private static void CheckDepth(int depth) { if (depth > MaxDepth) throw new InvalidDataException("物品组件嵌套超过限制。"); }
}
