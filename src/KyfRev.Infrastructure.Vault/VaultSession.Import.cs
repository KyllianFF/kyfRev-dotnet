using System.Text;
using KyfRev.Application.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Links;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault;

/// <remarks>
/// Import d'un dossier de notes Markdown (coffre Obsidian, notes Typora, VS Code…) : copié dans
/// <c>fiches/&lt;nom&gt;</c>, arborescence comprise ; rien n'est modifié à la source et rien n'est
/// écrasé. Les liens <c>[[Note]]</c> deviennent des liens Markdown relatifs, les pièces jointes
/// citées sont copiées dans le dossier de ressources de la fiche, les dossiers cachés sont ignorés.
/// </remarks>
public sealed partial class VaultSession
{
    /// <summary>Au-delà, l'import est refusé : ce n'est sans doute pas un dossier de notes.</summary>
    public const int MaxImportedNotes = 5000;

    private const int MaxImportDepth = 20;

    /// <summary>Importe le dossier <paramref name="source"/> dans <c>fiches/&lt;nom&gt;</c>.</summary>
    /// <param name="source">Dossier de notes, hors du coffre.</param>
    /// <param name="name">Nom du dossier à créer.</param>
    /// <param name="track">Les fiches commencent leur cycle aujourd'hui ; sinon elles attendent, non suivies.</param>
    /// <param name="now">Instant présent.</param>
    public ImportReport ImportFolder(string source, string name, bool track, Moment now)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(name);
        if (!Directory.Exists(source))
        {
            throw VaultException.UnknownFolder(source);
        }
        source = Path.TrimEndingDirectorySeparator(Io(source, () => Path.GetFullPath(source)));
        if (RealPaths.IsWithin(source, Root))
        {
            throw VaultException.InvalidPath(source, "ce dossier est déjà dans le coffre");
        }

        var ignored = new List<string>();
        var files = new List<string>();
        Walk(source, source, Root, 0, files, ignored);
        var sourceNotes = files.Where(IsNote).ToList();
        if (sourceNotes.Count == 0)
        {
            throw VaultException.InvalidPath(source, "aucune note Markdown (.md) dans ce dossier");
        }
        if (sourceNotes.Count > MaxImportedNotes)
        {
            throw VaultException.InvalidPath(source, "plus de 5 000 notes : importez plutôt ses sous-dossiers un à un");
        }

        var folder = FreeImportFolder(name);
        var report = new ImportCounts(RelativeToSheets(folder), ignored);

        // Chemins des notes dans le coffre, noms rendus valides sous Windows.
        var notes = new List<(string Source, VaultPath Target)>();
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var note in sourceNotes)
        {
            var relative = Path.GetRelativePath(source, note);
            if (TargetOf(folder, relative, taken) is { } target)
            {
                notes.Add((note, target));
            }
            else
            {
                ignored.Add($"{relative} : nom de fichier refusé");
            }
        }

        // Résolution à la manière d'Obsidian : par nom de note, ou par chemin partiel ; les pièces jointes par nom.
        var byName = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < notes.Count; i++)
        {
            var key = Key(Path.GetFileNameWithoutExtension(notes[i].Source));
            if (!byName.TryGetValue(key, out var list))
            {
                byName[key] = list = [];
            }
            list.Add(i);
        }
        var attachments = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in files.Where(f => !IsNote(f)))
        {
            var key = Key(Path.GetFileName(file));
            if (!attachments.TryGetValue(key, out var list))
            {
                attachments[key] = list = [];
            }
            list.Add(file);
        }

        var written = new List<VaultPath>();
        foreach (var (noteSource, target) in notes)
        {
            var relativeSource = Path.GetRelativePath(source, noteSource);
            if (VaultStorage.Disk.TryReadAllBytes(noteSource) is not { } bytes)
            {
                ignored.Add($"{relativeSource} : illisible");
                continue;
            }
            if (!TryDecode(bytes, out var text))
            {
                ignored.Add($"{relativeSource} : pas en UTF-8 (réenregistrez-le en UTF-8)");
                continue;
            }
            var noteFolder = target.Parent?.Value ?? string.Empty;
            var sourceFolder = Path.GetDirectoryName(noteSource) ?? source;
            var resources = new ImportedResources(this, target);
            string? ResolveNote(string linked) =>
                FindNote(linked, notes, byName, source) is { } index ? MarkdownLinks.RelativeFrom(noteFolder, notes[index].Target.Value) : null;
            string? ResolveAttachment(string linked) =>
                FindAttachment(linked, sourceFolder, source, attachments) is { } file ? resources.Copy(file) : null;
            var (converted, links) = ConvertObsidianLinks(text, ResolveNote, ResolveAttachment);
            converted = TakeAttachments(converted, sourceFolder, source, resources);
            report.Links += links;
            report.Resources += resources.Copied.Count;
            ignored.AddRange(resources.Ignored.Select(reason => $"{relativeSource} : {reason}"));

            var absolute = Confine(target);
            CreateDirectory(Path.GetDirectoryName(absolute)!);
            try
            {
                CreateFile(absolute, Encoding.UTF8.GetBytes(converted));
                report.Sheets++;
                written.Add(target);
            }
            catch (VaultException exception)
            {
                ignored.Add($"{relativeSource} : {exception.Message}");
            }
        }

        if (track)
        {
            foreach (var path in written)
            {
                try
                {
                    TrackFile(path, now);
                    report.Tracked++;
                }
                catch (VaultException exception)
                {
                    ignored.Add($"{path.Value} : copiée, mais pas mise en révision ({exception.Message})");
                }
            }
        }
        else
        {
            // Non suivies : elles apparaissent dans « Santé du coffre ».
            Analyze(now);
        }
        return report.Build();
    }

    /// <summary>
    /// Convertit les liens Obsidian hors du code : <paramref name="note"/> rend le chemin relatif d'une
    /// note, <paramref name="attachment"/> celui d'une pièce jointe copiée.
    /// </summary>
    /// <returns>Le texte et le nombre de liens convertis.</returns>
    internal static (string Text, int Converted) ConvertObsidianLinks(string text, Func<string, string?> note, Func<string, string?> attachment)
    {
        var result = new StringBuilder(text.Length);
        var converted = 0;
        (char Mark, int Length)? fence = null;
        foreach (var line in LinesWithEndings(text))
        {
            var rest = line.TrimStart(' ');
            var indent = line.Length - rest.Length;
            char? mark = rest.Length > 0 && rest[0] is '`' or '~' ? rest[0] : null;
            var run = mark is { } m ? rest.TakeWhile(c => c == m).Count() : 0;
            if (fence is { } open)
            {
                if (indent <= 3 && mark == open.Mark && run >= open.Length && rest[run..].Trim().Length == 0)
                {
                    fence = null;
                }
                result.Append(line);
                continue;
            }
            if (indent <= 3 && run >= 3)
            {
                fence = (mark!.Value, run);
                result.Append(line);
                continue;
            }
            converted += ConvertLine(line, note, attachment, result);
        }
        return (result.ToString(), converted);
    }

    /// <summary>Une ligne hors bloc de code : <c>[[…]]</c> converti, sauf dans le code en ligne.</summary>
    private static int ConvertLine(string line, Func<string, string?> note, Func<string, string?> attachment, StringBuilder result)
    {
        var converted = 0;
        var i = 0;
        while (i < line.Length)
        {
            var rest = line.AsSpan(i);
            // Code en ligne : recopié tel quel jusqu'à la même série d'accents graves.
            if (rest[0] == '`')
            {
                var ticks = 0;
                while (ticks < rest.Length && rest[ticks] == '`')
                {
                    ticks++;
                }
                var marker = rest[..ticks];
                var close = rest[ticks..].IndexOf(marker, StringComparison.Ordinal);
                if (close >= 0)
                {
                    var length = ticks + close + ticks;
                    result.Append(rest[..length]);
                    i += length;
                }
                else
                {
                    result.Append(marker);
                    i += ticks;
                }
                continue;
            }
            var image = rest.StartsWith("![[", StringComparison.Ordinal);
            if ((image || rest.StartsWith("[[", StringComparison.Ordinal)) && rest.IndexOf("]]", StringComparison.Ordinal) is var end and >= 0)
            {
                var start = image ? 3 : 2;
                if (end >= start)
                {
                    var inside = rest[start..end].ToString();
                    if (inside.IndexOfAny(['[', '\n']) < 0)
                    {
                        var bar = inside.IndexOf('|', StringComparison.Ordinal);
                        var (linked, alias) = bar < 0 ? (inside, (string?)null) : (inside[..bar], inside[(bar + 1)..].Trim());
                        var hash = linked.IndexOf('#', StringComparison.Ordinal);
                        var (path, section) = hash < 0 ? (linked, string.Empty) : (linked[..hash], linked[(hash + 1)..]);
                        if (image)
                        {
                            if (attachment(path) is { } destination)
                            {
                                // « |300 » est une largeur chez Obsidian, pas un texte.
                                var label = alias is not null && !alias.All(c => char.IsAsciiDigit(c) || c == 'x') ? alias : string.Empty;
                                result.Append("![").Append(label).Append("](<").Append(MarkdownLinks.Encode(destination, angleBrackets: true)).Append(">)");
                                converted++;
                            }
                            else
                            {
                                result.Append(rest[..(end + 2)]);
                            }
                        }
                        else
                        {
                            var name = path[(path.LastIndexOfAny(['/', '\\']) + 1)..].Trim();
                            var label = !string.IsNullOrEmpty(alias)
                                ? alias
                                : section.Length == 0 ? name : name.Length == 0 ? section.Trim() : $"{name} › {section.Trim()}";
                            if (note(path) is { } destination)
                            {
                                result.Append('[').Append(label.Replace("[", "\\[", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal))
                                    .Append("](<").Append(MarkdownLinks.Encode(destination, angleBrackets: true)).Append(">)");
                                converted++;
                            }
                            else
                            {
                                // Note absente : son nom reste lisible, sans crochets.
                                result.Append(label);
                            }
                        }
                        i += end + 2;
                        continue;
                    }
                }
            }
            result.Append(rest[0]);
            i++;
        }
        return converted;
    }

    /// <summary>
    /// Liens Markdown ordinaires vers des fichiers de la source (images, PDF) : copiés dans les
    /// ressources de la note, destination réécrite.
    /// </summary>
    private static string TakeAttachments(string text, string noteFolder, string source, ImportedResources resources)
    {
        var result = new StringBuilder(text.Length);
        var copiedUpTo = 0;
        foreach (var link in MarkdownLinks.Find(text))
        {
            var raw = text[link.Start..link.End];
            var cut = raw.IndexOfAny(['#', '?']);
            var decoded = MarkdownLinks.Decode(cut < 0 ? raw : raw[..cut]);
            if (MarkdownLinks.IsExternal(decoded) || IsNote(decoded))
            {
                continue;
            }
            var file = Normalize(noteFolder, decoded);
            if (!RealPaths.IsWithin(file, source) || !File.Exists(file))
            {
                continue;
            }
            if (resources.Copy(file) is { } copied)
            {
                result.Append(text, copiedUpTo, link.Start - copiedUpTo).Append(MarkdownLinks.Encode(copied, link.AngleBrackets));
                copiedUpTo = link.End;
            }
        }
        return result.Append(text, copiedUpTo, text.Length - copiedUpTo).ToString();
    }

    /// <summary><c>fiches/&lt;nom&gt;</c>, ou <c>fiches/&lt;nom&gt; (2)</c>… s'il existe déjà.</summary>
    private VaultPath FreeImportFolder(string name)
    {
        var stem = FileNames.ForTitle(name);
        for (var number = 1; number < 100; number++)
        {
            var path = FolderPath(number == 1 ? stem : $"{stem} ({number})");
            if (!_storage.Exists(Confine(path)))
            {
                return path;
            }
        }
        throw VaultException.AlreadyExists(Path.Combine(Root, VaultLayout.Sheets, stem));
    }

    /// <summary>Parcourt la source : fichiers ordinaires, sans dossiers cachés ni liens, sans entrer dans le coffre.</summary>
    private static void Walk(string folder, string source, string vault, int depth, List<string> files, List<string> ignored)
    {
        if (depth > MaxImportDepth)
        {
            ignored.Add($"{Path.GetRelativePath(source, folder)} : trop profond, ignoré");
            return;
        }
        List<FileSystemInfo> entries;
        try
        {
            entries = [.. new DirectoryInfo(folder).EnumerateFileSystemInfos().OrderBy(e => e.Name, Utf8Order.Instance)];
        }
        catch (Exception exception) when (IsIo(exception))
        {
            return;
        }
        foreach (var entry in entries)
        {
            if (entry.Name.StartsWith('.') || entry.LinkTarget is not null)
            {
                continue;
            }
            if (entry is DirectoryInfo)
            {
                if (!string.Equals(Path.TrimEndingDirectorySeparator(entry.FullName), Path.TrimEndingDirectorySeparator(vault), StringComparison.Ordinal))
                {
                    Walk(entry.FullName, source, vault, depth + 1, files, ignored);
                }
            }
            else
            {
                files.Add(entry.FullName);
            }
        }
    }

    private static bool IsNote(string path) =>
        FileNameOf(path) is { } name && Extension(name) is { } extension && Ascii.EqualsIgnoreCase(extension, "md");

    /// <summary>Dernier composant d'un chemin (« / » ou « \ »), « . » ignorés ; aucun s'il remonte (« .. »).</summary>
    private static string? FileNameOf(string path) =>
        path.Split('/', '\\').LastOrDefault(part => part is not ("" or ".")) is { } last && last != ".." ? last : null;

    /// <summary>Clé de recherche : Windows et Obsidian ne distinguent pas la casse.</summary>
    private static string Key(string name) => RustText.ToLowercase(name.Trim());

    /// <summary>Chemin d'une note dans le coffre : chaque nom rendu valide, « (2) » si deux notes tombent sur le même nom.</summary>
    private static VaultPath? TargetOf(VaultPath folder, string relative, HashSet<string> taken)
    {
        var parts = relative.Split(Path.DirectorySeparatorChar);
        var path = folder;
        foreach (var part in parts[..^1])
        {
            if (!path.TryCombine(FileNames.ForTitle(part), out var next, out _))
            {
                return null;
            }
            path = next;
        }
        var file = parts[^1];
        var stem = FileNames.ForTitle(file.EndsWith(".md", StringComparison.Ordinal) ? file[..^3] : file);
        var name = $"{stem}.md";
        for (var number = 2; taken.Contains(Key($"{path}/{name}")); number++)
        {
            name = $"{stem} ({number}).md";
        }
        if (!path.TryCombine(name, out var target, out _))
        {
            return null;
        }
        taken.Add(Key(target.Value));
        return target;
    }

    /// <summary>La note visée par <c>[[cible]]</c> : par chemin depuis la source, sinon par nom, la plus proche de la racine.</summary>
    private static int? FindNote(string linked, List<(string Source, VaultPath Target)> notes, Dictionary<string, List<int>> byName, string source)
    {
        var target = linked.Trim();
        while (target.EndsWith(".md", StringComparison.Ordinal))
        {
            target = target[..^3];
        }
        if (target.Length == 0)
        {
            return null;
        }
        var name = target[(target.LastIndexOfAny(['/', '\\']) + 1)..];
        if (!byName.TryGetValue(Key(name), out var candidates))
        {
            return null;
        }
        if (target.IndexOfAny(['/', '\\']) >= 0)
        {
            var wanted = Key(target.Replace('\\', '/'));
            foreach (var i in candidates)
            {
                var relative = Path.GetRelativePath(source, notes[i].Source);
                var withoutExtension = Path.ChangeExtension(relative, null)!.Replace('\\', '/');
                if (Key(withoutExtension).EndsWith(wanted, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }
        return candidates.MinBy(i => Depth(notes[i].Source));
    }

    /// <summary>La pièce jointe visée : relative à la note, puis à la source, puis par nom n'importe où dans la source.</summary>
    private static string? FindAttachment(string linked, string noteFolder, string source, Dictionary<string, List<string>> attachments)
    {
        var target = linked.Trim();
        if (target.Length == 0 || MarkdownLinks.IsExternal(target) || IsNote(target))
        {
            return null;
        }
        foreach (var basePath in new[] { noteFolder, source })
        {
            var path = Normalize(basePath, target);
            if (RealPaths.IsWithin(path, source) && File.Exists(path))
            {
                return path;
            }
        }
        var name = target[(target.LastIndexOfAny(['/', '\\']) + 1)..];
        return attachments.TryGetValue(Key(name), out var list) ? list.MinBy(Depth) : null;
    }

    /// <summary>Base suivie d'un chemin relatif (« / » ou « \ »), « . » et « .. » résolus sur le texte.</summary>
    private static string Normalize(string basePath, string relative)
    {
        var path = basePath;
        foreach (var part in relative.Split('/', '\\'))
        {
            switch (part)
            {
                case "" or ".":
                    break;
                case "..":
                    path = Path.GetDirectoryName(path) ?? path;
                    break;
                default:
                    path = Path.Combine(path, part);
                    break;
            }
        }
        return path;
    }

    private static int Depth(string path) => path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Lignes avec leur fin de ligne (<c>split_inclusive('\n')</c>).</summary>
    private static IEnumerable<string> LinesWithEndings(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            var next = end < 0 ? text.Length : end + 1;
            yield return text[start..next];
            start = next;
        }
    }

    private sealed class ImportCounts(string folder, List<string> ignored)
    {
        public int Sheets { get; set; }

        public int Tracked { get; set; }

        public int Resources { get; set; }

        public int Links { get; set; }

        public ImportReport Build() => new(folder, Sheets, Tracked, Resources, Links, ignored);
    }

    /// <summary>Copie des pièces jointes d'une note dans son dossier de ressources ; une pièce citée deux fois n'est copiée qu'une fois.</summary>
    private sealed class ImportedResources(VaultSession vault, VaultPath note)
    {
        public SortedDictionary<string, string> Copied { get; } = new(StringComparer.Ordinal);

        public List<string> Ignored { get; } = [];

        /// <summary>Copie <paramref name="file"/> et rend la destination à écrire dans la note.</summary>
        public string? Copy(string file)
        {
            if (Copied.TryGetValue(file, out var already))
            {
                return already;
            }
            var originalName = Path.GetFileName(file);
            string name;
            try
            {
                name = FileNames.ForResource(originalName);
            }
            catch (VaultException exception)
            {
                Ignored.Add(exception.Message);
                return null;
            }
            if (!File.Exists(file) || new FileInfo(file).Length > MaxResourceBytes)
            {
                Ignored.Add($"« {originalName} » : plus de 50 Mo, non copiée");
                return null;
            }
            if (VaultStorage.Disk.TryReadAllBytes(file) is not { } bytes)
            {
                return null;
            }
            try
            {
                var folder = FileNames.ResourcesFolder(note);
                vault.CreateDirectory(vault.Confine(folder));
                var dot = name.LastIndexOf('.');
                var (stem, extension) = dot < 0 ? (name, string.Empty) : (name[..dot], name[(dot + 1)..]);
                for (var number = 1; number < 1000; number++)
                {
                    var candidate = number == 1 ? name : $"{stem}-{number}.{extension}";
                    if (!folder.TryCombine(candidate, out var path, out _))
                    {
                        return null;
                    }
                    if (vault.TryCreateFile(vault.Confine(path), bytes))
                    {
                        var link = $"{folder.Name}/{candidate}";
                        Copied[file] = link;
                        return link;
                    }
                }
            }
            catch (VaultException)
            {
                return null;
            }
            return null;
        }
    }
}
