using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using Microsoft.Extensions.Logging;

namespace KyfRev.Application.UseCases;

/// <summary>État de l'application vu par l'accueil.</summary>
/// <param name="Version">Version de kyfRev.</param>
/// <param name="Mode">Mode de l'application.</param>
/// <param name="Vault">Dossier du coffre ouvert.</param>
/// <param name="VaultToCreate">Coffre prévu (mode portable ou installé) mais pas encore créé, quand aucun n'est ouvert.</param>
/// <param name="OpenError">Message si le coffre demandé n'a pas pu être ouvert.</param>
/// <param name="Locked">
/// Dossier d'un coffre chiffré qui attend sa clé (son mot de passe ou sa clé de récupération) : l'écran de
/// déverrouillage la demande. Le coffre ouvert, s'il y en a un, le reste.
/// </param>
public sealed record AppState(string Version, AppMode Mode, string? Vault, string? VaultToCreate, string? OpenError, string? Locked = null);

/// <summary>
/// Ouverture et création du coffre. À chaque ouverture : la journée est préparée (tirage des
/// rappels), une sauvegarde est faite si aucune ne l'a été ce jour-là (puis la rotation, si elle
/// est active), et le coffre est retenu pour le prochain démarrage. Ni la préparation ni la
/// sauvegarde ne bloquent l'ouverture : un échec est seulement journalisé. Un coffre chiffré s'ouvre avec la clé que
/// ce poste a retenue ; à défaut, il attend son mot de passe ou sa clé de récupération (<see cref="Unlock"/>).
/// </summary>
public sealed partial class VaultUseCases(
    VaultHost host,
    IVaultStore store,
    IVaultKeys keys,
    IBackupFiles backups,
    IVaultLocations locations,
    TimeProvider time,
    ILogger<VaultUseCases> logger,
    string version)
{
    /// <summary>Ce que montre l'accueil.</summary>
    [ZeroXCommand("etat_application")]
    public AppState State() => StateWith(null);

    /// <summary>
    /// Ouvre le coffre prévu au démarrage : celui du mode portable, le dernier ouvert, ou celui du
    /// profil en mode installé. Un coffre prévu mais pas encore créé reste à l'accueil, qui
    /// propose de le créer.
    /// </summary>
    public AppState OpenAtStartup()
    {
        if (locations.VaultToOpen() is not { } root || root == locations.VaultToCreate())
        {
            return State();
        }
        return Open(root);
    }

    /// <summary>
    /// Ouvre un coffre ; il remplace celui qui était ouvert, fermé seulement une fois le nouveau
    /// ouvert. Rouvrir le coffre déjà ouvert ne fait rien. Un coffre chiffré s'ouvre avec la clé que ce poste a
    /// retenue ; sans elle, l'état le dit en attente de sa clé (<see cref="AppState.Locked"/>), et rien ne change.
    /// </summary>
    [ZeroXCommand("ouvrir_coffre")]
    public AppState Open(string root) => OpenOrCreate(root, create: false);

    /// <summary>Crée un coffre dans un dossier vide, puis l'ouvre.</summary>
    [ZeroXCommand("creer_coffre")]
    public AppState Create(string root) => OpenOrCreate(root, create: true);

    /// <summary>
    /// Ouvre le coffre chiffré de <paramref name="root"/> avec son mot de passe ou sa clé de récupération ; il remplace
    /// celui qui était ouvert, comme <see cref="Open"/>. Retenue, sa clé l'ouvrira ensuite sans rien demander sur ce
    /// poste ; sinon, celle qui l'était est oubliée. Refusé, l'état le dit toujours en attente de sa clé, avec la raison.
    /// </summary>
    /// <param name="root">Le dossier du coffre chiffré.</param>
    /// <param name="secret">Son mot de passe, ou sa clé de récupération.</param>
    /// <param name="remember">Vrai pour retenir sa clé sur ce poste, protégée par Windows pour la session de l'utilisateur.</param>
    public AppState Unlock(string root, VaultSecret secret, bool remember)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(secret);
        if (host.IsOpen(root, store.SameLocation))
        {
            return State();
        }
        var present = Present.Of(time);
        VaultKey key;
        try
        {
            // La dérivation du mot de passe prend de l'ordre d'une seconde : l'écran l'appelle hors de son fil.
            key = keys.KeyOf(root, secret);
        }
        catch (OperationException exception)
        {
            return LockedState(root, exception.Message);
        }
        using (key)
        {
            var state = Adopt(() => store.Open(root, key, present.Now), present, lockedOnFailure: root);
            if (state.OpenError is null && !(remember ? keys.Remember(root, key) : keys.Forget(root)))
            {
                LogKeyNotRemembered(logger, root, remember);
            }
            return state;
        }
    }

    private AppState OpenOrCreate(string root, bool create)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!create && host.IsOpen(root, store.SameLocation))
        {
            return State();
        }
        var present = Present.Of(time);
        if (!create && keys.IsEncrypted(root))
        {
            VaultKey? remembered;
            try
            {
                remembered = keys.Remembered(root);
            }
            catch (OperationException exception)
            {
                return LockedState(root, exception.Message);
            }
            if (remembered is null)
            {
                return LockedState(root, null);
            }
            using (remembered)
            {
                return Adopt(() => store.Open(root, remembered, present.Now), present, lockedOnFailure: null);
            }
        }
        return Adopt(() => create ? store.Initialize(root, present.Now) : store.Open(root, present.Now), present, lockedOnFailure: null);
    }

    /// <summary>
    /// Le coffre que rend <paramref name="open"/> devient celui de l'application : sa journée est préparée, sa
    /// sauvegarde du jour faite, et il est retenu pour le prochain démarrage. Refusé, l'état le dit, en attente de sa clé
    /// si c'était un coffre chiffré (<paramref name="lockedOnFailure"/>).
    /// </summary>
    private AppState Adopt(Func<IVault> open, Present present, string? lockedOnFailure)
    {
        IVault vault;
        try
        {
            vault = open();
        }
        catch (OperationException exception)
        {
            return lockedOnFailure is null ? StateWith(exception.Message) : LockedState(lockedOnFailure, exception.Message);
        }
        try
        {
            PrepareDay(vault, present);
            BackupOfTheDay(vault, present);
            if (!locations.Remember(vault.Root))
            {
                LogVaultNotRemembered(logger, vault.Root);
            }
        }
        catch
        {
            // Erreur de programmation : le nouveau coffre ne reste pas verrouillé.
            vault.Dispose();
            throw;
        }
        host.Replace(vault);
        return State();
    }

    private AppState LockedState(string root, string? error) => StateWith(error) with { Locked = root };

    private AppState StateWith(string? openError)
    {
        var open = host.Root;
        return new AppState(version, locations.Mode, open, open is null ? locations.VaultToCreate() : null, openError);
    }

    private void PrepareDay(IVault vault, Present present)
    {
        try
        {
            var day = vault.PrepareDay(present.Now);
            LogDayPrepared(logger, day.Day, day.NewReminders.Length, day.AlreadyPrepared);
        }
        catch (OperationException exception)
        {
            LogDayNotPrepared(logger, exception.Message);
        }
    }

    /// <summary>Sauvegarde quotidienne et rotation : un échec est journalisé, il ne bloque jamais l'ouverture.</summary>
    private void BackupOfTheDay(IVault vault, Present present)
    {
        var folder = BackupFolders.For(vault.Root);
        bool needed;
        try
        {
            needed = backups.DailyBackupNeeded(folder, DateOnly.FromDateTime(present.Local));
        }
        catch (OperationException exception)
        {
            LogBackupFolderUnreadable(logger, exception.Message);
            return;
        }
        if (!needed)
        {
            return;
        }
        try
        {
            var backup = vault.Backup(folder, present.Local);
            LogBackupDone(logger, backup.Path);
        }
        catch (OperationException exception)
        {
            LogBackupFailed(logger, exception.Message);
            return;
        }
        if (!vault.Preferences.BackupRotation)
        {
            return;
        }
        try
        {
            var removed = backups.ApplyRotation(folder, RotationPolicy.Default);
            LogRotationDone(logger, removed.Count);
        }
        catch (OperationException exception)
        {
            LogRotationFailed(logger, exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Journée du {Day:yyyy-MM-dd} préparée : {Reminders} rappel(s) tiré(s), déjà préparée : {Already}.")]
    private static partial void LogDayPrepared(ILogger logger, DateOnly day, int reminders, bool already);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Coffre {Root} ouvert mais pas retenu : il ne sera pas rouvert au prochain démarrage.")]
    private static partial void LogVaultNotRemembered(ILogger logger, string root);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Coffre chiffré {Root} ouvert, mais sa clé n'a pas pu être retenue ou oubliée sur ce poste (retenir : {Remember}).")]
    private static partial void LogKeyNotRemembered(ILogger logger, string root, bool remember);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Préparation de la journée impossible : {Reason}")]
    private static partial void LogDayNotPrepared(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dossier des sauvegardes illisible : {Reason}")]
    private static partial void LogBackupFolderUnreadable(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sauvegarde du jour : {Path}")]
    private static partial void LogBackupDone(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sauvegarde impossible : {Reason}")]
    private static partial void LogBackupFailed(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rotation des sauvegardes : {Count} copie(s) retirée(s).")]
    private static partial void LogRotationDone(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rotation impossible : {Reason}")]
    private static partial void LogRotationFailed(ILogger logger, string reason);
}

/// <summary>Où ranger les sauvegardes d'un coffre.</summary>
internal static class BackupFolders
{
    /// <summary>À côté du coffre : <c>MesRévisions/Coffre</c> → <c>MesRévisions/Sauvegardes</c>.</summary>
    public static string For(string vaultRoot)
    {
        var root = Path.TrimEndingDirectorySeparator(vaultRoot);
        return Path.GetDirectoryName(root) is { } parent
            ? Path.Combine(parent, "Sauvegardes")
            : Path.Combine(root, "..", "Sauvegardes");
    }
}
