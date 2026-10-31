using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Quotes;

namespace KyfRev.Infrastructure.Vault.Tests.Scenarios;

/// <summary>Opérations des scénarios sur les annotations, les rappels, les réglages, les citations, les schémas et les modèles.</summary>
internal sealed partial class ScenarioRunner
{
    private string ExecuteComplements(string op, JsonElement operation, Moment now)
    {
        switch (op)
        {
            case "creer_annotation":
                _names[Text(operation, "nom")] = Vault.CreateAnnotation(Ref(operation, "fiche"), Anchor(operation), Raw(operation, "commentaire"), now);
                return string.Empty;
            case "creer_definition":
                _names[Text(operation, "nom")] = Vault.CreateDefinition(Ref(operation, "fiche"), Anchor(operation), Definition(operation), now);
                return string.Empty;
            case "modifier_definition":
                Vault.ModifyDefinition(Ref(operation, "annotation"), Definition(operation), now);
                return string.Empty;
            case "modifier_commentaire":
                Vault.ModifyComment(Ref(operation, "annotation"), Raw(operation, "commentaire"), now);
                return string.Empty;
            case "reancrer":
                {
                    var state = Raw(operation, "etat") switch
                    {
                        "a_verifier" => AnchorState.NeedsReview,
                        "introuvable" => AnchorState.NotFound,
                        _ => AnchorState.Anchored,
                    };
                    Vault.Reanchor(Ref(operation, "annotation"), Anchor(operation), state, now);
                    return string.Empty;
                }
            case "modifier_passage":
                Vault.ModifyPassage(Ref(operation, "annotation"), Anchor(operation), operation.TryGetProperty("recommencer", out var restart) && restart.GetBoolean(), now);
                return string.Empty;
            case "corbeille_annotation":
                Vault.TrashAnnotation(Ref(operation, "annotation"), now);
                return string.Empty;
            case "restaurer_annotation":
                Vault.RestoreAnnotation(Ref(operation, "annotation"), now);
                return string.Empty;
            case "reviser_annotation":
                Vault.ApplyAnnotationReview(Ref(operation, "annotation"), tracking => tracking.ValidateReview(now, null, null));
                return string.Empty;
            case "reporter_annotation":
                {
                    var to = Day(Raw(operation, "vers")!);
                    Vault.ApplyToAnnotationTracking(Ref(operation, "annotation"), tracking => tracking.Postpone(now, to));
                    return string.Empty;
                }
            case "annotation":
                return Describe(Vault.Annotation(Ref(operation, "annotation")));
            case "annotations_a_verifier":
                return Vault.AnnotationsToCheck.ToString(CultureInfo.InvariantCulture);
            case "passages_marques":
                return Vault.MarkedPassages(Ref(operation, "fiche"), now.Day).ToString(CultureInfo.InvariantCulture);
            case "traiter_rappel":
                {
                    var choice = Raw(operation, "choix") switch
                    {
                        "certains_points_a_revoir" => ReminderOutcome.SomePointsToReview,
                        "a_revoir_entierement" => ReminderOutcome.ReviewEntirely,
                        _ => ReminderOutcome.Known,
                    };
                    Vault.HandleReminder(Ref(operation, "fiche"), choice, now);
                    return string.Empty;
                }
            case "inclure_dans_rappels":
                Vault.IncludeInReminders(Ref(operation, "fiche"), operation.GetProperty("inclure").GetBoolean(), now);
                return string.Empty;
            case "preparer_journee":
                {
                    var day = Vault.PrepareDay(now);
                    return day.AlreadyPrepared ? "deja" : $"[{string.Join(';', day.NewReminders)}] {day.UnservedPriority}";
                }
            case "reglages":
                {
                    var p = Vault.Preferences;
                    return $"etapes={Debug(p.Steps)} rappel_actif={Bool(p.ActiveRecall)} max_par_jour={p.MaxPerDay} attente={p.MaxWaitDays} repos={Debug(p.RestSequenceMonths)}"
                        + $" inclure={Bool(p.IncludeManualArchiving)} rotation={Bool(p.BackupRotation)} citations={Bool(p.QuotesActive)} en_ligne={Bool(p.QuotesOnline)}"
                        + $" domaines={List(p.QuoteDomains)} schemas={Bool(p.SchemasActive)}";
                }
            case "modifier_reglages":
                {
                    var p = Vault.Preferences;
                    if (operation.TryGetProperty("etapes", out var steps))
                    {
                        p = p with { Steps = Numbers(steps) };
                    }
                    if (operation.TryGetProperty("rappel_actif", out var recall))
                    {
                        p = p with { ActiveRecall = recall.GetBoolean() };
                    }
                    if (operation.TryGetProperty("max_par_jour", out var max))
                    {
                        p = p with { MaxPerDay = max.GetInt32() };
                    }
                    if (operation.TryGetProperty("attente_max_jours", out var wait))
                    {
                        p = p with { MaxWaitDays = wait.GetInt32() };
                    }
                    if (operation.TryGetProperty("sequence_repos_mois", out var rest))
                    {
                        p = p with { RestSequenceMonths = Numbers(rest) };
                    }
                    if (operation.TryGetProperty("inclure_archivage_manuel", out var include))
                    {
                        p = p with { IncludeManualArchiving = include.GetBoolean() };
                    }
                    if (operation.TryGetProperty("rotation_des_sauvegardes", out var rotation))
                    {
                        p = p with { BackupRotation = rotation.GetBoolean() };
                    }
                    if (operation.TryGetProperty("citations_actives", out var quotes))
                    {
                        p = p with { QuotesActive = quotes.GetBoolean() };
                    }
                    if (operation.TryGetProperty("citations_en_ligne", out var online))
                    {
                        p = p with { QuotesOnline = online.GetBoolean() };
                    }
                    if (operation.TryGetProperty("domaines_citations", out var domains))
                    {
                        p = p with { QuoteDomains = Strings(domains) };
                    }
                    if (operation.TryGetProperty("schemas_actifs", out var schemas))
                    {
                        p = p with { SchemasActive = schemas.GetBoolean() };
                    }
                    Vault.ChangePreferences(p);
                    return string.Empty;
                }
            case "definir_examen":
                Vault.DefineExam(Raw(operation, "dossier") ?? string.Empty, Raw(operation, "nom_examen") ?? string.Empty, Day(Raw(operation, "jour")!));
                return string.Empty;
            case "retirer_examen":
                Vault.RemoveExam(Raw(operation, "dossier") ?? string.Empty);
                return string.Empty;
            case "examens_a_venir":
                return string.Join(';', Vault.UpcomingExams(now.Day).Select(e =>
                    $"{Json(e.Folder)} {Json(e.Name)} {e.Day:yyyy-MM-dd} {e.DaysLeft} {e.Items} {e.AfterExam}"));
            case "resserrer":
                return Vault.TightenBeforeExam(Raw(operation, "dossier") ?? string.Empty, now).ToString(CultureInfo.InvariantCulture);
            case "enregistrer_donnees_extension":
                Vault.SaveExtensionData(Raw(operation, "extension") ?? string.Empty, Text(operation, "texte"));
                return string.Empty;
            case "donnees_extension":
                return Optional(Vault.ExtensionData(Raw(operation, "extension") ?? string.Empty));
            case "ajouter_citation":
                {
                    var quote = Vault.AddQuote(Content(operation), now);
                    _names[Text(operation, "nom")] = quote.Path;
                    return Describe(quote);
                }
            case "modifier_citation":
                return Describe(Vault.ModifyQuote(Ref(operation, "citation"), Content(operation), now));
            case "corbeille_citation":
            case "restaurer_citation":
                {
                    var name = Raw(operation, "citation")!;
                    var path = Ref(operation, "citation");
                    var moved = op == "corbeille_citation" ? Vault.TrashQuote(path) : Vault.RestoreQuote(path);
                    if (_names.ContainsKey(name))
                    {
                        _names[name] = moved;
                    }
                    return moved;
                }
            case "citations":
                {
                    var list = Vault.Quotes();
                    return $"citations=[{string.Join(';', list.Quotes.Select(q => q.Path))}] corbeille=[{string.Join(';', list.Trash.Select(q => q.Path))}]"
                        + $" illisibles=[{string.Join(';', list.Unreadable.Select(u => u.Path))}]";
                }
            case "lire_citation":
                {
                    var path = Ref(operation, "citation");
                    var list = Vault.Quotes();
                    return list.Quotes.Concat(list.Trash).FirstOrDefault(q => q.Path == path) is { } quote ? Describe(quote) : "absente";
                }
            case "retenir_citation_en_ligne":
                {
                    var found = operation.TryGetProperty("trouvees", out var items) ? items.EnumerateArray().Select(Content).ToList() : [];
                    if (Vault.KeepOnlineQuote(Raw(operation, "page") ?? string.Empty, found, now) is not { } quote)
                    {
                        return "aucune";
                    }
                    if (Raw(operation, "nom") is { } name)
                    {
                        _names[name] = quote.Path;
                    }
                    return Describe(quote);
                }
            case "creer_schema":
                {
                    var schema = Vault.CreateSchema(Text(operation, "texte"));
                    _names[Text(operation, "nom")] = schema.Id;
                    return schema.Fingerprint;
                }
            case "dupliquer_schema":
                {
                    var schema = Vault.DuplicateSchema(Ref(operation, "schema"));
                    _names[Text(operation, "nom")] = schema.Id;
                    return schema.Fingerprint;
                }
            case "enregistrer_schema":
                {
                    var id = Ref(operation, "schema");
                    var expected = Raw(operation, "empreinte") == "actuelle" ? TryReadSchema(id) : Raw(operation, "empreinte")!;
                    return Vault.SaveSchema(id, Text(operation, "texte"), expected);
                }
            case "lire_schema":
                {
                    var schema = Vault.ReadSchema(Ref(operation, "schema"));
                    return $"{schema.Fingerprint} {Json(schema.Text)}";
                }
            case "modeles":
                return string.Join(';', Vault.Templates().Select(t => $"{Json(t.Name)} {Bool(t.BuiltIn)} {Json(t.Description)}"));
            case "corps_du_modele":
                return Json(Vault.TemplateBody(Raw(operation, "modele") ?? string.Empty, Raw(operation, "titre") ?? string.Empty, Raw(operation, "dossier") ?? string.Empty, now.Day));
            case "enregistrer_modele":
                return Json(Vault.SaveAsTemplate(Ref(operation, "fiche"), Raw(operation, "nom_modele") ?? string.Empty));
            default:
                throw new InvalidOperationException($"Opération inconnue : {op}");
        }
    }

    private string Ref(JsonElement operation, string key)
    {
        var name = operation.GetProperty(key).GetString()!;
        return _names.GetValueOrDefault(name, name);
    }

    private static string? Raw(JsonElement operation, string key) =>
        operation.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private string TryReadSchema(string id)
    {
        try
        {
            return Vault.ReadSchema(id).Fingerprint;
        }
        catch (VaultException)
        {
            return string.Empty;
        }
    }

    private static StoredAnchor Anchor(JsonElement operation)
    {
        var anchor = operation.GetProperty("ancre");
        return new StoredAnchor(
            Raw(anchor, "exact") ?? string.Empty,
            Raw(anchor, "avant") ?? string.Empty,
            Raw(anchor, "apres") ?? string.Empty,
            anchor.TryGetProperty("debut", out var start) ? start.GetInt64() : 0,
            anchor.TryGetProperty("fin", out var end) ? end.GetInt64() : 0,
            anchor.TryGetProperty("section", out var section) ? Strings(section) : [],
            anchor.TryGetProperty("ligne_source", out var line) ? line.GetInt64() : null,
            Raw(anchor, "empreinte_fiche") ?? string.Empty,
            UnknownProperties.None);
    }

    private static StoredDefinition Definition(JsonElement operation) =>
        new(Raw(operation, "terme") ?? string.Empty, Raw(operation, "explication") ?? string.Empty);

    private static QuoteContent Content(JsonElement value)
    {
        var source = value.TryGetProperty("source", out var s) ? s : default;
        string? SourceField(string key) => source.ValueKind == JsonValueKind.Object ? Raw(source, key) : null;
        SourceKind? kind = SourceField("type") switch
        {
            null => null,
            "livre" => SourceKind.Book,
            "journal" => SourceKind.Newspaper,
            "article" => SourceKind.Article,
            "discours" => SourceKind.Speech,
            "site" => SourceKind.Website,
            "film" => SourceKind.Film,
            "chanson" => SourceKind.Song,
            "emission" => SourceKind.Broadcast,
            "inconnue" => SourceKind.Unknown,
            _ => SourceKind.Other,
        };
        return new QuoteContent(
            Raw(value, "texte") ?? string.Empty,
            Raw(value, "auteur"),
            Raw(value, "date"),
            new QuoteSource(kind, SourceField("titre"), SourceField("page"), SourceField("url")),
            Raw(value, "contexte"),
            value.TryGetProperty("etiquettes", out var tags) ? Strings(tags) : []);
    }

    private static string Describe(StoredAnnotation a)
    {
        var state = a.AnchorState switch
        {
            AnchorState.Anchored => "Ancree",
            AnchorState.NeedsReview => "AVerifier",
            _ => "Introuvable",
        };
        var tracking = a.Tracking.Cycle is { } cycle ? $"cycle {cycle.Number} {cycle.Step} {cycle.DueDate:yyyy-MM-dd}" : "archive";
        var trashed = a.TrashedAt is { } at ? InstantText.Format(at) : "-";
        return $"{a.SheetId} {Json(a.Anchor.Exact)} {state} {Optional(a.Comment)} {Optional(a.Definition?.Term)} {Optional(a.Definition?.Explanation)} {trashed} {a.Anchor.Start} {a.Anchor.End} {tracking}";
    }

    private static string Describe(Quote q)
    {
        var s = q.Content.Source;
        var kind = s.Kind switch
        {
            null => "-",
            SourceKind.Book => "Livre",
            SourceKind.Newspaper => "Journal",
            SourceKind.Article => "Article",
            SourceKind.Speech => "Discours",
            SourceKind.Website => "Site",
            SourceKind.Film => "Film",
            SourceKind.Song => "Chanson",
            SourceKind.Broadcast => "Emission",
            SourceKind.Other => "Autre",
            _ => "Inconnue",
        };
        return $"{q.Path} {q.Id ?? "-"} {(q.Year is { } year ? year.ToString(CultureInfo.InvariantCulture) : "-")} {(q.AddedOn is { } day ? day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "-")} {q.Origin ?? "-"}"
            + $" {Json(q.Content.Text)} {Optional(q.Content.Author)} {Optional(q.Content.Date)} {kind} {Optional(s.Title)} {Optional(s.Page)} {Optional(s.Address)}"
            + $" {Optional(q.Content.Context)} {List(q.Content.Tags)}";
    }

    private static ImmutableArray<int> Numbers(JsonElement array) => [.. array.EnumerateArray().Select(n => n.GetInt32())];

    private static ImmutableArray<string> Strings(JsonElement array) => [.. array.EnumerateArray().Select(t => t.GetString()!)];

    private static string Debug(IEnumerable<int> values) => $"[{string.Join(", ", values)}]";

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Optional(string? text) => text is null ? "null" : Json(text);

    private static DateOnly Day(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
