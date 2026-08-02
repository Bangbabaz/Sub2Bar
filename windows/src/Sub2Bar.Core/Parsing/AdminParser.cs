using System.Text.Json;
using Sub2Bar.Core.Models;

namespace Sub2Bar.Core.Parsing;

public static class AdminParser
{
    public static PageResult<AdminAccount> ParseAccounts(string json)
    {
        var data = EnvelopeParser.Parse(json).Data;
        var items = FindItems(data)
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(ParseAccount)
            .Where(account => account.Id != 0)
            .ToArray();

        return new PageResult<AdminAccount>(items,
            JsonValue.Int(data, "page", "current_page") ?? 1,
            JsonValue.Int(data, "page_size", "pageSize", "per_page") ?? items.Length,
            JsonValue.Int(data, "total", "total_count") ?? items.Length,
            JsonValue.Int(data, "pages", "total_pages") ?? 1);
    }

    public static PageResult<AdminSubscription> ParseSubscriptions(string json)
    {
        var data = EnvelopeParser.Parse(json).Data;
        var items = FindItems(data)
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(ParseSubscription)
            .Where(subscription => subscription.Id != 0)
            .ToArray();

        return new PageResult<AdminSubscription>(items,
            JsonValue.Int(data, "page", "current_page") ?? 1,
            JsonValue.Int(data, "page_size", "pageSize", "per_page") ?? items.Length,
            JsonValue.Int(data, "total", "total_count") ?? items.Length,
            JsonValue.Int(data, "pages", "total_pages") ?? 1);
    }

    public static AdminAccountUsage ParseUsage(long accountId, string json)
    {
        var data = EnvelopeParser.Parse(json).Data;
        return new AdminAccountUsage(
            accountId,
            JsonValue.Date(data, "updated_at", "updatedAt"),
            ParseUsageWindow(data, "five_hour", QuotaPeriod.FiveHour),
            ParseUsageWindow(data, "seven_day", QuotaPeriod.SevenDay));
    }

    private static IEnumerable<JsonElement> FindItems(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Array)
        {
            return data.EnumerateArray().ToArray();
        }

        return JsonValue.TryGet(data, out var items, "items", "accounts", "results")
            ? JsonValue.ArrayOrSingle(items)
            : [];
    }

    private static AdminAccount ParseAccount(JsonElement item)
    {
        var credentials = JsonValue.TryGet(item, out var credentialsValue, "credentials") &&
                          credentialsValue.ValueKind == JsonValueKind.Object
            ? credentialsValue
            : default;
        var extra = JsonValue.TryGet(item, out var extraValue, "extra") &&
                    extraValue.ValueKind == JsonValueKind.Object
            ? extraValue
            : default;

        var groups = new List<string>();
        if (JsonValue.TryGet(item, out var groupsValue, "groups") && groupsValue.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in groupsValue.EnumerateArray())
            {
                var name = group.ValueKind == JsonValueKind.Object
                    ? JsonValue.String(group, "name")
                    : JsonValue.AsString(group) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    groups.Add(name);
                }
            }
        }

        return new AdminAccount(
            JsonValue.Long(item, "id", "account_id") ?? 0,
            JsonValue.String(item, "name", "remark", "label"),
            FirstNotEmpty(JsonValue.String(item, "username", "user_name"),
                credentials.ValueKind == JsonValueKind.Object ? JsonValue.String(credentials, "username") : string.Empty),
            FirstNotEmpty(JsonValue.String(item, "email"),
                credentials.ValueKind == JsonValueKind.Object ? JsonValue.String(credentials, "email") : string.Empty),
            JsonValue.String(item, "platform"),
            JsonValue.String(item, "type", "account_type"),
            FirstNotEmpty(JsonValue.String(item, "plan", "plan_type", "subscription_type", "account_tier"),
                credentials.ValueKind == JsonValueKind.Object
                    ? JsonValue.String(credentials, "plan", "plan_type", "subscription_type", "account_tier")
                    : string.Empty,
                extra.ValueKind == JsonValueKind.Object
                    ? JsonValue.String(extra, "plan", "plan_type", "subscription_type", "account_tier")
                    : string.Empty),
            JsonValue.String(item, "status"),
            JsonValue.Int(item, "concurrency"),
            JsonValue.Int(item, "current_concurrency"),
            JsonValue.Int(item, "priority"),
            JsonValue.Bool(item, "schedulable", "is_schedulable") ?? false,
            groups,
            JsonValue.Double(item, "quota_used") ??
                (extra.ValueKind == JsonValueKind.Object ? JsonValue.Double(extra, "quota_used") : null),
            JsonValue.Double(item, "quota_limit") ??
                (extra.ValueKind == JsonValueKind.Object ? JsonValue.Double(extra, "quota_limit") : null));
    }

    private static AdminSubscription ParseSubscription(JsonElement item)
    {
        var group = JsonValue.TryGet(item, out var groupValue, "group") &&
                    groupValue.ValueKind == JsonValueKind.Object
            ? groupValue
            : default;
        var user = JsonValue.TryGet(item, out var userValue, "user") &&
                   userValue.ValueKind == JsonValueKind.Object
            ? userValue
            : default;
        var windows = new List<QuotaWindow>();
        AddSubscriptionWindow(item, group, windows, "daily", QuotaPeriod.Daily);
        AddSubscriptionWindow(item, group, windows, "weekly", QuotaPeriod.Weekly);
        AddSubscriptionWindow(item, group, windows, "monthly", QuotaPeriod.Monthly);

        return new AdminSubscription(
            JsonValue.Long(item, "id", "subscription_id") ?? 0,
            JsonValue.Long(item, "user_id", "userId", "account_id"),
            FirstNotEmpty(JsonValue.String(item, "name", "subscription_name", "plan_name"),
                group.ValueKind == JsonValueKind.Object ? JsonValue.String(group, "name") : string.Empty,
                "订阅"),
            FirstNotEmpty(JsonValue.String(item, "username", "user_name"),
                JsonValue.String(item, "email"),
                user.ValueKind == JsonValueKind.Object ? JsonValue.String(user, "username", "user_name") : string.Empty,
                user.ValueKind == JsonValueKind.Object ? JsonValue.String(user, "email") : string.Empty,
                user.ValueKind == JsonValueKind.Object ? JsonValue.String(user, "name") : string.Empty,
                "未知账号"),
            JsonValue.String(item, "status"),
            JsonValue.Date(item, "expires_at", "expiresAt"),
            windows);
    }

    private static void AddSubscriptionWindow(JsonElement item, JsonElement group,
        ICollection<QuotaWindow> windows, string prefix, QuotaPeriod period)
    {
        var used = JsonValue.Double(item, $"{prefix}_usage_usd", $"{prefix}_used", $"{prefix}_usage");
        var limit = JsonValue.Double(item, $"{prefix}_limit_usd", $"{prefix}_limit", $"{prefix}_quota");
        limit ??= group.ValueKind == JsonValueKind.Object
            ? JsonValue.Double(group, $"{prefix}_limit_usd", $"{prefix}_limit", $"{prefix}_quota")
            : null;
        var windowStart = JsonValue.Date(item, $"{prefix}_window_start");
        if (!used.HasValue && !limit.HasValue && !windowStart.HasValue)
        {
            return;
        }

        var resetsAt = period switch
        {
            QuotaPeriod.Daily => windowStart?.AddDays(1),
            QuotaPeriod.Weekly => windowStart?.AddDays(7),
            QuotaPeriod.Monthly => windowStart?.AddMonths(1),
            _ => null
        };
        var normalizedLimit = limit is > 0 ? limit : null;
        double? usedPercentage = normalizedLimit.HasValue && used.HasValue
            ? Math.Clamp(used.Value / normalizedLimit.Value * 100, 0, 100)
            : null;
        windows.Add(new QuotaWindow(period, used ?? 0, normalizedLimit, usedPercentage, resetsAt));
    }

    private static AdminUsageWindow? ParseUsageWindow(JsonElement data, string propertyName, QuotaPeriod period)
    {
        if (!JsonValue.TryGet(data, out var value, propertyName) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var stats = JsonValue.TryGet(value, out var statsValue, "window_stats", "stats") &&
                    statsValue.ValueKind == JsonValueKind.Object
            ? statsValue
            : default;
        return new AdminUsageWindow(
            period,
            JsonValue.Double(value, "utilization", "percentage"),
            JsonValue.Date(value, "resets_at", "reset_at"),
            JsonValue.Long(value, "remaining_seconds"),
            stats.ValueKind == JsonValueKind.Object ? JsonValue.Long(stats, "requests") : null,
            stats.ValueKind == JsonValueKind.Object ? JsonValue.Long(stats, "tokens") : null,
            stats.ValueKind == JsonValueKind.Object ? JsonValue.Double(stats, "cost") : null,
            stats.ValueKind == JsonValueKind.Object
                ? JsonValue.Double(stats, "standard_cost") ?? JsonValue.Double(stats, "cost")
                : null,
            stats.ValueKind == JsonValueKind.Object
                ? JsonValue.Double(stats, "user_cost") ?? JsonValue.Double(stats, "cost")
                : null);
    }

    private static string FirstNotEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
