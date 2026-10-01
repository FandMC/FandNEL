namespace FandNEL.Proxy.Packet.Minecraft.Metadata;

/// <summary>元数据值的原始网络编码；未知值不可安全猜测长度，读取失败时应透传完整包。</summary>
public sealed record MetadataValue(int SerializerId, ReadOnlyMemory<byte> RawValue)
{
    public MetadataSerializer? KnownSerializer(MetadataProtocol protocol)
        => MetadataValueCodec.TryGetSerializer(protocol, SerializerId, out var value) ? value : null;
}

public sealed record MetadataEntry(byte Index, int SerializerId, ReadOnlyMemory<byte> RawValue)
{
    public MetadataValue Value => new(SerializerId, RawValue);
}
