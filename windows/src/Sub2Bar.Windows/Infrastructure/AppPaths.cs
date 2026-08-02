using System.IO;

namespace Sub2Bar.Windows.Infrastructure;

public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sub2Bar");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string CredentialsFile => Path.Combine(DataDirectory, "credentials.dat");
    public static string WebViewDataDirectory => Path.Combine(DataDirectory, "WebView2");

    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);
}
