using System.Collections.Frozen;
using System.Windows;
using System.Windows.Media;

namespace KyfRev.DesignSystem.Tokens;

/// <summary>Les thèmes de kyfRev.</summary>
public enum ThemeKind
{
    /// <summary>Thème clair.</summary>
    Light,

    /// <summary>Thème sombre.</summary>
    Dark,

    /// <summary>Contraste élevé de Windows : les couleurs choisies par l'utilisateur dans Windows.</summary>
    HighContrast,
}

/// <summary>
/// Couleurs de Windows reprises en contraste élevé. Windows garantit le contraste de leurs paires
/// (texte sur fenêtre, texte mis en évidence sur mise en évidence, lien sur fenêtre).
/// </summary>
public enum SystemColor
{
    /// <summary>Fond de fenêtre.</summary>
    Window,

    /// <summary>Texte de fenêtre.</summary>
    WindowText,

    /// <summary>Fond mis en évidence (sélection, élément actif).</summary>
    Highlight,

    /// <summary>Texte mis en évidence.</summary>
    HighlightText,

    /// <summary>Lien.</summary>
    HotTrack,
}

/// <summary>La couleur de chaque rôle, selon le thème.</summary>
public static class ThemeColors
{
    private static readonly FrozenDictionary<ColorRole, TokenColor> LightColors = Build(Palette.Light, light: true);
    private static readonly FrozenDictionary<ColorRole, TokenColor> DarkColors = Build(Palette.Dark, light: false);

    /// <summary>
    /// Couleurs système du contraste élevé, par rôle : le texte et les fonds suivent la fenêtre,
    /// ce qui est actif suit la mise en évidence, les liens leur couleur propre. Les couleurs
    /// fonctionnelles s'effacent : l'icône et le libellé suffisent.
    /// </summary>
    private static readonly FrozenDictionary<ColorRole, SystemColor> HighContrastColors = new Dictionary<ColorRole, SystemColor>
    {
        [ColorRole.Background] = SystemColor.Window,
        [ColorRole.Surface] = SystemColor.Window,
        [ColorRole.SurfaceAlt] = SystemColor.Window,
        [ColorRole.Divider] = SystemColor.WindowText,
        [ColorRole.ControlBorder] = SystemColor.WindowText,
        [ColorRole.Text] = SystemColor.WindowText,
        [ColorRole.TextSecondary] = SystemColor.WindowText,
        [ColorRole.Primary] = SystemColor.Highlight,
        [ColorRole.PrimaryHover] = SystemColor.Highlight,
        [ColorRole.PrimaryPressed] = SystemColor.Highlight,
        [ColorRole.OnPrimary] = SystemColor.HighlightText,
        [ColorRole.Link] = SystemColor.HotTrack,
        [ColorRole.Focus] = SystemColor.WindowText,
        [ColorRole.MarkerSheet] = SystemColor.WindowText,
        [ColorRole.MarkerPassage] = SystemColor.WindowText,
        [ColorRole.MarkerReminder] = SystemColor.WindowText,
        [ColorRole.PillSheetBackground] = SystemColor.Window,
        [ColorRole.PillSheetText] = SystemColor.WindowText,
        [ColorRole.PillPassageBackground] = SystemColor.Window,
        [ColorRole.PillPassageText] = SystemColor.WindowText,
        [ColorRole.PillReminderBackground] = SystemColor.Window,
        [ColorRole.PillReminderText] = SystemColor.WindowText,
        [ColorRole.Selection] = SystemColor.Highlight,
        [ColorRole.OnSelection] = SystemColor.HighlightText,
        [ColorRole.Highlight] = SystemColor.Highlight,
        [ColorRole.HighlightActive] = SystemColor.Highlight,
        [ColorRole.OnHighlight] = SystemColor.HighlightText,
        [ColorRole.NavigationActiveBackground] = SystemColor.Highlight,
        [ColorRole.NavigationActiveText] = SystemColor.HighlightText,
        [ColorRole.Critical] = SystemColor.WindowText,
        [ColorRole.CriticalBackground] = SystemColor.Window,
        [ColorRole.Warning] = SystemColor.WindowText,
        [ColorRole.WarningBackground] = SystemColor.Window,
        [ColorRole.Success] = SystemColor.WindowText,
        [ColorRole.SuccessBackground] = SystemColor.Window,
        [ColorRole.Info] = SystemColor.WindowText,
        [ColorRole.InfoBackground] = SystemColor.Window,
        [ColorRole.Scrim] = SystemColor.Window,
    }.ToFrozenDictionary();

    /// <summary>Les couleurs du thème clair ou sombre.</summary>
    /// <exception cref="ArgumentException">Le contraste élevé n'a pas de couleurs fixes : voir <see cref="HighContrast"/>.</exception>
    public static IReadOnlyDictionary<ColorRole, TokenColor> For(ThemeKind theme) => theme switch
    {
        ThemeKind.Light => LightColors,
        ThemeKind.Dark => DarkColors,
        _ => throw new ArgumentException("Le contraste élevé prend les couleurs de Windows.", nameof(theme)),
    };

    /// <summary>Couleur système de Windows employée pour <paramref name="role"/> en contraste élevé.</summary>
    public static SystemColor HighContrast(ColorRole role) => HighContrastColors[role];

    private static FrozenDictionary<ColorRole, TokenColor> Build(ThemeNeutrals neutrals, bool light)
    {
        var (violet, periwinkle, mauve) = (Palette.Violet, Palette.Periwinkle, Palette.Mauve);
        // Thème clair : la nuance 600 pour ce qui se détache sur fond clair, les nuances 100 en
        // fond de pastille ; thème sombre : les nuances claires sur fond sombre, et l'inverse.
        int Pick(int lightShade, int darkShade) => light ? lightShade : darkShade;
        return new Dictionary<ColorRole, TokenColor>
        {
            [ColorRole.Background] = neutrals.Background,
            [ColorRole.Surface] = neutrals.Surface,
            [ColorRole.SurfaceAlt] = neutrals.SurfaceAlt,
            [ColorRole.Divider] = neutrals.Divider,
            [ColorRole.ControlBorder] = neutrals.ControlBorder,
            [ColorRole.Text] = neutrals.Text,
            [ColorRole.TextSecondary] = neutrals.TextSecondary,
            [ColorRole.Primary] = violet[Pick(600, 400)],
            [ColorRole.PrimaryHover] = violet[Pick(700, 300)],
            [ColorRole.PrimaryPressed] = violet[Pick(800, 200)],
            [ColorRole.OnPrimary] = neutrals.OnPrimary,
            [ColorRole.Link] = violet[Pick(600, 300)],
            [ColorRole.Focus] = violet[Pick(600, 400)],
            [ColorRole.MarkerSheet] = violet[Pick(600, 400)],
            [ColorRole.MarkerPassage] = periwinkle[Pick(600, 400)],
            [ColorRole.MarkerReminder] = mauve[Pick(600, 400)],
            [ColorRole.PillSheetBackground] = violet[Pick(100, 800)],
            [ColorRole.PillSheetText] = violet[Pick(800, 100)],
            [ColorRole.PillPassageBackground] = periwinkle[Pick(100, 800)],
            [ColorRole.PillPassageText] = periwinkle[Pick(800, 100)],
            [ColorRole.PillReminderBackground] = mauve[Pick(100, 800)],
            [ColorRole.PillReminderText] = mauve[Pick(800, 100)],
            [ColorRole.Selection] = periwinkle[Pick(200, 800)],
            [ColorRole.OnSelection] = neutrals.Text,
            [ColorRole.Highlight] = mauve[Pick(200, 800)],
            [ColorRole.HighlightActive] = mauve[Pick(300, 700)],
            [ColorRole.OnHighlight] = neutrals.Text,
            [ColorRole.NavigationActiveBackground] = violet[Pick(100, 800)],
            [ColorRole.NavigationActiveText] = violet[Pick(800, 100)],
            [ColorRole.Critical] = neutrals.Critical,
            [ColorRole.CriticalBackground] = neutrals.CriticalBackground,
            [ColorRole.Warning] = neutrals.Warning,
            [ColorRole.WarningBackground] = neutrals.WarningBackground,
            [ColorRole.Success] = neutrals.Success,
            [ColorRole.SuccessBackground] = neutrals.SuccessBackground,
            [ColorRole.Info] = periwinkle[Pick(800, 100)],
            [ColorRole.InfoBackground] = periwinkle[Pick(100, 800)],
            [ColorRole.Scrim] = neutrals.Scrim,
        }.ToFrozenDictionary();
    }

    /// <summary>Valeur actuelle d'une couleur de Windows.</summary>
    public static Color Current(SystemColor color) => color switch
    {
        SystemColor.Window => SystemColors.WindowColor,
        SystemColor.WindowText => SystemColors.WindowTextColor,
        SystemColor.Highlight => SystemColors.HighlightColor,
        SystemColor.HighlightText => SystemColors.HighlightTextColor,
        _ => SystemColors.HotTrackColor,
    };
}
