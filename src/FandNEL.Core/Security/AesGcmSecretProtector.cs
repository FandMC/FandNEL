using System.Security.Cryptography;

namespace FandNEL.Core.Security;

/// <summary>
/// Authenticated encryption using AES-GCM. The output format is:
/// version (1 byte), nonce (12 bytes), tag (16 bytes), ciphertext (N bytes).
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector, IDisposable
{
    private const byte CurrentVersion = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;
    private int _disposed;

    public AesGcmSecretProtector(ReadOnlySpan<byte> key)
    {
        if (key.Length is not (16 or 24 or 32))
        {
            throw new ArgumentException("AES-GCM keys must be 128, 192, or 256 bits.", nameof(key));
        }

        _key = key.ToArray();
    }

    public byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        ThrowIfDisposed();
        var output = new byte[1 + NonceSize + TagSize + plaintext.Length];
        output[0] = CurrentVersion;
        var nonce = output.AsSpan(1, NonceSize);
        var tag = output.AsSpan(1 + NonceSize, TagSize);
        var encrypted = output.AsSpan(1 + NonceSize + TagSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, encrypted, tag);
        return output;
    }

    public byte[] Unprotect(ReadOnlySpan<byte> ciphertext)
    {
        ThrowIfDisposed();
        if (ciphertext.Length < 1 + NonceSize + TagSize || ciphertext[0] != CurrentVersion)
        {
            throw new CryptographicException("The encrypted payload format is invalid or unsupported.");
        }

        var nonce = ciphertext.Slice(1, NonceSize);
        var tag = ciphertext.Slice(1 + NonceSize, TagSize);
        var encrypted = ciphertext.Slice(1 + NonceSize + TagSize);
        var plaintext = new byte[encrypted.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, encrypted, tag, plaintext);
        return plaintext;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            CryptographicOperations.ZeroMemory(_key);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
    }
}
