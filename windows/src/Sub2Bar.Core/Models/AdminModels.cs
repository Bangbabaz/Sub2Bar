namespace Sub2Bar.Core.Models;

public sealed record AdminAccount(
    long Id,
    string Name,
    string Username,
    string Email,
    string Platform,
    string Type,
    string Plan,
    string Status,
    int? Concurrency,
    int? CurrentConcurrency,
    int? Priority,
    bool IsSchedulable,
    IReadOnlyList<string> Groups,
    double? QuotaUsed,
    double? QuotaLimit)
{
    public string DisplayName => FirstNotEmpty(Username, Email, Name, $"#{Id}");

    private static string FirstNotEmpty(params string[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value));
}

public sealed record AdminUsageWindow(
    QuotaPeriod Period,
    double? Utilization,
    DateTimeOffset? ResetsAt,
    long? RemainingSeconds,
    long? Requests,
    long? Tokens,
    double? Cost,
    double? StandardCost,
    double? UserCost);

public sealed record AdminAccountUsage(
    long AccountId,
    DateTimeOffset? UpdatedAt,
    AdminUsageWindow? FiveHour,
    AdminUsageWindow? SevenDay);

public sealed record AdminSubscription(
    long Id,
    long? UserId,
    string Name,
    string Username,
    string Status,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<QuotaWindow> Windows);

public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total,
    int Pages);
