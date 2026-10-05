using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Net.Http.Headers;

namespace FandNEL.GameLauncher.Downloads;

/// <summary>优先使用可恢复的并发 Range 下载；服务端不支持时回退到顺序下载。</summary>
internal static class FastHttpDownloader
{
    private const int BufferSize = 128 * 1024;
    private const int MaxConcurrentSegments = 8;
    private const int SegmentRetryCount = 3;
    private const long MinimumSegmentedDownloadSize = 1 << 20;
    private const long TargetSegmentSize = 4 << 20;

    public static async Task DownloadAsync(
        HttpClient client,
        Uri source,
        string destination,
        Action<long, long?>? reportProgress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        var metadata = await TryGetMetadataAsync(client, source, cancellationToken).ConfigureAwait(false);
        if (metadata is { SupportsRanges: true } metadataValue
            && metadataValue.Length >= MinimumSegmentedDownloadSize)
        {
            try
            {
                await DownloadInSegmentsAsync(client, source, destination, metadataValue.Length,
                    reportProgress, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                TryDelete(destination);
            }
        }

        await DownloadSingleAsync(client, source, destination, reportProgress, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DownloadMetadata?> TryGetMetadataAsync(
        HttpClient client,
        Uri source,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, source);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is not { } length)
                return null;
            return new DownloadMetadata(length, response.Headers.AcceptRanges.Contains("bytes"));
        }
        catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task DownloadInSegmentsAsync(
        HttpClient client,
        Uri source,
        string destination,
        long totalLength,
        Action<long, long?>? reportProgress,
        CancellationToken cancellationToken)
    {
        var segmentCount = Math.Min(
            MaxConcurrentSegments * 3,
            Math.Max(2, (int)Math.Ceiling(totalLength / (double)TargetSegmentSize)));
        using var mappedFile = MemoryMappedFile.CreateFromFile(
            destination,
            FileMode.Create,
            mapName: null,
            totalLength,
            MemoryMappedFileAccess.ReadWrite);
        using var semaphore = new SemaphoreSlim(MaxConcurrentSegments, MaxConcurrentSegments);
        var errors = new ConcurrentBag<Exception>();
        var progress = new TransferProgress(reportProgress, totalLength);

        var tasks = CalculateRanges(segmentCount, totalLength)
            .Select(range => DownloadRangeAsync(range));
        await Task.WhenAll(tasks).ConfigureAwait(false);
        if (!errors.IsEmpty)
            throw new AggregateException(errors);
        progress.Complete();

        async Task DownloadRangeAsync((long Start, long End) range)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                for (var attempt = 1; attempt <= SegmentRetryCount; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long attemptBytes = 0;
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, source);
                        request.Headers.Range = new RangeHeaderValue(range.Start, range.End);
                        using var response = await client.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                        if (response.StatusCode != HttpStatusCode.PartialContent)
                            throw new InvalidDataException("资源服务器未返回分段响应。");
                        var contentRange = response.Content.Headers.ContentRange;
                        if (contentRange is not null && (contentRange.From != range.Start || contentRange.To != range.End))
                            throw new InvalidDataException("资源服务器返回了错误的分段范围。");

                        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                        await using var output = mappedFile.CreateViewStream(
                            range.Start,
                            range.End - range.Start + 1,
                            MemoryMappedFileAccess.Write);
                        var buffer = new byte[BufferSize];
                        long completed = 0;
                        int length;
                        while ((length = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await output.WriteAsync(buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
                            completed += length;
                            attemptBytes += length;
                            progress.Add(length);
                        }

                        if (completed != range.End - range.Start + 1)
                            throw new InvalidDataException("资源分段下载不完整。");
                        return;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception) when (attempt < SegmentRetryCount)
                    {
                        if (attemptBytes != 0)
                            progress.Add(-attemptBytes);
                        await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        if (attemptBytes != 0)
                            progress.Add(-attemptBytes);
                        errors.Add(exception);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    private static async Task DownloadSingleAsync(
        HttpClient client,
        Uri source,
        string destination,
        Action<long, long?>? reportProgress,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            source,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var totalLength = response.Content.Headers.ContentLength;
        var progress = new TransferProgress(reportProgress, totalLength);
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[BufferSize];
        int length;
        while ((length = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
            progress.Add(length);
        }

        if (totalLength is { } expected && progress.BytesTransferred != expected)
            throw new InvalidDataException("资源下载不完整。");
        progress.Complete();
    }

    private static IEnumerable<(long Start, long End)> CalculateRanges(int count, long totalLength)
    {
        var segmentLength = totalLength / count;
        for (var index = 0; index < count; index++)
        {
            var start = index * segmentLength;
            var end = index == count - 1 ? totalLength - 1 : (index + 1) * segmentLength - 1;
            if (start <= end)
                yield return (start, end);
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { }
    }

    private readonly record struct DownloadMetadata(long Length, bool SupportsRanges);

    private sealed class TransferProgress(Action<long, long?>? callback, long? totalLength)
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly object _gate = new();
        private long _bytesTransferred;

        public long BytesTransferred => Interlocked.Read(ref _bytesTransferred);

        public void Add(long bytes)
        {
            var transferred = Interlocked.Add(ref _bytesTransferred, bytes);
            if (callback is null || _stopwatch.ElapsedMilliseconds < 150)
                return;
            lock (_gate)
            {
                if (_stopwatch.ElapsedMilliseconds < 150)
                    return;
                _stopwatch.Restart();
                callback(transferred, totalLength);
            }
        }

        public void Complete() => callback?.Invoke(BytesTransferred, totalLength);
    }
}
