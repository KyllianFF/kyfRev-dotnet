namespace KyfRev.Infrastructure.Vault.Analysis;

/// <summary>
/// Les copies de conflit qu'un outil de synchronisation pose à côté d'un fichier quand deux postes l'ont modifié sans se
/// voir : « Fiche (conflicted copy 2026-11-10).md », « Fiche.sync-conflict-20261110-101500-ABCDEFG.md »,
/// « f_….-PORTABLE.json ». kyfRev ne les lit pas comme les siennes : la plus ancienne des deux versions resterait
/// oubliée là. Les reconnaître à leur nom, sans rien ouvrir, suffit à les signaler.
/// </summary>
internal static class ConflictCopies
{
    /// <summary>
    /// Ce que les outils courants écrivent dans le nom d'une copie de conflit : Dropbox et Nextcloud, en anglais et en
    /// français ; Syncthing ; OneDrive Entreprise.
    /// </summary>
    private static readonly string[] Markers =
    [
        "conflicted copy",
        "copie en conflit",
        "copie conflictuelle",
        "conflit de",
        ".sync-conflict-",
        "_conflict-",
        "-conflict-",
    ];

    /// <summary>Longueur d'un identifiant de fiche : « f_ » et les 26 caractères de son ULID.</summary>
    private const int SheetIdLength = 28;

    /// <summary>Le nom porte la marque d'une copie de conflit.</summary>
    public static bool IsConflictName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Array.Exists(Markers, marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Un fichier de suivi ou d'annotations dont le nom commence par un identifiant de fiche, puis continue : kyfRev les
    /// nomme « identifiant.json », exactement. OneDrive, lui, ajoute le nom du poste (« f_…-PORTABLE.json ») sans autre
    /// marque : c'est le seul endroit où un suffixe suffit à le reconnaître, parce que le nom attendu est connu.
    /// </summary>
    public static bool IsTrackingCopy(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        const string extension = ".json";
        if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var stem = name[..^extension.Length];
        return stem.Length > SheetIdLength && VaultIds.IsSheetId(stem[..SheetIdLength]);
    }
}
