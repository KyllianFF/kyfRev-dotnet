using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes du clic droit de la vue « Fiches », ceux de la 0.x, lus dans les ressources (<c>Resources/SheetActionsText.resx</c>).</summary>
public static class SheetActionsText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.SheetActionsText", typeof(SheetActionsText).Assembly);

    /// <summary>« Archiver… ».</summary>
    public static string Archive => Text(nameof(Archive));

    /// <summary>« Archiver ».</summary>
    public static string ArchiveConfirm => Text(nameof(ArchiveConfirm));

    /// <summary>« « {0} » quitte les révisions. Elle reste dans Archives, avec son hi… ».</summary>
    public static string ArchiveMessageOf(object title) => Format("ArchiveMessage", title);

    /// <summary>« Archiver cette fiche ? ».</summary>
    public static string ArchiveTitle => Text(nameof(ArchiveTitle));

    /// <summary>« Fiche archivée. ».</summary>
    public static string Archived => Text(nameof(Archived));

    /// <summary>« Créer ».</summary>
    public static string CreateConfirm => Text(nameof(CreateConfirm));

    /// <summary>« Supprimer… ».</summary>
    public static string Delete => Text(nameof(Delete));

    /// <summary>« Ce dossier serait à {0} niveaux. Au-delà de trois sous-dossiers, l'… ».</summary>
    public static string DepthWarningOf(object depth) => Format("DepthWarning", depth);

    /// <summary>« Supprimer définitivement, sans passer par la corbeille ».</summary>
    public static string EraseBox => Text(nameof(EraseBox));

    /// <summary>« Supprimer définitivement ».</summary>
    public static string EraseConfirm => Text(nameof(EraseConfirm));

    /// <summary>« Cela ne s'annule pas : seules les sauvegardes déjà faites le contie… ».</summary>
    public static string EraseHelp => Text(nameof(EraseHelp));

    /// <summary>« Supprimé définitivement. ».</summary>
    public static string Erased => Text(nameof(Erased));

    /// <summary>« Dossier créé. ».</summary>
    public static string FolderCreated => Text(nameof(FolderCreated));

    /// <summary>« Nom du dossier ».</summary>
    public static string FolderNameLabel => Text(nameof(FolderNameLabel));

    /// <summary>« Dossier renommé. ».</summary>
    public static string FolderRenamed => Text(nameof(FolderRenamed));

    /// <summary>« Déplacer vers… ».</summary>
    public static string Move => Text(nameof(Move));

    /// <summary>« Déplacer ».</summary>
    public static string MoveConfirm => Text(nameof(MoveConfirm));

    /// <summary>« Vers le dossier ».</summary>
    public static string MoveLabel => Text(nameof(MoveLabel));

    /// <summary>« Déplacer « {0} » ».</summary>
    public static string MoveTitleOf(object title) => Format("MoveTitle", title);

    /// <summary>« Déplacé. ».</summary>
    public static string Moved => Text(nameof(Moved));

    /// <summary>« Indiquez un nom. ».</summary>
    public static string NameRequired => Text(nameof(NameRequired));

    /// <summary>« Nouveau dossier ».</summary>
    public static string NewFolder => Text(nameof(NewFolder));

    /// <summary>« Nouveau dossier dans « {0} » ».</summary>
    public static string NewFolderInTitleOf(object folder) => Format("NewFolderInTitle", folder);

    /// <summary>« Nouveau dossier à la racine ».</summary>
    public static string NewFolderTitle => Text(nameof(NewFolderTitle));

    /// <summary>« Nouvelle fiche ».</summary>
    public static string NewSheet => Text(nameof(NewSheet));

    /// <summary>« Ouvrir ».</summary>
    public static string Open => Text(nameof(Open));

    /// <summary>« Renommer… ».</summary>
    public static string Rename => Text(nameof(Rename));

    /// <summary>« Renommer ».</summary>
    public static string RenameConfirm => Text(nameof(RenameConfirm));

    /// <summary>« Renommer le dossier ».</summary>
    public static string RenameFolderTitle => Text(nameof(RenameFolderTitle));

    /// <summary>« Renommer la fiche ».</summary>
    public static string RenameTitle => Text(nameof(RenameTitle));

    /// <summary>« Fiche renommée. ».</summary>
    public static string Renamed => Text(nameof(Renamed));

    /// <summary>« Remettre en révision… ».</summary>
    public static string Restart => Text(nameof(Restart));

    /// <summary>« Remettre en révision ».</summary>
    public static string RestartConfirm => Text(nameof(RestartConfirm));

    /// <summary>« « {0} » redevient active : un nouveau cycle commence aujourd'hui (J… ».</summary>
    public static string RestartMessageOf(object title) => Format("RestartMessage", title);

    /// <summary>« Remettre en révision ? ».</summary>
    public static string RestartTitle => Text(nameof(RestartTitle));

    /// <summary>« Fiche remise en révision. ».</summary>
    public static string Restarted => Text(nameof(Restarted));

    /// <summary>« Réviser ».</summary>
    public static string Review => Text(nameof(Review));

    /// <summary>« Enregistrer ».</summary>
    public static string SaveConfirm => Text(nameof(SaveConfirm));

    /// <summary>« {0} fiches ».</summary>
    public static string SheetCountManyOf(object count) => Format("SheetCountMany", count);

    /// <summary>« {0} fiche ».</summary>
    public static string SheetCountOneOf(object count) => Format("SheetCountOne", count);

    /// <summary>« Étiquettes… ».</summary>
    public static string Tags => Text(nameof(Tags));

    /// <summary>« Étiquettes, séparées par des virgules ».</summary>
    public static string TagsLabel => Text(nameof(TagsLabel));

    /// <summary>« Étiquettes enregistrées. ».</summary>
    public static string TagsSaved => Text(nameof(TagsSaved));

    /// <summary>« Étiquettes de « {0} » ».</summary>
    public static string TagsTitleOf(object title) => Format("TagsTitle", title);

    /// <summary>« Titre ».</summary>
    public static string TitleLabel => Text(nameof(TitleLabel));

    /// <summary>« Au-delà de trois sous-dossiers, l'arborescence devient difficile à … ».</summary>
    public static string TooDeep => Text(nameof(TooDeep));

    /// <summary>« Mettre à la corbeille ».</summary>
    public static string TrashConfirm => Text(nameof(TrashConfirm));

    /// <summary>« « {0} » ne contient aucune fiche : il quitte l'arborescence. S'il c… ».</summary>
    public static string TrashEmptyFolderMessageOf(object folder) => Format("TrashEmptyFolderMessage", folder);

    /// <summary>« « {0} » et {1} qu'il contient, sous-dossiers et fiches archivées co… ».</summary>
    public static string TrashFolderMessageOf(object folder, object count) => Format("TrashFolderMessage", folder, count);

    /// <summary>« Supprimer ce dossier ? ».</summary>
    public static string TrashFolderTitle => Text(nameof(TrashFolderTitle));

    /// <summary>« « {0} » va à la corbeille, avec ses images et ses annotations : ell… ».</summary>
    public static string TrashMessageOf(object title) => Format("TrashMessage", title);

    /// <summary>« Supprimer cette fiche ? ».</summary>
    public static string TrashTitle => Text(nameof(TrashTitle));

    /// <summary>« Mis à la corbeille. ».</summary>
    public static string Trashed => Text(nameof(Trashed));

    /// <summary>« Modifier ».</summary>
    public static string Write => Text(nameof(Write));

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » introuvable.");
}
