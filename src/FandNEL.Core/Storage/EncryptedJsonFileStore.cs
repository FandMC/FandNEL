using System.Security.Cryptography;
using System.Text.Json;
using FandNEL.Core.Security;
using FandNEL.Core.Serialization;

namespace FandNEL.Core.Storage;

/// <summary>
/// Stores one versioned JSON document encrypted by an <see cref="ISecretProtector"/>.
/// Writes are serialized and committed by replacing a temporary file.
/// </summary>
public sealed class EncryptedJsonFileStore<T> : IEncryptedStore<T>, IAsyncDisposable
{
    private readonly string _path;
    private readonly ISecretProtector _protector;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly int _version;
    private readonly IJsonDocumentMigrator<T>? _migrator;

    public EncryptedJsonFileStore(
        string path,
        ISecretProtector protector,
        JsonSerializerOptions? jsonOptions = null,
        int version = 1,
        IJsonDocumentMigrator<T>? migrator = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(protector);
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "The document version must be positive.");
        }

        _path = Path.GetFullPath(path);
        _protector = protector;
        _jsonOptions = jsonOptions ?? JsonDefaults.Create();
        _version = version;
        _migrator = migrator;
    }

    public async ValueTask<T?> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var lease = await FileLease.AcquireAsync(_path, cancellationToken).ConfigureAwait(false);
            if (!File.Exists(_path))
            {
                return default;
            }

            var encrypted = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
            var json = _protector.Unprotect(encrypted);
            try
            {
                var document = JsonSerializer.Deserialize<VersionedDocument<JsonElement>>(json, _jsonOptions)
                    ?? throw new InvalidDataException("The encrypted document is empty.");

                if (document.Version < 1 || document.Version > _version)
                {
                    throw new InvalidDataException(
                        $"The encrypted document version {document.Version} is not supported by version {_version}.");
                }

                if (document.Version < _version)
                {
                    return _migrator is not null
                        ? _migrator.Migrate(document.Version, document.Payload)
                        : throw new InvalidDataException($"No migration is registered for document version {document.Version}.");
                }

                return document.Payload.Deserialize<T>(_jsonOptions)
                    ?? throw new InvalidDataException("The encrypted document payload is null.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The encrypted document contains invalid JSON.", exception);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(json);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask WriteAsync(T value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var lease = await FileLease.AcquireAsync(_path, cancellationToken).ConfigureAwait(false);
            var directory = Path.GetDirectoryName(_path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            var document = new VersionedDocument<T>(_version, value);
            var json = JsonSerializer.SerializeToUtf8Bytes(document, _jsonOptions);
            byte[] encrypted;
            try
            {
                encrypted = _protector.Protect(json);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(json);
            }

            var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await WriteAndFlushAsync(temporaryPath, encrypted, cancellationToken).ConfigureAwait(false);
                CommitTemporaryFile(temporaryPath);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(encrypted);
                TryDelete(temporaryPath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DeleteAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var lease = await FileLease.AcquireAsync(_path, cancellationToken).ConfigureAwait(false);
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async Task WriteAndFlushAsync(string path, byte[] payload, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private void CommitTemporaryFile(string temporaryPath)
    {
        if (OperatingSystem.IsWindows() && File.Exists(_path))
        {
            File.Replace(temporaryPath, _path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temporaryPath, _path, overwrite: true);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup after a failed commit.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup after a failed commit.
        }
    }
}
