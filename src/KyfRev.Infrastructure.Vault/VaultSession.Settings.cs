using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault;

public sealed partial class VaultSession
{
    /// <summary>Taille maximale des données d'une extension : 1 Mo.</summary>
    public const int MaxExtensionDataBytes = 1024 * 1024;

    /// <summary>Lignes gardées dans le journal d'une extension : les plus anciennes partent au-delà.</summary>
    public const int MaxJournalLines = 1000;

    /// <summary>Taille maximale d'une ligne du journal d'une extension : 4 Ko.</summary>
    public const int MaxJournalLineBytes = 4096;

    private static readonly VaultPath SettingsPath = VaultPath.Parse(VaultLayout.SettingsFile);

    /// <summary>
    /// Module Schémas réseau actif : le choix enregistré, sinon actif seulement si le dossier
    /// <c>schemas</c> contient déjà un schéma (un coffre neuf ne l'a pas).
    /// </summary>
    public bool SchemasActive => Settings.SchemasModule ?? HasSchemas();

    /// <summary>Réglages tels que l'écran Paramètres les montre.</summary>
    public VaultPreferences Preferences => new(
        Settings.Sequence.Values[1..],
        Settings.ActiveRecall,
        Settings.Reminders.MaxPerDay,
        Settings.Reminders.MaxWaitDays,
        Settings.Reminders.RestSequenceMonths,
        Settings.Reminders.IncludeManualArchiving,
        Settings.BackupRotation,
        Settings.Quotes.Active,
        Settings.Quotes.Online,
        Settings.Quotes.Domains,
        SchemasActive);

    /// <summary>
    /// Enregistre de nouveaux réglages, vérifiés par les règles de la méthode des J. Une séquence
    /// modifiée ne touche pas les cycles en cours (ils en gardent une copie).
    /// </summary>
    /// <exception cref="VaultException">Réglages refusés : rien n'est écrit.</exception>
    public void ChangePreferences(VaultPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var creation = ReviewSequence.Create([0, .. preferences.Steps]);
        if (!creation.IsSuccess)
        {
            throw VaultException.InvalidSettings(creation.Error.Message);
        }
        if (preferences.MaxPerDay == 0)
        {
            throw VaultException.InvalidSettings("il faut au moins un rappel par jour.");
        }
        var rules = Settings.Reminders with
        {
            MaxPerDay = preferences.MaxPerDay,
            MaxWaitDays = preferences.MaxWaitDays,
            RestSequenceMonths = preferences.RestSequenceMonths,
            IncludeManualArchiving = preferences.IncludeManualArchiving,
        };
        if (rules.Validate() is { } error)
        {
            throw VaultException.Refused(error);
        }
        var domains = QuoteSettings.AllDomains
            .Where(domain => preferences.QuoteDomains.Any(name => Ascii.EqualsIgnoreCase(name.Trim(), domain)))
            .ToImmutableArray();
        if (preferences.QuotesOnline && domains.IsEmpty)
        {
            throw VaultException.InvalidSettings("choisissez au moins un domaine pour la citation du jour.");
        }
        // Enregistré seulement s'il change : un coffre sans choix garde la règle par défaut.
        var schemas = preferences.SchemasActive != SchemasActive ? preferences.SchemasActive : Settings.SchemasModule;
        SaveSettings(Settings with
        {
            Sequence = creation.Sequence,
            ActiveRecall = preferences.ActiveRecall,
            Reminders = rules,
            BackupRotation = preferences.BackupRotation,
            Quotes = new QuoteSettings(preferences.QuotesActive, preferences.QuotesOnline, domains),
            SchemasModule = schemas,
        });
    }

    /// <summary>Données enregistrées par une extension (texte JSON), ou rien.</summary>
    public string? ExtensionData(string id)
    {
        var path = Confine(ExtensionDataPath(id));
        if (!_storage.FileExists(path))
        {
            return null;
        }
        var bytes = ReadAllBytes(path);
        return TryDecode(bytes, out var text) ? text : throw VaultException.Io(path, new InvalidDataException("fichier non encodé en UTF-8"));
    }

    /// <summary>Remplace les données d'une extension : du JSON valide, 1 Mo au plus.</summary>
    public void SaveExtensionData(string id, string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var path = ExtensionDataPath(id);
        var bytes = Encoding.UTF8.GetBytes(json);
        if (bytes.Length > MaxExtensionDataBytes)
        {
            throw VaultException.InvalidSettings("données d'extension de plus de 1 Mo");
        }
        try
        {
            using var document = JsonDocument.Parse(bytes);
        }
        catch (JsonException exception)
        {
            throw VaultException.InvalidSettings($"données d'extension illisibles : {exception.Message}");
        }
        var absolute = Confine(path);
        CreateDirectory(Path.GetDirectoryName(absolute)!);
        ReplaceFile(absolute, bytes);
    }

    private void SaveSettings(VaultSettingsFile settings)
    {
        ReplaceFile(Confine(SettingsPath), settings.Write());
        Settings = settings;
    }

    private bool HasSchemas()
    {
        var folder = VaultLayout.Absolute(Root, VaultLayout.Schemas);
        try
        {
            return _storage.DirectoryExists(folder) && _storage.Entries(folder).Any(entry => Extension(entry.Name) == "json");
        }
        catch (Exception exception) when (IsIo(exception))
        {
            return false;
        }
    }

    /// <summary>Le journal de ce qu'une extension a écrit dans le coffre : une ligne JSON par écriture ; rien s'il est vide.</summary>
    public string? ExtensionJournal(string id)
    {
        var path = Confine(ExtensionJournalPath(id));
        if (!_storage.FileExists(path))
        {
            return null;
        }
        var bytes = ReadAllBytes(path);
        return TryDecode(bytes, out var text) ? text : throw VaultException.Io(path, new InvalidDataException("fichier non encodé en UTF-8"));
    }

    /// <summary>Ajoute une ligne au journal d'une extension : du JSON sur une ligne. Les plus anciennes partent au-delà de mille.</summary>
    public void AppendExtensionJournal(string id, string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Contains('\n', StringComparison.Ordinal) || Encoding.UTF8.GetByteCount(line) > MaxJournalLineBytes)
        {
            throw VaultException.InvalidSettings("ligne de journal sur plusieurs lignes, ou de plus de 4 Ko");
        }
        try
        {
            using var document = JsonDocument.Parse(line);
        }
        catch (JsonException exception)
        {
            throw VaultException.InvalidSettings($"ligne de journal illisible : {exception.Message}");
        }
        var lines = (ExtensionJournal(id) ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        lines.Add(line);
        var kept = lines.Skip(Math.Max(0, lines.Count - MaxJournalLines));
        var absolute = Confine(ExtensionJournalPath(id));
        CreateDirectory(Path.GetDirectoryName(absolute)!);
        ReplaceFile(absolute, Encoding.UTF8.GetBytes(string.Join('\n', kept) + "\n"));
    }

    private static VaultPath ExtensionJournalPath(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return VaultIds.IsExtensionId(id)
            ? VaultPath.Create($"{VaultLayout.ExtensionJournals}/{id}.jsonl")
            : throw VaultException.InvalidPath(id, "identifiant d'extension invalide");
    }

    private static VaultPath ExtensionDataPath(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return VaultIds.IsExtensionId(id)
            ? VaultPath.Create($"{VaultLayout.ExtensionData}/{id}.json")
            : throw VaultException.InvalidPath(id, "identifiant d'extension invalide");
    }
}
