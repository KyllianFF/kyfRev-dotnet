using System.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Links;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;

namespace KyfRev.Infrastructure.Vault;

public sealed partial class VaultSession
{
    /// <summary>Taille maximale d'une ressource importée : 50 Mo.</summary>
    public const int MaxResourceBytes = 50 * 1024 * 1024;

    private static readonly VaultPath OperationFile = VaultPath.Parse(VaultLayout.OperationFile);

    /// <summary>
    /// Change le titre d'une fiche : l'en-tête est mis à jour et le fichier renommé en
    /// conséquence, avec son dossier de ressources. Les liens qui la visent sont réécrits.
    /// </summary>
    public void ChangeTitle(string id, string title, Moment now)
    {
        ArgumentNullException.ThrowIfNull(title);
        title = title.Trim();
        if (title.Length == 0)
        {
            throw VaultException.EmptyTitle();
        }
        var current = AvailableSheet(id).Sheet.Path;
        var folder = current.Parent ?? throw VaultException.OutsideVault(current.ToAbsolute(Root));
        var target = FreePath(folder, FileNames.ForTitle(title), current);
        Execute(id, Plan(id, target, title, "changement de titre"), now);
    }

    /// <summary>
    /// Déplace une fiche dans <paramref name="folder"/> (relatif à <c>fiches/</c>,
    /// <see langword="null"/> ou vide pour la racine), avec son dossier de ressources ; les liens
    /// sont réécrits.
    /// </summary>
    public void MoveSheet(string id, string? folder, Moment now)
    {
        var current = AvailableSheet(id).Sheet.Path;
        var trimmed = folder?.Trim();
        var directory = VaultPath.Create(string.IsNullOrEmpty(trimmed) ? VaultLayout.Sheets : $"{VaultLayout.Sheets}/{trimmed}");
        var target = FreePath(directory, WithoutMarkdownExtension(current.Name), current);
        Execute(id, Plan(id, target, title: null, "déplacement"), now);
    }

    /// <summary>
    /// Contenu d'une ressource du dossier des fiches (une image), pour l'affichage. Tout chemin hors de
    /// ce dossier, ou qui sort du coffre, est refusé ; les fiches aussi : ce ne sont pas des ressources.
    /// </summary>
    public byte[] ReadResource(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var target = VaultPath.Create(path);
        if (!target.IsWithin(VaultPath.Create(VaultLayout.Sheets)))
        {
            throw VaultException.InvalidPath(target.Value, "seules les ressources des fiches sont accessibles");
        }
        if (target.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            throw VaultException.InvalidPath(target.Value, "une fiche n'est pas une ressource");
        }
        var absolute = Confine(target);
        return Io(absolute, () => new FileInfo(absolute).Length > MaxResourceBytes
            ? throw VaultException.InvalidPath(target.Value, "ressource trop volumineuse")
            : File.ReadAllBytes(absolute));
    }

    /// <summary>
    /// Copie une ressource (image, PDF…) dans le dossier de ressources de la fiche, sous un nom
    /// sûr et sans jamais écraser un fichier existant.
    /// </summary>
    /// <returns>Destination à écrire dans le lien Markdown, relative à la fiche.</returns>
    public string AddResource(string id, string originalName, ReadOnlySpan<byte> content)
    {
        ArgumentNullException.ThrowIfNull(originalName);
        if (content.Length > MaxResourceBytes)
        {
            throw VaultException.ResourceRefused(originalName, "fichier de plus de 50 Mo");
        }
        var folder = FileNames.ResourcesFolder(AvailableSheet(id).Sheet.Path);
        var folderAbsolute = Confine(folder);
        CreateDirectory(folderAbsolute);

        var name = FileNames.ForResource(originalName);
        var dot = name.LastIndexOf('.');
        var (stem, extension) = dot < 0 ? (name, string.Empty) : (name[..dot], name[(dot + 1)..]);
        for (var number = 1; number < 1000; number++)
        {
            var candidate = number == 1 ? name : $"{stem}-{number}.{extension}";
            if (TryCreateFile(Confine(folder.Join(candidate)), content))
            {
                return MarkdownLinks.Encode($"{folder.Name}/{candidate}", angleBrackets: false);
            }
        }
        throw VaultException.AlreadyExists(folderAbsolute);
    }

    /// <summary>Termine une opération interrompue par un arrêt brutal ; rend les fichiers à vérifier.</summary>
    private List<VaultPath> ResumeInterruptedOperation()
    {
        var journal = Confine(OperationFile);
        if (!RealPaths.Exists(journal))
        {
            return [];
        }
        OperationJournalFile operation;
        try
        {
            operation = OperationJournalFile.Read(ReadAllBytes(journal));
        }
        catch (VaultFormatException)
        {
            return [OperationFile];
        }
        if (operation.State == OperationState.Done)
        {
            return [];
        }
        var toCheck = Apply(operation);
        ReplaceFile(journal, (operation with { State = OperationState.Done }).Write());
        return toCheck;
    }

    /// <summary>
    /// Premier chemin libre « radical.md », « radical (2).md »… dans <paramref name="folder"/>.
    /// Le chemin actuel de la fiche (<paramref name="except"/>) compte comme libre, ce qui permet
    /// un renommage qui ne change que la casse.
    /// </summary>
    private VaultPath FreePath(VaultPath folder, string stem, VaultPath except)
    {
        for (var number = 1; number < 1000; number++)
        {
            var candidate = folder.Join(number == 1 ? $"{stem}.md" : $"{stem} ({number}).md");
            if (candidate == except)
            {
                return candidate;
            }
            var file = Confine(candidate);
            var resources = Confine(FileNames.ResourcesFolder(candidate));
            if (RealPaths.SameEntry(file, except.ToAbsolute(Root)) || (!RealPaths.Exists(file) && !RealPaths.Exists(resources)))
            {
                return candidate;
            }
        }
        throw VaultException.AlreadyExists(folder.ToAbsolute(Root));
    }

    /// <summary>Prépare le renommage ou le déplacement de la fiche <paramref name="id"/> vers <paramref name="target"/>.</summary>
    private OperationJournalFile Plan(string id, VaultPath target, string? title, string description)
    {
        var current = AvailableSheet(id).Sheet.Path;
        if (!target.IsWithin(SheetsFolder) || !target.Name.EndsWith(".md", StringComparison.Ordinal))
        {
            throw VaultException.InvalidPath(target.Value, "une fiche doit être un fichier .md du dossier « fiches »");
        }
        var resourcesBefore = FileNames.ResourcesFolder(current);
        var resourcesAfter = FileNames.ResourcesFolder(target);
        var folderBefore = current.Parent?.Value ?? string.Empty;
        var folderAfter = target.Parent?.Value ?? string.Empty;

        // Où se retrouve chaque cible après l'opération.
        string? Move(string linked)
        {
            if (linked == current.Value)
            {
                return target.Value;
            }
            if (linked == resourcesBefore.Value)
            {
                return resourcesAfter.Value;
            }
            return linked.StartsWith(resourcesBefore.Value + "/", StringComparison.Ordinal)
                ? resourcesAfter.Value + linked[resourcesBefore.Value.Length..]
                : null;
        }

        var writes = new List<PlannedWrite>();

        // La fiche elle-même : titre éventuel, puis ses liens depuis son nouveau dossier.
        var bytes = ReadAllBytes(Confine(current));
        var text = Decode(bytes, current);
        var updated = title is null ? text : FrontMatter.ReplaceField(text, "titre", FrontMatter.TextValue(title));
        updated = MarkdownLinks.Rewrite(updated, folderBefore, folderAfter, Move) ?? updated;
        if (updated != text)
        {
            writes.Add(new PlannedWrite(target, Fingerprint.Of(bytes), updated));
        }

        // Les autres fichiers Markdown qui la visent (fiches suivies ou non).
        var others = _sheets.Values
            .Where(entry => entry.Sheet.Id != id && entry.Presence == FilePresence.Present)
            .Select(entry => entry.Sheet.Path)
            .Concat(Health.UntrackedList)
            .ToList();
        foreach (var path in others)
        {
            if (TryReadText(path) is not var (otherBytes, otherText))
            {
                continue;
            }
            var folder = path.Parent?.Value ?? string.Empty;
            if (MarkdownLinks.Rewrite(otherText, folder, folder, Move) is { } rewritten)
            {
                writes.Add(new PlannedWrite(path, Fingerprint.Of(otherBytes), rewritten));
            }
        }

        var moves = new List<PlannedMove>();
        if (target != current)
        {
            moves.Add(new PlannedMove(current, target));
            if (Directory.Exists(Confine(resourcesBefore)))
            {
                moves.Add(new PlannedMove(resourcesBefore, resourcesAfter));
            }
        }
        return new OperationJournalFile(1, OperationState.InProgress, $"{description} de {current} vers {target}", [.. moves], [.. writes], UnknownProperties.None);
    }

    /// <summary>
    /// Écrit le journal, applique l'opération, la marque terminée, puis met à jour les suivis
    /// concernés : fiches déplacées, fiches réécrites, et la fiche <paramref name="modified"/>
    /// dont la date de modification avance.
    /// </summary>
    private void Execute(string? modified, OperationJournalFile operation, Moment now)
    {
        var journal = Confine(OperationFile);
        ReplaceFile(journal, operation.Write());
        var toCheck = Apply(operation);
        ReplaceFile(journal, (operation with { State = OperationState.Done }).Write());

        // Chemins des fiches déplacées, puis empreintes (et titre) des fichiers réécrits.
        var rewritten = operation.Writes.Select(write => write.Path).ToList();
        var concerned = new List<string>();
        foreach (var entry in _sheets.Values.ToList())
        {
            var sheet = entry.Sheet;
            var moved = AfterMoves(sheet.Path, operation.Moves);
            if (moved is not null)
            {
                sheet = sheet with { Path = moved };
                _sheets[sheet.Id] = entry with { Sheet = sheet };
            }
            if (moved is not null || modified == sheet.Id || rewritten.Contains(sheet.Path))
            {
                concerned.Add(sheet.Id);
            }
        }
        foreach (var id in concerned)
        {
            var sheet = _sheets[id].Sheet;
            // Un fichier manquant garde son empreinte, mais son suivi enregistre le nouveau chemin.
            var absolute = Confine(sheet.Path);
            if (TryReadAllBytes(absolute) is { } bytes)
            {
                if (TryDecode(bytes, out var text)
                    && FrontMatter.Read(text) is { IsSuccess: true, Header.Title: { } title }
                    && title.Trim().Length > 0)
                {
                    sheet = sheet with { Title = title };
                }
                sheet = sheet with { Fingerprint = Fingerprint.Of(bytes) };
            }
            if (modified == id)
            {
                sheet = sheet with { ModifiedAt = now.Instant };
            }
            WriteTracking(sheet, create: false);
            _sheets[id] = _sheets[id] with { Sheet = sheet };
        }
        var untracked = Health.UntrackedList.Select(path => AfterMoves(path, operation.Moves) ?? path).ToList();
        Health.UntrackedList.Clear();
        Health.UntrackedList.AddRange(untracked);
        Health.LinksToCheckList.AddRange(toCheck);
    }

    /// <summary>
    /// Applique une opération étape par étape ; chaque étape déjà faite est reconnue et sautée,
    /// ce qui permet de rejouer l'opération sans risque. Rend ce qui n'a pas pu être fait.
    /// </summary>
    private List<VaultPath> Apply(OperationJournalFile operation)
    {
        var toCheck = new List<VaultPath>();
        foreach (var move in operation.Moves)
        {
            var from = Confine(move.From);
            var to = Confine(move.To);
            var free = !RealPaths.Exists(to) || RealPaths.SameEntry(from, to);
            if (RealPaths.Exists(from) && free)
            {
                CreateDirectory(Path.GetDirectoryName(to)!);
                Io(from, () =>
                {
                    if (Directory.Exists(from))
                    {
                        Directory.Move(from, to);
                    }
                    else
                    {
                        File.Move(from, to);
                    }
                    return true;
                });
            }
            else if (RealPaths.Exists(from) || !RealPaths.Exists(to))
            {
                // Destination prise entre-temps, ou les deux introuvables.
                toCheck.Add(move.From);
            }
        }
        foreach (var write in operation.Writes)
        {
            var path = Confine(write.Path);
            if (TryReadAllBytes(path) is not { } current)
            {
                toCheck.Add(write.Path);
                continue;
            }
            var content = Encoding.UTF8.GetBytes(write.Content);
            var fingerprint = Fingerprint.Of(current);
            if (fingerprint == write.FingerprintBefore)
            {
                ReplaceFile(path, content);
            }
            else if (fingerprint != Fingerprint.Of(content))
            {
                // Modifié entre-temps : on ne l'écrase pas.
                toCheck.Add(write.Path);
            }
        }
        return toCheck;
    }

    /// <summary>
    /// Où se trouve <paramref name="path"/> après les déplacements prévus : un déplacement porte
    /// sur ce fichier lui-même ou sur l'un de ses dossiers. <see langword="null"/> s'il ne bouge pas.
    /// </summary>
    private static VaultPath? AfterMoves(VaultPath path, IEnumerable<PlannedMove> moves)
    {
        foreach (var move in moves)
        {
            if (!path.Value.StartsWith(move.From.Value, StringComparison.Ordinal))
            {
                continue;
            }
            var rest = path.Value[move.From.Value.Length..];
            if (rest.Length == 0)
            {
                return move.To;
            }
            if (rest.StartsWith('/') && VaultPath.TryParse(move.To.Value + rest, out var moved, out _))
            {
                return moved;
            }
        }
        return null;
    }

    /// <summary>Octets et texte d'un fichier Markdown du coffre ; rien s'il est illisible ou pas en UTF-8.</summary>
    private (byte[] Bytes, string Text)? TryReadText(VaultPath path) =>
        TryReadAllBytes(Confine(path)) is { } bytes && TryDecode(bytes, out var text) ? (bytes, text) : null;

    private static byte[]? TryReadAllBytes(string absolute)
    {
        try
        {
            return File.ReadAllBytes(absolute);
        }
        catch (Exception exception) when (IsIo(exception))
        {
            return null;
        }
    }

    private static string WithoutMarkdownExtension(string name) =>
        name.EndsWith(".md", StringComparison.Ordinal) ? name[..^3] : name;
}
