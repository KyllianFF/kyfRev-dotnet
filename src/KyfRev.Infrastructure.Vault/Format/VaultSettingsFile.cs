using System.Collections.Immutable;
using System.Text.Json.Nodes;
using KyfRev.Application.Text;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Json;

namespace KyfRev.Infrastructure.Vault.Format;

/// <summary>Réglages du module Citations.</summary>
/// <param name="Active">Module actif : menu et citation du jour.</param>
/// <param name="Online">Citation du jour cherchée sur Wikiquote.</param>
/// <param name="Domains">Domaines de la citation en ligne.</param>
public sealed record QuoteSettings(bool Active, bool Online, ImmutableArray<string> Domains)
{
    /// <summary>Domaines proposés par défaut.</summary>
    public static ImmutableArray<string> AllDomains { get; } = ["philosophie", "art", "science", "informatique"];
}

/// <summary>Date d'examen d'un dossier (vide : tout le coffre).</summary>
public sealed record ExamSetting(string Folder, string Name, DateOnly Day, UnknownProperties Unknown);

/// <summary>
/// <c>.suivi/coffre.json</c> : réglages propres au coffre. Sa présence fait d'un dossier un coffre.
/// Les valeurs absentes prennent les mêmes défauts que dans la version 0.x.
/// </summary>
/// <param name="Format">Version du format.</param>
/// <param name="Id">Identifiant du coffre (<c>v_</c> + ULID), graine des tirages : jamais régénéré.</param>
/// <param name="CreatedOn">Date de création.</param>
/// <param name="Sequence">Séquence des J des nouveaux cycles.</param>
/// <param name="ActiveRecall">Rappel actif pour les passages (D29).</param>
/// <param name="Reminders">Règles des rappels de consolidation.</param>
/// <param name="Quotes">Module Citations.</param>
/// <param name="BackupRotation">Rotation des sauvegardes (14 j / 8 sem. / 24 mois).</param>
/// <param name="SchemasModule">Module Schémas réseau : choix enregistré, ou <see langword="null"/>.</param>
/// <param name="Exams">Examens, du plus proche au plus lointain.</param>
/// <param name="SectionUnknown">Propriétés inconnues de chaque section (« revision », « rappels »…).</param>
/// <param name="Unknown">Réglages inconnus du premier niveau, réécrits triés comme le fait la version 0.x.</param>
public sealed record VaultSettingsFile(
    long Format,
    string Id,
    DateOnly CreatedOn,
    ReviewSequence Sequence,
    bool ActiveRecall,
    ReminderRules Reminders,
    QuoteSettings Quotes,
    bool BackupRotation,
    bool? SchemasModule,
    ImmutableArray<ExamSetting> Exams,
    ImmutableDictionary<string, UnknownProperties> SectionUnknown,
    UnknownProperties Unknown)
{
    private static readonly string[] Known = ["format", "id", "creeLe", "revision", "rappels", "citations", "sauvegardes", "modules", "examens"];

    /// <summary>Lit le fichier.</summary>
    /// <exception cref="VaultFormatException">Le fichier est illisible : le coffre ne peut pas s'ouvrir.</exception>
    public static VaultSettingsFile Read(ReadOnlySpan<byte> json)
    {
        var obj = Fields.ParseObject(json);
        var sections = ImmutableDictionary.CreateBuilder<string, UnknownProperties>(StringComparer.Ordinal);

        var sequence = ReviewSequence.Default;
        var activeRecall = true;
        if (Fields.OptionalObject(obj, "revision") is { } revision)
        {
            var creation = ReviewSequence.Create(Fields.Array(revision, "sequence")
                .Select(item => (int)Fields.IntegerValue(item ?? throw new VaultFormatException("« sequence » contient null"), "sequence", 0, ushort.MaxValue)));
            sequence = creation.IsSuccess ? creation.Sequence : throw new VaultFormatException(creation.Error.Message);
            activeRecall = Fields.OptionalBoolean(revision, "rappelActif", fallback: true);
            sections["revision"] = UnknownProperties.From(Fields.Unknown(revision, "sequence", "rappelActif"));
        }

        var reminders = ReminderRules.Default;
        if (Fields.OptionalObject(obj, "rappels") is { } rules)
        {
            reminders = new ReminderRules(
                (int)Fields.Integer(rules, "delaiMinimalMois", 0, byte.MaxValue),
                [.. Fields.Array(rules, "sequenceReposMois").Select(item => (int)Fields.IntegerValue(item ?? throw new VaultFormatException("« sequenceReposMois » contient null"), "sequenceReposMois", 0, byte.MaxValue))],
                (int)Fields.Integer(rules, "maxParJour", 0, byte.MaxValue),
                (int)Fields.Integer(rules, "attenteMaxJours", 0, ushort.MaxValue),
                Fields.Boolean(rules, "inclureArchivageManuel"));
            sections["rappels"] = UnknownProperties.From(Fields.Unknown(rules, "delaiMinimalMois", "sequenceReposMois", "maxParJour", "attenteMaxJours", "inclureArchivageManuel"));
        }

        var quotes = new QuoteSettings(Active: false, Online: true, QuoteSettings.AllDomains);
        if (Fields.OptionalObject(obj, "citations") is { } citations)
        {
            quotes = new QuoteSettings(
                Fields.Boolean(citations, "active"),
                Fields.OptionalBoolean(citations, "enLigne", fallback: true),
                Fields.Has(citations, "domaines") ? Fields.Strings(Fields.Array(citations, "domaines"), "domaines") : QuoteSettings.AllDomains);
            sections["citations"] = UnknownProperties.From(Fields.Unknown(citations, "active", "enLigne", "domaines"));
        }

        var rotation = true;
        if (Fields.OptionalObject(obj, "sauvegardes") is { } backups)
        {
            rotation = Fields.Boolean(backups, "rotation");
            sections["sauvegardes"] = UnknownProperties.From(Fields.Unknown(backups, "rotation"));
        }

        bool? schemas = null;
        if (Fields.OptionalObject(obj, "modules") is { } modules)
        {
            schemas = modules.TryGetPropertyValue("schemas", out var value) && value is not null ? Fields.Boolean(modules, "schemas") : null;
            sections["modules"] = UnknownProperties.From(Fields.Unknown(modules, "schemas"));
        }

        var exams = (Fields.OptionalArray(obj, "examens") ?? [])
            .Select(item => item as JsonObject ?? throw new VaultFormatException("un examen doit être un objet"))
            .Select(exam => new ExamSetting(
                Fields.String(exam, "dossier"),
                Fields.String(exam, "nom"),
                Fields.Day(exam, "jour"),
                UnknownProperties.From(Fields.Unknown(exam, "dossier", "nom", "jour"))))
            .ToImmutableArray();

        return new VaultSettingsFile(
            Fields.Integer(obj, "format", 0, uint.MaxValue),
            Fields.String(obj, "id"),
            Fields.Day(obj, "creeLe"),
            sequence,
            activeRecall,
            reminders,
            quotes,
            rotation,
            schemas,
            exams,
            sections.ToImmutable(),
            UnknownProperties.From(Fields.Unknown(obj, Known)));
    }

    /// <summary>Réglages d'un nouveau coffre : les défauts de la version 0.x.</summary>
    /// <param name="id">Identifiant du coffre (<c>v_</c> + ULID).</param>
    /// <param name="createdOn">Jour de création.</param>
    public static VaultSettingsFile New(string id, DateOnly createdOn) => new(
        1,
        id,
        createdOn,
        ReviewSequence.Default,
        ActiveRecall: true,
        ReminderRules.Default,
        new QuoteSettings(Active: false, Online: true, QuoteSettings.AllDomains),
        BackupRotation: true,
        SchemasModule: null,
        Exams: [],
        ImmutableDictionary.Create<string, UnknownProperties>(StringComparer.Ordinal),
        UnknownProperties.None);

    /// <summary>Contenu du fichier, octet pour octet comme la version 0.x l'écrirait.</summary>
    public byte[] Write()
    {
        var obj = new JsonObject
        {
            ["format"] = Format,
            ["id"] = Id,
            ["creeLe"] = Fields.DayText(CreatedOn),
            ["revision"] = Section(
                "revision",
                new JsonObject
                {
                    ["sequence"] = new JsonArray([.. Sequence.Values.Select(value => (JsonNode?)JsonValue.Create(value))]),
                    ["rappelActif"] = ActiveRecall,
                }),
            ["rappels"] = Section(
                "rappels",
                new JsonObject
                {
                    ["delaiMinimalMois"] = Reminders.MinimumDelayMonths,
                    ["sequenceReposMois"] = new JsonArray([.. Reminders.RestSequenceMonths.Select(value => (JsonNode?)JsonValue.Create(value))]),
                    ["maxParJour"] = Reminders.MaxPerDay,
                    ["attenteMaxJours"] = Reminders.MaxWaitDays,
                    ["inclureArchivageManuel"] = Reminders.IncludeManualArchiving,
                }),
            ["citations"] = Section(
                "citations",
                new JsonObject
                {
                    ["active"] = Quotes.Active,
                    ["enLigne"] = Quotes.Online,
                    ["domaines"] = Fields.StringArray(Quotes.Domains),
                }),
            ["sauvegardes"] = Section("sauvegardes", new JsonObject { ["rotation"] = BackupRotation }),
        };

        var modules = new JsonObject();
        if (SchemasModule is { } schemas)
        {
            modules["schemas"] = schemas;
        }
        Section("modules", modules);
        if (modules.Count > 0)
        {
            obj["modules"] = modules;
        }

        if (!Exams.IsEmpty)
        {
            obj["examens"] = new JsonArray([.. Exams.Select(exam =>
            {
                var item = new JsonObject { ["dossier"] = exam.Folder, ["nom"] = exam.Name, ["jour"] = Fields.DayText(exam.Day) };
                Fields.AddUnknown(item, exam.Unknown.Properties);
                return (JsonNode?)item;
            })]);
        }

        // Comme la version 0.x : les réglages inconnus suivent, triés par ordre des octets UTF-8.
        Fields.AddUnknown(obj, Unknown.Properties.OrderBy(pair => pair.Key, Utf8Order.Instance));
        return SerdeJson.PrettyFile(obj);
    }

    private JsonObject Section(string name, JsonObject section)
    {
        if (SectionUnknown.TryGetValue(name, out var unknown))
        {
            Fields.AddUnknown(section, unknown.Properties);
        }
        return section;
    }
}
