using System.Text.Json;
using System.Text.Json.Serialization;
using FandNEL.GameLauncher.Models;

namespace FandNEL.GameLauncher.Services;

/// <summary>以版本化 JSON 原子写入启动配置。</summary>
public sealed class JsonLaunchConfigWriter : ILaunchConfigWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task WriteAsync(LaunchConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var path = Path.GetFullPath(configuration.FilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + $".fandnel-{Guid.NewGuid():N}.tmp";
        var payload = new VersionedLaunchConfiguration(1, configuration);
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(stream, payload, SerializerOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(temporaryPath); } catch (IOException) { }
            throw;
        }
    }

    private sealed record VersionedLaunchConfiguration(int Version, LaunchConfiguration Value);
}

