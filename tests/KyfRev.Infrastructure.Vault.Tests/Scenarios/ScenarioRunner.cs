using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Ids;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Text;

namespace KyfRev.Infrastructure.Vault.Tests.Scenarios;

/// <summary>
/// Joue un scénario de <c>fixtures/scenarios</c> sur <see cref="VaultSession"/>, comme
/// <c>tools/reference-vectors/src/scenarios.rs</c> le joue sur le crate <c>coffre</c> de la 0.x :
/// même vocabulaire, mêmes lignes de résultat.
/// </summary>
internal sealed partial class ScenarioRunner(string root, UlidGenerator ids) : IDisposable
{
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _reads = new(StringComparer.Ordinal);
    private VaultSession? _vault;

    private VaultSession Vault => _vault ?? throw new InvalidOperationException("Coffre fermé.");

    public void Dispose() => _vault?.Dispose();

    /// <summary>Ligne de résultat de l'opération <paramref name="n"/>, au format de la 0.x.</summary>
    public string Play(int n, JsonElement operation)
    {
        var op = operation.GetProperty("op").GetString()!;
        try
        {
            var value = Execute(op, operation);
            return value.Length == 0 ? $"{n} {op} ok" : $"{n} {op} ok {value}";
        }
        catch (VaultException exception)
        {
            return $"{n} {op} erreur {Describe(exception)}";
        }
    }

    private string Execute(string op, JsonElement operation)
    {
        var now = operation.TryGetProperty("le", out var day) ? At(day.GetString()!) : default;
        switch (op)
        {
            case "initialiser":
                _vault = VaultSession.Initialize(root, now, ids);
                return string.Empty;
            case "ouvrir":
                _vault?.Dispose();
                _vault = null;
                _vault = VaultSession.Open(root, now, ids);
                return string.Empty;
            case "analyser":
                Vault.Analyze(now);
                return string.Empty;
            case "creer_fiche":
                _names[Text(operation, "nom")] = Vault.CreateSheet(Optional(operation, "dossier"), Text(operation, "titre"), Text(operation, "corps"), now);
                return string.Empty;
            case "suivre_fichier":
                _names[Text(operation, "nom")] = Vault.TrackFile(VaultPath.Create(Text(operation, "chemin")), now);
                return string.Empty;
            case "lire_fiche":
                {
                    var id = Sheet(operation);
                    var content = Vault.ReadSheet(id);
                    _reads[id] = content.Fingerprint;
                    return content.Fingerprint;
                }
            case "enregistrer_fiche":
                {
                    var id = Sheet(operation);
                    return Vault.SaveSheet(id, Text(operation, "texte"), Fingerprint(operation, id), Session(operation), now);
                }
            case "enregistrer_corps":
                {
                    var id = Sheet(operation);
                    return Describe(Vault.SaveBody(id, Text(operation, "corps"), Fingerprint(operation, id), Session(operation), now));
                }
            case "garder_en_version":
                Vault.KeepAsVersion(Sheet(operation), Text(operation, "corps"), now);
                return string.Empty;
            case "versions":
                return string.Join(';', Vault.Versions(Sheet(operation)).Select(v => $"{v.Name} {InstantText.Format(v.SavedAt)} {v.Size}"));
            case "lire_version":
                return Json(Vault.ReadVersion(Sheet(operation), Text(operation, "version")));
            case "restaurer_version":
                {
                    var id = Sheet(operation);
                    return Describe(Vault.RestoreVersion(id, Text(operation, "version"), Fingerprint(operation, id), now));
                }
            case "modifier_etiquettes":
                {
                    var tags = operation.GetProperty("etiquettes").EnumerateArray().Select(tag => tag.GetString()!).ToList();
                    return List(Vault.SetTags(Sheet(operation), tags, now));
                }
            case "etiquettes":
                return List(Vault.AllTags());
            case "changer_titre":
                Vault.ChangeTitle(Sheet(operation), Text(operation, "titre"), now);
                return string.Empty;
            case "deplacer_fiche":
                Vault.MoveSheet(Sheet(operation), Optional(operation, "dossier"), now);
                return string.Empty;
            case "ajouter_ressource":
                return Vault.AddResource(Sheet(operation), Text(operation, "nom_fichier"), Encoding.UTF8.GetBytes(Text(operation, "contenu")));
            case "dossiers":
                return List(Vault.Folders());
            case "creer_dossier":
                return Json(Vault.CreateFolder(Text(operation, "dossier")));
            case "deplacer_dossier":
                return Json(Vault.MoveFolder(Text(operation, "dossier"), Text(operation, "vers"), now));
            case "corbeille_fiche":
                Vault.TrashSheet(Sheet(operation), now);
                return string.Empty;
            case "corbeille_dossier":
                return Vault.TrashFolder(Text(operation, "dossier"), now).ToString(CultureInfo.InvariantCulture);
            case "restaurer_fiche":
                Vault.RestoreSheet(Sheet(operation), now);
                return string.Empty;
            case "fiches_a_la_corbeille":
                return string.Join(';', Vault.TrashedSheets().Select(s =>
                    $"{s.Id} {Json(s.Title)} {Json(s.Folder)} {InstantText.Format(s.TrashedAt)} {s.Annotations}"));
            case "reviser":
                {
                    var rules = Vault.Settings.Reminders;
                    Vault.ApplyReview(Sheet(operation), tracking => tracking.ValidateReview(now, null, rules));
                    return string.Empty;
                }
            case "reporter":
                {
                    var to = DateOnly.ParseExact(Text(operation, "vers"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    Vault.ApplyToTracking(Sheet(operation), tracking => tracking.Postpone(now, to));
                    return string.Empty;
                }
            case "archiver":
                {
                    var rules = Vault.Settings.Reminders;
                    Vault.ApplyToTracking(Sheet(operation), tracking => tracking.ArchiveManually(now, rules));
                    return string.Empty;
                }
            case "sante":
                return Health();
            case "fiche":
                {
                    var entry = Vault.Sheet(Sheet(operation));
                    var presence = entry.Presence switch
                    {
                        FilePresence.Present => "present",
                        FilePresence.Missing => "manquant",
                        _ => "doublon",
                    };
                    var sheet = entry.Sheet;
                    return $"{sheet.Path} {Json(sheet.Title)} {List(sheet.Tags)} {presence} {sheet.Fingerprint}";
                }
            case "ecrire":
                {
                    var path = Absolute(Text(operation, "chemin"));
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    var bytes = operation.TryGetProperty("octets", out var raw)
                        ? [.. raw.EnumerateArray().Select(b => b.GetByte())]
                        : Encoding.UTF8.GetBytes(Text(operation, "contenu"));
                    File.WriteAllBytes(path, bytes);
                    if (operation.TryGetProperty("modifie_le", out var modified))
                    {
                        File.SetLastWriteTimeUtc(path, InstantText.Parse(modified.GetString()!, "modifie_le").UtcDateTime);
                    }
                    return string.Empty;
                }
            case "supprimer":
                {
                    var path = Absolute(Text(operation, "chemin"));
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive: true);
                    }
                    else
                    {
                        File.Delete(path);
                    }
                    return string.Empty;
                }
            case "deplacer":
                {
                    var (from, to) = (Absolute(Text(operation, "de")), Absolute(Text(operation, "vers")));
                    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                    if (Directory.Exists(from))
                    {
                        Directory.Move(from, to);
                    }
                    else
                    {
                        File.Move(from, to);
                    }
                    return string.Empty;
                }
            default:
                return ExecuteComplements(op, operation, now);
        }
    }

    private string Health()
    {
        var health = Vault.Health;
        static string Sorted(IEnumerable<string> items) => string.Join(';', items.OrderBy(item => item, Utf8Order.Instance));
        return $"deplacees=[{Sorted(health.Moved.Select(m => $"{m.Id} {m.From} {m.To}"))}]"
            + $" modifiees=[{Sorted(health.ModifiedOutside)}]"
            + $" manquantes=[{Sorted(health.Missing)}]"
            + $" doublons=[{Sorted(health.Duplicates.Select(d => $"{d.Id} {string.Join(',', d.Paths)}"))}]"
            + $" non_suivies=[{Sorted(health.Untracked.Select(p => p.Value))}]"
            + $" sans_suivi=[{Sorted(health.WithoutTracking.Select(w => $"{w.Id} {w.Path}"))}]"
            + $" illisibles=[{Sorted(health.Unreadable.Select(u => u.Path))}]"
            + $" liens=[{Sorted(health.LinksToCheck.Select(p => p.Value))}]"
            + $" orphelines=[{Sorted(health.OrphanAnnotations)}]";
    }

    /// <summary>Nom de variante Rust d'une erreur, et son message s'il ne contient pas de chemin absolu.</summary>
    private static string Describe(VaultException exception)
    {
        var (variant, comparable) = exception.Kind switch
        {
            VaultErrorKind.Io => ("Io", false),
            VaultErrorKind.InvalidPath => ("CheminInvalide", true),
            VaultErrorKind.OutsideVault => ("HorsDuCoffre", false),
            VaultErrorKind.NotAVault => ("PasUnCoffre", false),
            VaultErrorKind.FolderNotEmpty => ("DossierNonVide", false),
            VaultErrorKind.AlreadyOpen => ("CoffreDejaOuvert", true),
            VaultErrorKind.UnknownSheet => ("FicheInconnue", true),
            VaultErrorKind.MissingSheet => ("FicheManquante", true),
            VaultErrorKind.UnknownFolder => ("DossierInconnu", true),
            VaultErrorKind.UnresolvedDuplicate => ("DoublonNonResolu", true),
            VaultErrorKind.NotUtf8 => ("NonUtf8", true),
            VaultErrorKind.Conflict => ("Conflit", true),
            VaultErrorKind.IdentifierChanged => ("IdentifiantModifie", true),
            VaultErrorKind.AlreadyExists => ("DejaExistant", false),
            VaultErrorKind.AlreadyTracked => ("DejaSuivie", true),
            VaultErrorKind.ResourceRefused => ("RessourceRefusee", true),
            VaultErrorKind.EmptyTitle => ("TitreVide", true),
            VaultErrorKind.UnknownVersion => ("VersionInconnue", true),
            VaultErrorKind.InvalidHeader => ("EnteteInvalide", false),
            VaultErrorKind.UnreadableTracking => ("SuiviIllisible", false),
            VaultErrorKind.UnreadableSettings => ("ParametresIllisibles", false),
            VaultErrorKind.Tracking => ("Suivi", true),
            VaultErrorKind.UnknownAnnotation => ("AnnotationInconnue", true),
            VaultErrorKind.AnnotationInTrash => ("AnnotationALaCorbeille", true),
            VaultErrorKind.UnreadableAnnotations => ("AnnotationsIllisibles", true),
            VaultErrorKind.InvalidAnchor => ("AncreInvalide", true),
            VaultErrorKind.NotADefinition => ("PasUneDefinition", true),
            VaultErrorKind.InvalidDefinition => ("DefinitionInvalide", true),
            VaultErrorKind.InvalidSettings => ("ReglagesInvalides", true),
            VaultErrorKind.InvalidQuote => ("CitationInvalide", true),
            VaultErrorKind.UnknownQuote => ("CitationInconnue", true),
            VaultErrorKind.QuoteInTrash => ("CitationALaCorbeille", true),
            VaultErrorKind.UnknownSchema => ("SchemaInconnu", true),
            VaultErrorKind.InvalidSchema => ("SchemaInvalide", true),
            _ => (exception.Kind.ToString(), true),
        };
        return comparable ? $"{variant} : {exception.Message}" : variant;
    }

    private static string Describe(SaveResult result) => result switch
    {
        SheetSaved saved => $"enregistre {saved.Fingerprint}",
        SaveConflict conflict => $"conflit {conflict.DiskFingerprint} {Json(conflict.DiskBody)}",
        _ => throw new InvalidOperationException(),
    };

    private string Text(JsonElement operation, string key)
    {
        var raw = operation.GetProperty(key).GetString()!;
        return _names.Aggregate(raw, (text, name) => text.Replace("{{" + name.Key + "}}", name.Value, StringComparison.Ordinal));
    }

    private string? Optional(JsonElement operation, string key) =>
        operation.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null ? Text(operation, key) : null;

    private string Sheet(JsonElement operation)
    {
        var name = operation.GetProperty("fiche").GetString()!;
        return _names.GetValueOrDefault(name, name);
    }

    private string Fingerprint(JsonElement operation, string id) => operation.GetProperty("empreinte").GetString() switch
    {
        "actuelle" => TryRead(id),
        "derniere_lecture" => _reads.GetValueOrDefault(id, string.Empty),
        var other => other!,
    };

    private string TryRead(string id)
    {
        try
        {
            return Vault.ReadSheet(id).Fingerprint;
        }
        catch (VaultException)
        {
            return string.Empty;
        }
    }

    private static ulong Session(JsonElement operation) => operation.GetProperty("session").GetUInt64();

    private string Absolute(string relative) => Path.Combine([root, .. relative.Split('/')]);

    private static Moment At(string day)
    {
        var date = DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new Moment(date, new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));
    }

    private static string Json(string text) => SerdeJson.Compact(JsonValue.Create(text));

    private static string List(IEnumerable<string> texts) => SerdeJson.Compact(Fields.StringArray(texts));
}
