namespace FandNEL.Proxy.Packet.Minecraft.Components;

/// <summary>已知组件的原始网络 payload。payload 不解码为 SDK 类型，写回时逐字节保留。</summary>
public sealed record RawDataComponent(int TypeId, ReadOnlyMemory<byte> RawPayload);

public sealed record ItemStackData(
    int Count,
    int ItemId,
    DataComponentPatch? Components,
    ReadOnlyMemory<byte> RawPayload = default)
{
    public bool IsEmpty => Count <= 0;
}
