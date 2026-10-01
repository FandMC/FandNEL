using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Packet.Minecraft.Components;

namespace FandNEL.Proxy.Packet.Minecraft.Metadata;

/// <summary>为 1.21.8/1.21.10 提供元数据 value 的边界解析和原样写回。</summary>
public static class MetadataValueCodec
{
    private const int MaxDepth = 64;

    public static MetadataValue ReadValue(ReadOnlyMemory<byte> payload, int serializerId,
        MetadataProtocol protocol, out int consumed)
    {
        var reader = new PacketReader(payload);
        SkipValue(reader, serializerId, protocol, 0);
        consumed = reader.Position;
        return new MetadataValue(serializerId, payload[..consumed]);
    }

    public static MetadataValue ReadValue(MetadataProtocol protocol, ReadOnlyMemory<byte> payload,
        int serializerId, out int consumed) => ReadValue(payload, serializerId, protocol, out consumed);

    public static MetadataValue ReadValue(ReadOnlyMemory<byte> payload, MetadataSerializer serializer,
        MetadataProtocol protocol, out int consumed) => ReadValue(payload, (int)serializer, protocol, out consumed);

    public static void WriteValue(PacketWriter writer, MetadataValue value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteBytes(value.RawValue.Span);
    }

    public static bool TryGetSerializer(MetadataProtocol protocol, int id, out MetadataSerializer serializer)
    {
        if (protocol == MetadataProtocol.V1218)
        {
            serializer = id switch
            {
                >= 0 and <= 34 => (MetadataSerializer)id,
                _ => default
            };
            return id is >= 0 and <= 34;
        }

        if (id is >= 0 and <= 15)
        {
            serializer = (MetadataSerializer)id;
            return true;
        }
        serializer = id switch
        {
            16 => MetadataSerializer.ParticleV12110,
            17 => MetadataSerializer.ParticlesV12110,
            18 => MetadataSerializer.VillagerDataV12110,
            19 => MetadataSerializer.OptionalUnsignedIntV12110,
            20 => MetadataSerializer.PoseV12110,
            21 => MetadataSerializer.CatVariantV12110,
            22 => MetadataSerializer.CowVariantV12110,
            23 => MetadataSerializer.WolfVariantV12110,
            24 => MetadataSerializer.WolfSoundVariantV12110,
            25 => MetadataSerializer.FrogVariantV12110,
            26 => MetadataSerializer.PigVariantV12110,
            27 => MetadataSerializer.ChickenVariantV12110,
            28 => MetadataSerializer.OptionalGlobalPositionV12110,
            29 => MetadataSerializer.PaintingVariantV12110,
            30 => MetadataSerializer.SnifferStateV12110,
            31 => MetadataSerializer.ArmadilloStateV12110,
            32 => MetadataSerializer.CopperGolemStateV12110,
            33 => MetadataSerializer.WeatheringCopperStateV12110,
            34 => MetadataSerializer.Vector3V12110,
            35 => MetadataSerializer.QuaternionV12110,
            36 => MetadataSerializer.ResolvableProfileV12110,
            _ => default
        };
        return id is >= 0 and <= 36;
    }

    public static void SkipValue(PacketReader r, int serializerId, MetadataProtocol protocol, int depth = 0)
    {
        if (!TryGetSerializer(protocol, serializerId, out _))
            throw new NotSupportedException($"协议 {protocol} 未知实体元数据 serializer：{serializerId}。应保留完整原包。");
        CheckDepth(depth);
        // 按 wire id 分协议分支，避免 1.21.8 的 CompoundTag=16 与 1.21.10 的 Particle=16 混淆。
        if (protocol == MetadataProtocol.V1218)
        {
            switch (serializerId)
            {
                case 0: case 8: r.Skip(1); return;
                case 1: case 14: case 15: case 12: case 20: case 21: case 22: case 23: case 24: case 25: case 26: case 27: case 28: case 31: case 32: r.ReadVarInt(); return;
                case 2: r.ReadVarLong(); return;
                case 3: r.ReadFloat(); return;
                case 4: r.ReadString(); return;
                case 5: case 16: r.ReadNetworkNbt(); return;
                case 6: Optional(r, x => x.ReadNetworkNbt()); return;
                case 7: SkipItemStack(r, protocol, depth + 1); return;
                case 9: case 33: r.Skip(3 * sizeof(float)); return;
                case 34: r.Skip(4 * sizeof(float)); return;
                case 10: r.Skip(sizeof(long)); return;
                case 11: Optional(r, x => x.Skip(sizeof(long))); return;
                case 13: Optional(r, x => x.Skip(16)); return;
                case 17: SkipParticle(r, protocol, depth + 1); return;
                case 18: List(r, x => SkipParticle(x, protocol, depth + 1), 1_048_576); return;
                case 19: r.ReadVarInt(); r.ReadVarInt(); r.ReadVarInt(); return;
                case 29: Optional(r, x => { x.ReadString(); x.Skip(sizeof(long)); }); return;
                case 30: SkipPaintingVariantReference(r); return;
                default: throw new NotSupportedException($"实体元数据 serializer 未实现：{serializerId}。");
            }
        }
        else
        {
            switch (serializerId)
            {
                case 0: case 8: r.Skip(1); return;
                case 1: case 14: case 15: case 12: case 19: case 20: case 21: case 22: case 23: case 24: case 25: case 26: case 27: case 30: case 31: case 32: case 33: r.ReadVarInt(); return;
                case 2: r.ReadVarLong(); return;
                case 3: r.ReadFloat(); return;
                case 4: r.ReadString(); return;
                case 5: r.ReadNetworkNbt(); return;
                case 6: Optional(r, x => x.ReadNetworkNbt()); return;
                case 7: SkipItemStack(r, protocol, depth + 1); return;
                case 9: case 34: r.Skip(3 * sizeof(float)); return;
                case 35: r.Skip(4 * sizeof(float)); return;
                case 10: r.Skip(sizeof(long)); return;
                case 11: Optional(r, x => x.Skip(sizeof(long))); return;
                case 13: Optional(r, x => x.Skip(16)); return;
                case 16: SkipParticle(r, protocol, depth + 1); return;
                case 17: List(r, x => SkipParticle(x, protocol, depth + 1), 1_048_576); return;
                case 18: r.ReadVarInt(); r.ReadVarInt(); r.ReadVarInt(); return;
                case 28: Optional(r, x => { x.ReadString(); x.Skip(sizeof(long)); }); return;
                case 29: SkipPaintingVariantReference(r); return;
                case 36: SkipResolvableProfile(r); return;
                default: throw new NotSupportedException($"实体元数据 serializer 未实现：{serializerId}。");
            }
        }
    }

    private static void SkipItemStack(PacketReader r, MetadataProtocol protocol, int depth)
    {
        CheckDepth(depth); var count = r.ReadVarInt(); if (count <= 0) return; r.ReadVarInt();
        DataComponentNetworkCodec.SkipPatch(r, protocol == MetadataProtocol.V12110
            ? DataComponentProtocol.V12110 : DataComponentProtocol.V1218, depth + 1);
    }

    private static void SkipParticle(PacketReader r, MetadataProtocol protocol, int depth)
    {
        // 两个参考版本均未提供粒子注册表及载荷格式，不能复用 1.20.6 的粒子 id。
        throw new NotSupportedException($"协议 {protocol} 粒子元数据尚未适配，应保留完整原包。");
    }

    private static void SkipPaintingVariantReference(PacketReader r)
    {
        var marker = r.ReadVarInt();
        if (marker < 0) throw new InvalidDataException("画作变种 Holder 标记不能为负数。");
        if (marker == 0)
            throw new NotSupportedException("暂不解析内联画作元数据，应保留完整原包。");
    }

    private static void SkipResolvableProfile(PacketReader r)
    {
        var kind = r.ReadVarInt();
        if (kind == 0) { Optional(r, x => x.ReadString(16)); Optional(r, x => x.Skip(16)); }
        else if (kind == 1) { r.Skip(16); r.ReadString(16); }
        else throw new InvalidDataException($"未知 ResolvableProfile 类型：{kind}。");
        List(r, x => { x.ReadString(64); x.ReadString(); Optional(x, y => y.ReadString(1024)); }, 16);
        Optional(r, x => x.ReadString()); Optional(r, x => x.ReadString()); Optional(r, x => x.ReadString());
    }

    private static void List(PacketReader r, Action<PacketReader> read, int max)
    { var count = r.ReadVarInt(); if (count < 0 || count > max) throw new InvalidDataException($"元数据列表数量非法：{count}。"); for (var i = 0; i < count; i++) read(r); }
    private static void Optional(PacketReader r, Action<PacketReader> read) { if (r.ReadBoolean()) read(r); }
    private static void CheckDepth(int depth) { if (depth > MaxDepth) throw new InvalidDataException("元数据嵌套超过限制。"); }
}
