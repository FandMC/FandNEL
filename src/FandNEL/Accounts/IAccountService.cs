using FandNEL.Gateway;
using FandNEL.Protocol.Authentication;

namespace FandNEL.Accounts;

public interface IAccountService
{
    Task<IReadOnlyList<AccountSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<AccountSession> LoginAsync(
        LoginRequest request,
        ILoginChallengeHandler challengeHandler,
        CancellationToken cancellationToken = default);
    Task RenameAsync(string userId, string alias, CancellationToken cancellationToken = default);
    Task RemoveAsync(string userId, CancellationToken cancellationToken = default);
    Task RefreshAsync(string userId, CancellationToken cancellationToken = default);
}

