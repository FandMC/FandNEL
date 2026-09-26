namespace FandNEL.Core.Security;

/// <summary>
/// Provides authenticated protection for sensitive bytes.
/// Implementations must return a self-contained payload that can be persisted.
/// </summary>
public interface ISecretProtector
{
    byte[] Protect(ReadOnlySpan<byte> plaintext);

    byte[] Unprotect(ReadOnlySpan<byte> ciphertext);
}
