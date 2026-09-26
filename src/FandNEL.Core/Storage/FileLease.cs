using System.Diagnostics;

namespace FandNEL.Core.Storage;

/// <summary>
/// Serializes access to a document across store instances and application processes.
/// The lock file is retained intentionally: deleting it would allow Unix callers
/// to hold locks against different inodes.
/// </summary>
internal static class FileLease
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static async ValueTask<FileStream> AcquireAsync(string documentPath, CancellationToken cancellationToken)
    {
        EnsureDirectory(documentPath);
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return Open(documentPath);
            }
            catch (IOException) when (Stopwatch.GetElapsedTime(started) < Timeout)
            {
                await Task.Delay(40, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static FileStream Acquire(string documentPath)
    {
        EnsureDirectory(documentPath);
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            try
            {
                return Open(documentPath);
            }
            catch (IOException) when (Stopwatch.GetElapsedTime(started) < Timeout)
            {
                Thread.Sleep(40);
            }
        }
    }

    private static FileStream Open(string documentPath) => new(
        documentPath + ".lock",
        FileMode.OpenOrCreate,
        FileAccess.ReadWrite,
        FileShare.None);

    private static void EnsureDirectory(string documentPath)
    {
        var directory = Path.GetDirectoryName(documentPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }
}
