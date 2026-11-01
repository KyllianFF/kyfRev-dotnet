using System.Runtime.InteropServices;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Windows;

/// <summary>Barre de titre claire ou sombre, par le gestionnaire de fenêtres de Windows (DWM).</summary>
public sealed partial class WindowChrome : IWindowChrome
{
    /// <summary><c>DWMWA_USE_IMMERSIVE_DARK_MODE</c> (Windows 10 20H1 et suivants).</summary>
    private const uint UseImmersiveDarkMode = 20;

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

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint window, uint attribute, ref int value, uint size);
}
