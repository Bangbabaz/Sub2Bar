using Sub2Bar.Core.Models;

namespace Sub2Bar.Windows.ViewModels;

public sealed record QuotaWindowDisplay(
    string Period,
    string Used,
    string UsedAndLimit,
    string Remaining,
    double? UsedPercentage,
    string Reset,
    string ToolTip)
{
    public bool HasUsedPercentage => UsedPercentage.HasValue;
    public bool HasLimit => !string.IsNullOrWhiteSpace(UsedAndLimit);
    public string RemainingAndReset => string.IsNullOrWhiteSpace(Reset)
        ? Remaining
        : $"{Remaining} · {Reset}";

    public static QuotaWindowDisplay From(QuotaWindow window, QuotaDisplayMode mode)
    {
        var period = window.Period switch
        {
            QuotaPeriod.Daily => "今日",
            QuotaPeriod.Weekly => "本周",
            QuotaPeriod.Monthly => "本月",
            QuotaPeriod.FiveHour => "5h",
            QuotaPeriod.SevenDay => "7d",
            _ => "额度"
        };
        var remainingPercent = window.RemainingPercentage;
        double? usedPercent = null;
        if (window.UsedPercentage is { } reportedUsedPercent)
        {
            usedPercent = Math.Clamp(reportedUsedPercent, 0, 100);
        }
        else if (window.Limit is > 0)
        {
            usedPercent = Math.Clamp(window.Used / window.Limit.Value * 100, 0, 100);
        }
        var used = $"已用 {Money(window.Used)}";
        var usedAndLimit = window.Limit.HasValue
            ? $"{Money(window.Used)} / {Money(window.Limit.Value)}"
            : string.Empty;
        var remaining = mode == QuotaDisplayMode.RemainingPercentage
            ? remainingPercent.HasValue
                ? $"剩余 {remainingPercent:0.#}%"
                : "不限额"
            : window.Remaining.HasValue
                ? $"剩余 {Money(window.Remaining.Value)}"
                : remainingPercent.HasValue
                    ? $"剩余 {remainingPercent:0.#}%"
                    : "不限额";
        var reset = ResetText(window.ResetsAt);
        var remainingDetail = remainingPercent.HasValue ? $"剩余 {remainingPercent:0.#}%" : "不限额";
        var tooltip = $"{period}\n{used}\n{remainingDetail}\n{reset}";
        return new QuotaWindowDisplay(period, used, usedAndLimit, remaining, usedPercent, reset, tooltip);
    }

    private static string Money(double value) => $"${value:0.##}";

    private static string ResetText(DateTimeOffset? resetsAt)
    {
        if (!resetsAt.HasValue)
        {
            return string.Empty;
        }

        var remaining = resetsAt.Value - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            return "即将重置";
        }

        if (remaining.TotalDays >= 1)
        {
            return $"{Math.Ceiling(remaining.TotalDays):0}d 后重置";
        }

        if (remaining.TotalHours >= 1)
        {
            return $"{Math.Ceiling(remaining.TotalHours):0}h 后重置";
        }

        return $"{Math.Max(1, Math.Ceiling(remaining.TotalMinutes)):0} 分钟后重置";
    }
}

public sealed record SubscriptionDisplay(
    string Name,
    string Expiry,
    IReadOnlyList<QuotaWindowDisplay> Windows)
{
    public static SubscriptionDisplay From(SubscriptionQuota subscription, QuotaDisplayMode mode) => new(
        subscription.Name,
        subscription.ExpiresAt.HasValue
            ? $"到期 {subscription.ExpiresAt.Value.ToLocalTime():yyyy-MM-dd}"
            : string.Empty,
        subscription.Windows.Select(window => QuotaWindowDisplay.From(window, mode)).ToArray());
}

public sealed record AdminUsageProgressDisplay(
    string Period,
    string Used,
    string Remaining,
    double? UsedPercentage,
    string Statistics)
{
    public bool HasUsedPercentage => UsedPercentage.HasValue;
    public bool HasStatistics => !string.IsNullOrWhiteSpace(Statistics);

    public static AdminUsageProgressDisplay From(string period, AdminUsageWindow? window)
    {
        var usedPercentage = window?.Utilization is { } utilization
            ? Math.Clamp(utilization, 0, 100)
            : (double?)null;
        return new AdminUsageProgressDisplay(
            period,
            usedPercentage.HasValue ? $"已用 {usedPercentage.Value:0.#}%" : "已用 --",
            usedPercentage.HasValue ? $"剩余 {100 - usedPercentage.Value:0.#}%" : "剩余 --",
            usedPercentage,
            StatisticsText(window));
    }

    private static string StatisticsText(AdminUsageWindow? window)
    {
        var userCost = window?.UserCost;
        if (window is null || (!window.Requests.HasValue && !window.Tokens.HasValue && !userCost.HasValue))
        {
            return string.Empty;
        }

        var requests = FormatScaled(window.Requests, 1_000d, "K");
        var tokens = FormatScaled(window.Tokens, 1_000_000d, "M");
        var amount = userCost.HasValue
            ? $"${Math.Round(userCost.Value, 2, MidpointRounding.AwayFromZero):0.00}"
            : "--";
        return $"请求 {requests} · Token {tokens} · 金额 {amount}";
    }

    private static string FormatScaled(long? value, double scale, string unit) =>
        value.HasValue ? $"{value.Value / scale:0.##}{unit}" : "--";
}

public sealed record AdminAccountDisplay(
    long Id,
    string Name,
    string Metadata,
    string UsedAndLimit,
    double UsedPercentage,
    bool HasUsage,
    IReadOnlyList<AdminUsageProgressDisplay> UsageProgress)
{
    public bool HasUsedAndLimit => !string.IsNullOrWhiteSpace(UsedAndLimit);
    public bool HasUsageProgress => UsageProgress.Count > 0;

    public static AdminAccountDisplay From(AdminAccount account, AdminAccountUsage? usage)
    {
        var plan = string.IsNullOrWhiteSpace(account.Plan) ? string.Empty : $" · {account.Plan}";
        if (account.Type.Equals("oauth", StringComparison.OrdinalIgnoreCase))
        {
            var usageProgress = new[]
            {
                AdminUsageProgressDisplay.From("5h", usage?.FiveHour),
                AdminUsageProgressDisplay.From("7d", usage?.SevenDay)
            };
            return new AdminAccountDisplay(account.Id, account.DisplayName,
                $"OAuth · {account.Platform}{plan}", string.Empty, 0, false,
                usageProgress);
        }

        var used = account.QuotaUsed ?? 0;
        var usedPercentage = account.QuotaLimit is > 0
            ? Math.Clamp(used / account.QuotaLimit.Value * 100, 0, 100)
            : 0;
        var usedAndLimit = account.QuotaLimit.HasValue
            ? $"${used:0.##} / ${account.QuotaLimit.Value:0.##}"
            : string.Empty;
        return new AdminAccountDisplay(account.Id, account.DisplayName,
            $"API Key · {account.Platform}{plan}", usedAndLimit, usedPercentage,
            account.QuotaLimit is > 0, []);
    }
}

public sealed record AdminSubscriptionDisplay(
    string Name,
    string Owner,
    string Expiry,
    IReadOnlyList<QuotaWindowDisplay> Windows)
{
    public static AdminSubscriptionDisplay From(AdminSubscription subscription, QuotaDisplayMode mode)
    {
        var boundedWindows = subscription.Windows.Where(window => window.Limit.HasValue).ToArray();
        var windows = boundedWindows.Length > 0 ? boundedWindows : subscription.Windows;
        return new AdminSubscriptionDisplay(
            subscription.Name,
            subscription.Username,
            subscription.ExpiresAt.HasValue
                ? $"到期 {subscription.ExpiresAt.Value.ToLocalTime():yyyy-MM-dd}"
                : "长期有效",
            windows.Select(window => QuotaWindowDisplay.From(window, mode)).ToArray());
    }
}

public sealed class SelectableAdminAccount : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private bool _isSelected;

    public SelectableAdminAccount(AdminAccount account, AdminAccountUsage? usage, bool isSelected)
    {
        Account = account;
        Display = AdminAccountDisplay.From(account, usage);
        _isSelected = isSelected;
    }

    public AdminAccount Account { get; }
    public AdminAccountDisplay Display { get; }
    public long Id => Account.Id;
    public string Name => string.IsNullOrWhiteSpace(Account.Name) ? Account.DisplayName : Account.Name;
    public string DisplayName => Account.DisplayName;
    public string StatusDetails => string.IsNullOrWhiteSpace(Account.Plan)
        ? $"· {Account.Platform} · {Account.Type}"
        : $"· {Account.Platform} · {Account.Type} {Account.Plan}";
    public string StatusLabel => Account.Status.Trim().ToLowerInvariant() switch
    {
        "active" when Account.IsSchedulable => "正常",
        "active" => "暂停",
        "inactive" => "停用",
        "error" => "错误",
        "rate_limited" => "限流",
        "temp_unschedulable" => "临时不可调度",
        "unschedulable" => "不可调度",
        "" => "未知",
        _ => Account.Status
    };
    public string StatusColor => Account.Status.Trim().ToLowerInvariant() switch
    {
        "active" when Account.IsSchedulable => "#35B97F",
        "inactive" or "error" => "#E45D5D",
        "rate_limited" or "temp_unschedulable" => "#F0A23A",
        _ => "#A9B0AC"
    };
    public string UsedAndLimit => Display.UsedAndLimit;
    public double UsedPercentage => Display.UsedPercentage;
    public bool HasUsage => Display.HasUsage;
    public bool HasUsedAndLimit => Display.HasUsedAndLimit;
    public bool HasUsageProgress => Display.HasUsageProgress;
    public IReadOnlyList<AdminUsageProgressDisplay> UsageProgress => Display.UsageProgress;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
