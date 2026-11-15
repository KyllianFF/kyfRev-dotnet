using System.Globalization;
using System.Text;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Les libellés de la vue « Définitions ».</summary>
public static class DefinitionsText
{
    /// <summary>Le titre de la vue.</summary>
    public const string Title = "Définitions";

    /// <summary>Tout le coffre, dans la liste des dossiers.</summary>
    public const string WholeVault = "Tout le coffre";

    /// <summary>La recherche.</summary>
    public const string SearchLabel = "Chercher un terme ou une explication";

    /// <summary>Le bouton qui ouvre une nouvelle définition.</summary>
    public const string NewDefinition = "Nouvelle définition";

    /// <summary>Le même, en fenêtre étroite.</summary>
    public const string NewShort = "Nouvelle";

    /// <summary>La liste des endroits où accrocher la nouvelle définition.</summary>
    public const string PlacesName = "Où accrocher la définition";

    /// <summary>La recherche des endroits est en cours.</summary>
    public const string SearchingPlaces = "Recherche des endroits où ce terme est écrit…";

    /// <summary>Avant que le terme soit écrit.</summary>
    public const string TypeTheTerm = "Écrivez le terme : kyfRev cherche où il est écrit dans vos fiches, pour y accrocher la définition.";

    /// <summary>L'ordre des définitions.</summary>
    public const string OrderLabel = "Ordre des définitions";

    /// <summary>Par ordre alphabétique.</summary>
    public const string OrderAlphabet = "A → Z";

    /// <summary>Par dossier et par fiche.</summary>
    public const string OrderBySheet = "Par fiche";

    /// <summary>Les dernières écrites d'abord.</summary>
    public const string OrderRecent = "Récentes";

    /// <summary>Le filtre des définitions marquées dans toutes les fiches.</summary>
    public const string OnlyEverywhere = "Marquées partout";

    /// <summary>La pastille d'une définition marquée dans toutes les fiches.</summary>
    public const string EverywherePill = "partout";

    /// <summary>Le bouton du menu d'une carte.</summary>
    public const string MoreActions = "Plus d'actions";

    /// <summary>Le terme n'est écrit nulle part : la définition n'a pas où s'accrocher.</summary>
    public static string NoPlace(string term) =>
        $"« {term} » n'est écrit dans aucune fiche. Une définition s'attache à un mot écrit dans une fiche : écrivez-le d'abord dans celle qui en parle.";

    /// <summary>Combien d'endroits ont été trouvés ; <paramref name="more"/> : il y en a peut-être d'autres.</summary>
    public static string PlacesFound(int count, bool more) =>
        count == 1 ? "Où l'accrocher : le seul endroit où ce terme est écrit"
        : more ? string.Create(Culture, $"Où l'accrocher : les {count} premiers endroits où ce terme est écrit")
        : string.Create(Culture, $"Où l'accrocher : {count} endroits où ce terme est écrit");

    /// <summary>Une définition vient d'être ajoutée.</summary>
    public static string Created(string term) => $"Définition de « {term} » ajoutée.";

    /// <summary>Le compte du glossaire : définitions, fiches, et celles marquées partout.</summary>
    public static string Summary(int definitions, int sheets, int everywhere)
    {
        var text = string.Create(Culture, $"{Count(definitions)} dans {sheets} fiche{(sheets > 1 ? "s" : string.Empty)}");
        return everywhere == 0
            ? text
            : string.Create(Culture, $"{text} · {everywhere} marquée{(everywhere > 1 ? "s" : string.Empty)} dans toutes les fiches");
    }

    /// <summary>Le compte d'un glossaire filtré : combien sont montrées, sur combien.</summary>
    public static string Shown(int shown, int definitions) => string.Create(Culture, $"{Count(shown)} sur {definitions}");

    /// <summary>La liste des dossiers.</summary>
    public const string FoldersLabel = "Dossiers";

    /// <summary>La liste des définitions.</summary>
    public const string ListName = "Définitions du dossier";

    /// <summary>Le titre, sans aucune définition.</summary>
    public const string EmptyTitle = "Aucune définition pour l'instant";

    /// <summary>Ce qu'il faut faire, sans aucune définition.</summary>
    public const string EmptyDescription = "Choisissez « Nouvelle définition » ; ou, dans une fiche, sélectionnez un terme, puis « Définir » au clic droit.";

    /// <summary>Rien ne répond à la recherche.</summary>
    public const string NoMatch = "Aucune définition ici pour cette recherche.";

    /// <summary>La lecture du glossaire.</summary>
    public const string Loading = "Chargement des définitions";

    /// <summary>Aller voir dans la fiche.</summary>
    public const string SeeInSheet = "Voir dans la fiche";

    /// <summary>Modifier la définition.</summary>
    public const string Edit = "Modifier";

    /// <summary>Renoncer à la modification.</summary>
    public const string Cancel = "Annuler";

    /// <summary>Enregistrer la modification.</summary>
    public const string Save = "Enregistrer";

    /// <summary>Le terme, en modification.</summary>
    public const string TermLabel = "Terme défini";

    /// <summary>L'explication, en modification.</summary>
    public const string ExplanationLabel = "Explication du terme";

    /// <summary>La position du passage est incertaine.</summary>
    public const string ToCheck = "La fiche a changé : la position du passage est à vérifier.";

    /// <summary>Le passage n'est plus dans la fiche.</summary>
    public const string Lost = "Passage introuvable dans la fiche ; la définition reste révisable.";

    /// <summary>La case d'une définition : son terme se marque dans toutes les fiches.</summary>
    public const string MarkEverywhere = "Marquer ce mot dans toutes les fiches";

    /// <summary>Le clic droit d'une définition marquée partout : elle ne se marque plus qu'à sa place.</summary>
    public const string MarkHereOnly = "Ne marquer ce mot que dans sa fiche";

    /// <summary>La culture des dates.</summary>
    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Le nombre de définitions.</summary>
    public static string Count(int count) => string.Create(Culture, $"{count} définition{(count > 1 ? "s" : string.Empty)}");

    /// <summary>Un texte sans accents ni majuscules, pour chercher sans y penser.</summary>
    public static string WithoutAccents(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(char.ToUpperInvariant(character));
            }
        }
        return result.ToString();
    }
}
