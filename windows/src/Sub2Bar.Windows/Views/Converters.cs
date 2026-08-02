using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Sub2Bar.Windows.Views;

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

public sealed class ProgressBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Healthy = Frozen("#35B97F");
    private static readonly SolidColorBrush Critical = Frozen("#E45D5D");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var used = value is double percentage ? percentage : 0;
        return used >= 100 ? Critical : Healthy;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static SolidColorBrush Frozen(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}

public sealed class DisplayModeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "RemainingPercentage" => "剩余比例",
            "UsedAmount" => "已用额度",
            _ => "剩余额度"
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString()?.Equals(parameter?.ToString(), StringComparison.OrdinalIgnoreCase) == true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter is not null
            ? Enum.Parse(targetType, parameter.ToString()!, true)
            : Binding.DoNothing;
}

public sealed class NonZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
