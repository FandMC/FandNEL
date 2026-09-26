using System.Security.Cryptography;
using System.Text;

namespace FandNEL.Core.Storage;

/// <summary>
/// Provides a stable random device identifier stored in the local application data folder.
/// The identifier is not a hardware fingerprint and can be deleted to regenerate it.
/// </summary>
public sealed class DeviceIdentity
{
    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FandNEL",
        "device-id");
    private readonly string _path;
    private readonly object _sync = new();
    private string? _value;

    public DeviceIdentity(string? path = null)
    {
        _path = Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? DefaultPath : path);
    }

    public string GetOrCreate()
    {
        lock (_sync)
        {
            if (_value is not null)
            {
                return _value;
            }

            using var lease = FileLease.Acquire(_path);
            if (File.Exists(_path))
            {
                var existing = File.ReadAllText(_path, Encoding.UTF8).Trim();
                if (Guid.TryParse(existing, out var parsed))
                {
                    _value = parsed.ToString("D");
                    return _value;
                }
            }

            var value = Guid.NewGuid().ToString("D");
            var directory = Path.GetDirectoryName(_path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temporaryPath, value, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (OperatingSystem.IsWindows() && File.Exists(_path))
                {
                    File.Replace(temporaryPath, _path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temporaryPath, _path, overwrite: true);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            _value = value;
            return value;
        }
    }

    public string GetOrCreateHashed()
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(GetOrCreate()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
