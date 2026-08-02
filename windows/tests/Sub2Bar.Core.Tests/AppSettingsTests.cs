using Sub2Bar.Core.Models;
using Xunit;

namespace Sub2Bar.Core.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void PinnedOpacity_DefaultsToThirtyPercent()
    {
        var settings = new AppSettings();

        Assert.Equal(30, settings.PinnedOpacityPercent);
        Assert.False(settings.MinimalMode);
        Assert.Equal(QuotaDisplayMode.RemainingAmount, settings.DisplayMode);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(45, 45)]
    [InlineData(120, 100)]
    public void Normalize_ClampsPinnedOpacity(int value, int expected)
    {
        var settings = new AppSettings { PinnedOpacityPercent = value };

        Assert.Equal(expected, settings.Normalize().PinnedOpacityPercent);
    }
}
