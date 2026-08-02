using System.Windows;
using Sub2Bar.Windows.Infrastructure;

namespace Sub2Bar.Windows;

public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private AppController? _controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new SingleInstance();
        if (!_singleInstance.IsPrimary)
        {
            await SingleInstance.SignalPrimaryAsync();
            Shutdown();
            return;
        }

        _controller = new AppController(_singleInstance,
            e.Args.Any(argument => argument.Equals("--background", StringComparison.OrdinalIgnoreCase)));
        try
        {
            await _controller.InitializeAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Sub2Bar 启动失败：{exception.Message}", "Sub2Bar",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}

