namespace FandNEL.Core.Storage;

public interface IEncryptedStore<T>
{
    ValueTask<T?> ReadAsync(CancellationToken cancellationToken = default);

    ValueTask WriteAsync(T value, CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(CancellationToken cancellationToken = default);
}
