using KyfRev.Domain.Reviews;

namespace KyfRev.Infrastructure.Vault;

/// <summary>Nature d'un refus ou d'un échec d'accès au coffre.</summary>
public enum VaultErrorKind
{
    /// <summary>Lecture ou écriture impossible (disque, droits, fichier ouvert ailleurs).</summary>
    Io,

    /// <summary>Chemin refusé : absolu, « .. », caractère ou nom interdit par Windows.</summary>
    InvalidPath,

    /// <summary>Le chemin mène hors du coffre (y compris par un lien symbolique).</summary>
    OutsideVault,

    /// <summary>Le dossier n'est pas un coffre (<c>.suivi/coffre.json</c> absent).</summary>
    NotAVault,

    /// <summary>Un nouveau coffre doit être créé dans un dossier vide.</summary>
    FolderNotEmpty,

    /// <summary>Le coffre est déjà ouvert par une autre fenêtre ou une autre version.</summary>
    AlreadyOpen,

    /// <summary>Aucune fiche ne porte cet identifiant.</summary>
    UnknownSheet,

    /// <summary>Le fichier de la fiche est introuvable.</summary>
    MissingSheet,

    /// <summary>Le dossier est introuvable.</summary>
    UnknownFolder,

    /// <summary>Plusieurs fichiers portent l'identifiant de la fiche.</summary>
    UnresolvedDuplicate,

    /// <summary>Le fichier n'est pas encodé en UTF-8 : il n'est pas modifié.</summary>
    NotUtf8,

    /// <summary>Le fichier a changé depuis sa lecture : rien n'a été écrit.</summary>
    Conflict,

    /// <summary>L'identifiant de l'en-tête d'une fiche ne peut être ni modifié ni retiré.</summary>
    IdentifierChanged,

    /// <summary>La destination existe déjà : rien n'a été écrasé.</summary>
    AlreadyExists,

    /// <summary>Le fichier est déjà suivi.</summary>
    AlreadyTracked,

    /// <summary>Ressource refusée (nom, extension, taille).</summary>
    ResourceRefused,

    /// <summary>Une fiche doit avoir un titre.</summary>
    EmptyTitle,

    /// <summary>La version demandée est introuvable.</summary>
    UnknownVersion,

    /// <summary>En-tête YAML illisible.</summary>
    InvalidHeader,

    /// <summary>Fichier de suivi illisible.</summary>
    UnreadableTracking,

    /// <summary>Réglages du coffre illisibles.</summary>
    UnreadableSettings,

    /// <summary>Opération refusée par les règles de la méthode des J.</summary>
    Tracking,
}

/// <summary>
/// Refus ou échec d'une opération sur le coffre. Le message s'adresse à l'utilisateur ; il est
/// celui de la version 0.x, mot pour mot. Une opération qui échoue ne laisse jamais de fichier
/// à moitié écrit.
/// </summary>
public sealed class VaultException : Exception
{
    /// <summary>Échec d'accès sans précision.</summary>
    public VaultException()
        : this(VaultErrorKind.Io, "Accès au coffre impossible.")
    {
    }

    /// <summary>Échec d'accès avec le message donné.</summary>
    public VaultException(string message)
        : this(VaultErrorKind.Io, message)
    {
    }

    /// <summary>Échec d'accès avec le message donné et sa cause.</summary>
    public VaultException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = VaultErrorKind.Io;
    }

    private VaultException(VaultErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>Nature de l'échec.</summary>
    public VaultErrorKind Kind { get; }

    /// <summary>Pour <see cref="VaultErrorKind.Conflict"/> : empreinte du contenu actuel sur le disque.</summary>
    public string? DiskFingerprint { get; private init; }

    /// <summary>Pour <see cref="VaultErrorKind.Tracking"/> : la règle de la méthode des J qui refuse l'opération.</summary>
    public TrackingError? TrackingError { get; private init; }

    internal static VaultException Io(string path, Exception cause) =>
        new(VaultErrorKind.Io, $"Impossible d'accéder à « {path} » : {cause.Message}", cause);

    internal static VaultException InvalidPath(string text, string reason) =>
        new(VaultErrorKind.InvalidPath, $"Chemin refusé « {text} » : {reason}.");

    internal static VaultException OutsideVault(string path) =>
        new(VaultErrorKind.OutsideVault, $"« {path} » sort du coffre : opération refusée.");

    internal static VaultException NotAVault(string root) =>
        new(VaultErrorKind.NotAVault, $"« {root} » n'est pas un coffre de révision (dossier .suivi/coffre.json absent).");

    internal static VaultException FolderNotEmpty(string root) =>
        new(VaultErrorKind.FolderNotEmpty, $"Le dossier « {root} » n'est pas vide : un nouveau coffre doit être créé dans un dossier vide.");

    internal static VaultException AlreadyOpen(string holder) =>
        new(VaultErrorKind.AlreadyOpen, $"Ce coffre est déjà ouvert ({holder}). Fermez l'autre fenêtre avant de continuer.");

    internal static VaultException UnknownSheet(string id) =>
        new(VaultErrorKind.UnknownSheet, $"Aucune fiche ne porte l'identifiant {id}.");

    internal static VaultException MissingSheet(string title) =>
        new(VaultErrorKind.MissingSheet, $"Le fichier de la fiche « {title} » est introuvable ; consultez « Santé du coffre ».");

    internal static VaultException UnknownFolder(string folder) =>
        new(VaultErrorKind.UnknownFolder, $"Le dossier « {folder} » est introuvable.");

    internal static VaultException UnresolvedDuplicate(string title) =>
        new(VaultErrorKind.UnresolvedDuplicate, $"Plusieurs fichiers portent l'identifiant de « {title} » ; consultez « Santé du coffre ».");

    internal static VaultException NotUtf8(string path) =>
        new(VaultErrorKind.NotUtf8, $"« {path} » n'est pas encodé en UTF-8 : l'application ne le modifiera pas.");

    internal static VaultException Conflict(string diskFingerprint) =>
        new(VaultErrorKind.Conflict, "La fiche a été modifiée en dehors de l'application depuis son ouverture.") { DiskFingerprint = diskFingerprint };

    internal static VaultException IdentifierChanged() =>
        new(VaultErrorKind.IdentifierChanged, "L'identifiant d'une fiche (champ « id » de l'en-tête) ne peut pas être modifié ni retiré.");

    internal static VaultException AlreadyExists(string path) =>
        new(VaultErrorKind.AlreadyExists, $"« {path} » existe déjà : rien n'a été écrasé.");

    internal static VaultException AlreadyTracked(string path) =>
        new(VaultErrorKind.AlreadyTracked, $"« {path} » est déjà suivi (son en-tête contient un identifiant).");

    internal static VaultException ResourceRefused(string name, string reason) =>
        new(VaultErrorKind.ResourceRefused, $"Ressource « {name} » refusée : {reason}.");

    internal static VaultException EmptyTitle() =>
        new(VaultErrorKind.EmptyTitle, "Titre vide : une fiche doit avoir un titre.");

    internal static VaultException UnknownVersion(string name) =>
        new(VaultErrorKind.UnknownVersion, $"La version « {name} » est introuvable.");

    internal static VaultException InvalidHeader(string path, string message) =>
        new(VaultErrorKind.InvalidHeader, $"En-tête illisible dans « {path} » : {message}");

    internal static VaultException UnreadableTracking(string path, string message) =>
        new(VaultErrorKind.UnreadableTracking, $"Fichier de suivi illisible « {path} » : {message}");

    internal static VaultException UnreadableSettings(string message) =>
        new(VaultErrorKind.UnreadableSettings, $"Réglages du coffre illisibles : {message}");

    internal static VaultException Refused(TrackingError error) =>
        new(VaultErrorKind.Tracking, error.Message) { TrackingError = error };
}
