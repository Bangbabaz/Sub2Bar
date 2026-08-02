using Sub2Bar.Core.Models;
using Sub2Bar.Core.Networking;
using Sub2Bar.Core.Services;
using Xunit;

namespace Sub2Bar.Core.Tests;

public sealed class RefreshCoordinatorTests
{
    private static readonly Credentials Credentials = new(
        "https://example.com/sub2api", "old-access", "refresh", "test-agent");

    [Fact]
    public async Task RefreshAsync_RefreshesRejectedAccessTokenOnce()
    {
        var client = new FakeClient { RejectFirstProfile = true };
        var coordinator = new RefreshCoordinator(client);

        var result = await coordinator.RefreshAsync(
            Credentials, [], TestContext.Current.CancellationToken);

        Assert.Equal(RefreshStatus.Loaded, result.Status);
        Assert.True(result.CredentialsChanged);
        Assert.Equal("new-access", result.UpdatedCredentials?.AccessToken);
        Assert.Equal(1, client.RefreshCalls);
        Assert.Equal(2, client.ProfileCalls);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotRefreshBindingMismatch()
    {
        var client = new FakeClient { BindingMismatch = true };
        var coordinator = new RefreshCoordinator(client);

        var result = await coordinator.RefreshAsync(
            Credentials, [], TestContext.Current.CancellationToken);

        Assert.Equal(RefreshStatus.RequiresAuthentication, result.Status);
        Assert.Equal(0, client.RefreshCalls);
    }

    [Fact]
    public async Task RefreshAsync_PreservesLastDataOnNetworkFailure()
    {
        var client = new FakeClient();
        var coordinator = new RefreshCoordinator(client);
        var first = await coordinator.RefreshAsync(
            Credentials, [], TestContext.Current.CancellationToken);
        client.NetworkFailure = true;

        var failed = await coordinator.RefreshAsync(
            Credentials, [], TestContext.Current.CancellationToken);

        Assert.Equal(RefreshStatus.Loaded, first.Status);
        Assert.Equal(RefreshStatus.Failed, failed.Status);
        Assert.Same(first.Data, failed.Data);
    }

    [Fact]
    public async Task RefreshAsync_AdminFailureDoesNotDiscardPersonalQuota()
    {
        var client = new FakeClient { AdminFailure = true };
        var coordinator = new RefreshCoordinator(client);

        var result = await coordinator.RefreshAsync(
            Credentials, [], TestContext.Current.CancellationToken);

        Assert.Equal(RefreshStatus.Loaded, result.Status);
        Assert.NotNull(result.Data?.Quota);
        Assert.NotNull(result.Data?.AdminErrorMessage);
    }

    private sealed class FakeClient : ISub2ApiClient
    {
        public bool RejectFirstProfile { get; init; }
        public bool BindingMismatch { get; init; }
        public bool NetworkFailure { get; set; }
        public bool AdminFailure { get; init; }
        public int ProfileCalls { get; private set; }
        public int RefreshCalls { get; private set; }

        public Task<CurrentUser> GetCurrentUserAsync(Credentials credentials, CancellationToken cancellationToken)
        {
            ProfileCalls++;
            if (BindingMismatch)
            {
                throw new ApiException(ApiErrorKind.SessionBindingMismatch, "mismatch");
            }

            if (NetworkFailure)
            {
                throw new ApiException(ApiErrorKind.Network, "offline");
            }

            if (RejectFirstProfile && ProfileCalls == 1)
            {
                throw new ApiException(ApiErrorKind.CredentialRejected, "expired");
            }

            return Task.FromResult(new CurrentUser(1, "demo", "admin"));
        }

        public Task<QuotaSnapshot> GetQuotaAsync(Credentials credentials, string accountName,
            CancellationToken cancellationToken) => Task.FromResult(new QuotaSnapshot(accountName, 18.75,
            [new SubscriptionQuota("1", "Claude Max", "active", null,
                [new QuotaWindow(QuotaPeriod.Daily, 3.5, 10, 35, null)])], DateTimeOffset.Now));

        public Task<PageResult<AdminAccount>> GetAdminAccountsAsync(Credentials credentials,
            CancellationToken cancellationToken)
        {
            if (AdminFailure)
            {
                throw new ApiException(ApiErrorKind.Network, "admin unavailable");
            }

            return Task.FromResult(new PageResult<AdminAccount>([], 1, 20, 0, 1));
        }

        public Task<PageResult<AdminSubscription>> GetAdminSubscriptionsAsync(Credentials credentials,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PageResult<AdminSubscription>([], 1, 20, 0, 1));

        public Task<AdminAccountUsage> GetAdminUsageAsync(Credentials credentials, long accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AdminAccountUsage(accountId, null, null, null));

        public Task<TokenPair> RefreshTokenAsync(Credentials credentials, CancellationToken cancellationToken)
        {
            RefreshCalls++;
            return Task.FromResult(new TokenPair("new-access", "new-refresh"));
        }
    }
}
