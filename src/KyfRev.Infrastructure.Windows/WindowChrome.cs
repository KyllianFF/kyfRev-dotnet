using System.Runtime.InteropServices;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Windows;

/// <summary>Le cadre de la fenêtre : clair ou sombre, par le gestionnaire de fenêtres de Windows (DWM), et ce qui dépasse de l'écran une fois agrandie.</summary>
public sealed partial class WindowChrome : IWindowChrome
{
    /// <summary><c>DWMWA_USE_IMMERSIVE_DARK_MODE</c> (Windows 10 20H1 et suivants).</summary>
    private const uint UseImmersiveDarkMode = 20;

    /// <summary><c>MONITOR_DEFAULTTONEAREST</c>.</summary>
    private const uint NearestMonitor = 2;

    /// <summary><c>DWMWA_EXTENDED_FRAME_BOUNDS</c> : le rectangle visible d'une fenêtre.</summary>
    private const uint ExtendedFrameBounds = 9;

    /// <inheritdoc/>
    public bool UseDarkTitleBar(nint windowHandle, bool dark)
    {
        if (windowHandle == 0)
        {
            return false;
        }
        var value = dark ? 1 : 0;
        return DwmSetWindowAttribute(windowHandle, UseImmersiveDarkMode, ref value, sizeof(int)) == 0;
    }

    /// <inheritdoc/>
    public WindowOverhang MaximizedOverhang(nint windowHandle)
    {
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (windowHandle == 0 || !GetWindowRect(windowHandle, out var window) || !GetMonitorInfoW(MonitorFromWindow(windowHandle, NearestMonitor), ref monitor))
        {
            return default;
        }
        var work = monitor.Work;
        return new WindowOverhang(
            Math.Max(0, work.Left - window.Left),
            Math.Max(0, work.Top - window.Top),
            Math.Max(0, window.Right - work.Right),
            Math.Max(0, window.Bottom - work.Bottom));
    }

    /// <inheritdoc/>
    public ScreenBounds VisibleBounds(nint windowHandle) =>
        windowHandle != 0 && DwmGetWindowAttribute(windowHandle, ExtendedFrameBounds, out var area, (uint)Marshal.SizeOf<Area>()) == 0
            ? new ScreenBounds(area.Left, area.Top, area.Right, area.Bottom)
            : default;

    /// <inheritdoc/>
    public ScreenArea WorkArea(nint windowHandle)
    {
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (windowHandle == 0 || !GetMonitorInfoW(MonitorFromWindow(windowHandle, NearestMonitor), ref monitor))
        {
            return default;
        }
        return new ScreenArea(monitor.Work.Right - monitor.Work.Left, monitor.Work.Bottom - monitor.Work.Top);
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint window, out Area area);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint window, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint window, uint attribute, ref int value, uint size);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(nint window, uint attribute, out Area area, uint size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Area
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Area Whole;
        public Area Work;
        public int Flags;
    }
}
