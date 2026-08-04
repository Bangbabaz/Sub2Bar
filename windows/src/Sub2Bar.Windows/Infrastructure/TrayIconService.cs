using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using Sub2Bar.Windows.ViewModels;

namespace Sub2Bar.Windows.Infrastructure;

public sealed class TrayIconService : IDisposable
{
    private readonly MainViewModel _viewModel;

    public TrayIconService(MainViewModel viewModel, Action toggleFlyout, Action showFlyout)
    {
        _viewModel = viewModel;
        Icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(
                new Uri("pack://application:,,,/Assets/Sub2Bar.ico", UriKind.Absolute)),
            ToolTipText = viewModel.TrayToolTip,
            ContextMenu = CreateMenu(showFlyout)
        };
        Icon.TrayLeftMouseUp += (_, _) => toggleFlyout();
        Icon.ForceCreate();
        _viewModel.PresentationChanged += Update;
        Update();
    }

    public TaskbarIcon Icon { get; }

    public void Update()
    {
        Icon.ToolTipText = Truncate(_viewModel.TrayToolTip, 120);
    }

    private ContextMenu CreateMenu(Action showFlyout)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItem("显示 Sub2Bar", showFlyout));
        menu.Items.Add(MenuItem("立即刷新", () => _ = _viewModel.RefreshAsync()));
        menu.Items.Add(MenuItem("打开控制台", () => _viewModel.OpenConsoleCommand.Execute(null)));
        menu.Items.Add(MenuItem("设置", () => _viewModel.OpenSettingsCommand.Execute(null)));
        menu.Items.Add(MenuItem("退出", () => _viewModel.ExitCommand.Execute(null)));
        return menu;
    }

    private static MenuItem MenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 1)] + "…";

    public void Dispose()
    {
        _viewModel.PresentationChanged -= Update;
        Icon.Dispose();
    }
}
