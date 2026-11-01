using System.Runtime.InteropServices;
using KyfRev.Application.Ports;
using Microsoft.Win32;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// Préférences d'affichage lues dans Windows : thème des applications (registre de
/// l'utilisateur), contraste élevé et animations (<c>SystemParametersInfo</c>). Un changement
/// dans les paramètres de Windows est signalé par <see cref="Changed"/>.
/// </summary>
public sealed partial class WindowsAppearance : ISystemAppearance, IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightTheme = "AppsUseLightTheme";
    private const uint GetHighContrast = 0x0042;
    private const uint GetClientAreaAnimation = 0x1042;
    private const uint HighContrastOn = 0x1;

    private bool _disposed;

    /// <summary>Lit les préférences et suit leurs changements.</summary>
    public WindowsAppearance()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public bool PrefersDarkTheme => ReadPrefersDark(Registry.CurrentUser);

    /// <inheritdoc/>
    public bool HighContrast
    {
        get
        {
            var settings = new HighContrastSettings { Size = (uint)Marshal.SizeOf<HighContrastSettings>() };
            return SystemParametersInfo(GetHighContrast, settings.Size, ref settings, 0) && (settings.Flags & HighContrastOn) != 0;
        }
    }

    /// <inheritdoc/>
    public bool ReducedMotion
    {
        get
        {
            var animated = 1;
            return SystemParametersInfo(GetClientAreaAnimation, 0, ref animated, 0) && animated == 0;
        }
    }

    /// <summary>Cesse de suivre les changements.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _disposed = true;
        }
    }

    /// <summary>Thème sombre demandé, d'après la clé de personnalisation de <paramref name="root"/> (clair si la valeur manque).</summary>
    internal static bool ReadPrefersDark(RegistryKey root)
    {
        using var key = root.OpenSubKey(PersonalizeKey);
        return key?.GetValue(AppsUseLightTheme) is int light && light == 0;
    }

    /// <summary>Signale un changement des préférences d'affichage.</summary>
    internal void OnPreferencesChanged() => Changed?.Invoke(this, EventArgs.Empty);

    internal void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.Accessibility or UserPreferenceCategory.VisualStyle)
        {
            OnPreferencesChanged();
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint parameter, ref HighContrastSettings value, uint winIni);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint parameter, ref int value, uint winIni);

    /// <summary>Structure <c>HIGHCONTRASTW</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrastSettings
    {
        public uint Size;
        public uint Flags;
        public nint DefaultScheme;
    }
}
