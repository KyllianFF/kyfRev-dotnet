using KyfRev.Application.Model;
using KyfRev.Application.Ports;

namespace KyfRev.Application.UseCases;

/// <summary>
/// Santé du coffre et réparations : rien n'est jamais effacé ni réparé en silence ; une
/// réparation ne fait qu'ajouter (un suivi, un identifiant neuf pour une copie).
/// </summary>
public sealed class MaintenanceUseCases(VaultHost host, IBackupFiles backups, TimeProvider time)
{
    /// <summary>Rapport « Santé du coffre », liens cassés compris.</summary>
    [ZeroXCommand("rapport_sante")]
    public Outcome<HealthDetails> Health() => host.Use(vault => vault.HealthDetails());

    /// <summary>Analyse complète du coffre, à la demande ; rend le nouveau rapport.</summary>
    [ZeroXCommand("analyser_coffre")]
    public Outcome<HealthDetails> Analyze()
    {
        var now = Present.Of(time).Now;
        return host.Use(vault =>
        {
            vault.Analyze(now);
            return vault.HealthDetails();
        });
    }

    /// <summary>Fichier qui porte un identifiant sans fichier de suivi : son suivi est recréé (J0) ; rend l'identifiant.</summary>
    [ZeroXCommand("recreer_suivi")]
    public Outcome<string> RecreateTracking(string path)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.RecreateTracking(path, now));
    }

    /// <summary>Doublon d'identifiant : <paramref name="original"/> le garde, les copies en reçoivent un neuf ; rend ceux-ci.</summary>
    [ZeroXCommand("garder_original")]
    public Outcome<IReadOnlyList<string>> KeepOriginal(string id, string original)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.KeepOriginal(id, original, now));
    }

    /// <summary>Fiche manquante, recopiée depuis la sauvegarde nommée <paramref name="backup"/> ; rend son chemin.</summary>
    [ZeroXCommand("restaurer_fiche")]
    public Outcome<string> RestoreFromBackup(string id, string backup)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.RestoreSheetFromBackup(id, BackupUseCases.Named(backups, vault, backup), now));
    }
}

/// <summary>
/// Sauvegardes, rangées à côté du coffre (<c>Sauvegardes</c>), et leur restauration : toujours
/// dans un nouveau dossier, jamais par-dessus le coffre ouvert. La rotation est la seule
/// suppression automatique.
/// </summary>
public sealed class BackupUseCases(VaultHost host, IBackupFiles backups, TimeProvider time)
{
    /// <summary>Les sauvegardes du coffre, de la plus récente à la plus ancienne.</summary>
    [ZeroXCommand("sauvegardes")]
    public Outcome<BackupList> List() => host.Use(vault =>
    {
        var folder = BackupFolders.For(vault.Root);
        return new BackupList(folder, vault.Preferences.BackupRotation, backups.List(folder));
    });

    /// <summary>Sauvegarde immédiate, puis rotation si elle est active.</summary>
    [ZeroXCommand("sauvegarder_maintenant")]
    public Outcome<BackupEntry> BackupNow()
    {
        var local = Present.Of(time).Local;
        return host.Use(vault =>
        {
            var folder = BackupFolders.For(vault.Root);
            var backup = vault.Backup(folder, local);
            if (vault.Preferences.BackupRotation)
            {
                backups.ApplyRotation(folder, RotationPolicy.Default);
            }
            return backups.Describe(backup.Path);
        });
    }

    /// <summary>« Restaurer tout » : la sauvegarde est extraite dans un nouveau dossier, à côté du coffre ; rend son chemin.</summary>
    [ZeroXCommand("restaurer_sauvegarde")]
    public Outcome<string> Restore(string name)
    {
        var day = Present.Of(time).Now.Day;
        return host.Use(vault =>
        {
            var backup = Named(backups, vault, name);
            var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(vault.Root))
                ?? throw new RefusedException("Le coffre n'a pas de dossier parent.");
            return backups.Restore(backup, parent, day);
        });
    }

    /// <summary>Chemin d'une sauvegarde du dossier des sauvegardes, d'après son nom seul.</summary>
    internal static string Named(IBackupFiles backups, IVault vault, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var path = Path.Combine(BackupFolders.For(vault.Root), name);
        return backups.TimestampOf(name) is not null && backups.IsFile(path)
            ? path
            : throw new RefusedException($"La sauvegarde « {name} » est introuvable.");
    }
}

/// <summary>Exports dans un dossier choisi par l'utilisateur, hors du coffre ; un fichier existant n'est jamais écrasé.</summary>
public sealed class ExportUseCases(VaultHost host, TimeProvider time)
{
    /// <summary>Écrit l'export dans <paramref name="folder"/> ; rend les fichiers créés.</summary>
    [ZeroXCommand("exporter")]
    public Outcome<IReadOnlyList<string>> Export(ExportKind kind, string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var local = Present.Of(time).Local;
        return Path.IsPathFullyQualified(folder)
            ? host.Use(vault => vault.Export(kind, folder, local, time.LocalTimeZone))
            : Outcome.Refused<IReadOnlyList<string>>("Choisissez un dossier pour l'export.");
    }
}

/// <summary>Refus décidé par un cas d'usage lui-même.</summary>
internal sealed class RefusedException : OperationException
{
    public RefusedException()
    {
    }

    public RefusedException(string message)
        : base(message)
    {
    }

    public RefusedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
