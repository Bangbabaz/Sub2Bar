using System.Diagnostics;

namespace Sub2Bar.Windows.Infrastructure;

public static class BrowserLauncher
{
    public static void Open(string address)
    {
        Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
    }
}

