using System.Text;
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
    private readonly UlidGenerator _ids;
    private readonly string _realRoot;
    private readonly HashSet<(string Id, ulong Session)> _savedSessions = [];
    private SortedDictionary<string, SheetEntry> _sheets = new(StringComparer.Ordinal);

    private VaultSession(string root, VaultLock vaultLock, VaultSettingsFile settings, UlidGenerator ids)
    {
        Root = root;
        _lock = vaultLock;
        Settings = settings;
        _ids = ids;
        _realRoot = RealPaths.OfExistingPart(root);
    }

    /// <summary>Racine du coffre (chemin absolu).</summary>
    public string Root { get; }

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
    /// <exception cref="VaultException">Dossier non vide, ou écriture impossible.</exception>
    public static VaultSession Initialize(string root, Moment now, UlidGenerator? ids = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ids ??= UlidGenerator.System;
        if (Directory.Exists(root) && Io(root, () => Directory.EnumerateFileSystemEntries(root).Any()))
        {
            throw VaultException.FolderNotEmpty(root);
        }
        foreach (var folder in new[] { VaultLayout.Sheets, VaultLayout.Quotes, VaultLayout.Tracking })
        {
            CreateDirectory(VaultLayout.Absolute(root, folder));
        }
        CreateFile(VaultLayout.Absolute(root, ".suivi/LISEZMOI.txt"), Encoding.UTF8.GetBytes(Readme));
        CreateFile(VaultLayout.Absolute(root, VaultLayout.SettingsFile), VaultSettingsFile.New($"v_{ids.Next()}", now.Day).Write());
        return Open(root, now, ids);
    }

    /// <summary>
    /// Ouvre un coffre : verrou, réglages, reprise d'une opération interrompue, puis analyse
    /// complète (qui met à jour les suivis des fiches déplacées ou modifiées hors de l'application).
    /// </summary>
    /// <param name="root">Dossier du coffre.</param>
    /// <param name="now">Instant présent.</param>
    /// <param name="ids">Générateur d'identifiants (celui du système par défaut).</param>
    /// <exception cref="VaultException">Pas un coffre, déjà ouvert, réglages illisibles, ou lecture impossible.</exception>
    public static VaultSession Open(string root, Moment now, UlidGenerator? ids = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var absolute = Io(root, () => Path.GetFullPath(root));
        var settingsPath = VaultLayout.Absolute(absolute, VaultLayout.SettingsFile);
        if (!File.Exists(settingsPath))
        {
            throw VaultException.NotAVault(absolute);
        }
        var vaultLock = Io(absolute, () => VaultLock.TryAcquire(absolute, now.Instant, out var holder)
            ?? throw VaultException.AlreadyOpen(holder is null ? "par une autre fenêtre" : $"sur {holder.Machine} depuis {InstantText.Format(holder.Since)}"));
        try
        {
            VaultSettingsFile settings;
            try
            {
                settings = VaultSettingsFile.Read(ReadAllBytes(settingsPath));
            }
            catch (VaultFormatException exception)
            {
                throw VaultException.UnreadableSettings(exception.Message);
            }
            foreach (var folder in new[] { VaultLayout.Sheets, VaultLayout.Quotes, VaultLayout.SheetTracking, VaultLayout.Annotations, VaultLayout.Versions })
            {
                CreateDirectory(VaultLayout.Absolute(absolute, folder));
            }
            var vault = new VaultSession(absolute, vaultLock, settings, ids ?? UlidGenerator.System);
            // Une opération interrompue (arrêt brutal pendant un renommage) est terminée avant tout.
            var toCheck = vault.ResumeInterruptedOperation();
            vault.Analyze(now);
            vault.Health.LinksToCheckList.AddRange(toCheck);
            return vault;
        }
        catch
        {
            vaultLock.Dispose();
            throw;
        }
    }

    /// <summary>Libère le verrou du coffre.</summary>
    public void Dispose() => _lock.Dispose();

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
        foreach (var path in ListMarkdown(SheetsFolder, report.UnreadableList))
        {
            var absolute = path.ToAbsolute(Root);
            byte[] bytes;
            DateTime modified;
            try
            {
                // La date d'abord : un changement pendant la lecture fera relire la fiche (liens cassés).
                modified = File.GetLastWriteTimeUtc(absolute);
                bytes = File.ReadAllBytes(absolute);
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
        var trackingFolder = VaultLayout.Absolute(Root, VaultLayout.SheetTracking);
        foreach (var file in Io(trackingFolder, () => Directory.EnumerateFileSystemEntries(trackingFolder).Order(StringComparer.Ordinal).ToList()))
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
                var tracking = TrackedSheetFile.Read(File.ReadAllBytes(file));
                if (tracking.Id == stem && VaultIds.IsSheetId(stem))
                {
                    trackings.Add(tracking);
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
                if (changed)
                {
                    WriteTracking(sheet, create: false);
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
        var real = Io(absolute, () => RealPaths.OfExistingPartBelow(_realRoot, path.Value.Replace('/', Path.DirectorySeparatorChar)));
        return RealPaths.IsWithin(real, _realRoot) ? absolute : throw VaultException.OutsideVault(absolute);
    }

    internal void WriteTracking(TrackedSheetFile sheet, bool create)
    {
        var absolute = Confine(VaultPath.Create($"{VaultLayout.SheetTracking}/{sheet.Id}.json"));
        if (create)
        {
            CreateFile(absolute, sheet.Write());
        }
        else
        {
            ReplaceFile(absolute, sheet.Write());
        }
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
            foreach (var entry in Io(current, () => new DirectoryInfo(current).EnumerateFileSystemInfos().ToList()))
            {
                var name = entry.Name;
                if (name.StartsWith('.') || entry.LinkTarget is not null)
                {
                    continue;
                }
                if (entry is DirectoryInfo)
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

    internal static byte[] ReadAllBytes(string absolute) => Io(absolute, () => File.ReadAllBytes(absolute));

    internal static void CreateDirectory(string absolute) => Io(absolute, () => Directory.CreateDirectory(absolute));

    /// <summary>Crée le fichier de façon atomique ; refusé s'il existe déjà.</summary>
    internal static void CreateFile(string absolute, ReadOnlySpan<byte> content)
    {
        if (!TryCreateFile(absolute, content))
        {
            throw VaultException.AlreadyExists(absolute);
        }
    }

    /// <summary>Crée le fichier de façon atomique ; <see langword="false"/> s'il existe déjà.</summary>
    internal static bool TryCreateFile(string absolute, ReadOnlySpan<byte> content)
    {
        try
        {
            return AtomicFile.TryCreateNew(absolute, content);
        }
        catch (Exception exception) when (IsIo(exception))
        {
            throw VaultException.Io(absolute, exception);
        }
    }

    internal static void ReplaceFile(string absolute, ReadOnlySpan<byte> content)
    {
        try
        {
            AtomicFile.Replace(absolute, content);
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

    internal static bool IsIo(Exception exception) => exception is IOException or UnauthorizedAccessException;

    private static DateTimeOffset? LastWrite(string absolute)
    {
        try
        {
            return new DateTimeOffset(File.GetLastWriteTimeUtc(absolute), TimeSpan.Zero);
        }
        catch (Exception exception) when (IsIo(exception))
        {
            return null;
        }
    }

    /// <summary>Une fiche telle que trouvée sur le disque pendant l'analyse.</summary>
    private sealed record FoundSheet(VaultPath Path, string Fingerprint, string? Title, List<string> Tags, DateTimeOffset? LastWrite);
}
