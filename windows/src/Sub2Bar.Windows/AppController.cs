using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Sub2Bar.Core.Networking;
using Sub2Bar.Core.Services;
using Sub2Bar.Windows.Infrastructure;
using Sub2Bar.Windows.ViewModels;
using Sub2Bar.Windows.Views;

namespace Sub2Bar.Windows;

public sealed class AppController : IDisposable
{
    private readonly SingleInstance _singleInstance;
    private readonly bool _backgroundLaunch;
    private readonly SettingsStore _settingsStore = new();
    private readonly CredentialStore _credentialStore = new();
    private readonly StartupRegistration _startupRegistration = new();
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly DispatcherTimer _refreshTimer = new();
    private MainViewModel? _viewModel;
    private TrayIconService? _trayIcon;
    private FloatingWindow? _floatingWindow;
    private SettingsWindow? _settingsWindow;
    private LoginWindow? _loginWindow;
    private ServerResponseWindow? _responseWindow;
    private bool _exiting;

    public AppController(SingleInstance singleInstance, bool backgroundLaunch)
    {
        _singleInstance = singleInstance;
        _backgroundLaunch = backgroundLaunch;
    }

    public async Task InitializeAsync()
    {
        var apiClient = new Sub2ApiClient(_httpClient);
        _viewModel = new MainViewModel(new RefreshCoordinator(apiClient), _settingsStore,
            _credentialStore, _startupRegistration);
        await _viewModel.InitializeAsync();

        _floatingWindow = new FloatingWindow(_viewModel.SetMinimalModeAsync) { DataContext = _viewModel };
        _trayIcon = new TrayIconService(_viewModel, ToggleFlyout, ShowFlyout);
        _viewModel.LoginRequested += QueueLogin;
        _viewModel.SettingsRequested += ShowSettings;
        _viewModel.ExitRequested += Exit;
        _viewModel.SettingsChanged += ConfigureTimer;
        _viewModel.ServerResponseRequested += ShowServerResponse;
        _refreshTimer.Tick += RefreshTimer_Tick;
        ConfigureTimer();
        _singleInstance.StartListening(() => Application.Current.Dispatcher.BeginInvoke(ShowFlyout));

        if (!_viewModel.HasServerAddress)
        {
            if (!_backgroundLaunch)
            {
                ShowSettings();
            }
            return;
        }

        if (!_viewModel.CanRefresh)
        {
            if (!_backgroundLaunch)
            {
                ShowLogin();
            }
            return;
        }

        await _viewModel.RefreshAsync();
    }

    private void ToggleFlyout()
    {
        if (_floatingWindow is null || _trayIcon is null)
        {
            return;
        }

        if (_floatingWindow.IsVisible)
        {
            _floatingWindow.Hide();
        }
        else
        {
            _floatingWindow.ShowNear();
        }
    }

    private void ShowFlyout()
    {
        if (_floatingWindow is null || _trayIcon is null)
        {
            return;
        }

        _floatingWindow.ShowNear();
    }

    private void QueueLogin() => Application.Current.Dispatcher.BeginInvoke(ShowLogin);

    private void ShowLogin()
    {
        if (_viewModel is null || _exiting)
        {
            return;
        }

        if (!_viewModel.HasServerAddress)
        {
            ShowSettings();
            return;
        }

        _floatingWindow?.Hide();
        if (_loginWindow is not null)
        {
            _loginWindow.Activate();
            return;
        }

        _loginWindow = new LoginWindow(_viewModel.Settings.ServerAddress, _viewModel.CompleteLoginAsync);
        _loginWindow.Closed += (_, _) => _loginWindow = null;
        _loginWindow.Show();
        _loginWindow.Activate();
    }

    private void ShowSettings()
    {
        if (_viewModel is null || _exiting)
        {
            return;
        }

        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var connectionHealthy = _viewModel.StatusText is "连接正常" or "个人额度已更新";
        var settingsViewModel = new SettingsViewModel(_viewModel.Settings, _viewModel.StatusText,
            _viewModel.CanRefresh, connectionHealthy,
            _viewModel.ApplySettingsAsync, _viewModel.RefreshAsync,
            () => _viewModel.ReloginCommand.Execute(null));
        _settingsWindow = new SettingsWindow(settingsViewModel);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ShowServerResponse()
    {
        if (_viewModel is null || !_viewModel.HasServerResponse || _exiting)
        {
            return;
        }

        if (_responseWindow is not null)
        {
            _responseWindow.Activate();
            return;
        }

        _responseWindow = new ServerResponseWindow(_viewModel.ServerResponsePath,
            _viewModel.ServerResponseBody);
        _responseWindow.Closed += (_, _) => _responseWindow = null;
        _responseWindow.Show();
    }

    private void ConfigureTimer()
    {
        if (_viewModel is null)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(_viewModel.Settings.RefreshIntervalMinutes);
        if (_refreshTimer.Interval != interval)
        {
            _refreshTimer.Stop();
            _refreshTimer.Interval = interval;
        }
        if (!_refreshTimer.IsEnabled)
        {
            _refreshTimer.Start();
        }
        _floatingWindow?.SetPinnedOpacity(_viewModel.Settings.PinnedOpacityPercent);
        _floatingWindow?.SetMinimalMode(_viewModel.Settings.MinimalMode);
        (_settingsWindow?.DataContext as SettingsViewModel)?.UpdateFrom(_viewModel.Settings);
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_viewModel?.CanRefresh == true)
        {
            await _viewModel.RefreshAsync();
        }
    }

    private void Exit()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _refreshTimer.Stop();
        _loginWindow?.Close();
        _settingsWindow?.Close();
        _responseWindow?.Close();
        _floatingWindow?.CloseForExit();
        _trayIcon?.Dispose();
        _trayIcon = null;
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _httpClient.Dispose();
        _trayIcon?.Dispose();
    }
}
