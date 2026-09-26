using FandNEL.GameLauncher.Models;

namespace FandNEL.GameLauncher.Downloads;

public interface IResourceDownloader
{
    Task DownloadAsync(
        IReadOnlyList<DownloadRequest> resources,
        string destinationDirectory,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

