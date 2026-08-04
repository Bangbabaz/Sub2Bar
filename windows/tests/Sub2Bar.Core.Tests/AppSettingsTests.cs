using System.Text.Json;
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
        Assert.True(settings.AlwaysOnTop);
        Assert.False(settings.MinimalMode);
        Assert.Equal(QuotaDisplayMode.RemainingAmount, settings.DisplayMode);
    }

    [Fact]
    public void AlwaysOnTop_DefaultsToEnabledWhenMissingFromStoredSettings()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"launchAtLogin\":false}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(settings);
        Assert.True(settings.AlwaysOnTop);
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
