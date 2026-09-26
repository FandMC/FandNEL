namespace FandNEL.GameLauncher.Models;

/// <summary>待下载到游戏目录的资源。</summary>
public sealed record DownloadRequest(Uri Source, string RelativePath, string? Sha256 = null, long? ExpectedLength = null)
{
    public void Validate()
    {
        if (!Source.IsAbsoluteUri || Source.Scheme is not ("http" or "https"))
            throw new ArgumentException("资源地址必须是 HTTP 或 HTTPS URL。", nameof(Source));
        if (string.IsNullOrWhiteSpace(RelativePath))
            throw new ArgumentException("资源相对路径不能为空。", nameof(RelativePath));

        var normalized = RelativePath.Replace('\\', '/');
        if (Path.IsPathRooted(normalized) || normalized.Split('/').Any(static part => part == ".."))
            throw new ArgumentException("资源路径必须位于游戏目录内。", nameof(RelativePath));
        if (Sha256 is not null && (Sha256.Length != 64 || !Sha256.All(Uri.IsHexDigit)))
            throw new ArgumentException("SHA-256 校验值必须是 64 位十六进制字符串。", nameof(Sha256));
        if (ExpectedLength is < 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedLength));
    }
}

