using System.Collections.Immutable;

namespace KyfRev.DesignSystem.Tokens;

/// <summary>Une paire de rôles employée ensemble, et le contraste qu'elle doit atteindre (WCAG 2.1).</summary>
/// <param name="Foreground">Texte ou repère.</param>
/// <param name="Background">Fond sur lequel il se pose.</param>
/// <param name="Minimum">Contraste minimal : 4,5 pour un texte, 3 pour un contour ou un repère.</param>
public sealed record ContrastRequirement(ColorRole Foreground, ColorRole Background, double Minimum);

/// <summary>
/// Toutes les paires de rôles que les composants emploient. Seuils WCAG 2.1 : 4,5:1 pour le texte
/// (1.4.3), 3:1 pour les contours et repères d'interface (1.4.11). Les rôles décoratifs
/// (<see cref="Decorative"/>) n'ont pas de seuil : ils ne portent jamais seuls une information.
/// </summary>
public static class ContrastRequirements
{
    /// <summary>Contraste minimal d'un texte.</summary>
    public const double Text = 4.5;

    /// <summary>Contraste minimal d'un contour ou d'un repère d'interface.</summary>
    public const double Graphic = 3.0;

    private static readonly ColorRole[] Grounds = [ColorRole.Background, ColorRole.Surface, ColorRole.SurfaceAlt];

    /// <summary>Rôles décoratifs, sans seuil : séparateurs et voile des dialogues.</summary>
    public static ImmutableArray<ColorRole> Decorative { get; } = [ColorRole.Divider, ColorRole.Scrim];

    /// <summary>Les paires exigées.</summary>
    public static ImmutableArray<ContrastRequirement> All { get; } =
    [
        .. Over([ColorRole.Text, ColorRole.TextSecondary, ColorRole.Link], Grounds, Text),
        .. Over([ColorRole.Text], [ColorRole.CriticalBackground, ColorRole.WarningBackground, ColorRole.SuccessBackground, ColorRole.InfoBackground], Text),
        .. Over([ColorRole.OnPrimary], [ColorRole.Primary, ColorRole.PrimaryHover, ColorRole.PrimaryPressed], Text),
        new(ColorRole.PillSheetText, ColorRole.PillSheetBackground, Text),
        new(ColorRole.PillPassageText, ColorRole.PillPassageBackground, Text),
        new(ColorRole.PillReminderText, ColorRole.PillReminderBackground, Text),
        new(ColorRole.OnSelection, ColorRole.Selection, Text),
        new(ColorRole.OnHighlight, ColorRole.Highlight, Text),
        new(ColorRole.OnHighlight, ColorRole.HighlightActive, Text),
        new(ColorRole.NavigationActiveText, ColorRole.NavigationActiveBackground, Text),
        new(ColorRole.Critical, ColorRole.CriticalBackground, Text),
        new(ColorRole.Warning, ColorRole.WarningBackground, Text),
        new(ColorRole.Success, ColorRole.SuccessBackground, Text),
        new(ColorRole.Info, ColorRole.InfoBackground, Text),
        .. Over([ColorRole.Critical, ColorRole.Warning, ColorRole.Success], [ColorRole.Background, ColorRole.Surface], Text),
        .. Over(
            [ColorRole.ControlBorder, ColorRole.Focus, ColorRole.Primary, ColorRole.MarkerSheet, ColorRole.MarkerPassage, ColorRole.MarkerReminder],
            Grounds,
            Graphic),
    ];

    private static IEnumerable<ContrastRequirement> Over(ColorRole[] foregrounds, ColorRole[] backgrounds, double minimum) =>
        foregrounds.SelectMany(foreground => backgrounds.Select(background => new ContrastRequirement(foreground, background, minimum)));
}
