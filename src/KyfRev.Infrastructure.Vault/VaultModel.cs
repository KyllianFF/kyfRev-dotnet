using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault;

/// <summary>Où en est le fichier Markdown d'une fiche suivie.</summary>
public enum FilePresence
{
    /// <summary>Le fichier est là.</summary>
    Present,

    /// <summary>Le fichier a disparu (supprimé ou sorti du coffre) ; le suivi est conservé en attendant une décision.</summary>
    Missing,

    /// <summary>Plusieurs fichiers portent l'identifiant de la fiche.</summary>
    Duplicated,
}

/// <summary>Une fiche suivie : son fichier de suivi et la présence de son fichier Markdown.</summary>
/// <param name="Sheet">Fichier de suivi (<c>.suivi/fiches/&lt;id&gt;.json</c>).</param>
/// <param name="Presence">Présence du fichier Markdown.</param>
public sealed record SheetEntry(TrackedSheetFile Sheet, FilePresence Presence);

/// <summary>Texte d'une fiche et la preuve de la version lue.</summary>
/// <param name="Text">Texte complet, en-tête compris.</param>
/// <param name="Fingerprint">À rendre à l'enregistrement : la preuve que l'on modifie bien la version lue.</param>
public sealed record SheetContent(string Text, string Fingerprint);

/// <summary>Une fiche déplacée hors de l'application, rattachée par son identifiant.</summary>
public sealed record SheetMove(string Id, VaultPath From, VaultPath To);

/// <summary>Plusieurs fichiers portent le même identifiant.</summary>
public sealed record DuplicateSheet(string Id, IReadOnlyList<VaultPath> Paths);

/// <summary>Un fichier impossible à lire, laissé intact.</summary>
public sealed record UnreadableFile(string Path, string Message);

/// <summary>Un fichier qui porte un identifiant de fiche sans fichier de suivi.</summary>
public sealed record UntrackedId(string Id, VaultPath Path);

/// <summary>
/// Résultat de l'analyse du coffre (« Santé du coffre »). Rien n'est jamais effacé : chaque
/// problème est signalé et attend une décision de l'utilisateur.
/// </summary>
public sealed class HealthReport
{
    /// <summary>Fiches déplacées hors de l'application, rattachées automatiquement (information).</summary>
    public IReadOnlyList<SheetMove> Moved => MovedList;

    /// <summary>Fiches modifiées hors de l'application (information).</summary>
    public IReadOnlyList<string> ModifiedOutside => ModifiedOutsideList;

    /// <summary>Suivi présent, fichier introuvable : le suivi est conservé.</summary>
    public IReadOnlyList<string> Missing => MissingList;

    /// <summary>Plusieurs fichiers portent le même identifiant.</summary>
    public IReadOnlyList<DuplicateSheet> Duplicates => DuplicatesList;

    /// <summary>Fichiers <c>.md</c> sans identifiant : « Suivre cette fiche ».</summary>
    public IReadOnlyList<VaultPath> Untracked => UntrackedList;

    /// <summary>Fichiers avec un identifiant mais sans fichier de suivi.</summary>
    public IReadOnlyList<UntrackedId> WithoutTracking => WithoutTrackingList;

    /// <summary>Fichiers illisibles (en-tête, encodage, suivi), laissés intacts.</summary>
    public IReadOnlyList<UnreadableFile> Unreadable => UnreadableList;

    /// <summary>Fichiers dont les liens n'ont pas pu être mis à jour après un renommage : à vérifier.</summary>
    public IReadOnlyList<VaultPath> LinksToCheck => LinksToCheckList;

    /// <summary>Nombre de problèmes qui attendent une décision.</summary>
    public int Problems => Missing.Count + Duplicates.Count + Untracked.Count + WithoutTracking.Count + Unreadable.Count + LinksToCheck.Count;

    internal List<SheetMove> MovedList { get; } = [];

    internal List<string> ModifiedOutsideList { get; } = [];

    internal List<string> MissingList { get; } = [];

    internal List<DuplicateSheet> DuplicatesList { get; } = [];

    internal List<VaultPath> UntrackedList { get; } = [];

    internal List<UntrackedId> WithoutTrackingList { get; } = [];

    internal List<UnreadableFile> UnreadableList { get; } = [];

    internal List<VaultPath> LinksToCheckList { get; } = [];
}

/// <summary>Issue d'un enregistrement du corps d'une fiche : <see cref="SheetSaved"/> ou <see cref="SaveConflict"/>.</summary>
public abstract record SaveResult
{
    private protected SaveResult()
    {
    }
}

/// <summary>Enregistré.</summary>
/// <param name="Fingerprint">Empreinte du nouveau contenu.</param>
public sealed record SheetSaved(string Fingerprint) : SaveResult;

/// <summary>Refusé : la fiche a changé sur le disque depuis sa lecture. Rien n'a été écrit.</summary>
/// <param name="DiskFingerprint">Empreinte du contenu actuel.</param>
/// <param name="DiskBody">Corps actuel, pour comparer.</param>
public sealed record SaveConflict(string DiskFingerprint, string DiskBody) : SaveResult;

/// <summary>Version antérieure d'une fiche, dans <c>.suivi/versions/&lt;id&gt;/</c>.</summary>
public sealed record SheetVersion(string Name, DateTimeOffset SavedAt, long Size);

/// <summary>Une fiche à la corbeille.</summary>
/// <param name="Id">Identifiant de la fiche.</param>
/// <param name="Title">Titre.</param>
/// <param name="Folder">Dossier d'origine, relatif à <c>fiches/</c>.</param>
/// <param name="TrashedAt">Mise à la corbeille.</param>
/// <param name="Annotations">Annotations et définitions rangées avec elle.</param>
public sealed record TrashedSheet(string Id, string Title, string Folder, DateTimeOffset TrashedAt, int Annotations);
