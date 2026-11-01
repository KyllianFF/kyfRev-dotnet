using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes de la barre de filtres des listes, lus dans les ressources (<c>Resources/ListText.resx</c>).</summary>
public static class ListText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.ListText", typeof(ListText).Assembly);

    /// <summary>« Filtres ».</summary>
    public static string Filters => Text(nameof(Filters));

    /// <summary>« Filtres · … ».</summary>
    public static string FiltersWithCountOf(object count) => Format("FiltersWithCount", count);

    /// <summary>« Retirer le filtre … ».</summary>
    public static string RemoveFilterOf(object filter) => Format("RemoveFilter", filter);

    /// <summary>« Rechercher ».</summary>
    public static string SearchName => Text(nameof(SearchName));

    /// <summary>« Titre ou étiquette… ».</summary>
    public static string SearchPlaceholder => Text(nameof(SearchPlaceholder));

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
