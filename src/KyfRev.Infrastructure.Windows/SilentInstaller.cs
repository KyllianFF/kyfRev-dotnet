using System.ComponentModel;
using System.Diagnostics;
using KyfRev.Application.Ports;
using Microsoft.Win32;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// L'installateur de kyfRev (<c>installer/kyfRev.iss</c>), lancé sans aucune fenêtre pour une mise à jour. Le mode de
/// l'installation en place se lit dans sa clé de désinstallation : pour la personne seule, ou pour tous, auquel cas
/// Windows demande l'autorisation d'un administrateur. Rien n'est écrit dans le registre.
/// </summary>
public sealed class SilentInstaller : IUpdateInstaller
{
    /// <summary>La clé de désinstallation qu'écrit l'installateur (son <c>AppId</c>).</summary>
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{69A07F65-7110-4520-A128-737A61015928}_is1";

    /// <summary>Le mutex que tient l'installateur pendant qu'il tourne (<c>SetupMutex</c> de <c>installer/kyfRev.iss</c>).</summary>
    public const string InstallingMutex = "kyfRevInstallation";

    private readonly (RegistryKey Root, string Key) _user;
    private readonly (RegistryKey Root, string Key) _machine;
    private readonly Func<ProcessStartInfo, bool> _start;
    private readonly string _application;
    private readonly Lazy<InstallScope?> _scope;

    /// <summary>Sur le registre de ce poste, pour le kyfRev qui tourne.</summary>
    public SilentInstaller()
        : this((Registry.CurrentUser, UninstallKey), (Registry.LocalMachine, UninstallKey), information =>
        {
            using var process = Process.Start(information);
            return process is not null;
        })
    {
    }

    internal SilentInstaller((RegistryKey Root, string Key) user, (RegistryKey Root, string Key) machine, Func<ProcessStartInfo, bool> start, string? application = null)
    {
        _user = user;
        _machine = machine;
        _start = start;
        _application = Path.TrimEndingDirectorySeparator(application ?? AppContext.BaseDirectory);
        _scope = new Lazy<InstallScope?>(FindScope);
    }

    /// <inheritdoc/>
    public InstallScope? Scope => _scope.Value;

    /// <summary>Vrai pendant qu'une installation de kyfRev tourne sur cette session.</summary>
    public static bool IsInstalling() => IsInstalling(InstallingMutex);

    /// <summary>Vrai si le mutex <paramref name="name"/> existe sur cette session.</summary>
    internal static bool IsInstalling(string name)
    {
        if (!Mutex.TryOpenExisting(name, out var mutex))
        {
            return false;
        }
        mutex.Dispose();
        return true;
    }

    /// <summary>
    /// Les options de l'installateur : aucune fenêtre ni question, pas de redémarrage de Windows, le mode de l'installation
    /// en place, un journal ; <c>/RELANCER</c> rouvre kyfRev à la fin.
    /// </summary>
    public static string Arguments(InstallScope scope, bool reopen, string log)
    {
        ArgumentException.ThrowIfNullOrEmpty(log);
        var mode = scope == InstallScope.AllUsers ? "/ALLUSERS" : "/CURRENTUSER";
        return $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART {mode} /LOG=\"{log}\"" + (reopen ? " /RELANCER" : string.Empty);
    }

    /// <inheritdoc/>
    public bool Start(string installer, bool reopen, string log)
    {
        ArgumentException.ThrowIfNullOrEmpty(installer);
        if (Scope is not { } scope)
        {
            return false;
        }
        try
        {
            return _start(new ProcessStartInfo(installer, Arguments(scope, reopen, log))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(installer) ?? string.Empty,
            });
        }
        catch (Win32Exception exception)
        {
            // Lancement refusé par Windows : la mise à jour n'a pas lieu, et le démarrage suivant le dit.
            Trace.TraceWarning($"Installateur non lancé : {exception.Message}");
            return false;
        }
    }

    /// <summary>Pour la personne si sa clé désigne ce dossier, pour tous si c'est celle du poste ; rien sinon.</summary>
    private InstallScope? FindScope()
    {
        if (Designates(_user))
        {
            return InstallScope.CurrentUser;
        }
        return Designates(_machine) ? InstallScope.AllUsers : null;
    }

    private bool Designates((RegistryKey Root, string Key) uninstall)
    {
        using var key = uninstall.Root.OpenSubKey(uninstall.Key);
        var folder = ((key?.GetValue("InstallLocation") as string) ?? string.Empty).Trim('"');
        return folder.Length > 0 && string.Equals(Path.TrimEndingDirectorySeparator(folder), _application, StringComparison.OrdinalIgnoreCase);
    }
}
