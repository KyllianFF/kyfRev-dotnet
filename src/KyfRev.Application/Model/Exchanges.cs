namespace KyfRev.Application.Model;

/// <summary>Ce que l'import d'un dossier de notes a fait.</summary>
/// <param name="Folder">Dossier créé, relatif à <c>fiches/</c>.</param>
/// <param name="Sheets">Notes copiées.</param>
/// <param name="Tracked">Parmi elles, celles qui sont entrées en révision (J0 aujourd'hui).</param>
/// <param name="Resources">Pièces jointes copiées dans les dossiers de ressources.</param>
/// <param name="Links">Liens <c>[[…]]</c> convertis en liens Markdown.</param>
/// <param name="Ignored">Ce qui n'a pas pu être repris, avec la raison.</param>
public sealed record ImportReport(string Folder, int Sheets, int Tracked, int Resources, int Links, IReadOnlyList<string> Ignored);

/// <summary>Ce que l'on exporte.</summary>
public enum ExportKind
{
    /// <summary>Suivi des révisions et historique complet (deux fichiers CSV).</summary>
    Tracking,

    /// <summary>Passages annotés et commentaires, fiche par fiche (Markdown).</summary>
    Annotations,

    /// <summary>Tout le coffre (<c>.zip</c>, comme une sauvegarde).</summary>
    Vault,

    /// <summary>Citations hors corbeille (Markdown et CSV).</summary>
    Quotes,
}

/// <summary>Format d'une fiche partagée.</summary>
public enum ShareFormat
{
    /// <summary>Le texte Markdown, sans l'en-tête technique de kyfRev.</summary>
    Markdown,

    /// <summary>Une archive <c>.zip</c> : la fiche et ses images.</summary>
    MarkdownWithImages,

    /// <summary>Texte brut préparé par l'interface.</summary>
    Text,

    /// <summary>Page web préparée par l'interface ; les images du coffre y sont intégrées.</summary>
    Html,
}
