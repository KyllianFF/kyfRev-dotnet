using Microsoft.Win32;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// kyfRev lancé à l'ouverture de la session Windows : une valeur à son nom dans la clé « Run » du compte, rien pour les
/// autres comptes, rien qui demande d'être administrateur. Retirée quand on ne le veut plus.
/// </summary>
public sealed class SessionStartup
{
    /// <summary>L'option qui garde kyfRev dans la barre d'état, sans ouvrir sa fenêtre.</summary>
    public const string InTrayArgument = "--barre-etat";

    /// <summary>Le nom de la valeur, dans la clé.</summary>
    public const string ValueName = "kyfRev";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly RegistryKey _root;
    private readonly string _key;
    private readonly string? _application;

    /// <summary>Pour le compte en cours, et le programme qui tourne.</summary>
    public SessionStartup()
        : this(Registry.CurrentUser, RunKey, Environment.ProcessPath)
    {
    }

    internal SessionStartup(RegistryKey root, string key, string? application)
    {
        _root = root;
        _key = key;
        _application = application;
    }

    /// <summary>La commande que Windows lance, ou rien si kyfRev ne se lance pas avec la session.</summary>
    public string? Command
    {
        get
        {
            using var run = _root.OpenSubKey(_key);
            return run?.GetValue(ValueName) as string;
        }
    }

    /// <summary>
    /// Inscrit kyfRev pour qu'il se lance avec la session (dans la barre d'état si <paramref name="inTray"/>), ou l'en retire.
    /// Sans programme connu (un essai hors d'un exécutable), rien n'est inscrit.
    /// </summary>
    /// <returns>Vrai si la clé dit maintenant ce qui est voulu.</returns>
    public bool Apply(bool enabled, bool inTray)
    {
        if (!enabled)
        {
            using var existing = _root.OpenSubKey(_key, writable: true);
            existing?.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        if (string.IsNullOrEmpty(_application))
        {
            return false;
        }
        var command = inTray ? $"\"{_application}\" {InTrayArgument}" : $"\"{_application}\"";
        if (Command == command)
        {
            return true;
        }
        using var run = _root.CreateSubKey(_key, writable: true);
        run.SetValue(ValueName, command, RegistryValueKind.String);
        return true;
    }
}
