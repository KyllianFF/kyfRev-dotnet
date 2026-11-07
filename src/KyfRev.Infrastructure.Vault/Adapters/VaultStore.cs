using KyfRev.Application.Ports;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Backups;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Ids;

namespace KyfRev.Infrastructure.Vault.Adapters;

/// <summary>Ouverture et création des coffres au format 1.</summary>
/// <param name="ids">Identifiants à attribuer ; rien : tirés au hasard (les tests les rejouent).</param>
/// <param name="storage">Là où vivent les fichiers du coffre : le disque par défaut, ou le stockage d'un coffre chiffré
/// déverrouillé, qui reste à l'appelant.</param>
public sealed class VaultStore(UlidGenerator? ids = null, VaultStorage? storage = null) : IVaultStore
{
    /// <inheritdoc/>
    public IVault Open(string root, Moment now) => Adapt(() => VaultSession.Open(root, now, ids, storage));

    /// <inheritdoc/>
    public IVault Initialize(string root, Moment now) => Adapt(() => VaultSession.Initialize(root, now, ids, storage));

    /// <summary>Même dossier une fois les liens résolus ; à défaut (dossier absent), même texte.</summary>
    public bool SameLocation(string first, string second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return RealPaths.Exists(first) && RealPaths.Exists(second) ? RealPaths.SameEntry(first, second) : first == second;
    }

    /// <summary>La session passe à l'adaptateur, qui la fermera ; fermée tout de suite si cela échoue.</summary>
    private static VaultAdapter Adapt(Func<VaultSession> open)
    {
        VaultSession? session = null;
        try
        {
            session = open();
            var adapter = new VaultAdapter(session);
            session = null;
            return adapter;
        }
        finally
        {
            session?.Dispose();
        }
    }
}

/// <summary>Fichiers de sauvegarde (<see cref="VaultBackups"/>), hors de tout coffre.</summary>
public sealed class BackupFiles : IBackupFiles
{
    /// <inheritdoc/>
    public IReadOnlyList<BackupEntry> List(string folder) => [.. VaultBackups.List(folder).Select(backup => Describe(backup.Path))];

    /// <inheritdoc/>
    public bool DailyBackupNeeded(string folder, DateOnly day) => VaultBackups.DailyBackupNeeded(folder, day);

    /// <inheritdoc/>
    public IReadOnlyList<string> ApplyRotation(string folder, RotationPolicy policy) => VaultBackups.ApplyRotation(folder, policy);

    /// <inheritdoc/>
    public string Restore(string backup, string parentFolder, DateOnly day) => VaultBackups.Restore(backup, parentFolder, day);

    /// <summary>Nom, horodatage lu dans le nom et taille (0 si elle est illisible).</summary>
    public BackupEntry Describe(string path)
    {
        var name = Path.GetFileName(path);
        long size;
        try
        {
            size = new FileInfo(path).Length;
        }
        catch (Exception exception) when (VaultSession.IsIo(exception))
        {
            size = 0;
        }
        return new BackupEntry(name, VaultBackups.TimestampOf(name) ?? default, size);
    }

    /// <inheritdoc/>
    public DateTime? TimestampOf(string name) => VaultBackups.TimestampOf(name);

    /// <inheritdoc/>
    public bool IsFile(string path) => File.Exists(path);
}
