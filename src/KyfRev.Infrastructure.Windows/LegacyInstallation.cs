using System.ComponentModel;
using System.Diagnostics;
using KyfRev.Application.Ports;
using Microsoft.Win32;

namespace KyfRev.Infrastructure.Windows;

/// <summary>La 0.x, lue dans sa clé de désinstallation, pour l'utilisateur ou pour tous. Rien n'y est écrit.</summary>
public sealed class LegacyInstallation : ILegacyInstallation
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\kyfRev";
    private const string InstalledApps = "ms-settings:appsfeatures";

    private readonly IReadOnlyList<RegistryKey> _roots;
    private readonly string _key;
    private readonly Func<ProcessStartInfo, bool> _start;

    /// <summary>Sur le registre de ce poste.</summary>
    public LegacyInstallation()
        : this([Registry.CurrentUser, Registry.LocalMachine], UninstallKey, information =>
        {
            using var process = Process.Start(information);
            return true;
        })
    {
    }

    internal LegacyInstallation(IReadOnlyList<RegistryKey> roots, string key, Func<ProcessStartInfo, bool> start)
    {
        _roots = roots;
        _key = key;
        _start = start;
    }

    /// <inheritdoc/>
    public LegacyInstall? Find()
    {
        foreach (var root in _roots)
        {
            using var key = root.OpenSubKey(_key);
            if (key?.GetValue("DisplayVersion") is string version && version.StartsWith("0.", StringComparison.Ordinal))
            {
                return new LegacyInstall(version, ((key.GetValue("InstallLocation") as string) ?? string.Empty).Trim('"'));
            }
        }
        return null;
    }

    /// <inheritdoc/>
    public bool OpenInstalledApps()
    {
        try
        {
            return _start(new ProcessStartInfo(InstalledApps) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
