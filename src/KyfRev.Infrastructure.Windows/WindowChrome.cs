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

    /// <summary><c>ABM_GETSTATE</c> : la barre des tâches se masque-t-elle d'elle-même ?</summary>
    private const uint AppBarGetState = 0x4;

    /// <summary><c>ABM_GETAUTOHIDEBAREX</c> : la barre qui se masque sur tel bord de tel écran.</summary>
    private const uint AppBarGetAutoHideBarEx = 0xB;

    /// <summary><c>ABS_AUTOHIDE</c>.</summary>
    private const nuint AutoHide = 0x1;

    /// <summary>
    /// Ce que la fenêtre laisse libre au bord d'une barre qui se masque : deux pixels, comme Windows Terminal et Chromium. Un seul
    /// suffit à la souris, le second couvre l'arrondi d'un écran agrandi à 150 %.
    /// </summary>
    internal const int HiddenTaskbarRoom = 2;

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
        if (windowHandle == 0 || !ClientBounds(windowHandle, out var window) || !GetMonitorInfoW(MonitorFromWindow(windowHandle, NearestMonitor), ref monitor))
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
    public bool LeaveRoomForHiddenTaskbar(nint windowHandle, nint calcSizeParameters)
    {
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (windowHandle == 0 || calcSizeParameters == 0 || !IsZoomed(windowHandle)
            || !GetMonitorInfoW(MonitorFromWindow(windowHandle, NearestMonitor), ref monitor))
        {
            return false;
        }
        var edges = HiddenTaskbarEdges(monitor.Whole);
        if (edges == ScreenEdges.None)
        {
            return false;
        }
        var whole = new ScreenBounds(monitor.Whole.Left, monitor.Whole.Top, monitor.Whole.Right, monitor.Whole.Bottom);
        var proposed = Marshal.PtrToStructure<Area>(calcSizeParameters);
        var room = Trimmed(new ScreenBounds(proposed.Left, proposed.Top, proposed.Right, proposed.Bottom), whole, edges);
        Marshal.StructureToPtr(new Area { Left = room.Left, Top = room.Top, Right = room.Right, Bottom = room.Bottom }, calcSizeParameters, fDeleteOld: false);
        return true;
    }

    /// <summary>
    /// La zone cliente <paramref name="proposed"/> arrêtée à <see cref="HiddenTaskbarRoom"/> pixels de chaque bord
    /// <paramref name="edges"/> de l'écran <paramref name="monitor"/> ; les autres bords ne bougent pas.
    /// </summary>
    internal static ScreenBounds Trimmed(ScreenBounds proposed, ScreenBounds monitor, ScreenEdges edges) => new(
        edges.HasFlag(ScreenEdges.Left) ? Math.Max(proposed.Left, monitor.Left + HiddenTaskbarRoom) : proposed.Left,
        edges.HasFlag(ScreenEdges.Top) ? Math.Max(proposed.Top, monitor.Top + HiddenTaskbarRoom) : proposed.Top,
        edges.HasFlag(ScreenEdges.Right) ? Math.Min(proposed.Right, monitor.Right - HiddenTaskbarRoom) : proposed.Right,
        edges.HasFlag(ScreenEdges.Bottom) ? Math.Min(proposed.Bottom, monitor.Bottom - HiddenTaskbarRoom) : proposed.Bottom);

    /// <summary>Les bords de l'écran <paramref name="monitor"/> où une barre des tâches se masque d'elle-même ; aucun si elle reste affichée.</summary>
    internal static ScreenEdges HiddenTaskbarEdges(ScreenBounds monitor) =>
        HiddenTaskbarEdges(new Area { Left = monitor.Left, Top = monitor.Top, Right = monitor.Right, Bottom = monitor.Bottom });

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

    private static ScreenEdges HiddenTaskbarEdges(Area monitor)
    {
        var state = new AppBarData { Size = (uint)Marshal.SizeOf<AppBarData>() };
        if ((SHAppBarMessage(AppBarGetState, ref state) & AutoHide) == 0)
        {
            return ScreenEdges.None;
        }
        var edges = ScreenEdges.None;
        // ABE_LEFT, ABE_TOP, ABE_RIGHT, ABE_BOTTOM : une barre par bord et par écran.
        foreach (var (edge, flag) in new[] { (0u, ScreenEdges.Left), (1u, ScreenEdges.Top), (2u, ScreenEdges.Right), (3u, ScreenEdges.Bottom) })
        {
            var bar = new AppBarData { Size = (uint)Marshal.SizeOf<AppBarData>(), Edge = edge, Bounds = monitor };
            if (SHAppBarMessage(AppBarGetAutoHideBarEx, ref bar) != 0)
            {
                edges |= flag;
            }
        }
        return edges;
    }

    /// <summary>La zone cliente de la fenêtre, en pixels de l'écran.</summary>
    private static bool ClientBounds(nint window, out Area area)
    {
        area = default;
        var origin = default(Spot);
        if (!GetClientRect(window, out var client) || !ClientToScreen(window, ref origin))
        {
            return false;
        }
        area = new Area { Left = origin.X, Top = origin.Y, Right = origin.X + client.Right, Bottom = origin.Y + client.Bottom };
        return true;
    }

    /// <inheritdoc/>
    public void RefreshFrame(nint windowHandle)
    {
        if (windowHandle != 0)
        {
            SetWindowPos(windowHandle, 0, 0, 0, 0, 0, NoSize | NoMove | NoZOrder | NoActivate | FrameChanged);
        }
    }

    private const uint NoSize = 0x0001;
    private const uint NoMove = 0x0002;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;
    private const uint FrameChanged = 0x0020;

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint window, out Area area);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(nint window, ref Spot point);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsZoomed(nint window);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("shell32.dll")]
    private static partial nuint SHAppBarMessage(uint message, ref AppBarData data);

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
    private struct Spot
    {
        public int X;
        public int Y;
    }

    /// <summary><c>APPBARDATA</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint Size;
        public nint Window;
        public uint CallbackMessage;
        public uint Edge;
        public Area Bounds;
        public nint Parameter;
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

/// <summary>Les bords d'un écran.</summary>
[Flags]
internal enum ScreenEdges
{
    /// <summary>Aucun.</summary>
    None = 0,

    /// <summary>À gauche.</summary>
    Left = 1,

    /// <summary>En haut.</summary>
    Top = 2,

    /// <summary>À droite.</summary>
    Right = 4,

    /// <summary>En bas.</summary>
    Bottom = 8,
}
