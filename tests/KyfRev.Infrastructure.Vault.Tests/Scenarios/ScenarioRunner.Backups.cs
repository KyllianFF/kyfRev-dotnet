using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using KyfRev.Application.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Backups;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault.Tests.Scenarios;

/// <summary>
/// Opérations des scénarios sur les sauvegardes, les exports, le partage, l'import d'un dossier de
/// notes et les réparations. Ce qui est écrit hors du coffre l'est à côté de lui
/// (<c>&lt;coffre&gt;.dehors</c>) ; un chemin « coffre/… » désigne un emplacement dans le coffre.
/// </summary>
internal sealed partial class ScenarioRunner
{
    private string Outside => root + ".dehors";

    private string ExecuteBackups(string op, JsonElement operation, Moment now)
    {
        switch (op)
        {
            case "ecrire_dehors":
                {
                    var path = Place(operation, "chemin");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    var bytes = operation.TryGetProperty("octets", out var raw)
                        ? [.. raw.EnumerateArray().Select(b => b.GetByte())]
                        : Encoding.UTF8.GetBytes(Text(operation, "contenu"));
                    File.WriteAllBytes(path, bytes);
                    return string.Empty;
                }
            case "sauvegarder":
                {
                    var backup = Vault.Backup(Place(operation, "dossier"), Timestamp(operation));
                    return $"{Path.GetFileName(backup.Path)} {Format(backup.Timestamp)} {backup.FileCount}";
                }
            case "archive":
                return ArchiveContent(Place(operation, "chemin"));
            case "sauvegardes":
                return string.Join(';', VaultBackups.List(Place(operation, "dossier")).Select(b => $"{Format(b.Timestamp)} {Path.GetFileName(b.Path)}"));
            case "sauvegarde_necessaire":
                return Bool(VaultBackups.DailyBackupNeeded(Place(operation, "dossier"), now.Day));
            case "rotation":
                {
                    var policy = new RotationPolicy(Count(operation, "jours"), Count(operation, "semaines"), Count(operation, "mois"));
                    return string.Join(';', VaultBackups.ApplyRotation(Place(operation, "dossier"), policy).Select(Path.GetFileName));
                }
            case "restaurer_coffre":
                {
                    var restored = VaultBackups.Restore(Place(operation, "sauvegarde"), Place(operation, "dossier"), now.Day);
                    return $"{Json(Path.GetFileName(restored))} {Tree(restored)}";
                }
            case "restaurer_fiche_sauvegarde":
                return Vault.RestoreSheetFromBackup(Sheet(operation), Place(operation, "sauvegarde"), now).Value;
            case "exporter":
                {
                    var kind = Raw(operation, "genre") switch
                    {
                        "suivi" => ExportKind.Tracking,
                        "annotations" => ExportKind.Annotations,
                        "coffre" => ExportKind.Vault,
                        _ => ExportKind.Quotes,
                    };
                    var folder = Place(operation, "dossier");
                    Directory.CreateDirectory(folder);
                    return string.Join(';', Vault.Export(kind, folder, Timestamp(operation), TimeZoneInfo.Utc).Select(Summary));
                }
            case "partager":
                {
                    var format = Raw(operation, "format") switch
                    {
                        "markdown" => ShareFormat.Markdown,
                        "markdown_images" => ShareFormat.MarkdownWithImages,
                        "texte" => ShareFormat.Text,
                        _ => ShareFormat.Html,
                    };
                    var folder = Place(operation, "dossier");
                    Directory.CreateDirectory(folder);
                    return Summary(Vault.ShareSheet(Sheet(operation), format, folder, Optional(operation, "contenu")));
                }
            case "importer":
                {
                    var known = Vault.Sheets.Select(entry => entry.Sheet.Id).ToHashSet(StringComparer.Ordinal);
                    var report = Vault.ImportFolder(Place(operation, "source"), Text(operation, "nom_dossier"), operation.GetProperty("suivre").GetBoolean(), now);
                    if (Raw(operation, "nom") is { } name && Vault.Sheets.Select(entry => entry.Sheet.Id).FirstOrDefault(id => !known.Contains(id)) is { } created)
                    {
                        _names[name] = created;
                    }
                    return $"{Json(report.Folder)} {report.Sheets} {report.Tracked} {report.Resources} {report.Links} {List(report.Ignored)}";
                }
            case "rapport_sante":
                return HealthDetails();
            case "garder_original":
                {
                    var created = Vault.KeepOriginal(Sheet(operation), VaultPath.Create(Text(operation, "original")), now);
                    if (Raw(operation, "nom") is { } name && created.Count > 0)
                    {
                        _names[name] = created[0];
                    }
                    return string.Join(';', created);
                }
            case "recreer_suivi":
                return Vault.RecreateTracking(VaultPath.Create(Text(operation, "chemin")), now);
            case "annotations_corbeille":
                return string.Join(';', Vault.TrashedAnnotations().Select(a =>
                    $"{a.Id} {a.SheetId} {Json(a.Title)} {Json(a.Exact)} {Optional(a.Comment)} {InstantText.Format(a.TrashedAt)}"));
            default:
                return ExecuteReading(op, operation, now);
        }
    }

    private string HealthDetails()
    {
        var r = Vault.HealthDetails();
        static string Sorted(IEnumerable<string> items) => string.Join(';', items.OrderBy(item => item, Utf8Order.Instance));
        static string Concerned(ConcernedSheet s) => $"{s.Id} {Json(s.Title)} {s.Path}";
        static string Passage(ConcernedPassage p) => $"{p.Id} {p.SheetId} {Json(p.Title)} {Json(p.Exact)} {State(p.State)}";
        return $"problemes={r.Problems}"
            + $" deplacees=[{Sorted(r.Moved.Select(m => $"{Json(m.Title)} {m.From} {m.To}"))}]"
            + $" modifiees=[{Sorted(r.ModifiedOutside.Select(Concerned))}]"
            + $" manquantes=[{Sorted(r.Missing.Select(Concerned))}]"
            + $" doublons=[{Sorted(r.Duplicates.Select(d => $"{d.Id} {Json(d.Title)} {string.Join(',', d.Paths)}"))}]"
            + $" non_suivies=[{Sorted(r.Untracked)}]"
            + $" sans_suivi=[{Sorted(r.WithoutTracking.Select(Concerned))}]"
            + $" illisibles=[{Sorted(r.Unreadable.Select(u => u.Path))}]"
            + $" liens=[{Sorted(r.LinksToCheck)}]"
            + $" liens_casses=[{string.Join(';', r.BrokenLinks.Select(l => $"{l.SheetId} {Json(l.Title)} {Json(l.Target)}"))}]"
            + $" orphelines=[{Sorted(r.OrphanAnnotations.Select(Passage))}]"
            + $" a_verifier=[{string.Join(';', r.AnnotationsToCheck.Select(Passage))}]";
    }

    private static string State(AnchorState state) => state switch
    {
        AnchorState.Anchored => "Ancree",
        AnchorState.NeedsReview => "AVerifier",
        _ => "Introuvable",
    };

    /// <summary>Emplacement d'un chemin du scénario : dans le coffre (« coffre/… ») ou dehors.</summary>
    private string Place(JsonElement operation, string key)
    {
        var text = Text(operation, key);
        return text.StartsWith("coffre/", StringComparison.Ordinal)
            ? Path.Combine([root, .. text["coffre/".Length..].Split('/')])
            : Path.Combine([Outside, .. text.Split('/')]);
    }

    private static DateTime Timestamp(JsonElement operation) =>
        DateTime.ParseExact(Raw(operation, "horodatage")!, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Format(DateTime timestamp) => timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    private static int Count(JsonElement operation, string key) => operation.GetProperty(key).GetInt32();

    /// <summary>Un fichier écrit hors du coffre : son nom, puis le contenu d'une archive ou le texte.</summary>
    private static string Summary(string path) =>
        path.EndsWith(".zip", StringComparison.Ordinal)
            ? $"{Path.GetFileName(path)} [{ArchiveContent(path)}]"
            : $"{Path.GetFileName(path)} {Json(Encoding.UTF8.GetString(File.ReadAllBytes(path)))}";

    /// <summary>Entrées d'une archive, dans leur ordre : « nom:empreinte ».</summary>
    private static string ArchiveContent(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        return string.Join(';', archive.Entries.Select(entry =>
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return $"{entry.FullName}:{Files.Fingerprint.Of(buffer.ToArray())}";
        }));
    }

    /// <summary>Tous les fichiers sous <paramref name="folder"/>, triés : « chemin:empreinte ».</summary>
    private static string Tree(string folder) => string.Join(';', Directory
        .EnumerateFiles(folder, "*", SearchOption.AllDirectories)
        .Select(file => $"{Path.GetRelativePath(folder, file).Replace('\\', '/')}:{Files.Fingerprint.Of(File.ReadAllBytes(file))}")
        .Order(Utf8Order.Instance));
}
