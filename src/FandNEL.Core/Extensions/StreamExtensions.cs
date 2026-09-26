using System.Buffers.Binary;
using System.Net.Sockets;

namespace FandNEL.Core.Extensions;

public static class StreamExtensions
{
    public static async Task<MemoryStream> ReadSteamWithInt16Async(this NetworkStream stream, CancellationToken cancellationToken = default)
    {
        var prefix = new byte[2];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt16LittleEndian(prefix);
        if (length < 0) throw new InvalidDataException("认证帧长度不能为负数。");
        var body = new byte[length];
        await stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
        return new MemoryStream(body, writable: false);
    }
}
