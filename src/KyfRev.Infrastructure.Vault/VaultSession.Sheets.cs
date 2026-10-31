using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;
using KyfRev.Infrastructure.Vault.Text;

namespace KyfRev.Infrastructure.Vault;

public sealed partial class VaultSession
{
    /// <summary>
    /// Crée une fiche dans <paramref name="folder"/> (relatif à <c>fiches/</c>, <see langword="null"/>
    /// pour la racine). La création vaut révision J0. Un fichier existant n'est jamais écrasé :
    /// « Titre (2).md » est choisi à la place.
    /// </summary>
    /// <returns>Identifiant de la nouvelle fiche.</returns>
    public string CreateSheet(string? folder, string title, string body, Moment now)
    {
        ArgumentNullException.ThrowIfNull(title);
        title = title.Trim() is { Length: > 0 } trimmed ? trimmed : FileNames.Untitled;
        var directory = VaultPath.Create(folder is null ? VaultLayout.Sheets : $"{VaultLayout.Sheets}/{folder}");
        CreateDirectory(Confine(directory));

        var id = $"f_{_ids.Next()}";
        var text = FrontMatter.Compose(id, title, now.Day) + body;
        var bytes = Encoding.UTF8.GetBytes(text);
        var tracking = Started(Tracking.Start(now, Settings.Sequence, StartOrigin.Creation));

        var stem = FileNames.ForTitle(title);
        VaultPath path;
        for (var number = 1; ; number++)
        {
            path = directory.Join(number == 1 ? $"{stem}.md" : $"{stem} ({number}).md");
            if (TryCreateFile(Confine(path), bytes))
            {
                break;
            }
            if (number >= 1000)
            {
                throw VaultException.AlreadyExists(path.ToAbsolute(Root));
            }
        }

        var sheet = new TrackedSheetFile(1, id, path, title, [], Fingerprint.Of(bytes), now.Instant, tracking, UnknownProperties.None);
        WriteTracking(sheet, create: true);
        _sheets[id] = new SheetEntry(sheet, FilePresence.Present);
        return id;
    }

    /// <summary>
    /// Ajoute au suivi un fichier <c>.md</c> existant sans identifiant (« Suivre cette fiche ») :
    /// un <c>id</c> est inséré dans son en-tête, sans rien changer d'autre, et ce jour vaut J0.
    /// </summary>
    /// <returns>Identifiant de la fiche.</returns>
    public string TrackFile(VaultPath path, Moment now)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!path.IsWithin(SheetsFolder))
        {
            throw VaultException.InvalidPath(path.Value, "une fiche doit se trouver dans le dossier « fiches »");
        }
        var absolute = Confine(path);
        var bytes = ReadAllBytes(absolute);
        var text = Decode(bytes, path);
        var header = ReadHeader(text, path);
        if (header.Id is not null)
        {
            throw VaultException.AlreadyTracked(path.Value);
        }

        var id = $"f_{_ids.Next()}";
        var updated = Encoding.UTF8.GetBytes(FrontMatter.AddId(text, id));
        var tracking = Started(Tracking.Start(now, Settings.Sequence, StartOrigin.Adoption));
        // Relecture juste avant d'écrire : on ne remplace que ce qu'on a lu.
        var reread = ReadAllBytes(absolute);
        if (!reread.AsSpan().SequenceEqual(bytes))
        {
            throw VaultException.Conflict(Fingerprint.Of(reread));
        }
        ReplaceFile(absolute, updated);

        var title = header.Title ?? (path.Name.EndsWith(".md", StringComparison.Ordinal) ? path.Name[..^3] : path.Name);
        var sheet = new TrackedSheetFile(1, id, path, title, header.Tags, Fingerprint.Of(updated), now.Instant, tracking, UnknownProperties.None);
        WriteTracking(sheet, create: true);
        Health.UntrackedList.RemoveAll(untracked => untracked == path);
        _sheets[id] = new SheetEntry(sheet, FilePresence.Present);
        return id;
    }

    /// <summary>Texte d'une fiche présente, et la preuve de la version lue.</summary>
    public SheetContent ReadSheet(string id)
    {
        var path = AvailableSheet(id).Sheet.Path;
        var bytes = ReadAllBytes(Confine(path));
        return new SheetContent(Decode(bytes, path), Fingerprint.Of(bytes));
    }

    /// <summary>
    /// Enregistre le nouveau texte complet d'une fiche (en-tête compris ; son identifiant ne peut
    /// pas changer).
    /// </summary>
    /// <param name="id">Identifiant de la fiche.</param>
    /// <param name="text">Nouveau texte.</param>
    /// <param name="expectedFingerprint">Empreinte du contenu lu (<see cref="ReadSheet"/>).</param>
    /// <param name="session">Session d'édition : l'ancien contenu est copié dans les versions une fois par session.</param>
    /// <param name="now">Instant présent.</param>
    /// <returns>Empreinte du nouveau contenu.</returns>
    /// <exception cref="VaultException">
    /// Conflit si le fichier a changé depuis sa lecture : rien n'est écrit. L'appelant relit,
    /// compare, puis réessaie avec l'empreinte actuelle ; l'autre version est alors d'abord copiée
    /// dans les versions.
    /// </exception>
    public string SaveSheet(string id, string text, string expectedFingerprint, ulong session, Moment now) =>
        WriteText(id, text, expectedFingerprint, session, now);

    /// <summary>
    /// Enregistre le corps d'une fiche en gardant son en-tête octet pour octet. Refusé, sans rien
    /// écrire, si la fiche a changé sur le disque depuis sa lecture : le corps actuel est rendu
    /// pour comparer.
    /// </summary>
    public SaveResult SaveBody(string id, string body, string expectedFingerprint, ulong session, Moment now) =>
        ReplaceBody(id, body, expectedFingerprint, session, now);

    /// <summary>
    /// Range un corps dans les versions de la fiche sans toucher au fichier : le texte en cours
    /// d'édition, avant de recharger celui du disque.
    /// </summary>
    public void KeepAsVersion(string id, string body, Moment now)
    {
        var read = ReadSheet(id);
        SaveVersion(id, Encoding.UTF8.GetBytes(HeaderPart(read.Text) + body), now);
    }

    /// <summary>Versions d'une fiche, de la plus récente à la plus ancienne.</summary>
    public IReadOnlyList<SheetVersion> Versions(string id)
    {
        var absolute = Confine(VersionsFolder(id));
        if (!Directory.Exists(absolute))
        {
            return [];
        }
        var versions = new List<(int Number, SheetVersion Version)>();
        foreach (var file in Io(absolute, () => new DirectoryInfo(absolute).EnumerateFileSystemInfos().ToList()))
        {
            if (ParseVersionName(file.Name) is { } parsed)
            {
                var size = file is FileInfo info ? info.Length : 0;
                versions.Add((parsed.Number, new SheetVersion(file.Name, parsed.SavedAt, size)));
            }
        }
        return [.. versions
            .OrderByDescending(v => v.Version.SavedAt)
            .ThenByDescending(v => v.Number)
            .Select(v => v.Version)];
    }

    /// <summary>Corps d'une version (son en-tête n'est pas montré).</summary>
    public string ReadVersion(string id, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (ParseVersionName(name) is null)
        {
            throw VaultException.UnknownVersion(name);
        }
        var path = VersionsFolder(id).Join(name);
        var absolute = Confine(path);
        if (!File.Exists(absolute))
        {
            throw VaultException.UnknownVersion(name);
        }
        var text = Decode(ReadAllBytes(absolute), path);
        return text[FrontMatter.Split(text).BodyStart..];
    }

    /// <summary>
    /// Remet le corps d'une version. Le contenu actuel est d'abord copié dans les versions : une
    /// restauration se défait en restaurant celui-ci.
    /// </summary>
    public SaveResult RestoreVersion(string id, string name, string expectedFingerprint, Moment now) =>
        ReplaceBody(id, ReadVersion(id, name), expectedFingerprint, session: null, now);

    /// <summary>Remplace les étiquettes de la fiche, dans son en-tête.</summary>
    /// <returns>Les étiquettes normalisées réellement enregistrées.</returns>
    public ImmutableArray<string> SetTags(string id, IEnumerable<string> tags, Moment now)
    {
        var sheet = AvailableSheet(id).Sheet;
        var absolute = Confine(sheet.Path);
        var bytes = ReadAllBytes(absolute);
        var diskFingerprint = Fingerprint.Of(bytes);
        // Une modification extérieure non encore analysée ne doit pas être écrasée sans le savoir.
        if (diskFingerprint != sheet.Fingerprint)
        {
            throw VaultException.Conflict(diskFingerprint);
        }
        var text = Decode(bytes, sheet.Path);
        var header = ReadHeader(text, sheet.Path);
        var normalized = FrontMatter.NormalizeTags(tags);
        var updated = Encoding.UTF8.GetBytes(FrontMatter.ReplaceField(text, header.TagsKey, FrontMatter.ListValue(normalized)));
        ReplaceFile(absolute, updated);

        sheet = sheet with { Tags = normalized, Fingerprint = Fingerprint.Of(updated), ModifiedAt = now.Instant };
        WriteTracking(sheet, create: false);
        _sheets[id] = _sheets[id] with { Sheet = sheet };
        return normalized;
    }

    /// <summary>Toutes les étiquettes utilisées, sans doublon (casse ignorée), triées.</summary>
    public IReadOnlyList<string> AllTags()
    {
        var sorted = _sheets.Values
            .SelectMany(entry => entry.Sheet.Tags)
            .OrderBy(RustText.ToLowercase, Utf8Order.Instance)
            .ToList();
        var result = new List<string>();
        foreach (var tag in sorted)
        {
            if (result.Count == 0 || !RustText.EqualsIgnoringCase(result[^1], tag))
            {
                result.Add(tag);
            }
        }
        return result;
    }

    /// <summary>
    /// Applique une opération de la méthode des J (reporter, archiver, redémarrer…) au suivi
    /// d'une fiche, puis l'enregistre. Si l'opération est refusée ou si l'écriture échoue, rien
    /// ne change.
    /// </summary>
    /// <returns>Le nouveau suivi.</returns>
    /// <exception cref="VaultException">Opération refusée (<see cref="VaultErrorKind.Tracking"/>) ou écriture impossible.</exception>
    public Tracking ApplyToTracking(string id, Func<Tracking, TrackingResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var result = operation(Sheet(id).Sheet.Tracking);
        var tracking = result.IsSuccess ? result.Tracking : throw VaultException.Refused(result.Error);
        StoreTracking(id, tracking);
        return tracking;
    }

    /// <summary>Valide une révision d'une fiche (voir <see cref="Tracking.ValidateReview"/>) et l'enregistre.</summary>
    /// <returns>L'issue de la révision.</returns>
    public ReviewOutcome ApplyReview(string id, Func<Tracking, ReviewResult> review)
    {
        ArgumentNullException.ThrowIfNull(review);
        var result = review(Sheet(id).Sheet.Tracking);
        if (!result.IsSuccess)
        {
            throw VaultException.Refused(result.Error);
        }
        StoreTracking(id, result.Tracking);
        return result.Outcome;
    }

    private void StoreTracking(string id, Tracking tracking)
    {
        var sheet = Sheet(id).Sheet with { Tracking = tracking };
        WriteTracking(sheet, create: false);
        _sheets[id] = _sheets[id] with { Sheet = sheet };
    }

    /// <summary>Cœur de l'enregistrement ; sans session, l'ancien contenu est toujours copié dans les versions.</summary>
    private string WriteText(string id, string text, string expectedFingerprint, ulong? session, Moment now)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sheet = AvailableSheet(id).Sheet;
        var absolute = Confine(sheet.Path);
        var current = ReadAllBytes(absolute);
        var diskFingerprint = Fingerprint.Of(current);
        if (diskFingerprint != expectedFingerprint)
        {
            throw VaultException.Conflict(diskFingerprint);
        }
        var header = ReadHeader(text, sheet.Path);
        if (header.Id != id)
        {
            throw VaultException.IdentifierChanged();
        }
        var bytes = Encoding.UTF8.GetBytes(text);
        var fingerprint = Fingerprint.Of(bytes);
        if (fingerprint == diskFingerprint)
        {
            return fingerprint;
        }

        var modifiedElsewhere = diskFingerprint != sheet.Fingerprint;
        var alreadySaved = session is { } key && _savedSessions.Contains((id, key));
        if (modifiedElsewhere || !alreadySaved)
        {
            SaveVersion(id, current, now);
        }
        if (session is { } newKey)
        {
            _savedSessions.Add((id, newKey));
        }
        ReplaceFile(absolute, bytes);

        sheet = sheet with { Fingerprint = fingerprint, ModifiedAt = now.Instant };
        if (header.Title is { } title && title.Trim().Length > 0)
        {
            sheet = sheet with { Title = title };
        }
        WriteTracking(sheet, create: false);
        _sheets[id] = _sheets[id] with { Sheet = sheet };
        return fingerprint;
    }

    private SaveResult ReplaceBody(string id, string body, string expectedFingerprint, ulong? session, Moment now)
    {
        ArgumentNullException.ThrowIfNull(body);
        var read = ReadSheet(id);
        if (read.Fingerprint != expectedFingerprint)
        {
            return Conflict(read);
        }
        try
        {
            return new SheetSaved(WriteText(id, HeaderPart(read.Text) + body, read.Fingerprint, session, now));
        }
        catch (VaultException exception) when (exception.Kind == VaultErrorKind.Conflict)
        {
            // Fiche modifiée entre la lecture et l'écriture.
            return Conflict(ReadSheet(id));
        }

        static SaveResult Conflict(SheetContent disk) =>
            new SaveConflict(disk.Fingerprint, disk.Text[FrontMatter.Split(disk.Text).BodyStart..]);
    }

    private VaultPath VersionsFolder(string id)
    {
        _ = Sheet(id);
        return VaultPath.Create($"{VaultLayout.Versions}/{id}");
    }

    private static Tracking Started(TrackingResult result) =>
        result.IsSuccess ? result.Tracking : throw VaultException.Refused(result.Error);

    /// <summary>L'en-tête YAML d'une fiche, gardé tel quel à l'enregistrement du corps.</summary>
    private static string HeaderPart(string text) => text[..FrontMatter.Split(text).BodyStart];

    private static string Decode(byte[] bytes, VaultPath path) =>
        TryDecode(bytes, out var text) ? text : throw VaultException.NotUtf8(path.Value);

    private static SheetHeader ReadHeader(string text, VaultPath path)
    {
        var reading = FrontMatter.Read(text);
        return reading.IsSuccess ? reading.Header : throw VaultException.InvalidHeader(path.Value, reading.Error);
    }

    /// <summary>Horodatage et numéro d'un nom de version (« 20261010T081530Z-2.md ») ; rien pour tout autre nom.</summary>
    private static (DateTimeOffset SavedAt, int Number)? ParseVersionName(string name)
    {
        if (!name.EndsWith(".md", StringComparison.Ordinal))
        {
            return null;
        }
        var stem = name[..^3];
        var dash = stem.IndexOf('-', StringComparison.Ordinal);
        var (stamp, number) = dash < 0 ? (stem, "1") : (stem[..dash], stem[(dash + 1)..]);
        static bool Digits(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);
        var shaped = stamp.Length == 16
            && Digits(stamp[..8]) && stamp[8] == 'T' && Digits(stamp[9..15]) && stamp[15] == 'Z';
        if (!shaped || number.Length > 3 || !Digits(number)
            || !DateTime.TryParseExact(stamp, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var savedAt))
        {
            return null;
        }
        return (new DateTimeOffset(savedAt, TimeSpan.Zero), int.Parse(number, CultureInfo.InvariantCulture));
    }
}
