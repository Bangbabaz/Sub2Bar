using System.Text.Json;
using Sub2Bar.Core.Models;

namespace Sub2Bar.Core.Parsing;

public static class QuotaParser
{
    public static QuotaSnapshot Parse(string json, string? fallbackAccountName = null,
        DateTimeOffset? fetchedAt = null)
    {
        var data = EnvelopeParser.Parse(json).Data;
        var now = fetchedAt ?? DateTimeOffset.Now;
        var accountName = FirstNotEmpty(
            JsonValue.String(data, "username", "account_name", "accountName", "name"),
            fallbackAccountName ?? string.Empty,
            "用户");

        var subscriptions = FindSubscriptions(data)
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select((item, index) => ParseSubscription(item, index, now))
            .ToArray();

        if (subscriptions.Length == 0 && LooksLikeQuota(data))
        {
            subscriptions = [ParseSubscription(data, 0, now)];
        }

        return new QuotaSnapshot(accountName, JsonValue.Double(data, "balance", "credit", "remaining_balance"),
            subscriptions, now);
    }

    private static IEnumerable<JsonElement> FindSubscriptions(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Array)
        {
            return data.EnumerateArray().ToArray();
        }

        if (JsonValue.TryGet(data, out var subscriptions,
                "subscriptions", "subscription_quotas", "subscriptionQuotas", "quotas", "items"))
        {
            return JsonValue.ArrayOrSingle(subscriptions);
        }

        return [];
    }

    private static SubscriptionQuota ParseSubscription(JsonElement item, int index, DateTimeOffset fetchedAt)
    {
        var group = JsonValue.TryGet(item, out var groupValue, "group") &&
                    groupValue.ValueKind == JsonValueKind.Object
            ? groupValue
            : default;

        var name = FirstNotEmpty(
            JsonValue.String(item, "name", "subscription_name", "title"),
            group.ValueKind == JsonValueKind.Object ? JsonValue.String(group, "name") : string.Empty,
            $"订阅 {index + 1}");
        var windows = new List<QuotaWindow>();

        if (JsonValue.TryGet(item, out var windowArray, "windows", "quota_windows", "quotaWindows"))
        {
            foreach (var window in JsonValue.ArrayOrSingle(windowArray))
            {
                if (window.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var period = ParsePeriod(JsonValue.String(window, "period", "type", "name"));
                windows.Add(ParseWindow(window, period, fetchedAt));
            }
        }

        AddNestedWindow(item, windows, "daily", QuotaPeriod.Daily, fetchedAt);
        AddNestedWindow(item, windows, "weekly", QuotaPeriod.Weekly, fetchedAt);
        AddNestedWindow(item, windows, "monthly", QuotaPeriod.Monthly, fetchedAt);

        var progress = JsonValue.TryGet(item, out var progressValue, "progress") &&
                       progressValue.ValueKind == JsonValueKind.Object
            ? progressValue
            : default;
        if (progress.ValueKind == JsonValueKind.Object)
        {
            AddNestedWindow(progress, windows, "daily", QuotaPeriod.Daily, fetchedAt);
            AddNestedWindow(progress, windows, "weekly", QuotaPeriod.Weekly, fetchedAt);
            AddNestedWindow(progress, windows, "monthly", QuotaPeriod.Monthly, fetchedAt);
        }

        AddFlatWindow(item, group, windows, "daily", QuotaPeriod.Daily, fetchedAt);
        AddFlatWindow(item, group, windows, "weekly", QuotaPeriod.Weekly, fetchedAt);
        AddFlatWindow(item, group, windows, "monthly", QuotaPeriod.Monthly, fetchedAt);

        var distinctWindows = windows
            .GroupBy(window => window.Period)
            .Select(grouping => grouping.OrderByDescending(Completeness).First())
            .ToArray();

        return new SubscriptionQuota(
            FirstNotEmpty(JsonValue.String(item, "id", "subscription_id", "subscriptionId"), (index + 1).ToString()),
            name,
            FirstNotEmpty(JsonValue.String(item, "status"), "active"),
            JsonValue.Date(item, "expires_at", "expiresAt", "expiration"),
            distinctWindows);
    }

    private static void AddNestedWindow(JsonElement source, ICollection<QuotaWindow> target, string name,
        QuotaPeriod period, DateTimeOffset fetchedAt)
    {
        if (JsonValue.TryGet(source, out var value, name) && value.ValueKind == JsonValueKind.Object)
        {
            target.Add(ParseWindow(value, period, fetchedAt));
        }
    }

    private static void AddFlatWindow(JsonElement item, JsonElement group, ICollection<QuotaWindow> target,
        string prefix, QuotaPeriod period, DateTimeOffset fetchedAt)
    {
        var used = JsonValue.Double(item, $"{prefix}_usage_usd", $"{prefix}_used", $"{prefix}_usage");
        var limit = JsonValue.Double(item, $"{prefix}_limit_usd", $"{prefix}_limit", $"{prefix}_quota");
        limit ??= group.ValueKind == JsonValueKind.Object
            ? JsonValue.Double(group, $"{prefix}_limit_usd", $"{prefix}_limit", $"{prefix}_quota")
            : null;
        var percentage = JsonValue.Double(item, $"{prefix}_percentage", $"{prefix}_used_percentage");
        var resetsAt = JsonValue.Date(item, $"{prefix}_resets_at", $"{prefix}_reset_at", $"{prefix}_reset");
        var windowStart = JsonValue.Date(item, $"{prefix}_window_start");
        resetsAt ??= period switch
        {
            QuotaPeriod.Daily => windowStart?.AddDays(1),
            QuotaPeriod.Weekly => windowStart?.AddDays(7),
            QuotaPeriod.Monthly => windowStart?.AddMonths(1),
            _ => null
        };
        var seconds = JsonValue.Long(item, $"{prefix}_reset_in_seconds", $"{prefix}_remaining_seconds");

        if (used.HasValue || limit.HasValue || percentage.HasValue || resetsAt.HasValue || seconds.HasValue)
        {
            target.Add(CreateWindow(period, used, limit, percentage, resetsAt, seconds, fetchedAt));
        }
    }

    private static QuotaWindow ParseWindow(JsonElement value, QuotaPeriod period, DateTimeOffset fetchedAt)
    {
        return CreateWindow(
            period,
            JsonValue.Double(value, "used", "usage", "used_usd", "usage_usd", "amount"),
            JsonValue.Double(value, "limit", "quota", "limit_usd", "quota_usd"),
            JsonValue.Double(value, "percentage", "used_percentage", "utilization"),
            JsonValue.Date(value, "resets_at", "reset_at", "resetAt"),
            JsonValue.Long(value, "reset_in_seconds", "remaining_seconds"),
            fetchedAt);
    }

    private static QuotaWindow CreateWindow(QuotaPeriod period, double? used, double? limit, double? percentage,
        DateTimeOffset? resetsAt, long? resetInSeconds, DateTimeOffset fetchedAt)
    {
        var normalizedPercentage = percentage;
        if (normalizedPercentage is > 0 and <= 1 && limit is > 1)
        {
            normalizedPercentage *= 100;
        }

        normalizedPercentage ??= limit is > 0 && used.HasValue
            ? used.Value / limit.Value * 100
            : null;

        return new QuotaWindow(period, used ?? 0, limit is > 0 ? limit : null,
            normalizedPercentage is null ? null : Math.Clamp(normalizedPercentage.Value, 0, 100),
            resetsAt ?? (resetInSeconds.HasValue ? fetchedAt.AddSeconds(resetInSeconds.Value) : null));
    }

    private static int Completeness(QuotaWindow window) =>
        (window.Limit.HasValue ? 4 : 0) + (window.Used != 0 ? 2 : 0) +
        (window.UsedPercentage.HasValue ? 1 : 0) + (window.ResetsAt.HasValue ? 1 : 0);

    private static QuotaPeriod ParsePeriod(string value) => value.Trim().ToLowerInvariant() switch
    {
        "daily" or "day" => QuotaPeriod.Daily,
        "weekly" or "week" => QuotaPeriod.Weekly,
        "monthly" or "month" => QuotaPeriod.Monthly,
        "five_hour" or "five-hour" or "5h" => QuotaPeriod.FiveHour,
        "seven_day" or "seven-day" or "7d" => QuotaPeriod.SevenDay,
        _ => QuotaPeriod.Other
    };

    private static bool LooksLikeQuota(JsonElement data) =>
        JsonValue.TryGet(data, out _, "daily", "weekly", "monthly", "daily_usage_usd", "daily_used");

    private static string FirstNotEmpty(params string[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value));
}
