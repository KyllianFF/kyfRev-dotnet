using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf;

/// <summary>Textes de la coquille, lus dans les ressources (<c>Resources/ShellText.resx</c>) : l'interface se traduit sans toucher au code.</summary>
public static class ShellText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.ShellText", typeof(ShellText).Assembly);

    /// <summary>« kyfRev ».</summary>
    public static string AppName => Text(nameof(AppName));

    /// <summary>« Archives ».</summary>
    public static string Archives => Text(nameof(Archives));

    /// <summary>Explication des vues à venir.</summary>
    public static string ComingSoon => Text(nameof(ComingSoon));

    /// <summary>« Définitions ».</summary>
    public static string Definitions => Text(nameof(Definitions));

    /// <summary>« Mode concentration ».</summary>
    public static string FocusMode => Text(nameof(FocusMode));

    /// <summary>« Galerie des composants ».</summary>
    public static string Gallery => Text(nameof(Gallery));

    /// <summary>Explication de la galerie.</summary>
    public static string GalleryDescription => Text(nameof(GalleryDescription));

    /// <summary>« Santé du coffre ».</summary>
    public static string Health => Text(nameof(Health));

    /// <summary>« Raccourcis clavier ».</summary>
    public static string Help => Text(nameof(Help));

    /// <summary>« Navigation principale ».</summary>
    public static string MainNavigation => Text(nameof(MainNavigation));

    /// <summary>« Aucune commande ne correspond. ».</summary>
    public static string NoCommand => Text(nameof(NoCommand));

    /// <summary>« Rechercher ».</summary>
    public static string Search => Text(nameof(Search));

    /// <summary>« Rechercher une commande ou une vue ».</summary>
    public static string SearchPlaceholder => Text(nameof(SearchPlaceholder));

    /// <summary>« Navigation secondaire ».</summary>
    public static string SecondaryNavigation => Text(nameof(SecondaryNavigation));

    /// <summary>« Paramètres ».</summary>
    public static string Settings => Text(nameof(Settings));

    /// <summary>Raccourci du mode concentration.</summary>
    public static string ShortcutFocusMode => Text(nameof(ShortcutFocusMode));

    /// <summary>Raccourci des vues du menu.</summary>
    public static string ShortcutGoTo => Text(nameof(ShortcutGoTo));

    /// <summary>Raccourci de l'aide.</summary>
    public static string ShortcutHelp => Text(nameof(ShortcutHelp));

    /// <summary>Raccourci de la recherche.</summary>
    public static string ShortcutSearch => Text(nameof(ShortcutSearch));

    /// <summary>Raccourci pour agrandir.</summary>
    public static string ShortcutZoomIn => Text(nameof(ShortcutZoomIn));

    /// <summary>Raccourci pour réduire.</summary>
    public static string ShortcutZoomOut => Text(nameof(ShortcutZoomOut));

    /// <summary>Raccourci de la taille normale.</summary>
    public static string ShortcutZoomReset => Text(nameof(ShortcutZoomReset));

    /// <summary>« Fiches ».</summary>
    public static string Sheets => Text(nameof(Sheets));

    /// <summary>« Statistiques ».</summary>
    public static string Statistics => Text(nameof(Statistics));

    /// <summary>« Thème ».</summary>
    public static string Theme => Text(nameof(Theme));

    /// <summary>« Sombre ».</summary>
    public static string ThemeDark => Text(nameof(ThemeDark));

    /// <summary>« Clair ».</summary>
    public static string ThemeLight => Text(nameof(ThemeLight));

    /// <summary>« Comme Windows ».</summary>
    public static string ThemeSystem => Text(nameof(ThemeSystem));

    /// <summary>« Aujourd'hui ».</summary>
    public static string Today => Text(nameof(Today));

    /// <summary>« Aller à : … ».</summary>
    public static string GoTo(string view) => Format(nameof(GoTo), view);

    /// <summary>« Thème : … ».</summary>
    public static string ThemeTo(string theme) => Format(nameof(ThemeTo), theme);

    /// <summary>« Zoom : … % ».</summary>
    public static string Zoom(int percent) => Format(nameof(Zoom), percent);

    private static string Format(string name, object value) => string.Format(CultureInfo.CurrentCulture, Text(name), value);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
