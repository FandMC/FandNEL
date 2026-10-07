using System.Security.Cryptography;

namespace FandNEL.Core.Security;

/// <summary>
/// Cross-platform file-key protector for Linux/macOS. A random 256-bit key is stored
/// with user-only file permissions in the local application data folder; deleting the
/// key file invalidates previously protected payloads, mirroring the DPAPI user scope.
/// </summary>
public sealed class AesGcmFileSecretProtector : ISecretProtector, IDisposable
{
    private readonly AesGcmSecretProtector _inner;
    private readonly bool _ownsInner;
    private int _disposed;

    public AesGcmFileSecretProtector(string keyFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyFilePath);
        _inner = new AesGcmSecretProtector(LoadOrCreateKey(keyFilePath));
        _ownsInner = true;
    }

    private AesGcmFileSecretProtector(AesGcmSecretProtector inner) => _inner = inner;

    /// <summary>Creates the platform protector: DPAPI on Windows, file-key AES-GCM elsewhere.</summary>
    public static ISecretProtector CreatePlatformProtector(string? keyDirectory = null)
    {
        if (OperatingSystem.IsWindows())
            return new WindowsDpapiSecretProtector();
        var directory = keyDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FandNEL", "keys");
        return new AesGcmFileSecretProtector(Path.Combine(directory, "protected-key"));
    }

    public byte[] Protect(ReadOnlySpan<byte> plaintext) => _inner.Protect(plaintext);

    public byte[] Unprotect(ReadOnlySpan<byte> ciphertext) => _inner.Unprotect(ciphertext);

    private static byte[] LoadOrCreateKey(string keyFilePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(keyFilePath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        if (File.Exists(keyFilePath))
        {
            var existing = File.ReadAllBytes(keyFilePath);
            if (existing.Length == 32)
                return existing;
            // Key files of unexpected size are treated as corrupt and regenerated.
            File.Delete(keyFilePath);
        }

        var key = RandomNumberGenerator.GetBytes(32);
        var temporaryPath = $"{keyFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(key);
                stream.Flush(flushToDisk: true);
            }
            ApplyUserOnlyPermissions(temporaryPath);
            File.Move(temporaryPath, keyFilePath, overwrite: true);
            ApplyUserOnlyPermissions(keyFilePath);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { }
        }
        // Reload through the file so both creations and restarts observe identical permissions.
        return File.ReadAllBytes(keyFilePath);
    }

    private static void ApplyUserOnlyPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
            return;
        // Owner read/write only; UnixFileMode is a no-op metadata call on Windows.
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        if (_ownsInner)
            _inner.Dispose();
    }
}
