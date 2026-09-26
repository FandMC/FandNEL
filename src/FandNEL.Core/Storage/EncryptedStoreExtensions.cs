namespace FandNEL.Core.Storage;

public static class EncryptedStoreExtensions
{
    /// <summary>
    /// Releases stores that own resources while preserving the small
    /// <see cref="IEncryptedStore{T}"/> contract for custom implementations.
    /// </summary>
    public static ValueTask DisposeAsync<T>(this IEncryptedStore<T> store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store is IAsyncDisposable disposable
            ? disposable.DisposeAsync()
            : ValueTask.CompletedTask;
    }
}
