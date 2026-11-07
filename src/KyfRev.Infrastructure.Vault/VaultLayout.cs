namespace KyfRev.Infrastructure.Vault;

/// <summary>Emplacements du format 1 du coffre, relatifs à sa racine.</summary>
public static class VaultLayout
{
    /// <summary>Version du format que cette application lit et écrit.</summary>
    public const long SupportedFormat = 1;

    /// <summary>Fiches Markdown.</summary>
    public const string Sheets = "fiches";

    /// <summary>Corbeille des fiches, dans <see cref="Sheets"/>.</summary>
    public const string SheetsTrash = "fiches/.supprimees";

    /// <summary>Suffixe du dossier des pièces jointes d'une fiche (« X.md » → « X.ressources »).</summary>
    public const string ResourcesSuffix = ".ressources";

    /// <summary>Citations (module facultatif).</summary>
    public const string Quotes = "citations";

    /// <summary>Schémas réseau (module facultatif).</summary>
    public const string Schemas = "schemas";

    /// <summary>Données de suivi de l'application.</summary>
    public const string Tracking = ".suivi";

    /// <summary>Réglages du coffre ; leur présence fait d'un dossier un coffre.</summary>
    public const string SettingsFile = ".suivi/coffre.json";

    /// <summary>Suivis des fiches.</summary>
    public const string SheetTracking = ".suivi/fiches";

    /// <summary>Annotations, un fichier par fiche.</summary>
    public const string Annotations = ".suivi/annotations";

    /// <summary>Versions des fiches.</summary>
    public const string Versions = ".suivi/versions";

    /// <summary>Suivis et annotations des fiches à la corbeille.</summary>
    public const string TrashTracking = ".suivi/corbeille";

    /// <summary>Données des extensions, un fichier JSON par extension.</summary>
    public const string ExtensionData = ".suivi/extensions";

    /// <summary>Journaux de ce que les extensions ont écrit dans les fiches, un fichier JSON Lines par extension.</summary>
    public const string ExtensionJournals = ".suivi/extensions/journaux";

    /// <summary>Journal d'opération sur plusieurs fichiers.</summary>
    public const string OperationFile = ".suivi/operation.json";

    /// <summary>Tirages quotidiens des rappels.</summary>
    public const string DrawsFile = ".suivi/rappels.json";

    /// <summary>Citation du jour trouvée en ligne.</summary>
    public const string OnlineQuoteFile = ".suivi/citation-en-ligne.json";

    /// <summary>Fichier verrouillé par le système tant que le coffre est ouvert.</summary>
    public const string LockFile = ".suivi/verrou";

    /// <summary>Qui tient le coffre ouvert.</summary>
    public const string LockInfoFile = ".suivi/verrou-info.json";

    /// <summary>En-tête d'un coffre chiffré, en clair et sans rien de secret : sa présence fait d'un dossier un coffre chiffré.</summary>
    public const string EncryptedHeaderFile = "kyfrev-chiffre.json";

    /// <summary>Index chiffré d'un coffre chiffré : chaque chemin du coffre et son objet.</summary>
    public const string EncryptedIndexFile = "index.kyf";

    /// <summary>Objets d'un coffre chiffré, un par fichier du coffre.</summary>
    public const string EncryptedObjects = "objets";

    /// <summary>Chemin absolu d'un emplacement du coffre.</summary>
    public static string Absolute(string root, string relative) => Path.Combine([root, .. relative.Split('/')]);
}

/// <summary>Identifiants des objets du coffre.</summary>
public static class VaultIds
{
    private const int UlidLength = 26;

    /// <summary>Identifiant de fiche : <c>f_</c> suivi de 26 caractères alphanumériques ASCII.</summary>
    public static bool IsSheetId(string id) => IsPrefixedUlid(id, "f_");

    /// <summary>Identifiant de schéma : <c>s_</c> suivi de 26 caractères alphanumériques ASCII.</summary>
    public static bool IsSchemaId(string id) => IsPrefixedUlid(id, "s_");

    /// <summary>Identifiant d'extension : 2 à 40 caractères <c>[a-z0-9-]</c>, sans tiret aux bords.</summary>
    public static bool IsExtensionId(string id) =>
        id.Length is >= 2 and <= 40
        && id.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
        && !id.StartsWith('-') && !id.EndsWith('-');

    private static bool IsPrefixedUlid(string id, string prefix) =>
        id.Length == prefix.Length + UlidLength
        && id.StartsWith(prefix, StringComparison.Ordinal)
        && id.AsSpan(prefix.Length).ContainsOnlyAsciiLetterOrDigit();

    private static bool ContainsOnlyAsciiLetterOrDigit(this ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiLetterOrDigit(c))
            {
                return false;
            }
        }
        return true;
    }
}
