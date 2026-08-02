namespace Sub2Bar.Core.Models;

public enum RefreshStatus
{
    Idle,
    Refreshing,
    Loaded,
    Failed,
    RequiresAuthentication
}

public sealed record RefreshData(
    CurrentUser User,
    QuotaSnapshot Quota,
    IReadOnlyList<AdminAccount> AdminAccounts,
    IReadOnlyList<AdminSubscription> AdminSubscriptions,
    IReadOnlyDictionary<long, AdminAccountUsage> AdminUsage,
    DateTimeOffset RefreshedAt,
    string? AdminErrorMessage = null);

public sealed record RefreshResult(
    RefreshStatus Status,
    RefreshData? Data,
    string? ErrorMessage = null,
    bool CredentialsChanged = false,
    Credentials? UpdatedCredentials = null,
    string? ResponseBody = null,
    string? RequestPath = null);
