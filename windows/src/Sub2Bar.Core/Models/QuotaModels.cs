namespace Sub2Bar.Core.Models;

public enum QuotaPeriod
{
    Daily,
    Weekly,
    Monthly,
    FiveHour,
    SevenDay,
    Other
}

public sealed record QuotaWindow(
    QuotaPeriod Period,
    double Used,
    double? Limit,
    double? UsedPercentage,
    DateTimeOffset? ResetsAt)
{
    public double? Remaining => Limit is > 0 ? Math.Max(0, Limit.Value - Used) : null;
    public double? RemainingPercentage => UsedPercentage is not null
        ? Math.Clamp(100 - UsedPercentage.Value, 0, 100)
        : Limit is > 0
            ? Math.Clamp((Limit.Value - Used) / Limit.Value * 100, 0, 100)
            : null;
}

public sealed record SubscriptionQuota(
    string Id,
    string Name,
    string Status,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<QuotaWindow> Windows)
{
    public bool IsActive => string.IsNullOrWhiteSpace(Status) ||
                            Status.Equals("active", StringComparison.OrdinalIgnoreCase);
}

public sealed record CurrentUser(long? Id, string Username, string Role)
{
    public bool IsAdmin => Role.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                           Role.Equals("administrator", StringComparison.OrdinalIgnoreCase);
}

public sealed record QuotaSnapshot(
    string AccountName,
    double? Balance,
    IReadOnlyList<SubscriptionQuota> Subscriptions,
    DateTimeOffset FetchedAt)
{
    public double? LowestRemainingPercentage
    {
        get
        {
            var values = Subscriptions
                .Where(subscription => subscription.IsActive)
                .SelectMany(subscription => subscription.Windows)
                .Select(window => window.RemainingPercentage)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToArray();
            return values.Length == 0 ? null : values.Min();
        }
    }
}
