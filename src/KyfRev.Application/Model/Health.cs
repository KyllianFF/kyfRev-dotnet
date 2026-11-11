namespace KyfRev.Application.Model;

/// <summary>Un fichier impossible à lire, laissé intact.</summary>
public sealed record UnreadableFile(string Path, string Message);

/// <summary>Une fiche citée par la santé du coffre.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="Title">Titre (l'identifiant si la fiche est inconnue).</param>
/// <param name="Path">Chemin dans le coffre (vide si la fiche est inconnue).</param>
public sealed record ConcernedSheet(string Id, string Title, string Path);

/// <summary>Une fiche déplacée hors de l'application et rattachée.</summary>
public sealed record MovedSheet(string Title, string From, string To);

/// <summary>Plusieurs fichiers portent l'identifiant de la fiche.</summary>
public sealed record DuplicateDetails(string Id, string Title, IReadOnlyList<string> Paths);

/// <summary>Un lien ou une image d'une fiche qui mène à un fichier absent du coffre.</summary>
/// <param name="SheetId">Fiche qui contient le lien.</param>
/// <param name="Title">Son titre.</param>
/// <param name="Target">Destination telle qu'écrite dans la fiche (décodée).</param>
public sealed record BrokenLink(string SheetId, string Title, string Target);

/// <summary>Un passage annoté cité par la santé du coffre.</summary>
public sealed record ConcernedPassage(string Id, string SheetId, string Title, string Exact, AnchorState State);

/// <summary>Rapport complet pour l'écran « Santé du coffre ».</summary>
/// <param name="Moved">Information : fiches déplacées hors de l'application et rattachées.</param>
/// <param name="ModifiedOutside">Information : fiches modifiées hors de l'application.</param>
/// <param name="Missing">Fiches dont le fichier est introuvable.</param>
/// <param name="Duplicates">Identifiants portés par plusieurs fichiers.</param>
/// <param name="Untracked">Fichiers <c>.md</c> sans identifiant.</param>
/// <param name="WithoutTracking">Fichiers qui portent un identifiant, mais sans fichier de suivi.</param>
/// <param name="Unreadable">Fichiers illisibles, laissés intacts.</param>
/// <param name="LinksToCheck">Fichiers dont les liens n'ont pas pu être mis à jour.</param>
/// <param name="BrokenLinks">Liens qui mènent à un fichier absent.</param>
/// <param name="OrphanAnnotations">Annotations dont la fiche est introuvable.</param>
/// <param name="AnnotationsToCheck">Passages dont la position est à vérifier ou introuvable.</param>
/// <param name="Problems">Problèmes qui attendent une décision (les informations n'en sont pas).</param>
public sealed record HealthDetails(
    IReadOnlyList<MovedSheet> Moved,
    IReadOnlyList<ConcernedSheet> ModifiedOutside,
    IReadOnlyList<ConcernedSheet> Missing,
    IReadOnlyList<DuplicateDetails> Duplicates,
    IReadOnlyList<string> Untracked,
    IReadOnlyList<ConcernedSheet> WithoutTracking,
    IReadOnlyList<UnreadableFile> Unreadable,
    IReadOnlyList<string> LinksToCheck,
    IReadOnlyList<BrokenLink> BrokenLinks,
    IReadOnlyList<ConcernedPassage> OrphanAnnotations,
    IReadOnlyList<ConcernedPassage> AnnotationsToCheck,
    int Problems)
{
    /// <summary>
    /// Copies de conflit laissées par un outil de synchronisation (Dropbox, OneDrive, Syncthing, Nextcloud), reconnues à
    /// leur nom : chemins dans le coffre. Elles comptent parmi les problèmes.
    /// </summary>
    public IReadOnlyList<string> ConflictCopies { get; init; } = [];
}
