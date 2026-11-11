using System.Globalization;
using System.Text;
using KyfRev.Application.Text;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Format;

namespace KyfRev.Infrastructure.Vault;

/// <remarks>
/// Exports : suivi des révisions en CSV (pour Excel : UTF-8 avec BOM, séparateur « ; », fins de
/// ligne CRLF), annotations et citations en Markdown, coffre complet en <c>.zip</c>. Les fichiers
/// sont écrits dans un dossier existant, hors du coffre, sans jamais rien écraser.
/// </remarks>
public sealed partial class VaultSession
{
    /// <summary>Écrit un export dans <paramref name="folder"/> ; rend les fichiers créés.</summary>
    /// <param name="kind">Ce que l'on exporte.</param>
    /// <param name="folder">Dossier existant, hors du coffre.</param>
    /// <param name="timestamp">Heure locale : son jour figure dans les noms.</param>
    /// <param name="zone">Fuseau des dates des événements horodatés (celui du poste par défaut).</param>
    public IReadOnlyList<string> Export(ExportKind kind, string folder, DateTime timestamp, TimeZoneInfo? zone = null)
    {
        CheckOutputFolder(folder);
        var day = DateOnly.FromDateTime(timestamp);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        switch (kind)
        {
            case ExportKind.Tracking:
                {
                    var (tracking, history) = TrackingCsv(zone ?? TimeZoneInfo.Local);
                    return
                    [
                        CreateWithoutOverwriting(folder, $"kyfRev - suivi {day:yyyy-MM-dd}", "csv", utf8.GetBytes(tracking)),
                    CreateWithoutOverwriting(folder, $"kyfRev - historique {day:yyyy-MM-dd}", "csv", utf8.GetBytes(history)),
                ];
                }
            case ExportKind.Annotations:
                return [CreateWithoutOverwriting(folder, $"kyfRev - annotations {day:yyyy-MM-dd}", "md", utf8.GetBytes(AnnotationsMarkdown(day)))];
            case ExportKind.Vault:
                return [Backup(folder, timestamp).Path];
            case ExportKind.Anki:
                return [CreateWithoutOverwriting(folder, $"kyfRev - anki {day:yyyy-MM-dd}", "txt", utf8.GetBytes(AnkiNotes()))];
            case ExportKind.Calendar:
                {
                    var local = zone ?? TimeZoneInfo.Local;
                    var stamp = new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Unspecified), local.GetUtcOffset(timestamp));
                    return [CreateWithoutOverwriting(folder, $"kyfRev - échéances {day:yyyy-MM-dd}", "ics", utf8.GetBytes(DueDatesCalendar(day, stamp)))];
                }
            default:
                {
                    var (markdown, csv) = QuotesExport(day);
                    return
                    [
                        CreateWithoutOverwriting(folder, $"kyfRev - citations {day:yyyy-MM-dd}", "md", utf8.GetBytes(markdown)),
                    CreateWithoutOverwriting(folder, $"kyfRev - citations {day:yyyy-MM-dd}", "csv", utf8.GetBytes(csv)),
                ];
                }
        }
    }

    /// <summary>
    /// Suivi des révisions (une ligne par fiche ou passage) et historique complet (une ligne par
    /// événement), au format CSV d'Excel.
    /// </summary>
    public (string Tracking, string History) TrackingCsv(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var tracking = new StringBuilder("\uFEFF");
        var history = new StringBuilder("\uFEFF");
        tracking.Append(CsvLine(["Type", "Identifiant", "Fiche", "Dossier", "Passage", "Statut", "Étape attendue", "Échéance", "Cycle en cours",
            "Dernière révision", "Cycles commencés", "Cycles terminés", "Archivée le", "Rappel possible le"]));
        history.Append(CsvLine(["Type", "Identifiant", "Fiche", "Passage", "Événement", "Date", "Cycle", "Étape", "Prévue le", "Retard (jours)", "Détail"]));
        foreach (var entry in SheetsByTitle())
        {
            var sheet = entry.Sheet;
            var folder = sheet.Path.Parent?.Value ?? string.Empty;
            var items = new List<(string Kind, string Id, string Passage, Tracking Tracking)> { ("Fiche", sheet.Id, string.Empty, sheet.Tracking) };
            items.AddRange(VisibleAnnotations(sheet.Id).Select(a => ("Passage", a.Id, a.Anchor.Exact, a.Tracking)));
            foreach (var (kind, id, passage, itemTracking) in items)
            {
                tracking.Append(CsvLine([kind, id, CsvText(sheet.Title), CsvText(folder), CsvText(passage), .. StateCells(itemTracking)]));
                foreach (var trackingEvent in itemTracking.History)
                {
                    history.Append(CsvLine([kind, id, CsvText(sheet.Title), CsvText(passage), .. EventCells(trackingEvent, zone)]));
                }
            }
        }
        return (tracking.ToString(), history.ToString());
    }

    /// <summary>Passages annotés, fiche par fiche, en Markdown.</summary>
    public string AnnotationsMarkdown(DateOnly day)
    {
        var text = new StringBuilder().Append(CultureInfo.InvariantCulture, $"# Annotations\n\nExport de kyfRev du {day:yyyy-MM-dd}.\n");
        foreach (var entry in SheetsByTitle())
        {
            var annotations = VisibleAnnotations(entry.Sheet.Id);
            if (annotations.Count == 0)
            {
                continue;
            }
            text.Append(CultureInfo.InvariantCulture, $"\n## {entry.Sheet.Title}\n");
            if (entry.Sheet.Path.Parent is { } folder)
            {
                text.Append(CultureInfo.InvariantCulture, $"\n*{folder}*\n");
            }
            foreach (var annotation in annotations)
            {
                text.Append('\n');
                foreach (var line in RustLines(annotation.Anchor.Exact))
                {
                    text.Append(CultureInfo.InvariantCulture, $"> {line}\n");
                }
                if (annotation.Definition is { } definition)
                {
                    text.Append(CultureInfo.InvariantCulture, $"\n**{definition.Term}** : {definition.Explanation}\n");
                }
                if (annotation.Comment is { } comment)
                {
                    text.Append(CultureInfo.InvariantCulture, $"\n{comment}\n");
                }
                var details = new List<string>();
                if (!annotation.Anchor.Section.IsEmpty)
                {
                    details.Add(string.Join(" › ", annotation.Anchor.Section));
                }
                details.Add(annotation.Tracking.Cycle is { } cycle ? $"{cycle.StepLabel} le {cycle.DueDate:yyyy-MM-dd}" : "archivé");
                if (annotation.AnchorState == AnchorState.NeedsReview)
                {
                    details.Add("position à vérifier");
                }
                else if (annotation.AnchorState == AnchorState.NotFound)
                {
                    details.Add("introuvable dans la fiche");
                }
                text.Append(CultureInfo.InvariantCulture, $"\n*{string.Join(" · ", details)}*\n");
            }
        }
        return text.ToString();
    }

    /// <summary>Citations hors corbeille, en Markdown et en CSV.</summary>
    public (string Markdown, string Csv) QuotesExport(DateOnly day)
    {
        var markdown = new StringBuilder().Append(CultureInfo.InvariantCulture, $"# Citations\n\nExport de kyfRev du {day:yyyy-MM-dd}.\n");
        var csv = new StringBuilder("\uFEFF");
        csv.Append(CsvLine(["Texte", "Auteur", "Date", "Source", "Type", "Page", "Adresse", "Contexte", "Étiquettes", "Ajoutée le"]));
        foreach (var quote in Quotes().Quotes)
        {
            var c = quote.Content;
            markdown.Append('\n');
            foreach (var line in RustLines(c.Text))
            {
                markdown.Append(CultureInfo.InvariantCulture, $"> {line}\n");
            }
            var attribution = new StringBuilder(c.Author ?? "Auteur inconnu");
            if (c.Source.Title is { } title)
            {
                attribution.Append(CultureInfo.InvariantCulture, $", *{title}*");
            }
            if (c.Date is { } date)
            {
                attribution.Append(CultureInfo.InvariantCulture, $" ({date})");
            }
            markdown.Append(CultureInfo.InvariantCulture, $"\n— {attribution}\n");
            csv.Append(CsvLine(
            [
                CsvText(c.Text),
                CsvText(c.Author ?? string.Empty),
                CsvText(c.Date ?? string.Empty),
                CsvText(c.Source.Title ?? string.Empty),
                c.Source.Kind is { } kind ? KindDebugName(kind) : string.Empty,
                CsvText(c.Source.Page ?? string.Empty),
                CsvText(c.Source.Address ?? string.Empty),
                CsvText(c.Context ?? string.Empty),
                CsvText(string.Join(", ", c.Tags)),
                quote.AddedOn is { } added ? added.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty,
            ]));
        }
        return (markdown.ToString(), csv.ToString());
    }

    /// <summary>Un dossier existant, hors du coffre (exports et partages).</summary>
    private void CheckOutputFolder(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (!Directory.Exists(folder))
        {
            throw File.Exists(folder)
                ? VaultException.InvalidPath(folder, "ce n'est pas un dossier")
                : VaultException.Io(folder, new DirectoryNotFoundException("dossier introuvable"));
        }
        if (RealPaths.IsWithin(Io(folder, () => RealPaths.OfExistingPart(folder)), _realRoot))
        {
            throw VaultException.ExportInsideVault(folder);
        }
    }

    /// <summary>« radical.ext », sinon « radical (2).ext »… : jamais d'écrasement.</summary>
    internal static string CreateWithoutOverwriting(string folder, string stem, string extension, ReadOnlySpan<byte> content)
    {
        for (var number = 1; number < 1000; number++)
        {
            var path = Path.Combine(folder, number == 1 ? $"{stem}.{extension}" : $"{stem} ({number}).{extension}");
            if (TryCreateFile(DiskStorage.Instance, path, content))
            {
                return path;
            }
        }
        throw VaultException.AlreadyExists(folder);
    }

    /// <summary>Annotations hors corbeille d'une fiche, dans l'ordre du texte.</summary>
    private List<StoredAnnotation> VisibleAnnotations(string sheetId) =>
        [.. Annotations(sheetId).Where(a => a.TrashedAt is null).OrderBy(a => a.Anchor.Start)];

    private List<SheetEntry> SheetsByTitle() =>
        [.. _sheets.Values.OrderBy(entry => RustText.ToLowercase(entry.Sheet.Title), Utf8Order.Instance)];

    /// <summary>
    /// Texte saisi par l'utilisateur, prêt pour une cellule CSV : sur une ligne, protégé contre
    /// l'injection de formules (Excel exécute une cellule qui commence par =, +, - ou @) et entre
    /// guillemets si besoin.
    /// </summary>
    internal static string CsvText(string value)
    {
        value = value.Replace('\r', ' ').Replace('\n', ' ');
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t')
        {
            value = "'" + value;
        }
        return value.Contains(';', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    private static string CsvLine(IEnumerable<string> cells) => string.Join(';', cells) + "\r\n";

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Colonnes communes du suivi d'une fiche ou d'un passage.</summary>
    private static string[] StateCells(Tracking tracking)
    {
        var cycle = tracking.Cycle;
        var archive = tracking.Archive;
        return
        [
            archive is not null ? "archivée" : "active",
            cycle?.StepLabel ?? string.Empty,
            cycle is null ? string.Empty : Day(cycle.DueDate),
            cycle?.Number.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            cycle is null ? string.Empty : Day(cycle.LastReview),
            tracking.Counters.Started.ToString(CultureInfo.InvariantCulture),
            tracking.Counters.Completed.ToString(CultureInfo.InvariantCulture),
            archive is null ? string.Empty : Day(archive.ArchivedOn),
            archive is { IncludedInReminders: true } ? Day(archive.EligibleOn) : string.Empty,
        ];
    }

    /// <summary>Une ligne d'historique : événement, date, cycle, étape, prévue le, retard, détail.</summary>
    private static string[] EventCells(TrackingEvent trackingEvent, TimeZoneInfo zone)
    {
        string Local(DateTimeOffset instant) => Day(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime));
        string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        return trackingEvent switch
        {
            ReviewEvent e =>
            [
                e.Origin switch
                {
                    null => "Révision",
                    StartOrigin.Creation => "J0 : création",
                    StartOrigin.Adoption => "J0 : fiche suivie",
                    StartOrigin.Reminder => "J0 : rappel « à revoir entièrement »",
                    _ => "J0 : recommencé",
                },
                Day(e.Day), N(e.CycleNumber), e.StepLabel, Day(e.PlannedOn), N(e.DaysLate),
                e.Forgotten ? "oubliée" : string.Empty,
            ],
            PostponementEvent e => ["Report", Local(e.At), N(e.CycleNumber), e.StepLabel, Day(e.From), string.Empty, $"reportée au {Day(e.To)}"],
            InterruptionEvent e => ["Cycle interrompu", Day(e.Day), N(e.CycleNumber), string.Empty, string.Empty, string.Empty, string.Empty],
            ArchivingEvent e => ["Archivage", Day(e.Day), string.Empty, string.Empty, string.Empty, string.Empty, e.Reason == ArchiveReason.EndOfCycle ? "fin de cycle" : "à la main"],
            ReactivationEvent e => ["Remise en révision", Day(e.Day), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty],
            ReminderEvent e =>
            [
                "Rappel de consolidation", Day(e.Day), string.Empty, string.Empty, string.Empty, string.Empty,
                $"{e.Outcome switch
                {
                    ReminderOutcome.Known => "fiche connue",
                    ReminderOutcome.SomePointsToReview => "certains points à revoir",
                    _ => "à revoir entièrement",
                }} (tiré le {Day(e.DrawnOn)})",
            ],
            ReminderInclusionEvent e => [e.Include ? "Réinclusion dans les rappels" : "Exclusion des rappels", Local(e.At), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty],
            ModificationEvent e =>
            [
                e.Changed switch
                {
                    ModifiedObject.Comment => "Commentaire modifié",
                    ModifiedObject.Passage => "Passage modifié",
                    _ => "Définition modifiée",
                },
                Local(e.At), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
            ],
            _ => throw new InvalidOperationException("Événement inconnu."),
        };
    }

    /// <summary>Nom de variante Rust en minuscules, comme l'export de la 0.x (« emission »).</summary>
    private static string KindDebugName(SourceKind kind) => kind switch
    {
        SourceKind.Book => "livre",
        SourceKind.Newspaper => "journal",
        SourceKind.Article => "article",
        SourceKind.Speech => "discours",
        SourceKind.Website => "site",
        SourceKind.Film => "film",
        SourceKind.Song => "chanson",
        SourceKind.Broadcast => "emission",
        SourceKind.Other => "autre",
        _ => "inconnue",
    };

    /// <summary>Lignes comme <c>str::lines</c> : « \r » final retiré, pas de ligne vide finale.</summary>
    private static IEnumerable<string> RustLines(string text)
    {
        var lines = text.Split('\n');
        var count = text.EndsWith('\n') ? lines.Length - 1 : lines.Length;
        return lines.Take(count).Select(line => line.EndsWith('\r') ? line[..^1] : line);
    }
}
