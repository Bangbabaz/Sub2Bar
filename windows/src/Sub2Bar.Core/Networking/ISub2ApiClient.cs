using Sub2Bar.Core.Models;

namespace Sub2Bar.Core.Networking;

public interface ISub2ApiClient
{
    Task<CurrentUser> GetCurrentUserAsync(Credentials credentials, CancellationToken cancellationToken);
    Task<QuotaSnapshot> GetQuotaAsync(Credentials credentials, string accountName,
        CancellationToken cancellationToken);
    Task<PageResult<AdminAccount>> GetAdminAccountsAsync(Credentials credentials,
        CancellationToken cancellationToken);
    Task<PageResult<AdminSubscription>> GetAdminSubscriptionsAsync(Credentials credentials,
        CancellationToken cancellationToken);
    Task<AdminAccountUsage> GetAdminUsageAsync(Credentials credentials, long accountId,
        CancellationToken cancellationToken);
    Task<TokenPair> RefreshTokenAsync(Credentials credentials, CancellationToken cancellationToken);
}
