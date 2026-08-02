using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Sub2Bar.Core.Models;
using Sub2Bar.Core.Networking;
using Sub2Bar.Windows.Infrastructure;

namespace Sub2Bar.Windows.Views;

public partial class LoginWindow : Window
{
    private readonly string _serverAddress;
    private readonly Func<Credentials, Task> _onAuthenticated;
    private string _boundUserAgent = string.Empty;
    private string? _lastAccessToken;
    private bool _completed;

    public LoginWindow(string serverAddress, Func<Credentials, Task> onAuthenticated)
    {
        InitializeComponent();
        _serverAddress = ServerAddress.Normalize(serverAddress);
        _onAuthenticated = onAuthenticated;
        AddressText.Text = ServerAddress.Build(_serverAddress, "login").AbsoluteUri;
        SecurityGlyph.Foreground = _serverAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? System.Windows.Media.Brushes.LightGreen
            : System.Windows.Media.Brushes.Orange;
        Loaded += LoginWindow_Loaded;
    }

    private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= LoginWindow_Loaded;
        try
        {
            AppPaths.EnsureDataDirectory();
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: AppPaths.WebViewDataDirectory);
            await Browser.EnsureCoreWebView2Async(environment);
            _boundUserAgent = Browser.CoreWebView2.Settings.UserAgent;
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Browser.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            Browser.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            Browser.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            Browser.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            await Browser.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);
            await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(TokenCaptureScript.Value);
            Browser.CoreWebView2.Navigate(ServerAddress.Build(_serverAddress, "login").AbsoluteUri);
        }
        catch (Exception exception)
        {
            StatusText.Text = $"无法打开登录页面：{exception.Message}";
        }
    }

    private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_completed || !Uri.TryCreate(e.Source, UriKind.Absolute, out var source) ||
            !ServerAddress.IsSameOrigin(_serverAddress, source))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            var accessToken = GetString(root, "access_token");
            var refreshToken = GetString(root, "refresh_token");
            if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Equals(_lastAccessToken, StringComparison.Ordinal))
            {
                return;
            }

            _lastAccessToken = accessToken;
            _completed = true;
            StatusText.Text = "登录成功";
            await Task.Delay(800);
            await _onAuthenticated(new Credentials(_serverAddress, accessToken, refreshToken, _boundUserAgent));
            Close();
        }
        catch (JsonException)
        {
            StatusText.Text = "登录页面返回了无法识别的凭据。";
        }
        catch (Exception exception)
        {
            _completed = false;
            _lastAccessToken = null;
            StatusText.Text = $"保存登录状态失败：{exception.Message}";
        }
    }

    private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        AddressText.Text = e.Uri;
        StatusText.Text = "正在加载";
    }

    private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        BackButton.IsEnabled = Browser.CanGoBack;
        ForwardButton.IsEnabled = Browser.CanGoForward;
        StatusText.Text = e.IsSuccess ? "等待登录" : $"页面加载失败：{e.WebErrorStatus}";
    }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        BrowserLauncher.Open(e.Uri);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoBack) Browser.GoBack();
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoForward) Browser.GoForward();
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e) => Browser.Reload();

    private static string GetString(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
