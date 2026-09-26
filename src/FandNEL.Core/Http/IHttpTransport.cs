namespace FandNEL.Core.Http;

public interface IHttpTransport : IAsyncDisposable
{
    ValueTask<HttpResponseData> SendAsync(
        HttpRequestSpec request,
        CancellationToken cancellationToken = default);
}
