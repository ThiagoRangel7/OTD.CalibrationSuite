using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CalibrationSuite.Calibrator;

public sealed record DisplayGeometry(int Left, int Top, int Width, int Height)
{
    public static DisplayGeometry ForWindow(Window window)
    {
        // Windows selects the monitor with the largest intersection, or the nearest monitor.
        var monitor = MonitorFromWindow(new WindowInteropHelper(window).Handle, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
            throw new System.ComponentModel.Win32Exception();
        var rect = info.Monitor;
        return new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public void Place(Window window)
    {
        if (!SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, Left, Top, Width, Height, 0x0040 | 0x0004))
            throw new System.ComponentModel.Win32Exception();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size; public Rect Monitor, Work; public uint Flags;
    }
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
