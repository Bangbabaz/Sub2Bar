using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sub2Bar.Core.Models;
using Sub2Bar.Core.Networking;
using Sub2Bar.Core.Services;
using Sub2Bar.Windows.Infrastructure;

namespace Sub2Bar.Windows.ViewModels;

public enum TrayVisualState
{
    Normal,
    Warning,
    Error,
    Refreshing
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly RefreshCoordinator _refreshCoordinator;
    private readonly SettingsStore _settingsStore;
    private readonly CredentialStore _credentialStore;
    private readonly StartupRegistration _startupRegistration;
    private AppSettings _settings = new();
    private Credentials? _credentials;
    private RefreshData? _data;

    [ObservableProperty] private string _username = "未登录";
    [ObservableProperty] private string _balance = "--";
    [ObservableProperty] private bool _hasBalance;
    [ObservableProperty] private string _statusText = "等待登录";
    [ObservableProperty] private string _lastUpdated = "尚未刷新";
    [ObservableProperty] private string _trayToolTip = "Sub2Bar — 未登录";
    [ObservableProperty] private string _minimalDisplayValue = "--";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private string? _adminError;
    [ObservableProperty] private TrayVisualState _trayVisualState = TrayVisualState.Warning;
    [ObservableProperty] private bool _hasServerResponse;
    [ObservableProperty] private string _serverResponseBody = string.Empty;
    [ObservableProperty] private string _serverResponsePath = "服务器响应";
    [ObservableProperty] private string _adminAccountSelectionSummary = "显示 0 / 0";
    [ObservableProperty] private string _adminSubscriptionSummary = "显示 0 / 0";

    public MainViewModel(RefreshCoordinator refreshCoordinator, SettingsStore settingsStore,
        CredentialStore credentialStore, StartupRegistration startupRegistration)
    {
        _refreshCoordinator = refreshCoordinator;
        _settingsStore = settingsStore;
        _credentialStore = credentialStore;
        _startupRegistration = startupRegistration;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsRefreshing);
        ReloginCommand = new RelayCommand(Relogin);
        OpenConsoleCommand = new RelayCommand(OpenConsole, () => HasServerAddress);
        OpenSettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke());
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke());
        OpenServerResponseCommand = new RelayCommand(() => ServerResponseRequested?.Invoke(), () => HasServerResponse);
        ToggleAdminAccountCommand = new AsyncRelayCommand<SelectableAdminAccount>(ToggleAdminAccountAsync);
    }

    public ObservableCollection<SubscriptionDisplay> Subscriptions { get; } = [];
    public ObservableCollection<SelectableAdminAccount> AdminAccounts { get; } = [];
    public ObservableCollection<SelectableAdminAccount> AdminAccountOptions { get; } = [];
    public ObservableCollection<AdminSubscriptionDisplay> AdminSubscriptions { get; } = [];

    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand ReloginCommand { get; }
    public IRelayCommand OpenConsoleCommand { get; }
    public IRelayCommand OpenSettingsCommand { get; }
    public IRelayCommand ExitCommand { get; }
    public IRelayCommand OpenServerResponseCommand { get; }
    public IAsyncRelayCommand<SelectableAdminAccount> ToggleAdminAccountCommand { get; }

    public AppSettings Settings => _settings;
    public Credentials? Credentials => _credentials;
    public RefreshData? Data => _data;
    public bool HasServerAddress => !string.IsNullOrWhiteSpace(_settings.ServerAddress);
    public bool CanRefresh => HasServerAddress && _credentials?.HasAccessToken == true;

    public event Action? LoginRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;
    public event Action? SettingsChanged;
    public event Action? PresentationChanged;
    public event Action? ServerResponseRequested;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _settingsStore.LoadAsync(cancellationToken);
        _credentials = await _credentialStore.LoadAsync(cancellationToken);
        if (_credentials is not null && HasServerAddress)
        {
            try
            {
                if (!ServerAddress.Normalize(_credentials.ServerAddress)
                        .Equals(ServerAddress.Normalize(_settings.ServerAddress), StringComparison.Ordinal))
                {
                    _credentialStore.Delete();
                    _credentials = null;
                }
            }
            catch (ArgumentException)
            {
                _credentialStore.Delete();
                _credentials = null;
            }
        }
        try
        {
            if (_startupRegistration.IsEnabled != _settings.LaunchAtLogin)
            {
                _startupRegistration.SetEnabled(_settings.LaunchAtLogin);
            }
        }
        catch (Exception)
        {
            // Startup registration failure should not prevent the tray client from running.
        }

        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(HasServerAddress));
        OnPropertyChanged(nameof(CanRefresh));
        OpenConsoleCommand.NotifyCanExecuteChanged();
    }

    public async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        if (!HasServerAddress)
        {
            SettingsRequested?.Invoke();
            return;
        }

        if (_credentials is null)
        {
            LoginRequested?.Invoke();
            return;
        }

        IsRefreshing = true;
        RefreshCommand.NotifyCanExecuteChanged();
        StatusText = "正在刷新";
        TrayToolTip = "Sub2Bar — 正在刷新";
        TrayVisualState = TrayVisualState.Refreshing;
        PresentationChanged?.Invoke();

        try
        {
            var result = await _refreshCoordinator.RefreshAsync(_credentials,
                _settings.DisplayedAdminAccountIds);
            ServerResponseBody = result.ResponseBody ?? string.Empty;
            ServerResponsePath = result.RequestPath ?? "服务器响应";
            HasServerResponse = !string.IsNullOrWhiteSpace(ServerResponseBody);
            OpenServerResponseCommand.NotifyCanExecuteChanged();
            if (result.CredentialsChanged && result.UpdatedCredentials is not null)
            {
                _credentials = result.UpdatedCredentials;
                await _credentialStore.SaveAsync(_credentials);
            }

            if (result.Data is not null)
            {
                ApplyData(result.Data);
            }

            switch (result.Status)
            {
                case RefreshStatus.Loaded:
                    StatusText = result.Data?.AdminErrorMessage is null ? "连接正常" : "个人额度已更新";
                    TrayVisualState = ResolveQuotaState(result.Data?.Quota);
                    break;
                case RefreshStatus.RequiresAuthentication:
                    _credentialStore.Delete();
                    _credentials = null;
                    StatusText = result.ErrorMessage ?? "请重新登录";
                    TrayToolTip = "Sub2Bar — 需要重新登录";
                    TrayVisualState = TrayVisualState.Error;
                    OnPropertyChanged(nameof(Credentials));
                    OnPropertyChanged(nameof(CanRefresh));
                    LoginRequested?.Invoke();
                    break;
                case RefreshStatus.Failed:
                    StatusText = result.ErrorMessage ?? "刷新失败";
                    TrayVisualState = TrayVisualState.Error;
                    if (_data is null)
                    {
                        TrayToolTip = "Sub2Bar — 无数据";
                    }
                    break;
            }
        }
        finally
        {
            IsRefreshing = false;
            RefreshCommand.NotifyCanExecuteChanged();
            PresentationChanged?.Invoke();
        }
    }

    public async Task CompleteLoginAsync(Credentials credentials)
    {
        var normalizedAddress = ServerAddress.Normalize(credentials.ServerAddress);
        _credentials = credentials with { ServerAddress = normalizedAddress };
        _settings = _settings with { ServerAddress = normalizedAddress };
        await _credentialStore.SaveAsync(_credentials);
        await _settingsStore.SaveAsync(_settings);
        OnPropertyChanged(nameof(Credentials));
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(HasServerAddress));
        OnPropertyChanged(nameof(CanRefresh));
        OpenConsoleCommand.NotifyCanExecuteChanged();
        SettingsChanged?.Invoke();
        await RefreshAsync();
    }

    public async Task ApplySettingsAsync(AppSettings settings)
    {
        var normalized = settings.Normalize();
        normalized = normalized with { ServerAddress = ServerAddress.Normalize(normalized.ServerAddress) };
        var serverChanged = !_settings.ServerAddress.Equals(normalized.ServerAddress, StringComparison.Ordinal);
        var displayModeChanged = _settings.DisplayMode != normalized.DisplayMode;
        var launchAtLoginChanged = _settings.LaunchAtLogin != normalized.LaunchAtLogin;
        _settings = normalized;
        await _settingsStore.SaveAsync(_settings);
        if (launchAtLoginChanged)
        {
            _startupRegistration.SetEnabled(_settings.LaunchAtLogin);
        }

        if (serverChanged)
        {
            _credentialStore.Delete();
            _credentials = null;
            _data = null;
            ClearData();
            StatusText = "服务器已更改，请重新登录";
            TrayToolTip = "Sub2Bar — 未登录";
            TrayVisualState = TrayVisualState.Warning;
        }
        else if (_data is not null && displayModeChanged)
        {
            ApplyData(_data);
        }

        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(Credentials));
        OnPropertyChanged(nameof(HasServerAddress));
        OnPropertyChanged(nameof(CanRefresh));
        OpenConsoleCommand.NotifyCanExecuteChanged();
        SettingsChanged?.Invoke();
        PresentationChanged?.Invoke();

        if (serverChanged)
        {
            LoginRequested?.Invoke();
        }
    }

    public async Task SetMinimalModeAsync(bool enabled)
    {
        if (_settings.MinimalMode == enabled)
        {
            return;
        }

        var previous = _settings;
        _settings = _settings with { MinimalMode = enabled };
        try
        {
            await _settingsStore.SaveAsync(_settings);
        }
        catch
        {
            _settings = previous;
            throw;
        }

        OnPropertyChanged(nameof(Settings));
        SettingsChanged?.Invoke();
        PresentationChanged?.Invoke();
    }

    private void ApplyData(RefreshData data)
    {
        _data = data;
        Username = data.User.Username;
        HasBalance = data.Quota.Balance.HasValue;
        Balance = data.Quota.Balance.HasValue ? $"${data.Quota.Balance.Value:0.##}" : "--";
        LastUpdated = $"更新于 {data.RefreshedAt:HH:mm}";
        IsAdmin = data.User.IsAdmin;
        AdminError = data.AdminErrorMessage;

        Subscriptions.Clear();
        foreach (var subscription in data.Quota.Subscriptions.Where(item => item.IsActive))
        {
            Subscriptions.Add(SubscriptionDisplay.From(subscription, QuotaDisplayMode.RemainingAmount));
        }

        AdminAccountOptions.Clear();
        foreach (var account in data.AdminAccounts)
        {
            data.AdminUsage.TryGetValue(account.Id, out var usage);
            AdminAccountOptions.Add(new SelectableAdminAccount(account, usage,
                _settings.DisplayedAdminAccountIds.Contains(account.Id)));
        }
        ApplySelectedAdminAccounts();

        AdminSubscriptions.Clear();
        foreach (var subscription in data.AdminSubscriptions)
        {
            AdminSubscriptions.Add(AdminSubscriptionDisplay.From(subscription, QuotaDisplayMode.RemainingAmount));
        }
        AdminSubscriptionSummary = $"显示 {AdminSubscriptions.Count} / {AdminSubscriptions.Count}";

        var quotaWindows = data.Quota.Subscriptions
            .Where(subscription => subscription.IsActive)
            .SelectMany(subscription => subscription.Windows)
            .ToArray();
        var constrainedWindow = quotaWindows
            .Where(window => window.RemainingPercentage.HasValue)
            .OrderBy(window => window.RemainingPercentage)
            .FirstOrDefault() ?? quotaWindows.FirstOrDefault();
        var percentage = constrainedWindow?.RemainingPercentage;
        var amount = constrainedWindow?.Remaining;
        MinimalDisplayValue = BuildMinimalDisplayValue(_settings.DisplayMode, percentage,
            amount, constrainedWindow?.Used, data.Quota.Balance);
        TrayToolTip = percentage.HasValue && amount.HasValue
            ? $"Sub2Bar — 剩余 ${amount:0.##} ({percentage:0.#}%)"
            : percentage.HasValue
                ? $"Sub2Bar — 剩余 {percentage:0.#}%"
            : data.Quota.Balance.HasValue
                ? $"Sub2Bar — 余额 {Balance}"
                : "Sub2Bar — 已更新";
        OnPropertyChanged(nameof(Data));
    }

    private void ApplySelectedAdminAccounts()
    {
        AdminAccountSelectionSummary =
            $"显示 {AdminAccountOptions.Count(account => account.IsSelected)} / {AdminAccountOptions.Count}";

        AdminAccounts.Clear();
        foreach (var account in AdminAccountOptions.Where(account =>
                     _settings.DisplayedAdminAccountIds.Contains(account.Id)))
        {
            AdminAccounts.Add(account);
        }
    }

    private async Task ToggleAdminAccountAsync(SelectableAdminAccount? selection)
    {
        if (selection is null)
        {
            return;
        }

        var previousIds = _settings.DisplayedAdminAccountIds;
        _settings = _settings with
        {
            DisplayedAdminAccountIds = AdminAccountOptions
                .Where(account => account.IsSelected)
                .Select(account => account.Id)
                .ToArray()
        };

        try
        {
            await _settingsStore.SaveAsync(_settings);
            OnPropertyChanged(nameof(Settings));
            if (_data is not null)
            {
                ApplySelectedAdminAccounts();
            }

            SettingsChanged?.Invoke();
            PresentationChanged?.Invoke();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _settings = _settings with { DisplayedAdminAccountIds = previousIds };
            selection.IsSelected = !selection.IsSelected;
            if (_data is not null)
            {
                ApplySelectedAdminAccounts();
            }
            StatusText = exception.Message;
            TrayVisualState = TrayVisualState.Error;
            PresentationChanged?.Invoke();
        }
    }

    private static string BuildMinimalDisplayValue(QuotaDisplayMode mode, double? percentage,
        double? remaining, double? used, double? balance)
    {
        if (mode == QuotaDisplayMode.RemainingPercentage)
        {
            return percentage.HasValue ? $"{percentage.Value:0.#}%" : "--";
        }

        if (mode == QuotaDisplayMode.UsedAmount)
        {
            return used.HasValue ? Money(used.Value) : "--";
        }

        return (remaining ?? balance) is { } value ? Money(value) : "--";
    }

    private static string Money(double value) =>
        $"${value.ToString("0.##", CultureInfo.InvariantCulture)}";

    private void ClearData()
    {
        Username = "未登录";
        Balance = "--";
        HasBalance = false;
        MinimalDisplayValue = "--";
        LastUpdated = "尚未刷新";
        IsAdmin = false;
        AdminError = null;
        Subscriptions.Clear();
        AdminAccounts.Clear();
        AdminAccountOptions.Clear();
        AdminSubscriptions.Clear();
        AdminAccountSelectionSummary = "显示 0 / 0";
        AdminSubscriptionSummary = "显示 0 / 0";
        OnPropertyChanged(nameof(Data));
    }

    private void Relogin()
    {
        _credentialStore.Delete();
        _credentials = null;
        OnPropertyChanged(nameof(Credentials));
        OnPropertyChanged(nameof(CanRefresh));
        LoginRequested?.Invoke();
    }

    private void OpenConsole()
    {
        if (HasServerAddress)
        {
            BrowserLauncher.Open(ServerAddress.Build(_settings.ServerAddress, "usage").AbsoluteUri);
        }
    }

    private static TrayVisualState ResolveQuotaState(QuotaSnapshot? snapshot)
    {
        var remaining = snapshot?.LowestRemainingPercentage;
        return remaining switch
        {
            null => TrayVisualState.Warning,
            <= 10 => TrayVisualState.Error,
            <= 25 => TrayVisualState.Warning,
            _ => TrayVisualState.Normal
        };
    }
}
