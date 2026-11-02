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
    public const string SearchLabel = "Rechercher un terme";

    /// <summary>La liste des dossiers.</summary>
    public const string FoldersLabel = "Dossiers";

    /// <summary>La liste des définitions.</summary>
    public const string ListName = "Définitions du dossier";

    /// <summary>Le titre, sans aucune définition.</summary>
    public const string EmptyTitle = "Aucune définition pour l'instant";

    /// <summary>Ce qu'il faut faire, sans aucune définition.</summary>
    public const string EmptyDescription = "Dans une fiche, sélectionnez un terme, puis choisissez « Définir » au clic droit.";

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
