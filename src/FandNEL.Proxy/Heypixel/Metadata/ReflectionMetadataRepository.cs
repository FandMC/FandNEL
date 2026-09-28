using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;

namespace FandNEL.Proxy.Heypixel;

public static class ReflectionMetadataRepository
{
    private const string MetadataEndpoint = "https://api.codexus.today/api/PluginCipher/bjd/mapping/data";
    private const string SnapshotFileName = "mapping.snapshot.json";
    private const int MaximumDownloadBytes = 32 * 1024 * 1024;
    private const int MaximumExpandedBytes = 64 * 1024 * 1024;
    private const int MaximumArchiveEntries = 4096;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(30);
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly SemaphoreSlim InitializationGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };
    private static Dictionary<string, List<EnumMetadata>> _mappings = new(StringComparer.Ordinal);
    private static string? _loadedDirectory;
    private static string? _refreshDirectory;
    private static DateTimeOffset _nextRefresh;

    public static async Task InitializeAsync(
        CancellationToken cancellationToken = default,
        HttpClient? httpClient = null,
        string? cacheDirectory = null,
        bool refresh = false)
    {
        string directory = Path.GetFullPath(cacheDirectory ?? ReflectionMetadataPaths.CacheDirectory);
        await InitializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!refresh && string.Equals(_loadedDirectory, directory, StringComparison.Ordinal)
                && Volatile.Read(ref _mappings).Count > 0)
            {
                ScheduleRefresh(directory, httpClient, cancellationToken);
                return;
            }

            Dictionary<string, List<EnumMetadata>>? cached = null;
            try
            {
                cached = await ReadCacheAsync(directory, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            {
                Log.Warning("Heypixel: reflection metadata cache is invalid ({ErrorType})", exception.GetType().Name);
            }

            if (cached is { Count: > 0 } && !refresh)
            {
                Publish(cached, directory);
                ScheduleRefresh(directory, httpClient, cancellationToken);
                return;
            }

            try
            {
                HttpClient client = httpClient ?? SharedHttpClient;
                using var downloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                downloadCancellation.CancelAfter(DownloadTimeout);
                CancellationToken downloadToken = downloadCancellation.Token;
                using HttpResponseMessage locationResponse = await client.GetAsync(
                    MetadataEndpoint, HttpCompletionOption.ResponseHeadersRead, downloadToken).ConfigureAwait(false);
                locationResponse.EnsureSuccessStatusCode();
                byte[] locationBytes = await ReadLimitedAsync(locationResponse.Content, 64 * 1024, downloadToken)
                    .ConfigureAwait(false);
                using JsonDocument location = JsonDocument.Parse(locationBytes);
                if (!location.RootElement.TryGetProperty("body", out JsonElement body)
                    || body.ValueKind != JsonValueKind.String
                    || !Uri.TryCreate(body.GetString(), UriKind.Absolute, out Uri? archiveUri)
                    || archiveUri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new InvalidDataException("Heypixel metadata response has no valid HTTPS archive URL.");
                }

                using HttpResponseMessage archiveResponse = await client.GetAsync(
                    archiveUri, HttpCompletionOption.ResponseHeadersRead, downloadToken).ConfigureAwait(false);
                archiveResponse.EnsureSuccessStatusCode();
                byte[] bytes = await ReadLimitedAsync(archiveResponse.Content, MaximumDownloadBytes, downloadToken)
                    .ConfigureAwait(false);
                Dictionary<string, List<EnumMetadata>> downloaded = ReadArchive(bytes, downloadToken);
                Directory.CreateDirectory(directory);
                string snapshotPath = Path.Combine(directory, SnapshotFileName);
                string temporaryPath = snapshotPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(downloaded), cancellationToken)
                        .ConfigureAwait(false);
                    File.Move(temporaryPath, snapshotPath, overwrite: true);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }

                Publish(downloaded, directory);
                _refreshDirectory = directory;
                _nextRefresh = DateTimeOffset.UtcNow + RefreshInterval;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested
                && cached is { Count: > 0 }
                && exception is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or JsonException or OperationCanceledException)
            {
                Publish(cached, directory);
                Log.Warning("Heypixel: metadata refresh failed; using cached mappings ({ErrorType})", exception.GetType().Name);
            }
        }
        finally
        {
            InitializationGate.Release();
        }
    }

    public static EnumFieldMetadata? TryGetField(string className, string enumName, string fieldName)
    {
        Dictionary<string, List<EnumMetadata>> snapshot = Volatile.Read(ref _mappings);
        EnumMetadata? metadata = snapshot.TryGetValue(className, out List<EnumMetadata>? values)
            ? values.FirstOrDefault(value => value.EnumName == enumName)
            : null;
        return metadata is not null && metadata.Fields.TryGetValue(fieldName, out EnumFieldMetadata? field)
            ? new EnumFieldMetadata { Content = field.Content, HashCode = field.HashCode }
            : null;
    }

    public static string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var result = new StringBuilder(value.Length);
        var nonAsciiRun = new StringBuilder();
        foreach (char character in value)
        {
            if (character is >= ' ' and <= '~')
            {
                AppendHashedRun(result, nonAsciiRun);
                result.Append(character);
            }
            else
            {
                nonAsciiRun.Append(character);
            }
        }

        AppendHashedRun(result, nonAsciiRun);
        return result.ToString();
    }

    private static void Publish(Dictionary<string, List<EnumMetadata>> mappings, string directory)
    {
        Volatile.Write(ref _mappings, mappings);
        _loadedDirectory = directory;
    }

    private static void ScheduleRefresh(string directory, HttpClient? client, CancellationToken cancellationToken)
    {
        // Injected clients keep initialization deterministic for offline callers and tests.
        if (client is not null || (_refreshDirectory == directory && DateTimeOffset.UtcNow < _nextRefresh))
        {
            return;
        }

        _refreshDirectory = directory;
        _nextRefresh = DateTimeOffset.UtcNow + RefreshInterval;
        _ = RefreshInBackgroundAsync(directory, cancellationToken);
    }

    private static async Task RefreshInBackgroundAsync(string directory, CancellationToken cancellationToken)
    {
        try
        {
            await InitializeAsync(cancellationToken, cacheDirectory: directory, refresh: true).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The game connection ended before the optional refresh completed.
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException
            or UnauthorizedAccessException or JsonException or OperationCanceledException)
        {
            Log.Warning("Heypixel: background metadata refresh failed ({ErrorType})", exception.GetType().Name);
        }
    }

    private static async Task<Dictionary<string, List<EnumMetadata>>?> ReadCacheAsync(
        string directory, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        string snapshotPath = Path.Combine(directory, SnapshotFileName);
        string[] files = File.Exists(snapshotPath)
            ? [snapshotPath]
            : Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            return null;
        }

        var mappings = new Dictionary<string, List<EnumMetadata>>(StringComparer.Ordinal);
        long totalBytes = 0;
        foreach (string file in files)
        {
            totalBytes += new FileInfo(file).Length;
            if (totalBytes > MaximumExpandedBytes)
            {
                throw new InvalidDataException("Heypixel metadata cache exceeds its size limit.");
            }

            string json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            MergeMappings(mappings, json);
        }

        return mappings.Count > 0 ? mappings : null;
    }

    private static Dictionary<string, List<EnumMetadata>> ReadArchive(byte[] bytes, CancellationToken cancellationToken)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        if (archive.Entries.Count > MaximumArchiveEntries)
        {
            throw new InvalidDataException("Heypixel metadata archive contains too many entries.");
        }

        var mappings = new Dictionary<string, List<EnumMetadata>>(StringComparer.Ordinal);
        long totalBytes = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            totalBytes += entry.Length;
            if (totalBytes > MaximumExpandedBytes)
            {
                throw new InvalidDataException("Heypixel metadata archive exceeds its expanded size limit.");
            }

            using Stream stream = entry.Open();
            using var content = new MemoryStream();
            byte[] buffer = new byte[8192];
            int count;
            while ((count = stream.Read(buffer)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (content.Length + count > entry.Length)
                {
                    throw new InvalidDataException("Heypixel metadata archive has an invalid expanded size.");
                }

                content.Write(buffer, 0, count);
            }

            content.Position = 0;
            using var reader = new StreamReader(content, Encoding.UTF8);
            MergeMappings(mappings, reader.ReadToEnd());
        }

        if (mappings.Count == 0)
        {
            throw new InvalidDataException("Heypixel metadata archive contains no mappings.");
        }

        return mappings;
    }

    private static void MergeMappings(Dictionary<string, List<EnumMetadata>> target, string json)
    {
        Dictionary<string, List<EnumMetadata>> source = JsonSerializer.Deserialize<Dictionary<string, List<EnumMetadata>>>(
            json, JsonOptions) ?? throw new InvalidDataException("Heypixel metadata must contain a mapping object.");
        foreach ((string className, List<EnumMetadata> values) in source)
        {
            if (values is null || values.Any(value => value is null || value.EnumName is null || value.Fields is null
                || value.Fields.Values.Any(field => field is null || field.Content is null)))
            {
                throw new InvalidDataException("Heypixel metadata contains invalid enum fields.");
            }

            target[className] = values;
        }
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, int limit, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > limit)
        {
            throw new InvalidDataException("Heypixel metadata response exceeds its size limit.");
        }

        await using Stream input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (output.Length + count > limit)
            {
                throw new InvalidDataException("Heypixel metadata response exceeds its size limit.");
            }

            output.Write(buffer, 0, count);
        }

        return output.ToArray();
    }

    private static void AppendHashedRun(StringBuilder result, StringBuilder characters)
    {
        if (characters.Length == 0)
        {
            return;
        }

        byte[] input = Encoding.UTF8.GetBytes(characters.ToString() + "netease");
        result.Append(Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant());
        characters.Clear();
    }
}
