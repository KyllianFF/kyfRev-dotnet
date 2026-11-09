using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes de la vue « Fiches », lus dans les ressources (<c>Resources/SheetsText.resx</c>).</summary>
public static class SheetsText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.SheetsText", typeof(SheetsText).Assembly);

    /// <summary>« Tous ».</summary>
    public static string AllFolders => Text(nameof(AllFolders));

    /// <summary>« Rechercher dans les fiches ».</summary>
    public static string SearchPlaceholder => Text(nameof(SearchPlaceholder));

    /// <summary>« Rechercher dans les fiches (titre, étiquettes et texte) ».</summary>
    public static string SearchName => Text(nameof(SearchName));

    /// <summary>« Archivée ».</summary>
    public static string ArchivedRow => Text(nameof(ArchivedRow));

    /// <summary>« À la racine ».</summary>
    public static string AtRoot => Text(nameof(AtRoot));

    /// <summary>« … fiches ».</summary>
    public static string CountManyOf(object count) => Format("CountMany", count);

    /// <summary>« … fiche ».</summary>
    public static string CountOneOf(object count) => Format("CountOne", count);

    /// <summary>« Créer la fiche ».</summary>
    public static string CreateSheet => Text(nameof(CreateSheet));

    /// <summary>« aujourd'hui ».</summary>
    public static string DueToday => Text(nameof(DueToday));

    /// <summary>« Créez votre première fiche : sa révision J1 sera due demain. ».</summary>
    public static string EmptyDescription => Text(nameof(EmptyDescription));

    /// <summary>« Fiche vide ».</summary>
    public static string EmptyTemplate => Text(nameof(EmptyTemplate));

    /// <summary>« Aucune fiche pour l'instant ».</summary>
    public static string EmptyTitle => Text(nameof(EmptyTitle));

    /// <summary>« Dossier ».</summary>
    public static string FilterFolder => Text(nameof(FilterFolder));

    /// <summary>« Filtrer par étiquettes ».</summary>
    public static string TagsFilterName => Text(nameof(TagsFilterName));

    /// <summary>« Seulement les fiches qui portent cette étiquette ».</summary>
    public static string TagFilterAdd => Text(nameof(TagFilterAdd));

    /// <summary>« Retirer ce filtre ».</summary>
    public static string TagFilterRemove => Text(nameof(TagFilterRemove));

    /// <summary>« Tout afficher ».</summary>
    public static string TagsClear => Text(nameof(TagsClear));

    /// <summary>« Tri ».</summary>
    public static string FilterSort => Text(nameof(FilterSort));

    /// <summary>« État ».</summary>
    public static string FilterState => Text(nameof(FilterState));

    /// <summary>« … · … ».</summary>
    public static string FolderCountOf(object folder, object count) => Format("FolderCount", folder, count);

    /// <summary>« Fiches du coffre ».</summary>
    public static string ListName => Text(nameof(ListName));

    /// <summary>« Chargement des fiches… ».</summary>
    public static string Loading => Text(nameof(Loading));

    /// <summary>« Nouveau dossier… ».</summary>
    public static string NewFolderChoice => Text(nameof(NewFolderChoice));

    /// <summary>« Nom du nouveau dossier ».</summary>
    public static string NewFolderName => Text(nameof(NewFolderName));

    /// <summary>« Histoire/Révolution ».</summary>
    public static string NewFolderPlaceholder => Text(nameof(NewFolderPlaceholder));

    /// <summary>« Nouvelle fiche ».</summary>
    public static string NewSheet => Text(nameof(NewSheet));

    /// <summary>« Sans dossier ».</summary>
    public static string NoFolder => Text(nameof(NoFolder));

    /// <summary>« Aucune fiche ne correspond à ces filtres. ».</summary>
    public static string NoMatch => Text(nameof(NoMatch));

    /// <summary>« Ouvrir la fiche ».</summary>
    public static string OpenSheet => Text(nameof(OpenSheet));

    /// <summary>« Échéance ».</summary>
    public static string SortDueDate => Text(nameof(SortDueDate));

    /// <summary>« Par dossier ».</summary>
    public static string SortFolder => Text(nameof(SortFolder));

    /// <summary>« Titre ».</summary>
    public static string SortTitle => Text(nameof(SortTitle));

    /// <summary>« En révision ».</summary>
    public static string StateActive => Text(nameof(StateActive));

    /// <summary>« Toutes ».</summary>
    public static string StateAll => Text(nameof(StateAll));

    /// <summary>« Archivées ».</summary>
    public static string StateArchived => Text(nameof(StateArchived));

    /// <summary>« À réviser ».</summary>
    public static string StateDue => Text(nameof(StateDue));

    /// <summary>« … · … ».</summary>
    public static string StepOf(object step, object when) => Format("Step", step, when);

    /// <summary>« … · … en révision · … archivées ».</summary>
    public static string SummaryManyOf(object count, object active, object archived) => Format("SummaryMany", count, active, archived);

    /// <summary>« … · … en révision · … archivée ».</summary>
    public static string SummaryOneOf(object count, object active, object archived) => Format("SummaryOne", count, active, archived);

    /// <summary>« Modèle ».</summary>
    public static string Template => Text(nameof(Template));

    /// <summary>« Titre ».</summary>
    public static string TitleLabel => Text(nameof(TitleLabel));

    /// <summary>« Révolution française : les causes ».</summary>
    public static string TitlePlaceholder => Text(nameof(TitlePlaceholder));

    /// <summary>« Donnez un titre à la fiche. ».</summary>
    public static string TitleRequired => Text(nameof(TitleRequired));

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
