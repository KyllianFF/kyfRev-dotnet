using System.Text;
using KyfRev.Application.Ports;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Ids;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;

namespace KyfRev.Infrastructure.Vault;

/// <summary>
/// Un coffre ouvert, au format 1 de la version 0.x, qu'il lit et écrit sur place.
/// </summary>
/// <remarks>
/// Garanties, identiques à celles de la version 0.x :
/// <list type="bullet">
/// <item>aucune opération ne supprime de fiche : « supprimer » range dans une corbeille ;</item>
/// <item>une fiche n'est jamais écrasée sans l'empreinte du contenu lu (sinon : conflit) ;</item>
/// <item>avant la première modification de chaque session d'édition, et avant d'écraser une
/// modification faite hors de l'application, l'ancien contenu est copié dans
/// <c>.suivi/versions/&lt;id&gt;/</c> ;</item>
/// <item>toute écriture est atomique ; un renommage ou un déplacement passe par le journal
/// <c>.suivi/operation.json</c>, repris à l'ouverture après une coupure ;</item>
/// <item>tout chemin est relatif et confiné au coffre, y compris face aux liens symboliques ;</item>
/// <item>un fichier illisible n'est jamais réécrit.</item>
/// </list>
/// Le coffre reste verrouillé tant que l'objet n'est pas libéré (<see cref="Dispose"/>).
/// </remarks>
public sealed partial class VaultSession : IDisposable
{
    private const string Readme =
        "Ce dossier contient les données de suivi de l'application de révision :\n"
        + "calendriers des J, annotations, historique, versions des fiches et réglages.\n"
        + "\n"
        + "Ne le modifiez pas à la main. Vos fiches, elles, sont de simples fichiers\n"
        + "Markdown dans le dossier « fiches » : vous pouvez les lire et les copier avec\n"
        + "n'importe quel éditeur de texte.\n";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly VaultPath SheetsFolder = VaultPath.Parse(VaultLayout.Sheets);

    private readonly VaultLock _lock;
    private readonly VaultStorage _storage;
    private readonly UlidGenerator _ids;
    private readonly string _realRoot;
    private readonly HashSet<(string Id, ulong Session)> _savedSessions = [];
    private SortedDictionary<string, SheetEntry> _sheets = new(StringComparer.Ordinal);

    private VaultSession(string root, VaultStorage storage, VaultLock vaultLock, VaultSettingsFile settings, UlidGenerator ids, bool readOnly, DateTimeOffset openedAt)
    {
        IsReadOnly = readOnly;
        _openedAt = openedAt;
        _lastSeen = openedAt;
        Root = root;
        _storage = storage;
        _lock = vaultLock;
        Settings = settings;
        _ids = ids;
        _realRoot = storage.Raw.RealPath(root);
    }

    /// <summary>Racine du coffre (chemin absolu).</summary>
    public string Root { get; }

    /// <summary>Ouvert en lecture seule : rien ne s'y écrit, pas même la présence de ce poste.</summary>
    public bool IsReadOnly { get; }

    private readonly DateTimeOffset _openedAt;
    private DateTimeOffset _lastSeen;

    /// <summary>Réglages du coffre (<c>.suivi/coffre.json</c>).</summary>
    public VaultSettingsFile Settings { get; private set; }

    /// <summary>Résultat de la dernière analyse, complété par les opérations suivantes.</summary>
    public HealthReport Health { get; private set; } = new();

    /// <summary>Fiches suivies, par identifiant.</summary>
    public IEnumerable<SheetEntry> Sheets => _sheets.Values;

    /// <summary>Crée un coffre vide dans <paramref name="root"/> (dossier absent ou vide), puis l'ouvre.</summary>
    /// <param name="root">Dossier du coffre.</param>
    /// <param name="now">Instant présent.</param>
    /// <param name="ids">Générateur d'identifiants (celui du système par défaut).</param>
    /// <param name="storage">Là où vivent les fichiers du coffre : le disque par défaut. Il reste à l'appelant, qui le libère après la session.</param>
    /// <exception cref="VaultException">Dossier non vide, ou écriture impossible.</exception>
    public static VaultSession Initialize(string root, Moment now, UlidGenerator? ids = null, VaultStorage? storage = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ids ??= UlidGenerator.System;
        storage ??= DiskStorage.Instance;
        CheckServes(storage, Io(root, () => Path.GetFullPath(root)));
        if (storage.DirectoryExists(root) && Io(root, () => storage.Entries(root).Count > 0))
        {
            throw VaultException.FolderNotEmpty(root);
        }
        foreach (var folder in new[] { VaultLayout.Sheets, VaultLayout.Quotes, VaultLayout.Tracking })
        {
            CreateDirectory(storage, VaultLayout.Absolute(root, folder));
        }
        CreateFile(storage, VaultLayout.Absolute(root, ".suivi/LISEZMOI.txt"), Encoding.UTF8.GetBytes(Readme));
        CreateFile(storage, VaultLayout.Absolute(root, VaultLayout.SettingsFile), VaultSettingsFile.New($"v_{ids.Next()}", now.Day).Write());
        return Open(root, now, ids, storage);
    }

    /// <summary>
    /// Ouvre un coffre : verrou, réglages, reprise d'une opération interrompue, puis analyse
    /// complète (qui met à jour les suivis des fiches déplacées ou modifiées hors de l'application).
    /// </summary>
    /// <param name="root">Dossier du coffre.</param>
    /// <param name="now">Instant présent.</param>
    /// <param name="ids">Générateur d'identifiants (celui du système par défaut).</param>
    /// <param name="storage">Là où vivent les fichiers du coffre : le disque par défaut. Il reste à l'appelant, qui le libère après la session.</param>
    /// <param name="readOnly">
    /// En lecture seule (un autre poste l'a ouvert) : rien ne s'écrit, ni les informations du verrou, ni la présence, ni la
    /// reprise d'une opération interrompue, ni les dossiers qui manqueraient ; toute écriture est refusée.
    /// </param>
    /// <exception cref="VaultException">Pas un coffre, déjà ouvert, réglages illisibles, ou lecture impossible.</exception>
    public static VaultSession Open(string root, Moment now, UlidGenerator? ids = null, VaultStorage? storage = null, bool readOnly = false)
    {
        ArgumentNullException.ThrowIfNull(root);
        storage ??= DiskStorage.Instance;
        if (readOnly)
        {
            storage = new ReadOnlyStorage(storage);
        }
        var absolute = Io(root, () => Path.GetFullPath(root));
        CheckServes(storage, absolute);
        var settingsPath = VaultLayout.Absolute(absolute, VaultLayout.SettingsFile);
        if (!storage.FileExists(settingsPath))
        {
            throw storage.Raw.DirectoryExists(VaultLayout.Absolute(absolute, VaultLayout.Tracking))
                ? VaultException.IncompleteVault(absolute)
                : VaultException.NotAVault(absolute);
        }
        if (storage.Raw != storage && !readOnly)
        {
            // Le verrou reste sur le disque, dans un dossier .suivi que le coffre chiffré n'a pas encore.
            CreateDirectory(storage.Raw, VaultLayout.Absolute(absolute, VaultLayout.Tracking));
        }
        var vaultLock = Io(absolute, () => VaultLock.TryAcquire(absolute, now.Instant, writeInfo: !readOnly, out var holder)
            ?? throw VaultException.AlreadyOpen(holder is null ? "par une autre fenêtre" : $"sur {holder.Machine} depuis {InstantText.Format(holder.Since)}"));
        try
        {
            // Le stockage a pu lire ses fichiers avant le verrou : un autre processus a pu écrire depuis (l'index chiffré).
            Io(absolute, () =>
            {
                storage.Reload();
                return true;
            });
            // Les fichiers écrits par une version d'avant portent un attribut qui empêche leur synchronisation.
            if (!readOnly)
            {
                _ = Io(absolute, () => SyncedFiles.Release(absolute));
            }
            VaultSettingsFile settings;
            byte[] settingsBytes;
            try
            {
                settingsBytes = ReadAllBytes(storage, settingsPath);
                settings = VaultSettingsFile.Read(settingsBytes);
            }
            catch (VaultFormatException exception)
            {
                throw VaultException.UnreadableSettings(exception.Message);
            }
            foreach (var folder in new[] { VaultLayout.Sheets, VaultLayout.Quotes, VaultLayout.SheetTracking, VaultLayout.Annotations, VaultLayout.Versions }.Where(_ => !readOnly))
            {
                CreateDirectory(storage, VaultLayout.Absolute(absolute, folder));
            }
            var vault = new VaultSession(absolute, storage, vaultLock, settings, ids ?? UlidGenerator.System, readOnly, now.Instant);
            vault.KnowSettings(settingsBytes);
            // Une opération interrompue (arrêt brutal pendant un renommage) est terminée avant tout ; pas en lecture seule.
            var toCheck = readOnly ? [] : vault.ResumeInterruptedOperation();
            vault.Analyze(now);
            vault.Health.LinksToCheckList.AddRange(toCheck);
            if (!readOnly)
            {
                _ = ComputerPresence.Announce(absolute, storage.Raw, ComputerPresence.ThisComputer, now.Instant, now.Instant, open: true);
            }
            return vault;
        }
        catch
        {
            vaultLock.Dispose();
            throw;
        }
    }

    /// <summary>Libère le verrou du coffre ; ce poste dit l'avoir fermé.</summary>
    public void Dispose()
    {
        if (!IsReadOnly)
        {
            _ = ComputerPresence.Announce(Root, _storage.Raw, ComputerPresence.ThisComputer, _openedAt, _lastSeen, open: false);
        }
        _lock.Dispose();
    }

    /// <summary>
    /// Ce poste a toujours le coffre ouvert : il le redit (rien en lecture seule), et rend les autres postes qui l'ont
    /// ouvert, d'après leur présence.
    /// </summary>
    public IReadOnlyList<KyfRev.Application.Model.OtherComputer> Heartbeat(DateTimeOffset now)
    {
        if (!IsReadOnly)
        {
            _lastSeen = now;
            _ = ComputerPresence.Announce(Root, _storage.Raw, ComputerPresence.ThisComputer, _openedAt, now, open: true);
        }
        return ComputerPresence.Others(Root, _storage.Raw, ComputerPresence.ThisComputer, now);
    }

    /// <summary>Fiche suivie, présente ou non.</summary>
    /// <exception cref="VaultException">Aucune fiche ne porte cet identifiant.</exception>
    public SheetEntry Sheet(string id) =>
        _sheets.TryGetValue(id, out var entry) ? entry : throw VaultException.UnknownSheet(id);

    /// <summary>
    /// Analyse complète du coffre (à l'ouverture et sur demande) : rapproche les fichiers
    /// <c>.md</c> et les fichiers de suivi par identifiant. Une fiche déplacée, modifiée, renommée
    /// ou étiquetée hors de l'application voit son suivi mis à jour.
    /// </summary>
    /// <exception cref="VaultException">Un dossier du coffre ne peut pas être parcouru.</exception>
    public void Analyze(Moment now)
    {
        var report = new HealthReport();

        // 1. Les fiches sur le disque, regroupées par identifiant.
        var found = new SortedDictionary<string, List<FoundSheet>>(StringComparer.Ordinal);
        _links.Clear();
        ForgetSearchTexts();
        foreach (var path in ListMarkdown(SheetsFolder, report.UnreadableList))
        {
            var absolute = path.ToAbsolute(Root);
            byte[] bytes;
            DateTime modified;
            try
            {
                // La date d'abord : un changement pendant la lecture fera relire la fiche (liens cassés).
                modified = _storage.LastWriteTimeUtc(absolute);
                bytes = _storage.ReadAllBytes(absolute);
            }
            catch (Exception exception) when (IsIo(exception))
            {
                report.UnreadableList.Add(new UnreadableFile(path.Value, exception.Message));
                continue;
            }
            if (!TryDecode(bytes, out var text))
            {
                report.UnreadableList.Add(new UnreadableFile(path.Value, "fichier non encodé en UTF-8"));
                continue;
            }
            RememberLinks(path, text, modified, bytes.Length);
            var reading = FrontMatter.Read(text);
            if (!reading.IsSuccess)
            {
                report.UnreadableList.Add(new UnreadableFile(path.Value, reading.Error));
            }
            else if (reading.Header.Id is not { } id)
            {
                report.UntrackedList.Add(path);
            }
            else if (!VaultIds.IsSheetId(id))
            {
                report.UnreadableList.Add(new UnreadableFile(path.Value, $"identifiant « {id} » invalide"));
            }
            else
            {
                if (!found.TryGetValue(id, out var copies))
                {
                    found[id] = copies = [];
                }
                copies.Add(new FoundSheet(path, Fingerprint.Of(bytes), reading.Header.Title, [.. reading.Header.Tags], LastWrite(absolute)));
            }
        }

        // 2. Les fichiers de suivi.
        var trackings = new List<TrackedSheetFile>();
        _trackingPrints.Clear();
        var trackingFolder = VaultLayout.Absolute(Root, VaultLayout.SheetTracking);
        foreach (var file in Io(trackingFolder, () => _storage.Entries(trackingFolder).Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToList()))
        {
            var name = Path.GetFileName(file);
            if (Extension(name) != "json")
            {
                continue;
            }
            var stem = name[..name.LastIndexOf('.')];
            var shown = $"{VaultLayout.SheetTracking}/{stem}.json";
            try
            {
                var bytes = _storage.ReadAllBytes(file);
                var tracking = TrackedSheetFile.Read(bytes);
                if (tracking.Id == stem && VaultIds.IsSheetId(stem))
                {
                    trackings.Add(tracking);
                    KnowTracking(stem, bytes);
                }
                else
                {
                    report.UnreadableList.Add(new UnreadableFile(shown, "le nom du fichier ne correspond pas à l'identifiant"));
                }
            }
            catch (Exception exception) when (exception is VaultFormatException || IsIo(exception))
            {
                report.UnreadableList.Add(new UnreadableFile(shown, exception.Message));
            }
        }

        // 3. Rapprochement par identifiant.
        var sheets = new SortedDictionary<string, SheetEntry>(StringComparer.Ordinal);
        foreach (var tracking in trackings)
        {
            var sheet = tracking;
            FilePresence presence;
            if (!found.TryGetValue(sheet.Id, out var copies))
            {
                report.MissingList.Add(sheet.Id);
                presence = FilePresence.Missing;
            }
            else if (copies.Count == 1)
            {
                var copy = copies[0];
                var changed = false;
                if (copy.Path != sheet.Path)
                {
                    report.MovedList.Add(new SheetMove(sheet.Id, sheet.Path, copy.Path));
                    sheet = sheet with { Path = copy.Path };
                    changed = true;
                }
                if (copy.Fingerprint != sheet.Fingerprint)
                {
                    report.ModifiedOutsideList.Add(sheet.Id);
                    sheet = sheet with { Fingerprint = copy.Fingerprint, ModifiedAt = copy.LastWrite ?? now.Instant };
                    changed = true;
                }
                if (copy.Title is { } title && title.Trim().Length > 0 && title != sheet.Title)
                {
                    sheet = sheet with { Title = title };
                    changed = true;
                }
                if (!copy.Tags.SequenceEqual(sheet.Tags))
                {
                    sheet = sheet with { Tags = [.. copy.Tags] };
                    changed = true;
                }
                // Un suivi qui ne s'écrit pas (gardé ouvert par un autre programme) n'empêche pas d'ouvrir le coffre : il est
                // à jour en mémoire, et la prochaine écriture ou la prochaine analyse le rattrape.
                if (changed)
                {
                    _ = TryRewriteTracking(sheet);
                }
                presence = FilePresence.Present;
            }
            else
            {
                report.DuplicatesList.Add(new DuplicateSheet(sheet.Id, [.. copies.Select(c => c.Path)]));
                presence = FilePresence.Duplicated;
            }
            found.Remove(sheet.Id);
            sheets[sheet.Id] = new SheetEntry(sheet, presence);
        }
        foreach (var (id, copies) in found)
        {
            report.WithoutTrackingList.AddRange(copies.Select(copy => new UntrackedId(id, copy.Path)));
        }

        _sheets = sheets;
        LoadAnnotations(report);
        Health = report;
    }

    /// <summary>Fiche dont le fichier est présent, seul à porter son identifiant.</summary>
    internal SheetEntry AvailableSheet(string id)
    {
        var entry = Sheet(id);
        return entry.Presence switch
        {
            FilePresence.Present => entry,
            FilePresence.Missing => throw VaultException.MissingSheet(entry.Sheet.Title),
            _ => throw VaultException.UnresolvedDuplicate(entry.Sheet.Title),
        };
    }

    /// <summary>
    /// Chemin absolu d'un chemin du coffre, après avoir vérifié qu'il n'en sort pas, y compris
    /// par un lien symbolique ou une jonction.
    /// </summary>
    internal string Confine(VaultPath path)
    {
        var absolute = path.ToAbsolute(Root);
        // La racine réelle est résolue à l'ouverture : seuls les composants du chemin dans le coffre sont examinés.
        var real = Io(absolute, () => _storage.RealPathBelow(_realRoot, path.Value.Replace('/', Path.DirectorySeparatorChar)));
        return RealPaths.IsWithin(real, _realRoot) ? absolute : throw VaultException.OutsideVault(absolute);
    }

    /// <summary>
    /// Réécrit le suivi d'une fiche déjà suivie ; <see langword="false"/> s'il ne s'écrit pas. Pour ce qui suit une écriture
    /// déjà faite (le texte de la fiche) ou ce qui se rattrape (l'analyse) : le suivi en mémoire fait foi, le fichier
    /// suit à la prochaine écriture.
    /// </summary>
    internal bool TryRewriteTracking(TrackedSheetFile sheet)
    {
        try
        {
            WriteTracking(sheet, create: false);
            return true;
        }
        catch (VaultException exception) when (exception.Kind == VaultErrorKind.Io)
        {
            return false;
        }
    }

    internal void WriteTracking(TrackedSheetFile sheet, bool create)
    {
        var absolute = Confine(VaultPath.Create($"{VaultLayout.SheetTracking}/{sheet.Id}.json"));
        var bytes = sheet.Write();
        if (create)
        {
            CreateFile(absolute, bytes);
        }
        else
        {
            ReplaceFile(absolute, bytes);
        }
        KnowTracking(sheet.Id, bytes);
    }

    /// <summary>Copie un contenu de fiche dans <c>.suivi/versions/&lt;id&gt;/&lt;horodatage&gt;.md</c>.</summary>
    internal void SaveVersion(string id, ReadOnlySpan<byte> content, Moment now)
    {
        var folder = VaultPath.Create($"{VaultLayout.Versions}/{id}");
        var folderAbsolute = Confine(folder);
        CreateDirectory(folderAbsolute);
        var stamp = now.Instant.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        for (var number = 1; number < 1000; number++)
        {
            var name = number == 1 ? $"{stamp}.md" : $"{stamp}-{number}.md";
            if (TryCreateFile(Confine(folder.Join(name)), content))
            {
                return;
            }
        }
        throw VaultException.AlreadyExists(folderAbsolute);
    }

    /// <summary>
    /// Fichiers <c>.md</c> sous <paramref name="folder"/>, triés. Ignore les fichiers et dossiers
    /// cachés (dont les temporaires), les dossiers de ressources et les liens symboliques, qui
    /// pourraient mener hors du coffre.
    /// </summary>
    internal List<VaultPath> ListMarkdown(VaultPath folder, List<UnreadableFile> unreadable)
    {
        var found = new List<VaultPath>();
        var toVisit = new Stack<string>([folder.ToAbsolute(Root)]);
        while (toVisit.TryPop(out var current))
        {
            foreach (var entry in Io(current, () => _storage.Entries(current)))
            {
                var name = entry.Name;
                if (name.StartsWith('.') || entry.IsLink)
                {
                    continue;
                }
                if (entry.IsDirectory)
                {
                    if (!name.EndsWith(VaultLayout.ResourcesSuffix, StringComparison.Ordinal))
                    {
                        toVisit.Push(entry.FullName);
                    }
                }
                else if (Extension(name) is { } extension && Ascii.EqualsIgnoreCase(extension, "md"))
                {
                    if (VaultPath.TryFromAbsolute(Root, entry.FullName, out var path, out var reason))
                    {
                        found.Add(path);
                    }
                    else
                    {
                        var text = Path.GetRelativePath(Root, entry.FullName).Replace('\\', '/');
                        unreadable.Add(new UnreadableFile(entry.FullName, VaultException.InvalidPath(text, reason).Message));
                    }
                }
            }
        }
        found.Sort();
        return found;
    }

    /// <summary>Extension d'un nom de fichier, comme <c>Path::extension</c> en Rust (aucune pour « .json »).</summary>
    internal static string? Extension(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot <= 0 ? null : name[(dot + 1)..];
    }

    internal static bool TryDecode(byte[] bytes, out string text)
    {
        try
        {
            text = StrictUtf8.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    internal byte[] ReadAllBytes(string absolute) => ReadAllBytes(_storage, absolute);

    internal static byte[] ReadAllBytes(VaultStorage storage, string absolute) => Io(absolute, () => storage.ReadAllBytes(absolute));

    /// <summary>Le contenu d'un fichier du coffre, ou rien s'il ne se lit pas.</summary>
    internal byte[]? TryReadAllBytes(string absolute) => _storage.TryReadAllBytes(absolute);

    internal void CreateDirectory(string absolute) => CreateDirectory(_storage, absolute);

    internal static void CreateDirectory(VaultStorage storage, string absolute) => Io(absolute, () =>
    {
        storage.CreateDirectory(absolute);
        return true;
    });

    /// <summary>Crée le fichier de façon atomique ; refusé s'il existe déjà.</summary>
    internal void CreateFile(string absolute, ReadOnlySpan<byte> content) => CreateFile(_storage, absolute, content);

    /// <summary>Crée le fichier de façon atomique dans <paramref name="storage"/> ; refusé s'il existe déjà.</summary>
    internal static void CreateFile(VaultStorage storage, string absolute, ReadOnlySpan<byte> content)
    {
        if (!TryCreateFile(storage, absolute, content))
        {
            throw VaultException.AlreadyExists(absolute);
        }
    }

    /// <summary>Crée le fichier de façon atomique ; <see langword="false"/> s'il existe déjà.</summary>
    internal bool TryCreateFile(string absolute, ReadOnlySpan<byte> content) => TryCreateFile(_storage, absolute, content);

    private static bool TryCreateFile(VaultStorage storage, string absolute, ReadOnlySpan<byte> content)
    {
        try
        {
            return storage.TryCreateNew(absolute, content);
        }
        catch (Exception exception) when (IsIo(exception))
        {
            throw VaultException.Io(absolute, exception);
        }
    }

    internal void ReplaceFile(string absolute, ReadOnlySpan<byte> content)
    {
        ForgetSearchText(absolute);
        try
        {
            _storage.Replace(absolute, content);
        }
        catch (Exception exception) when (IsIo(exception))
        {
            throw VaultException.Io(absolute, exception);
        }
    }

    internal static T Io<T>(string path, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception) when (IsIo(exception))
        {
            throw VaultException.Io(path, exception);
        }
    }

    /// <summary>Un stockage lié à un dossier ne sert que celui-là.</summary>
    private static void CheckServes(VaultStorage storage, string absolute)
    {
        if (storage.Root is { } served && !string.Equals(Path.TrimEndingDirectorySeparator(served), Path.TrimEndingDirectorySeparator(absolute), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Ce stockage sert « {served} », pas « {absolute} ».", nameof(storage));
        }
    }

    internal static bool IsIo(Exception exception) => exception is IOException or UnauthorizedAccessException;

    private DateTimeOffset? LastWrite(string absolute)
    {
        try
        {
            return new DateTimeOffset(_storage.LastWriteTimeUtc(absolute), TimeSpan.Zero);
        }
        catch (Exception exception) when (IsIo(exception))
        {
            return null;
        }
    }

    /// <summary>Une fiche telle que trouvée sur le disque pendant l'analyse.</summary>
    private sealed record FoundSheet(VaultPath Path, string Fingerprint, string? Title, List<string> Tags, DateTimeOffset? LastWrite);
}
