using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KyfRev.Application.Ports;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;

namespace KyfRev.Infrastructure.Vault.Analysis;

/// <summary>
/// Analyse un coffre <b>en lecture seule</b> : aucun fichier n'est créé, modifié ni verrouillé, et
/// chaque fichier est ouvert en partage pour ne pas gêner une application qui aurait le coffre
/// ouvert. Reprend le rapprochement fiches ↔ suivis de la version 0.x et contrôle chaque fichier
/// du format 1.
/// </summary>
public static partial class VaultInspector
{
    private const int MaxSchemaBytes = 8 * 1024 * 1024;
    private const int MaxExtensionDataBytes = 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Analyse le coffre situé à <paramref name="root"/>.</summary>
    /// <param name="root">Le dossier du coffre.</param>
    /// <param name="storage">Là où vivent ses fichiers : le disque par défaut.</param>
    public static VaultInspection Inspect(string root, VaultStorage? storage = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var context = new Context(Path.GetFullPath(root), storage ?? DiskStorage.Instance);
        if (!context.Storage.DirectoryExists(context.Root) || !context.Storage.FileExists(VaultLayout.Absolute(context.Root, VaultLayout.SettingsFile)))
        {
            context.Add(FindingSeverity.Error, "pas-un-coffre", null, $"Ce dossier n'est pas un coffre kyfRev : {VaultLayout.SettingsFile} est introuvable.");
            return context.Result(isVault: false);
        }

        var settings = context.Read(VaultLayout.SettingsFile, VaultSettingsFile.Read, "reglages-illisibles", "Réglages illisibles : le coffre ne peut pas s'ouvrir");
        if (settings is not null)
        {
            context.CheckFormat(VaultLayout.SettingsFile, settings.Format);
            context.Summary = context.Summary with { VaultId = settings.Id, CreatedOn = settings.CreatedOn };
        }

        var sheetFiles = ScanSheetFiles(context);
        var trackings = ScanTrackings(context);
        MatchSheets(context, sheetFiles, trackings);
        var trashed = ScanTrash(context);
        ScanAnnotations(context, trackings.Keys.Concat(trashed).ToHashSet(StringComparer.Ordinal));
        ScanSmallFiles(context);
        ScanVersions(context);
        ScanQuotes(context);
        ScanSchemas(context);
        ScanExtensionData(context);
        return context.Result(isVault: true);
    }

    private static List<SheetFile> ScanSheetFiles(Context context)
    {
        var found = new List<SheetFile>();
        var sheets = VaultLayout.Absolute(context.Root, VaultLayout.Sheets);
        if (!context.Storage.DirectoryExists(sheets))
        {
            return found;
        }
        foreach (var file in EnumerateSheets(context.Storage, sheets))
        {
            if (!VaultPath.TryFromAbsolute(context.Root, file, out var path, out var reason))
            {
                context.Add(FindingSeverity.Error, "nom-refuse", Relative(context.Root, file), $"Nom de fichier refusé : {reason}.");
                continue;
            }
            var bytes = context.ReadBytes(path.Value);
            if (bytes is null)
            {
                continue;
            }
            string text;
            try
            {
                text = StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                context.Add(FindingSeverity.Error, "fiche-illisible", path.Value, "Fiche illisible : le fichier n'est pas en UTF-8.");
                continue;
            }
            var header = FrontMatter.Read(text);
            if (!header.IsSuccess)
            {
                context.Add(FindingSeverity.Error, "en-tete-illisible", path.Value, $"En-tête YAML illisible : {header.Error}");
                continue;
            }
            if (header.Header.Id is { } id && !VaultIds.IsSheetId(id))
            {
                context.Add(FindingSeverity.Error, "identifiant-invalide", path.Value, $"Identifiant « {id} » invalide dans l'en-tête.");
                continue;
            }
            found.Add(new SheetFile(path, Fingerprint.Of(bytes), header.Header.Id));
        }
        found.Sort((a, b) => a.Path.CompareTo(b.Path));
        return found;
    }

    /// <summary>Fichiers <c>.md</c> de <c>fiches/</c>, sans les éléments cachés, les dossiers de ressources ni les liens.</summary>
    private static IEnumerable<string> EnumerateSheets(VaultStorage storage, string directory)
    {
        foreach (var entry in storage.Entries(directory))
        {
            if (entry.Name.StartsWith('.') || entry.IsReparsePoint)
            {
                continue;
            }
            if (entry.IsDirectory)
            {
                if (entry.Name.EndsWith(VaultLayout.ResourcesSuffix, StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (var file in EnumerateSheets(storage, entry.FullName))
                {
                    yield return file;
                }
            }
            else if (Path.GetExtension(entry.Name).Equals(".md", StringComparison.OrdinalIgnoreCase))
            {
                yield return entry.FullName;
            }
        }
    }

    private static Dictionary<string, TrackedSheetFile> ScanTrackings(Context context)
    {
        var trackings = new Dictionary<string, TrackedSheetFile>(StringComparer.Ordinal);
        foreach (var (relative, id) in context.JsonFilesIn(VaultLayout.SheetTracking, VaultIds.IsSheetId))
        {
            var tracking = context.Read(relative, TrackedSheetFile.Read, "suivi-illisible", "Suivi illisible, laissé intact");
            if (tracking is null)
            {
                continue;
            }
            context.CheckFormat(relative, tracking.Format);
            if (tracking.Id != id)
            {
                context.Add(FindingSeverity.Error, "suivi-illisible", relative, $"Suivi illisible, laissé intact : l'identifiant {tracking.Id} ne correspond pas au nom du fichier.");
                continue;
            }
            trackings[id] = tracking;
        }
        return trackings;
    }

    private static void MatchSheets(Context context, List<SheetFile> files, Dictionary<string, TrackedSheetFile> trackings)
    {
        var byId = files.Where(f => f.Id is not null).GroupBy(f => f.Id!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        int active = 0, archived = 0, present = 0;
        foreach (var (id, tracking) in trackings.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (tracking.Tracking.Status == TrackingStatus.Active)
            {
                active++;
            }
            else
            {
                archived++;
            }
            if (!byId.TryGetValue(id, out var copies))
            {
                context.Add(FindingSeverity.Warning, "fiche-manquante", tracking.Path.Value, $"Fiche « {tracking.Title} » introuvable : son suivi est conservé, elle reste restaurable depuis une sauvegarde.");
                continue;
            }
            if (copies.Count > 1)
            {
                context.Add(FindingSeverity.Error, "doublon", tracking.Path.Value, $"Fiche « {tracking.Title} » en {copies.Count} exemplaires : {string.Join(", ", copies.Select(c => c.Path.Value))}.");
                continue;
            }
            present++;
            var file = copies[0];
            if (file.Path != tracking.Path)
            {
                context.Add(FindingSeverity.Information, "fiche-deplacee", file.Path.Value, $"Fiche déplacée hors de l'application (dernier chemin connu : {tracking.Path.Value}) ; son suivi la suit grâce à son identifiant.");
            }
            else if (file.Fingerprint != tracking.Fingerprint)
            {
                context.Add(FindingSeverity.Information, "fiche-modifiee", file.Path.Value, "Fiche modifiée hors de l'application depuis son dernier passage.");
            }
        }
        foreach (var (id, copies) in byId.Where(pair => !trackings.ContainsKey(pair.Key)))
        {
            foreach (var copy in copies)
            {
                context.Add(FindingSeverity.Warning, "sans-suivi", copy.Path.Value, $"La fiche porte l'identifiant {id}, mais son suivi est introuvable.");
            }
        }
        var untracked = files.Count(f => f.Id is null);
        context.Summary = context.Summary with { TrackedSheets = present, ActiveSheets = active, ArchivedSheets = archived, UntrackedFiles = untracked };
    }

    private static HashSet<string> ScanTrash(Context context)
    {
        var trashed = new HashSet<string>(StringComparer.Ordinal);
        const string annotationsSuffix = ".annotations";
        static bool IsTrashName(string name) =>
            VaultIds.IsSheetId(name) || (name.EndsWith(annotationsSuffix, StringComparison.Ordinal) && VaultIds.IsSheetId(name[..^annotationsSuffix.Length]));
        foreach (var (relative, name) in context.JsonFilesIn(VaultLayout.TrashTracking, IsTrashName))
        {
            if (VaultIds.IsSheetId(name))
            {
                if (context.Read(relative, TrackedSheetFile.Read, "suivi-illisible", "Suivi d'une fiche à la corbeille illisible, laissé intact") is { } tracking)
                {
                    context.CheckFormat(relative, tracking.Format);
                    trashed.Add(name);
                }
            }
            else if (context.Read(relative, AnnotationsFile.Read, "annotations-illisibles", "Annotations d'une fiche à la corbeille illisibles, laissées intactes") is { } annotations)
            {
                context.CheckFormat(relative, annotations.Format);
            }
        }
        var trash = VaultLayout.Absolute(context.Root, VaultLayout.SheetsTrash);
        if (context.Storage.DirectoryExists(trash))
        {
            foreach (var folder in context.Storage.Entries(trash).Where(entry => entry.IsDirectory).Select(entry => entry.FullName).Where(d => VaultIds.IsSheetId(Path.GetFileName(d))))
            {
                var info = $"{VaultLayout.SheetsTrash}/{Path.GetFileName(folder)}/corbeille.json";
                if (context.Exists(info) && context.Read(info, TrashInfoFile.Read, "corbeille-illisible", "Fiche de corbeille illisible") is { } trashInfo)
                {
                    context.CheckFormat(info, trashInfo.Format);
                }
            }
        }
        context.Summary = context.Summary with { TrashedSheets = trashed.Count };
        return trashed;
    }

    private static void ScanAnnotations(Context context, HashSet<string> knownSheets)
    {
        int annotations = 0, definitions = 0, trashedAnnotations = 0;
        foreach (var (relative, sheetId) in context.JsonFilesIn(VaultLayout.Annotations, VaultIds.IsSheetId))
        {
            var file = context.Read(relative, AnnotationsFile.Read, "annotations-illisibles", "Annotations illisibles, jamais écrasées");
            if (file is null)
            {
                continue;
            }
            context.CheckFormat(relative, file.Format);
            if (file.SheetId != sheetId)
            {
                context.Add(FindingSeverity.Error, "annotations-illisibles", relative, $"Annotations illisibles, jamais écrasées : elles portent la fiche {file.SheetId}, pas {sheetId}.");
                continue;
            }
            var live = file.Annotations.Where(a => a.TrashedAt is null).ToList();
            annotations += live.Count;
            definitions += live.Count(a => a.Definition is not null);
            trashedAnnotations += file.Annotations.Length - live.Count;
            if (live.Count > 0 && !knownSheets.Contains(sheetId))
            {
                context.Add(FindingSeverity.Warning, "annotations-orphelines", relative, $"{live.Count} annotation(s) d'une fiche introuvable : toujours révisables à partir du passage copié.");
            }
            var uncertain = live.Count(a => a.AnchorState != AnchorState.Anchored);
            if (uncertain > 0)
            {
                context.Add(FindingSeverity.Information, "ancrage-a-verifier", relative, $"{uncertain} passage(s) à vérifier ou introuvable(s) dans leur fiche.");
            }
        }
        context.Summary = context.Summary with { Annotations = annotations, Definitions = definitions, TrashedAnnotations = trashedAnnotations };
    }

    private static void ScanSmallFiles(Context context)
    {
        if (context.Exists(VaultLayout.DrawsFile) && context.Read(VaultLayout.DrawsFile, DrawJournalFile.Read, "tirages-illisibles", "Journal des tirages illisible : le tableau de bord ne pourra pas préparer la journée") is { } draws)
        {
            context.CheckFormat(VaultLayout.DrawsFile, draws.Format);
        }
        if (context.Exists(VaultLayout.OnlineQuoteFile) && context.Read(VaultLayout.OnlineQuoteFile, OnlineQuoteFile.Read, "citation-en-ligne-illisible", "Citation en ligne illisible (sans conséquence : elle est ignorée)") is { } quote)
        {
            context.CheckFormat(VaultLayout.OnlineQuoteFile, quote.Format);
        }
        if (context.Exists(VaultLayout.LockInfoFile, onDisk: true) && context.Read(VaultLayout.LockInfoFile, LockInfoFile.Read, "verrou-info-illisible", "Informations de verrou illisibles (sans conséquence)", onDisk: true) is { } lockInfo)
        {
            context.Add(FindingSeverity.Information, "derniere-ouverture", VaultLayout.LockInfoFile, $"Dernière ouverture : poste « {lockInfo.Machine} », depuis le {InstantText.Format(lockInfo.Since)}.");
        }
        if (context.Exists(VaultLayout.OperationFile) && context.Read(VaultLayout.OperationFile, OperationJournalFile.Read, "operation-illisible", "Journal d'opération illisible, laissé en place") is { } operation)
        {
            context.CheckFormat(VaultLayout.OperationFile, operation.Format);
            if (operation.State == OperationState.InProgress)
            {
                context.Add(FindingSeverity.Warning, "operation-interrompue", VaultLayout.OperationFile, $"Opération interrompue ({operation.Description}) : elle sera reprise à la prochaine ouverture.");
            }
        }
    }

    private static void ScanVersions(Context context)
    {
        var versions = VaultLayout.Absolute(context.Root, VaultLayout.Versions);
        if (!context.Storage.DirectoryExists(versions))
        {
            return;
        }
        var count = 0;
        foreach (var folder in context.Storage.Entries(versions).Where(entry => entry.IsDirectory).Select(entry => entry.FullName))
        {
            foreach (var file in context.Storage.Files(folder, "*"))
            {
                if (VersionName().IsMatch(Path.GetFileName(file)))
                {
                    count++;
                }
                else
                {
                    context.Add(FindingSeverity.Information, "version-inconnue", Relative(context.Root, file), "Fichier inattendu dans les versions : ignoré.");
                }
            }
        }
        context.Summary = context.Summary with { Versions = count };
    }

    private static void ScanQuotes(Context context)
    {
        var quotes = VaultLayout.Absolute(context.Root, VaultLayout.Quotes);
        var count = context.Storage.DirectoryExists(quotes)
            ? context.Storage.Files(quotes, "*").Count(file => Path.GetExtension(file) == ".md")
            : 0;
        context.Summary = context.Summary with { Quotes = count };
    }

    private static void ScanSchemas(Context context)
    {
        var count = 0;
        foreach (var (relative, id) in context.JsonFilesIn(VaultLayout.Schemas, VaultIds.IsSchemaId))
        {
            var bytes = context.ReadBytes(relative);
            if (bytes is null)
            {
                continue;
            }
            count++;
            if (bytes.Length > MaxSchemaBytes)
            {
                context.Add(FindingSeverity.Error, "schema-illisible", relative, "Schéma illisible : plus de 8 Mio.");
                continue;
            }
            try
            {
                var schema = Fields.ParseObject(bytes);
                var format = Fields.Integer(schema, "format", 1, 2);
                if (Fields.OptionalString(schema, "id") is { } schemaId && schemaId != id)
                {
                    context.Add(FindingSeverity.Error, "schema-illisible", relative, $"Schéma illisible : l'identifiant {schemaId} ne correspond pas au nom du fichier.");
                }
                _ = format;
            }
            catch (VaultFormatException exception)
            {
                context.Add(FindingSeverity.Error, "schema-illisible", relative, $"Schéma illisible : {exception.Message}.");
            }
        }
        context.Summary = context.Summary with { Schemas = count };
    }

    private static void ScanExtensionData(Context context)
    {
        var count = 0;
        foreach (var (relative, _) in context.JsonFilesIn(VaultLayout.ExtensionData, VaultIds.IsExtensionId))
        {
            var bytes = context.ReadBytes(relative);
            if (bytes is null)
            {
                continue;
            }
            count++;
            if (bytes.Length > MaxExtensionDataBytes)
            {
                context.Add(FindingSeverity.Error, "donnees-extension-illisibles", relative, "Données d'extension illisibles : plus de 1 Mio.");
                continue;
            }
            try
            {
                using var document = JsonDocument.Parse(bytes);
            }
            catch (JsonException exception)
            {
                context.Add(FindingSeverity.Error, "donnees-extension-illisibles", relative, $"Données d'extension illisibles : {exception.Message}");
            }
        }
        context.Summary = context.Summary with { ExtensionData = count };
    }

    private static string Relative(string root, string absolute) => Path.GetRelativePath(root, absolute).Replace('\\', '/');

    [GeneratedRegex(@"^\d{8}T\d{6}Z(-\d{1,3})?\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionName();

    private delegate T FileReader<out T>(ReadOnlySpan<byte> json);

    private sealed record SheetFile(VaultPath Path, string Fingerprint, string? Id);

    /// <summary>État de l'analyse : constats, comptes et lectures partagées.</summary>
    private sealed class Context(string root, VaultStorage storage)
    {
        private readonly ImmutableArray<Finding>.Builder _findings = ImmutableArray.CreateBuilder<Finding>();
        private int _filesRead;

        public string Root { get; } = root;

        /// <summary>Là où vivent les fichiers du coffre.</summary>
        public VaultStorage Storage { get; } = storage;

        public VaultSummary Summary { get; set; } = new();

        public void Add(FindingSeverity severity, string code, string? path, string message) =>
            _findings.Add(new Finding(severity, code, path, message));

        /// <summary>Le fichier existe, dans le coffre ou, pour le verrou, sur le disque (<paramref name="onDisk"/>).</summary>
        public bool Exists(string relative, bool onDisk = false) => (onDisk ? Storage.Raw : Storage).FileExists(VaultLayout.Absolute(Root, relative));

        /// <summary>Lit un fichier en partage complet : une autre application peut l'avoir ouvert.</summary>
        public byte[]? ReadBytes(string relative, bool onDisk = false)
        {
            try
            {
                var buffer = (onDisk ? Storage.Raw : Storage).ReadShared(VaultLayout.Absolute(Root, relative));
                _filesRead++;
                return buffer;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Add(FindingSeverity.Error, "lecture-impossible", relative, $"Lecture impossible : {exception.Message}");
                return null;
            }
        }

        public T? Read<T>(string relative, FileReader<T> reader, string code, string message, bool onDisk = false)
            where T : class
        {
            var bytes = ReadBytes(relative, onDisk);
            if (bytes is null)
            {
                return null;
            }
            try
            {
                return reader(bytes);
            }
            catch (VaultFormatException exception)
            {
                Add(FindingSeverity.Error, code, relative, $"{message} : {exception.Message}.");
                return null;
            }
        }

        public void CheckFormat(string relative, long format)
        {
            if (format > VaultLayout.SupportedFormat)
            {
                Add(FindingSeverity.Error, "format-plus-recent", relative, $"Format {format}, plus récent que cette version (format {VaultLayout.SupportedFormat}) : le coffre ne sera ouvert qu'en lecture.");
            }
        }

        /// <summary>
        /// Fichiers <c>&lt;nom&gt;.json</c> d'un dossier du coffre (extension exactement « json »),
        /// avec leur nom sans extension ; les autres fichiers y sont signalés.
        /// </summary>
        public IEnumerable<(string Relative, string Name)> JsonFilesIn(string relativeDirectory, Func<string, bool> isValidName)
        {
            var directory = VaultLayout.Absolute(Root, relativeDirectory);
            if (!Storage.DirectoryExists(directory))
            {
                yield break;
            }
            foreach (var file in Storage.Files(directory, "*").Order(StringComparer.Ordinal))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith(AtomicFile.TemporaryPrefix, StringComparison.Ordinal))
                {
                    continue;
                }
                var relative = $"{relativeDirectory}/{fileName}";
                if (!fileName.EndsWith(".json", StringComparison.Ordinal) || !isValidName(fileName[..^".json".Length]))
                {
                    Add(FindingSeverity.Information, "fichier-inattendu", relative, "Fichier inattendu : ignoré.");
                    continue;
                }
                yield return (relative, fileName[..^".json".Length]);
            }
        }

        public VaultInspection Result(bool isVault) =>
            new(Root, isVault, Summary with { FilesRead = _filesRead }, _findings.ToImmutable());
    }
}
