using System.Text.Json;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault;

/// <remarks>
/// Corbeille des fiches : « supprimer » une fiche la range sans rien effacer, et elle reste
/// restaurable.
/// <code>
/// fiches/.supprimees/&lt;id&gt;/&lt;Titre&gt;.md            le texte (dossier caché : hors des listes)
/// fiches/.supprimees/&lt;id&gt;/&lt;Titre&gt;.ressources/   ses images
/// fiches/.supprimees/&lt;id&gt;/corbeille.json        chemin d'origine et date
/// .suivi/corbeille/&lt;id&gt;.json                    son suivi (calendrier, historique)
/// .suivi/corbeille/&lt;id&gt;.annotations.json        ses annotations et définitions
/// fiches/.supprimees/dossiers/&lt;ulid&gt;/&lt;Nom&gt;/     ce qui restait d'un dossier
/// </code>
/// </remarks>
public sealed partial class VaultSession
{
    private const string TrashFolders = "fiches/.supprimees/dossiers";

    /// <summary>« Supprimer » une fiche : elle va à la corbeille avec ses images, son suivi et ses annotations.</summary>
    public void TrashSheet(string id, Moment now)
    {
        var path = Sheet(id).Sheet.Path;
        var moves = PrepareTrash(id, now);
        MoveUnderJournal($"mise à la corbeille de {path}", moves);
        Analyze(now);
    }

    /// <summary>
    /// « Supprimer » un dossier (relatif à <c>fiches/</c>) : chacune de ses fiches, sous-dossiers
    /// et fiches archivées compris, va à la corbeille et se restaure à sa place. Le reste du
    /// dossier va dans <c>fiches/.supprimees/dossiers/</c>. Rien n'est effacé.
    /// </summary>
    /// <returns>Nombre de fiches mises à la corbeille.</returns>
    public int TrashFolder(string folder, Moment now)
    {
        var path = FolderPath(folder);
        if (!Directory.Exists(Confine(path)))
        {
            throw VaultException.UnknownFolder(folder);
        }
        var ids = _sheets.Values.Where(entry => entry.Sheet.Path.IsWithin(path)).Select(entry => entry.Sheet.Id).ToList();
        var moves = new List<PlannedMove>();
        foreach (var id in ids)
        {
            moves.AddRange(PrepareTrash(id, now));
        }
        moves.Add(new PlannedMove(path, VaultPath.Create($"{TrashFolders}/{_ids.Next()}").Join(path.Name)));
        try
        {
            MoveUnderJournal($"mise à la corbeille du dossier {path}", moves);
        }
        finally
        {
            // Ce qui a été rangé quitte les listes, même si une étape a échoué.
            Analyze(now);
        }
        return ids.Count;
    }

    /// <summary>
    /// Remet une fiche de la corbeille à sa place (ou, si elle est prise, à côté : « Titre (2).md »),
    /// avec ses images, son suivi et ses annotations.
    /// </summary>
    public void RestoreSheet(string id, Moment now)
    {
        var (info, sheet) = ReadTrash(id);
        var storage = VaultPath.Create($"{VaultLayout.SheetsTrash}/{id}");
        var origin = info.OriginalPath;
        var folder = origin.Parent is { } parent && parent.IsWithin(SheetsFolder) ? parent : SheetsFolder;
        // La place d'origine a pu être reprise : seule la copie rangée compte comme libre.
        var stored = storage.Join(origin.Name);
        var destination = FreePath(folder, WithoutMarkdownExtension(origin.Name), stored);

        var moves = new List<PlannedMove>();
        if (RealPaths.Exists(Confine(stored)))
        {
            moves.Add(new PlannedMove(stored, destination));
        }
        var resources = storage.Join(FileNames.ResourcesFolder(origin).Name);
        if (RealPaths.Exists(Confine(resources)))
        {
            moves.Add(new PlannedMove(resources, FileNames.ResourcesFolder(destination)));
        }
        var annotations = VaultPath.Create($"{VaultLayout.TrashTracking}/{id}.annotations.json");
        if (RealPaths.Exists(Confine(annotations)))
        {
            moves.Add(new PlannedMove(annotations, VaultPath.Create($"{VaultLayout.Annotations}/{id}.json")));
        }
        var tracking = VaultPath.Create($"{VaultLayout.SheetTracking}/{id}.json");
        if (RealPaths.Exists(Confine(tracking)))
        {
            throw VaultException.AlreadyExists(Confine(tracking));
        }
        moves.Add(new PlannedMove(VaultPath.Create($"{VaultLayout.TrashTracking}/{id}.json"), tracking));
        MoveUnderJournal($"restauration de {origin}", moves);

        // Le suivi connaît son nouveau chemin (si la place d'origine était prise).
        if (sheet.Path != destination)
        {
            ReplaceFile(Confine(tracking), (sheet with { Path = destination }).Write());
        }
        Analyze(now);
    }

    /// <summary>Fiches à la corbeille, les plus récentes d'abord.</summary>
    public IReadOnlyList<TrashedSheet> TrashedSheets()
    {
        var folder = VaultLayout.Absolute(Root, VaultLayout.TrashTracking);
        if (!Directory.Exists(folder))
        {
            return [];
        }
        var sheets = new List<TrashedSheet>();
        foreach (var file in Io(folder, () => Directory.EnumerateFileSystemEntries(folder).Order(StringComparer.Ordinal).ToList()))
        {
            var name = Path.GetFileName(file);
            var id = name.EndsWith(".json", StringComparison.Ordinal) ? name[..^5] : string.Empty;
            if (!VaultIds.IsSheetId(id))
            {
                continue;
            }
            TrashInfoFile info;
            TrackedSheetFile sheet;
            try
            {
                (info, sheet) = ReadTrash(id);
            }
            catch (VaultException)
            {
                // Une fiche à la corbeille illisible est signalée par l'analyse, pas listée ici.
                continue;
            }
            sheets.Add(new TrashedSheet(id, sheet.Title, DisplayFolder(info.OriginalPath), info.TrashedAt, CountAnnotations(Path.Combine(folder, $"{id}.annotations.json"))));
        }
        return [.. sheets.OrderByDescending(sheet => sheet.TrashedAt)];
    }

    /// <summary>
    /// Écrit la fiche d'information de la corbeille de la fiche <paramref name="id"/>, et rend les
    /// déplacements qui la rangent (texte, images, suivi, annotations).
    /// </summary>
    private List<PlannedMove> PrepareTrash(string id, Moment now)
    {
        var sheet = Sheet(id).Sheet;
        var storage = VaultPath.Create($"{VaultLayout.SheetsTrash}/{id}");
        var info = new TrashInfoFile(1, id, sheet.Title, sheet.Path, now.Instant, UnknownProperties.None);
        var infoFile = Confine(storage.Join("corbeille.json"));
        CreateDirectory(Path.GetDirectoryName(infoFile)!);
        ReplaceFile(infoFile, info.Write());

        var moves = new List<PlannedMove>();
        void Plan(VaultPath from, VaultPath to)
        {
            if (RealPaths.Exists(Confine(from)))
            {
                moves.Add(new PlannedMove(from, to));
            }
        }
        Plan(sheet.Path, storage.Join(sheet.Path.Name));
        var resources = FileNames.ResourcesFolder(sheet.Path);
        Plan(resources, storage.Join(resources.Name));
        Plan(VaultPath.Create($"{VaultLayout.SheetTracking}/{id}.json"), VaultPath.Create($"{VaultLayout.TrashTracking}/{id}.json"));
        Plan(VaultPath.Create($"{VaultLayout.Annotations}/{id}.json"), VaultPath.Create($"{VaultLayout.TrashTracking}/{id}.annotations.json"));
        return moves;
    }

    private (TrashInfoFile Info, TrackedSheetFile Sheet) ReadTrash(string id)
    {
        if (!VaultIds.IsSheetId(id))
        {
            throw VaultException.UnknownSheet(id);
        }
        var tracking = Confine(VaultPath.Create($"{VaultLayout.TrashTracking}/{id}.json"));
        var infoFile = Confine(VaultPath.Create($"{VaultLayout.SheetsTrash}/{id}/corbeille.json"));
        if (!RealPaths.Exists(tracking))
        {
            throw VaultException.UnknownSheet(id);
        }
        TrackedSheetFile sheet;
        try
        {
            sheet = TrackedSheetFile.Read(ReadAllBytes(tracking));
        }
        catch (VaultFormatException exception)
        {
            throw VaultException.UnreadableTracking(tracking, exception.Message);
        }
        if (TryReadAllBytes(infoFile) is not { } bytes)
        {
            // Sans fiche d'information : l'emplacement connu du suivi fait foi.
            return (new TrashInfoFile(1, id, sheet.Title, sheet.Path, sheet.ModifiedAt, UnknownProperties.None), sheet);
        }
        try
        {
            return (TrashInfoFile.Read(bytes), sheet);
        }
        catch (VaultFormatException exception)
        {
            throw VaultException.UnreadableTracking(infoFile, exception.Message);
        }
    }

    /// <summary>Déplace des fichiers sous la protection du journal d'opération.</summary>
    private void MoveUnderJournal(string description, List<PlannedMove> moves)
    {
        var operation = new OperationJournalFile(1, OperationState.InProgress, description, [.. moves], [], UnknownProperties.None);
        var journal = Confine(OperationFile);
        ReplaceFile(journal, operation.Write());
        var toCheck = Apply(operation);
        ReplaceFile(journal, (operation with { State = OperationState.Done }).Write());
        if (toCheck.Count > 0)
        {
            throw VaultException.AlreadyExists(toCheck[0].ToAbsolute(Root));
        }
    }

    private static int CountAnnotations(string file)
    {
        if (TryReadAllBytes(file) is not { } bytes)
        {
            return 0;
        }
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("annotations", out var annotations)
                && annotations.ValueKind == JsonValueKind.Array
                    ? annotations.GetArrayLength()
                    : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}
