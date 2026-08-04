namespace Sub2Bar.Core.Models;

public enum QuotaDisplayMode
{
    RemainingAmount,
    RemainingPercentage,
    UsedAmount
}

public sealed record AppSettings
{
    public string ServerAddress { get; init; } = string.Empty;
    public int RefreshIntervalMinutes { get; init; } = 5;
    public QuotaDisplayMode DisplayMode { get; init; } = QuotaDisplayMode.RemainingAmount;
    public IReadOnlyList<long> DisplayedAdminAccountIds { get; init; } = [];
    public bool LaunchAtLogin { get; init; }
    public bool AlwaysOnTop { get; init; } = true;
    public bool MinimalMode { get; init; }
    public int PinnedOpacityPercent { get; init; } = 30;

    public AppSettings Normalize() => this with
    {
        ServerAddress = ServerAddress.Trim(),
        RefreshIntervalMinutes = RefreshIntervalMinutes is 1 or 5 or 15 or 30 or 60
            ? RefreshIntervalMinutes
            : 5,
        PinnedOpacityPercent = Math.Clamp(PinnedOpacityPercent, 10, 100),
        DisplayedAdminAccountIds = DisplayedAdminAccountIds.Distinct().ToArray()
    };
}
