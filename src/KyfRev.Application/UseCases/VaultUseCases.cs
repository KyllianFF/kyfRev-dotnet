using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using Microsoft.Extensions.Logging;

namespace KyfRev.Application.UseCases;

/// <summary>État de l'application vu par l'accueil.</summary>
/// <param name="Version">Version de kyfRev.</param>
/// <param name="Vault">Dossier du coffre ouvert.</param>
/// <param name="OpenError">Message si le coffre demandé n'a pas pu être ouvert.</param>
public sealed record AppState(string Version, string? Vault, string? OpenError);

/// <summary>
/// Ouverture et création du coffre. À chaque ouverture : la journée est préparée (tirage des
/// rappels), une sauvegarde est faite si aucune ne l'a été ce jour-là (puis la rotation, si elle
/// est active), et le coffre est retenu pour le prochain démarrage. Ni la préparation ni la
/// sauvegarde ne bloquent l'ouverture : un échec est seulement journalisé.
/// </summary>
public sealed partial class VaultUseCases(
    VaultHost host,
    IVaultStore store,
    IBackupFiles backups,
    IRecentVaults recent,
    TimeProvider time,
    ILogger<VaultUseCases> logger,
    string version)
{
    /// <summary>Ce que montre l'accueil.</summary>
    [ZeroXCommand("etat_application")]
    public AppState State() => new(version, host.Root, null);

    /// <summary>
    /// Ouvre un coffre ; il remplace celui qui était ouvert, fermé seulement une fois le nouveau
    /// ouvert. Rouvrir le coffre déjà ouvert ne fait rien.
    /// </summary>
    [ZeroXCommand("ouvrir_coffre")]
    public AppState Open(string root) => OpenOrCreate(root, create: false);

    /// <summary>Crée un coffre dans un dossier vide, puis l'ouvre.</summary>
    [ZeroXCommand("creer_coffre")]
    public AppState Create(string root) => OpenOrCreate(root, create: true);

    private AppState OpenOrCreate(string root, bool create)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!create && host.IsOpen(root, store.SameLocation))
        {
            return State();
        }
        var present = Present.Of(time);
        IVault vault;
        try
        {
            vault = create ? store.Initialize(root, present.Now) : store.Open(root, present.Now);
        }
        catch (OperationException exception)
        {
            return new AppState(version, host.Root, exception.Message);
        }
        try
        {
            PrepareDay(vault, present);
            BackupOfTheDay(vault, present);
            recent.Remember(vault.Root);
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
