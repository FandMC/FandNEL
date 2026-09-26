using System.Text;

namespace FandNEL.Core.Diagnostics;

public interface ITraceSink
{
    ValueTask WriteAsync(string message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional file sink enabled by FANDNEL_TRACE=1. It is intentionally dependency-free
/// so HTTP clients can use it without pulling in a logging framework.
/// </summary>
public sealed class FileTraceSink : ITraceSink, IAsyncDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileTraceSink(string? path = null)
    {
        _path = Path.GetFullPath(path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FandNEL",
            "trace.log"));
    }

    public static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("FANDNEL_TRACE"), "1", StringComparison.Ordinal);

    public async ValueTask WriteAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            var line = $"[{DateTimeOffset.UtcNow:O}] {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(_path, line, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
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
}
