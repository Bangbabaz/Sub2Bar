using Microsoft.Win32;

namespace Sub2Bar.Windows.Infrastructure;

public sealed class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Sub2Bar";

    public bool IsEnabled
    {
        get
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(ValueName) is string value &&
                   value.Contains(executablePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
        if (!enabled)
        {
            key.DeleteValue(ValueName, false);
            return;
        }

        var executablePath = Environment.ProcessPath ??
                             throw new InvalidOperationException("无法确定应用程序路径。");
        key.SetValue(ValueName, $"\"{executablePath}\" --background", RegistryValueKind.String);
    }
}
