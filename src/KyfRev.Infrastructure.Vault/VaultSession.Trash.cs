using System.Text.Json;
using KyfRev.Application.Ports;
using KyfRev.Domain.Calendar;
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
    public int TrashFolder(string folder, Moment now) => TrashFolderInto(folder, now).Sheets.Count;

    /// <summary>
    /// Efface pour de bon une fiche déjà à la corbeille. C'est la seule voie qui efface une fiche, et elle n'existe qu'à
    /// la demande expresse de la personne : seules les sauvegardes déjà faites la contiennent encore.
    /// </summary>
    public void PurgeSheet(string id)
    {
        var left = new List<string>();
        EraseSheet(id, left);
        ThrowIfLeft(left);
    }

    /// <summary>Efface pour de bon un dossier : ses fiches passent par la corbeille, puis tout est effacé.</summary>
    /// <returns>Nombre de fiches effacées.</returns>
    public int PurgeFolder(string folder, Moment now)
    {
        var (sheets, storage) = TrashFolderInto(folder, now);
        var left = new List<string>();
        foreach (var id in sheets)
        {
            EraseSheet(id, left);
        }
        Erase(storage, left);
        ThrowIfLeft(left);
        return sheets.Count;
    }

    /// <summary>
    /// Efface une fiche de la corbeille, le suivi d'abord : c'est lui qui la fait exister à la corbeille. Ce qui reste sur le
    /// disque après un effacement (<see cref="LeftOnDiskException"/>) est noté dans <paramref name="left"/>, et les
    /// effacements suivants ont lieu : la fiche ne reste jamais à moitié effacée, introuvable mais encore sauvegardée.
    /// </summary>
    private void EraseSheet(string id, List<string> left)
    {
        ReadTrash(id);
        Erase(VaultPath.Create($"{VaultLayout.TrashTracking}/{id}.json"), left);
        Erase(VaultPath.Create($"{VaultLayout.TrashTracking}/{id}.annotations.json"), left);
        Erase(VaultPath.Create($"{VaultLayout.SheetsTrash}/{id}"), left);
    }

    private void Erase(VaultPath path)
    {
        var left = new List<string>();
        Erase(path, left);
        ThrowIfLeft(left);
    }

    /// <summary>Efface pour de bon ; si une part reste sur le disque, l'effacement a eu lieu : elle est notée dans <paramref name="left"/>.</summary>
    private void Erase(VaultPath path, List<string> left)
    {
        var absolute = Confine(path);
        Io(absolute, () =>
        {
            try
            {
                _storage.Purge(absolute);
            }
            catch (LeftOnDiskException exception)
            {
                left.Add(exception.Message);
            }
            return true;
        });
    }

    /// <summary>Tout a été effacé, mais une part est restée sur le disque : le dire, d'un seul message.</summary>
    private static void ThrowIfLeft(List<string> left)
    {
        if (left.Count > 0)
        {
            throw VaultException.LeftOnDisk(left);
        }
    }

    private (List<string> Sheets, VaultPath Storage) TrashFolderInto(string folder, Moment now)
    {
        var path = FolderPath(folder);
        if (!_storage.DirectoryExists(Confine(path)))
        {
            throw VaultException.UnknownFolder(folder);
        }
        var ids = _sheets.Values.Where(entry => entry.Sheet.Path.IsWithin(path)).Select(entry => entry.Sheet.Id).ToList();
        var moves = new List<PlannedMove>();
        foreach (var id in ids)
        {
            moves.AddRange(PrepareTrash(id, now));
        }
        var storage = VaultPath.Create($"{TrashFolders}/{_ids.Next()}");
        moves.Add(new PlannedMove(path, storage.Join(path.Name)));
        try
        {
            MoveUnderJournal($"mise à la corbeille du dossier {path}", moves);
        }
        finally
        {
            // Ce qui a été rangé quitte les listes, même si une étape a échoué.
            Analyze(now);
        }
        return (ids, storage);
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
        if (_storage.Exists(Confine(stored)))
        {
            moves.Add(new PlannedMove(stored, destination));
        }
        var resources = storage.Join(FileNames.ResourcesFolder(origin).Name);
        if (_storage.Exists(Confine(resources)))
        {
            moves.Add(new PlannedMove(resources, FileNames.ResourcesFolder(destination)));
        }
        var annotations = VaultPath.Create($"{VaultLayout.TrashTracking}/{id}.annotations.json");
        if (_storage.Exists(Confine(annotations)))
        {
            moves.Add(new PlannedMove(annotations, VaultPath.Create($"{VaultLayout.Annotations}/{id}.json")));
        }
        var tracking = VaultPath.Create($"{VaultLayout.SheetTracking}/{id}.json");
        if (_storage.Exists(Confine(tracking)))
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
        if (!_storage.DirectoryExists(folder))
        {
            return [];
        }
        var sheets = new List<TrashedSheet>();
        foreach (var file in Io(folder, () => _storage.Entries(folder).Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToList()))
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
            if (_storage.Exists(Confine(from)))
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
        if (!_storage.Exists(tracking))
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

    private int CountAnnotations(string file)
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
