using Sub2Bar.Core.Models;
using Sub2Bar.Core.Networking;

namespace Sub2Bar.Core.Services;

public sealed class RefreshCoordinator(ISub2ApiClient apiClient, TimeProvider? timeProvider = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private RefreshResult? _lastResult;

    public RefreshResult? LastResult => _lastResult;

    public async Task<RefreshResult> RefreshAsync(Credentials credentials,
        IReadOnlyCollection<long> displayedAdminAccountIds, CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return _lastResult ?? new RefreshResult(RefreshStatus.Refreshing, null);
        }

        try
        {
            if (!credentials.HasAccessToken || string.IsNullOrWhiteSpace(credentials.BoundUserAgent))
            {
                return SetLast(new RefreshResult(RefreshStatus.RequiresAuthentication, _lastResult?.Data,
                    "请登录 Sub2API。"));
            }

            try
            {
                var data = await LoadDataAsync(credentials, displayedAdminAccountIds, cancellationToken);
                return SetLast(new RefreshResult(RefreshStatus.Loaded, data));
            }
            catch (ApiException exception) when (exception.Kind == ApiErrorKind.SessionBindingMismatch)
            {
                return SetLast(new RefreshResult(RefreshStatus.RequiresAuthentication, _lastResult?.Data,
                    "登录会话与当前网络不匹配，请重新登录。"));
            }
            catch (ApiException exception) when (exception.Kind == ApiErrorKind.CredentialRejected)
            {
                return await RefreshCredentialsAndRetryAsync(credentials, displayedAdminAccountIds,
                    exception, cancellationToken);
            }
            catch (ApiException exception)
            {
                return SetLast(new RefreshResult(RefreshStatus.Failed, _lastResult?.Data, exception.Message,
                    ResponseBody: exception.ResponseBody, RequestPath: exception.RequestPath));
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RefreshResult> RefreshCredentialsAndRetryAsync(Credentials credentials,
        IReadOnlyCollection<long> displayedAdminAccountIds, ApiException originalException,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
        {
            return SetLast(new RefreshResult(RefreshStatus.RequiresAuthentication, _lastResult?.Data,
                originalException.Message));
        }

        try
        {
            var pair = await apiClient.RefreshTokenAsync(credentials, cancellationToken);
            var updated = credentials with { AccessToken = pair.AccessToken, RefreshToken = pair.RefreshToken };
            var data = await LoadDataAsync(updated, displayedAdminAccountIds, cancellationToken);
            return SetLast(new RefreshResult(RefreshStatus.Loaded, data,
                CredentialsChanged: true, UpdatedCredentials: updated));
        }
        catch (ApiException exception) when (exception.IsCredentialRejected)
        {
            return SetLast(new RefreshResult(RefreshStatus.RequiresAuthentication, _lastResult?.Data,
                "登录已失效，请重新登录。"));
        }
        catch (ApiException exception)
        {
            return SetLast(new RefreshResult(RefreshStatus.Failed, _lastResult?.Data, exception.Message,
                ResponseBody: exception.ResponseBody, RequestPath: exception.RequestPath));
        }
    }

    private async Task<RefreshData> LoadDataAsync(Credentials credentials,
        IReadOnlyCollection<long> displayedAdminAccountIds, CancellationToken cancellationToken)
    {
        var user = await apiClient.GetCurrentUserAsync(credentials, cancellationToken);
        var quota = await apiClient.GetQuotaAsync(credentials, user.Username, cancellationToken);
        IReadOnlyList<AdminAccount> accounts = [];
        IReadOnlyList<AdminSubscription> subscriptions = [];
        var usage = new Dictionary<long, AdminAccountUsage>();
        string? adminError = null;

        if (user.IsAdmin)
        {
            try
            {
                accounts = (await apiClient.GetAdminAccountsAsync(credentials, cancellationToken)).Items;
            }
            catch (ApiException exception) when (!exception.IsCredentialRejected)
            {
                adminError = exception.Message;
            }

            try
            {
                subscriptions = (await apiClient.GetAdminSubscriptionsAsync(credentials, cancellationToken)).Items;
            }
            catch (ApiException exception) when (!exception.IsCredentialRejected)
            {
                adminError ??= exception.Message;
            }

            // Selection only controls the "我的额度" presentation; account management always needs usage.
            foreach (var account in accounts.Where(account =>
                         account.Type.Equals("oauth", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    usage[account.Id] = await apiClient.GetAdminUsageAsync(credentials, account.Id,
                        cancellationToken);
                }
                catch (ApiException exception) when (!exception.IsCredentialRejected)
                {
                    adminError ??= exception.Message;
                }
            }
        }

        return new RefreshData(user, quota, accounts, subscriptions, usage, _timeProvider.GetLocalNow(), adminError);
    }

    private RefreshResult SetLast(RefreshResult result)
    {
        _lastResult = result;
        return result;
    }
}
