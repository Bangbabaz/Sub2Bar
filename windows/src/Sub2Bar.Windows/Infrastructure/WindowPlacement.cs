using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Sub2Bar.Windows.Infrastructure;

public static class WindowPlacement
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly nint HwndTop = nint.Zero;

    public static void PlaceNear(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (!GetCursorPos(out var cursor) || !GetWindowRect(handle, out var windowRect))
        {
            PlaceInPrimaryWorkArea(window);
            return;
        }

        var monitor = MonitorFromPoint(cursor, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            PlaceInPrimaryWorkArea(window);
            return;
        }

        var width = windowRect.Right - windowRect.Left;
        var height = windowRect.Bottom - windowRect.Top;
        const int gap = 10;
        var taskbarOnLeft = info.Work.Left > info.Monitor.Left;
        var taskbarOnTop = info.Work.Top > info.Monitor.Top;

        var left = taskbarOnLeft
            ? info.Work.Left + gap
            : info.Work.Right - width - gap;
        var top = taskbarOnTop
            ? info.Work.Top + gap
            : info.Work.Bottom - height - gap;

        var minLeft = info.Work.Left + gap;
        var minTop = info.Work.Top + gap;
        var maxLeft = Math.Max(minLeft, info.Work.Right - width - gap);
        var maxTop = Math.Max(minTop, info.Work.Bottom - height - gap);
        left = Math.Clamp(left, minLeft, maxLeft);
        top = Math.Clamp(top, minTop, maxTop);
        SetWindowPos(handle, HwndTop, left, top, 0, 0,
            SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    private static void PlaceInPrimaryWorkArea(Window window)
    {
        window.Left = SystemParameters.WorkArea.Right - window.ActualWidth - 10;
        window.Top = SystemParameters.WorkArea.Bottom - window.ActualHeight - 10;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }
}
