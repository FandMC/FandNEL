using System.Security.Cryptography;
using System.Text;

namespace FandNEL.Core.Security;

/// <summary>
/// Owns a mutable byte buffer and clears it when disposed.
/// </summary>
public sealed class SecureBytes : IDisposable
{
    private byte[]? _buffer;

    private SecureBytes(byte[] buffer) => _buffer = buffer;

    public int Length => _buffer?.Length ?? 0;

    public static SecureBytes Copy(ReadOnlySpan<byte> source) => new(source.ToArray());

    public static SecureBytes FromUtf8(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new SecureBytes(Encoding.UTF8.GetBytes(value));
    }

    public byte[] ToArray()
    {
        var buffer = _buffer ?? throw new ObjectDisposedException(nameof(SecureBytes));
        return buffer.ToArray();
    }

    public ReadOnlySpan<byte> AsSpan() =>
        _buffer is { } buffer ? buffer : throw new ObjectDisposedException(nameof(SecureBytes));

    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is not null)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }

        GC.SuppressFinalize(this);
    }

    ~SecureBytes() => Dispose();
}
