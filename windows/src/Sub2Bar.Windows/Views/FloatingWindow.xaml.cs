using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Sub2Bar.Windows.Infrastructure;

namespace Sub2Bar.Windows.Views;

public partial class FloatingWindow : Window
{
    private const double ExpandedPanelWidth = 400;
    private const double ExpandedPanelHeight = 430;
    private const double MinimalPanelWidth = 100;
    private const double AdaptivePanelMaxHeight = 560;
    private readonly Func<bool, Task> _setMinimalMode;
    private bool _allowClose;
    private bool _hasBeenPositioned;
    private bool _isPinned;
    private bool _isCompact;
    private bool _minimalMode;
    private bool _minimalExpanded;
    private bool _updatingMinimalModeButton;
    private double _pinnedOpacity = 0.3;
    private readonly DispatcherTimer _compactTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(140)
    };

    public FloatingWindow(Func<bool, Task> setMinimalMode)
    {
        _setMinimalMode = setMinimalMode;
        InitializeComponent();
        _compactTimer.Tick += CompactTimer_Tick;
    }

    public void ShowNear()
    {
        var shouldPosition = !_hasBeenPositioned;
        if (shouldPosition)
        {
            Opacity = 0;
        }

        Show();
        if (shouldPosition)
        {
            UpdateLayout();
            WindowPlacement.PlaceNear(this);
            _hasBeenPositioned = true;
        }

        UpdatePinnedPresentation();
        Activate();
    }

    public void SetPinnedOpacity(int percentage)
    {
        _pinnedOpacity = Math.Clamp(percentage, 10, 100) / 100d;
        if ((_isPinned && _isCompact) || (_minimalMode && !IsMouseOver))
        {
            Opacity = _pinnedOpacity;
        }
    }

    public void SetAlwaysOnTop(bool enabled) => Topmost = enabled;

    public void SetMinimalMode(bool enabled)
    {
        _updatingMinimalModeButton = true;
        MinimalModeButton.IsChecked = enabled;
        _updatingMinimalModeButton = false;

        var modeChanged = _minimalMode != enabled;
        if (modeChanged)
        {
            _minimalMode = enabled;
            _compactTimer.Stop();
        }

        if (enabled)
        {
            if (modeChanged)
            {
                _minimalExpanded = IsMouseOver && FullPanel.Visibility == Visibility.Visible;
            }

            if (_minimalExpanded)
            {
                ShowExpandedPanel();
            }
            else
            {
                ShowMinimalPanel();
            }

            Opacity = IsMouseOver ? 1 : _pinnedOpacity;
        }
        else
        {
            _minimalExpanded = false;
            UpdatePinnedPresentation();
        }
    }

    public void CloseForExit()
    {
        _compactTimer.Stop();
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_minimalMode && ReferenceEquals(sender, MinimalPanel) && e.ClickCount >= 2)
        {
            _minimalExpanded = true;
            _compactTimer.Stop();
            ShowExpandedPanel();
            Opacity = 1;
            e.Handled = true;
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed || IsInsideButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        DragMove();
    }

    private static bool IsInsideButton(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is ButtonBase)
            {
                return true;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return false;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void PinButton_Checked(object sender, RoutedEventArgs e)
    {
        _isPinned = true;
        UpdatePinnedPresentation();
    }

    private void PinButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _isPinned = false;
        _compactTimer.Stop();
        SetCompactMode(false);
        Opacity = 1;
    }

    private async void MinimalModeButton_Checked(object sender, RoutedEventArgs e)
    {
        await ChangeMinimalModeAsync(true);
    }

    private async void MinimalModeButton_Unchecked(object sender, RoutedEventArgs e)
    {
        await ChangeMinimalModeAsync(false);
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _compactTimer.Stop();
        if (_minimalMode && !_minimalExpanded)
        {
            Opacity = 1;
            return;
        }

        ShowExpandedPanel();
        Opacity = 1;
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isPinned || _minimalMode)
        {
            _compactTimer.Stop();
            _compactTimer.Start();
        }
    }

    private void CompactTimer_Tick(object? sender, EventArgs e)
    {
        _compactTimer.Stop();
        if (!IsMouseOver && _minimalMode)
        {
            _minimalExpanded = false;
            ShowMinimalPanel();
            Opacity = _pinnedOpacity;
        }
        else if (!IsMouseOver && _isPinned)
        {
            SetCompactMode(true);
            Opacity = _pinnedOpacity;
        }
    }

    private void UpdatePinnedPresentation()
    {
        if (_minimalMode)
        {
            if (_minimalExpanded)
            {
                ShowExpandedPanel();
            }
            else
            {
                ShowMinimalPanel();
            }

            Opacity = IsMouseOver ? 1 : _pinnedOpacity;
            return;
        }

        var compact = _isPinned && !IsMouseOver;
        SetCompactMode(compact);
        Opacity = compact ? _pinnedOpacity : 1;
    }

    private void SetCompactMode(bool compact)
    {
        if (_minimalMode)
        {
            return;
        }

        _isCompact = compact;
        MinimalPanel.Visibility = Visibility.Collapsed;
        FullPanel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        PinnedCompactPanel.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        if (compact)
        {
            ApplyAdaptiveSize(ExpandedPanelWidth);
        }
        else
        {
            ApplyExpandedSize();
        }
    }

    private async Task ChangeMinimalModeAsync(bool enabled)
    {
        if (_updatingMinimalModeButton)
        {
            return;
        }

        SetMinimalMode(enabled);
        try
        {
            await _setMinimalMode(enabled);
        }
        catch
        {
            SetMinimalMode(!enabled);
        }
    }

    private void ShowExpandedPanel()
    {
        _isCompact = false;
        MinimalPanel.Visibility = Visibility.Collapsed;
        PinnedCompactPanel.Visibility = Visibility.Collapsed;
        FullPanel.Visibility = Visibility.Visible;
        ApplyExpandedSize();
    }

    private void ShowMinimalPanel()
    {
        _isCompact = false;
        FullPanel.Visibility = Visibility.Collapsed;
        PinnedCompactPanel.Visibility = Visibility.Collapsed;
        MinimalPanel.Visibility = Visibility.Visible;
        ApplyAdaptiveSize(MinimalPanelWidth);
    }

    private void ApplyExpandedSize()
    {
        ApplySize(ExpandedPanelWidth, ExpandedPanelHeight, false);
    }

    private void ApplyAdaptiveSize(double width)
    {
        ApplySize(width, double.NaN, true);
    }

    private void ApplySize(double width, double height, bool sizeToContent)
    {
        var preserveBottomRight = _hasBeenPositioned && ActualWidth > 0 && ActualHeight > 0;
        var right = Left + ActualWidth;
        var bottom = Top + ActualHeight;

        SizeToContent = System.Windows.SizeToContent.Manual;
        MaxHeight = sizeToContent ? AdaptivePanelMaxHeight : ExpandedPanelHeight;
        Width = width;
        Height = height;
        SizeToContent = sizeToContent
            ? System.Windows.SizeToContent.Height
            : System.Windows.SizeToContent.Manual;
        UpdateLayout();

        if (preserveBottomRight)
        {
            Left = right - ActualWidth;
            Top = bottom - ActualHeight;
        }
    }
}
