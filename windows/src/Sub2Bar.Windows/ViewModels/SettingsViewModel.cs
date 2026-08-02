using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sub2Bar.Core.Models;
using Sub2Bar.Core.Networking;

namespace Sub2Bar.Windows.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly Func<AppSettings, Task> _save;
    private readonly SemaphoreSlim _applyGate = new(1, 1);
    private IReadOnlyList<long> _displayedAdminAccountIds;
    private CancellationTokenSource? _applyDelay;
    private bool _ready;
    private bool _suppressApply;

    [ObservableProperty] private string _serverAddress;
    [ObservableProperty] private int _refreshIntervalMinutes;
    [ObservableProperty] private QuotaDisplayMode _displayMode;
    [ObservableProperty] private bool _launchAtLogin;
    [ObservableProperty] private bool _minimalMode;
    [ObservableProperty] private int _pinnedOpacityPercent;
    [ObservableProperty] private string? _validationMessage;

    public SettingsViewModel(AppSettings settings, string connectionStatus,
        bool isLoggedIn, bool isConnectionHealthy,
        Func<AppSettings, Task> save, Func<Task> refreshNow, Action relogin)
    {
        _save = save;
        _displayedAdminAccountIds = settings.DisplayedAdminAccountIds.ToArray();
        _serverAddress = settings.ServerAddress;
        _refreshIntervalMinutes = settings.RefreshIntervalMinutes;
        _displayMode = settings.DisplayMode;
        _launchAtLogin = settings.LaunchAtLogin;
        _minimalMode = settings.MinimalMode;
        _pinnedOpacityPercent = settings.PinnedOpacityPercent;
        ConnectionStatus = connectionStatus;
        IsLoggedIn = isLoggedIn;
        IsConnectionHealthy = isConnectionHealthy;
        RefreshIntervals = [1, 5, 15, 30, 60];
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
        RefreshNowCommand = new AsyncRelayCommand(refreshNow);
        ReloginCommand = new RelayCommand(relogin);
        _ready = true;
    }

    public IReadOnlyList<int> RefreshIntervals { get; }
    public string ConnectionStatus { get; }
    public bool IsLoggedIn { get; }
    public bool IsConnectionHealthy { get; }
    public string LoginStatus => IsLoggedIn ? "已登录" : "未登录";
    public IRelayCommand CloseCommand { get; }
    public IAsyncRelayCommand RefreshNowCommand { get; }
    public IRelayCommand ReloginCommand { get; }
    public event Action? CloseRequested;

    public void UpdateFrom(AppSettings settings)
    {
        _suppressApply = true;
        try
        {
            ServerAddress = settings.ServerAddress;
            RefreshIntervalMinutes = settings.RefreshIntervalMinutes;
            DisplayMode = settings.DisplayMode;
            LaunchAtLogin = settings.LaunchAtLogin;
            MinimalMode = settings.MinimalMode;
            PinnedOpacityPercent = settings.PinnedOpacityPercent;
            _displayedAdminAccountIds = settings.DisplayedAdminAccountIds.ToArray();
        }
        finally
        {
            _suppressApply = false;
        }
    }

    partial void OnServerAddressChanged(string value) => QueueApply();
    partial void OnRefreshIntervalMinutesChanged(int value) => QueueApply();
    partial void OnDisplayModeChanged(QuotaDisplayMode value) => QueueApply();
    partial void OnLaunchAtLoginChanged(bool value) => QueueApply();
    partial void OnMinimalModeChanged(bool value) => QueueApply();
    partial void OnPinnedOpacityPercentChanged(int value) => QueueApply();

    private void QueueApply()
    {
        if (!_ready || _suppressApply)
        {
            return;
        }

        _applyDelay?.Cancel();
        _applyDelay?.Dispose();
        _applyDelay = new CancellationTokenSource();
        _ = ApplyAfterDelayAsync(_applyDelay.Token);
    }

    private async Task ApplyAfterDelayAsync(CancellationToken cancellationToken)
    {
        var lockTaken = false;
        try
        {
            await Task.Delay(120, cancellationToken);
            await _applyGate.WaitAsync(cancellationToken);
            lockTaken = true;
            cancellationToken.ThrowIfCancellationRequested();

            var settings = new AppSettings
            {
                ServerAddress = Sub2Bar.Core.Networking.ServerAddress.Normalize(ServerAddress),
                RefreshIntervalMinutes = RefreshIntervalMinutes,
                DisplayMode = DisplayMode,
                LaunchAtLogin = LaunchAtLogin,
                MinimalMode = MinimalMode,
                PinnedOpacityPercent = PinnedOpacityPercent,
                DisplayedAdminAccountIds = _displayedAdminAccountIds
            };
            await _save(settings);
            ValidationMessage = null;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ValidationMessage = exception.Message;
        }
        finally
        {
            if (lockTaken)
            {
                _applyGate.Release();
            }
        }
    }
}
