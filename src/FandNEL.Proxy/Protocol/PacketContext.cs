using FandNEL.Proxy.Sessions;

namespace FandNEL.Proxy.Protocol;

public sealed class PacketContext
{
    private byte[] _payload;

    internal PacketContext(MinecraftConnection connection, PacketDirection direction, ConnectionState state, int packetId, byte[] payload)
    {
        Connection = connection;
        Direction = direction;
        State = state;
        PacketId = packetId;
        _payload = payload;
    }

    public MinecraftConnection Connection { get; }
    public PacketDirection Direction { get; }
    public ConnectionState State { get; }
    public ProtocolVersion Version => Connection.Version;
    public int PacketId { get; }
    public ReadOnlyMemory<byte> Payload => _payload;
    public bool IsCancelled { get; private set; }
    public bool PropagationStopped { get; private set; }
    public PacketReader CreateReader() => new(_payload);
    public void Cancel() => IsCancelled = true;
    public void StopPropagation() => PropagationStopped = true;

    public void ReplacePayload(ReadOnlySpan<byte> payload) => _payload = payload.ToArray();

    /// <summary>替换一个字段的字节范围，前缀和尾部由运行时保留，后续处理器看到修改后的载荷。</summary>
    public void ReplaceRange(int offset, int length, ReadOnlySpan<byte> replacement)
    {
        if (offset < 0 || length < 0 || offset > _payload.Length - length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        var updated = new byte[checked(_payload.Length - length + replacement.Length)];
        _payload.AsSpan(0, offset).CopyTo(updated);
        replacement.CopyTo(updated.AsSpan(offset));
        _payload.AsSpan(offset + length).CopyTo(updated.AsSpan(offset + replacement.Length));
        _payload = updated;
    }
}
