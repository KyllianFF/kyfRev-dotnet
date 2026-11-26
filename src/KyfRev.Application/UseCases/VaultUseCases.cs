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
/// <param name="Encrypted">Le coffre ouvert est chiffré : il se verrouille.</param>
/// <param name="Occupied">
/// Un coffre qu'un autre poste a ouvert, pas encore ouvert ici : la question est posée d'abord (<see cref="VaultUseCases.OpenAnyway"/>).
/// </param>
/// <param name="Others">Les autres postes qui l'ont ouvert : celui qui attend la réponse, ou celui ouvert en lecture seule.</param>
/// <param name="ReadOnly">Le coffre ouvert l'est en lecture seule : rien ne s'y écrit.</param>
public sealed record AppState(string Version, AppMode Mode, string? Vault, string? VaultToCreate, string? OpenError, string? Locked = null, bool Encrypted = false, string? Occupied = null, IReadOnlyList<OtherComputer>? Others = null, bool ReadOnly = false);

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
    string version,
    IVaultConversion? conversion = null)
{
    private readonly Lock _gate = new();

    /// <summary>La conversion des coffres ; sans elle, rien ne se convertit.</summary>
    private readonly IVaultConversion _conversion = conversion ?? new NoVaultConversion();

    /// <summary>Le coffre ouvert, s'il a été ouvert chiffré (avec sa clé) : lui seul se verrouille.</summary>
    private string? _encrypted;

    /// <summary>Les coffres chiffrés verrouillés dans cette session de l'application : seule leur clé, donnée de nouveau, les rouvre.</summary>
    private readonly List<string> _locked = [];

    /// <summary>
    /// Les réponses données pour un coffre qu'un autre poste a ouvert : l'ouvrir quand même (faux) ou en lecture seule
    /// (vrai). Gardées jusqu'à ce qu'il s'ouvre, déverrouillage compris.
    /// </summary>
    private readonly Dictionary<string, bool> _answers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Le coffre ouvert l'est en lecture seule, à cause de ces autres postes.</summary>
    private bool _readOnly;

    private IReadOnlyList<OtherComputer> _others = [];

    /// <summary>Les ouvertures en cours : un verrouillage attend qu'elles aient fini, sinon l'une ouvrirait un coffre juste après.</summary>
    private int _opening;

    /// <summary>Une ouverture, une création ou un déverrouillage est en cours, sur un autre fil peut-être.</summary>
    public bool IsOpening
    {
        get
        {
            lock (_gate)
            {
                return _opening > 0;
            }
        }
    }

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

    /// <summary>
    /// Ouvre le coffre de <paramref name="root"/>, qu'un autre poste a ouvert (<see cref="AppState.Occupied"/>), comme
    /// l'utilisateur l'a choisi : quand même, ou en lecture seule. Un coffre chiffré attend ensuite sa clé, comme
    /// <see cref="Open"/> ; la réponse vaut jusqu'à son ouverture.
    /// </summary>
    public AppState OpenAnyway(string root, bool forReading)
    {
        Answer(root, forReading);
        return Open(root);
    }

    /// <summary>
    /// Note la réponse pour le coffre de <paramref name="root"/>, qu'un autre poste a ouvert : l'ouvrir quand même, ou en
    /// lecture seule. L'ouverture ou le déverrouillage suivant ne redemande rien.
    /// </summary>
    public void Answer(string root, bool forReading)
    {
        ArgumentNullException.ThrowIfNull(root);
        lock (_gate)
        {
            _answers[root] = forReading;
        }
    }

    /// <summary>
    /// Le coffre ouvert en lecture seule passe en écriture : il est fermé, puis rouvert sans rien demander de plus ; un
    /// coffre chiffré dont ce poste n'a pas retenu la clé attend alors son mot de passe.
    /// </summary>
    public AppState Writable()
    {
        string root;
        lock (_gate)
        {
            if (!_readOnly || host.Root is not { } open)
            {
                return State();
            }
            root = open;
            _answers[root] = false;
        }
        Close();
        return Open(root);
    }

    /// <summary>
    /// Ce poste a toujours le coffre ouvert : il le redit, et rend les autres postes qui l'ont ouvert (rien sans coffre).
    /// La coquille l'appelle chaque minute.
    /// </summary>
    public IReadOnlyList<OtherComputer> Heartbeat()
    {
        var others = host.Use(vault => vault.Heartbeat(time.GetUtcNow()));
        return others.Succeeded ? others.Value : [];
    }

    /// <summary>
    /// Un autre poste a le coffre de <paramref name="root"/> ouvert, et l'utilisateur n'a pas encore répondu : l'état
    /// l'attend, sans rien ouvrir ni écrire. Rien sinon.
    /// </summary>
    private AppState? HeldBy(string root, Present present)
    {
        lock (_gate)
        {
            if (_answers.ContainsKey(root))
            {
                return null;
            }
        }
        var others = store.OthersHaving(root, present.Now.Instant);
        return others.Count == 0 ? null : StateWith(null) with { Occupied = root, Others = others };
    }

    /// <summary>L'utilisateur a choisi la lecture seule pour ce coffre.</summary>
    private bool ForReading(string root)
    {
        lock (_gate)
        {
            return _answers.TryGetValue(root, out var forReading) && forReading;
        }
    }

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
        using var opening = Opening();
        if (host.IsOpen(root, store.SameLocation))
        {
            return State();
        }
        var present = Present.Of(time);
        if (HeldBy(root, present) is { } held)
        {
            return held;
        }
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
            var forReading = ForReading(root);
            var state = Adopt(() => store.Open(root, key, present.Now, forReading), present, lockedOnFailure: root, encrypted: true, unlocked: root, forReading: forReading);
            if (state.OpenError is null)
            {
                if (!(remember ? keys.Remember(root, key) : keys.Forget(root)))
                {
                    LogKeyNotRemembered(logger, root, remember);
                }
            }
            return state;
        }
    }

    /// <summary>
    /// Vrai si le coffre chiffré de <paramref name="root"/> peut s'ouvrir avec Windows Hello sur ce poste, au démarrage
    /// comme après un verrouillage : sa clé y est retenue, scellée par lui.
    /// </summary>
    public bool OffersHello(string root) => keys.CanUseHello && keys.IsRememberedWithHello(root);

    /// <summary>Vrai si Windows Hello est configuré sur ce poste : retenir une clé la scelle par lui.</summary>
    public bool UsesHello => keys.CanUseHello;

    /// <summary>
    /// Ouvre le coffre chiffré de <paramref name="root"/> avec sa clé scellée par Windows Hello : Windows demande le code
    /// PIN, le visage ou l'empreinte. Il remplace celui qui était ouvert, comme <see cref="Open"/>, et n'est plus
    /// verrouillé. Si l'utilisateur renonce, l'état l'attend toujours, sans message ; un refus de Windows le dit.
    /// </summary>
    public AppState UnlockWithHello(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        using var opening = Opening();
        if (host.IsOpen(root, store.SameLocation))
        {
            return State();
        }
        var present = Present.Of(time);
        // Un autre poste l'a ouvert : la question vient avant le geste, qui ne se demande qu'une fois.
        if (HeldBy(root, present) is { } held)
        {
            return held;
        }
        VaultKey? key;
        try
        {
            key = keys.RememberedWithHello(root);
        }
        catch (OperationException exception)
        {
            return LockedState(root, exception.Message);
        }
        if (key is null)
        {
            return LockedState(root, null);
        }
        using (key)
        {
            var forReading = ForReading(root);
            return Adopt(() => store.Open(root, key, present.Now, forReading), present, lockedOnFailure: root, encrypted: true, unlocked: root, forReading: forReading);
        }
    }

    private AppState OpenOrCreate(string root, bool create)
    {
        ArgumentNullException.ThrowIfNull(root);
        using var opening = Opening();
        if (!create && host.IsOpen(root, store.SameLocation))
        {
            return State();
        }
        var present = Present.Of(time);
        if (!create && HeldBy(root, present) is { } held)
        {
            return held;
        }
        if (!create && keys.IsEncrypted(root))
        {
            if (WasLocked(root))
            {
                // Verrouillé ici, il ne se rouvre qu'avec son mot de passe : la clé retenue ne sert qu'au démarrage.
                return LockedState(root, null);
            }
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
                var readOnly = ForReading(root);
                return Adopt(() => store.Open(root, remembered, present.Now, readOnly), present, lockedOnFailure: null, encrypted: true, forReading: readOnly);
            }
        }
        var reading = !create && ForReading(root);
        return Adopt(() => create ? store.Initialize(root, present.Now) : store.Open(root, present.Now, reading), present, lockedOnFailure: null, encrypted: false, forReading: reading);
    }

    private bool WasLocked(string root)
    {
        string[] locked;
        lock (_gate)
        {
            locked = [.. _locked];
        }
        // Les chemins se comparent sur le disque, hors du verrou : rien n'y attend pendant ce temps.
        return locked.Any(other => store.SameLocation(other, root));
    }

    /// <summary>Compte une ouverture en cours, jusqu'à sa libération.</summary>
    private OpeningScope Opening()
    {
        lock (_gate)
        {
            _opening++;
        }
        return new OpeningScope(this);
    }

    /// <summary>L'ouverture comptée par <see cref="Opening"/> est finie.</summary>
    internal void OpenedScope() => Opened();

    private void Opened()
    {
        lock (_gate)
        {
            _opening--;
        }
    }

    /// <summary>
    /// Le coffre que rend <paramref name="open"/> devient celui de l'application : sa journée est préparée, sa
    /// sauvegarde du jour faite, et il est retenu pour le prochain démarrage. Refusé, l'état le dit, en attente de sa clé
    /// si c'était un coffre chiffré (<paramref name="lockedOnFailure"/>).
    /// </summary>
    private AppState Adopt(Func<IVault> open, Present present, string? lockedOnFailure, bool encrypted, string? unlocked = null, bool forReading = false)
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
            // En lecture seule, rien ne s'écrit : ni la journée, ni la sauvegarde du jour.
            if (!forReading)
            {
                PrepareDay(vault, present);
                BackupOfTheDay(vault, present);
            }
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
        lock (_gate)
        {
            host.Replace(vault);
            _encrypted = encrypted ? vault.Root : null;
            _readOnly = forReading;
            _others = forReading ? store.OthersHaving(vault.Root, present.Now.Instant) : [];
            _answers.Remove(vault.Root);
            if (unlocked is not null)
            {
                // Déverrouillé par sa clé : il n'est plus verrouillé ; les autres coffres verrouillés le restent.
                _locked.RemoveAll(locked => string.Equals(locked, unlocked, StringComparison.OrdinalIgnoreCase) || locked == vault.Root);
            }
        }
        return State();
    }

    /// <summary>
    /// Verrouille le coffre chiffré ouvert : il est fermé, une fois finie l'opération en cours, et sa clé effacée ; l'état
    /// l'attend de nouveau (<see cref="AppState.Locked"/>). Il ne s'ouvre ensuite qu'avec son mot de passe ou sa clé de
    /// récupération : la clé retenue sur ce poste ne sert qu'au démarrage, et plus du tout une fois le coffre verrouillé,
    /// même après une relance (<see cref="IVaultKeys.StayLocked"/>). Un coffre en clair ne se verrouille pas.
    /// </summary>
    public AppState Lock()
    {
        string root;
        lock (_gate)
        {
            if (_opening > 0 || host.Root is not { } open || open != _encrypted)
            {
                return State();
            }
            root = open;
            _locked.Add(root);
            _encrypted = null;
            _readOnly = false;
            host.Close();
        }
        LogVaultLocked(logger, root);
        if (!keys.StayLocked(root))
        {
            LogLockNotKept(logger, root);
        }
        return LockedState(root, null);
    }

    private AppState LockedState(string root, string? error) => StateWith(error) with { Locked = root };

    private AppState StateWith(string? openError)
    {
        string? open;
        bool encrypted;
        bool readOnly;
        IReadOnlyList<OtherComputer> others;
        lock (_gate)
        {
            open = host.Root;
            encrypted = open is not null && open == _encrypted;
            readOnly = open is not null && _readOnly;
            others = _others;
        }
        return new AppState(version, locations.Mode, open, open is null ? locations.VaultToCreate() : null, openError, Encrypted: encrypted, Others: readOnly ? others : null, ReadOnly: readOnly);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Coffre chiffré {Root} verrouillé.")]
    private static partial void LogVaultLocked(ILogger logger, string root);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Coffre {Root} verrouillé, mais ce poste n'a pas pu le noter : sa clé retenue le rouvrirait au prochain démarrage.")]
    private static partial void LogLockNotKept(ILogger logger, string root);

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

/// <summary>Une ouverture en cours, comptée jusqu'à sa libération.</summary>
internal readonly struct OpeningScope(VaultUseCases vaults) : IDisposable
{
    public void Dispose() => vaults.OpenedScope();
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
