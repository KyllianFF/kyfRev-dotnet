using KyfRev.DesignSystem.Tokens;

namespace KyfRev.DesignSystem;

/// <summary>
/// Clés des ressources du design system, telles que les styles les nomment
/// (<c>{DynamicResource Kyf.Brush.Primary}</c>). Toutes commencent par <see cref="Prefix"/>.
/// </summary>
public static class DesignKeys
{
    /// <summary>Préfixe de toutes les clés.</summary>
    public const string Prefix = "Kyf.";

    /// <summary>Police de l'interface.</summary>
    public const string UiFont = "Kyf.Font.Ui";

    /// <summary>Police des titres.</summary>
    public const string DisplayFont = "Kyf.Font.Display";

    /// <summary>Police du code.</summary>
    public const string CodeFont = "Kyf.Font.Code";

    /// <summary>Police des icônes.</summary>
    public const string IconFont = "Kyf.Font.Icons";

    /// <summary>Rayon des contrôles (<see cref="System.Windows.CornerRadius"/>).</summary>
    public const string ControlRadius = "Kyf.Radius.Control";

    /// <summary>Rayon des cartes.</summary>
    public const string CardRadius = "Kyf.Radius.Card";

    /// <summary>Rayon des dialogues.</summary>
    public const string DialogRadius = "Kyf.Radius.Dialog";

    /// <summary>Animations permises (<see cref="bool"/>) : faux si Windows demande de les réduire.</summary>
    public const string AnimationsEnabled = "Kyf.Motion.Enabled";

    /// <summary>Durée courte (<see cref="System.Windows.Duration"/>), nulle si les animations sont réduites.</summary>
    public const string FastDuration = "Kyf.Duration.Fast";

    /// <summary>Durée normale, nulle si les animations sont réduites.</summary>
    public const string NormalDuration = "Kyf.Duration.Normal";

    /// <summary>Le contraste élevé de Windows est actif : les couleurs sont les siennes, y compris celles du texte des fiches.</summary>
    public const string IsHighContrast = "Kyf.Theme.IsHighContrast";

    /// <summary>Hauteur minimale des boutons discrets.</summary>
    public const string CompactTarget = "Kyf.Target.Compact";

    /// <summary>Hauteur minimale des onglets.</summary>
    public const string TabTarget = "Kyf.Target.Tab";

    /// <summary>Pinceau d'un rôle de couleur.</summary>
    public static string Brush(ColorRole role) => $"Kyf.Brush.{role}";

    /// <summary>Couleur d'un rôle.</summary>
    public static string Color(ColorRole role) => $"Kyf.Color.{role}";

    /// <summary>Taille de police d'un style de texte.</summary>
    public static string FontSize(TypeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return $"Kyf.FontSize.{style.Name}";
    }

    /// <summary>Hauteur de ligne d'un style de texte.</summary>
    public static string LineHeight(TypeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return $"Kyf.LineHeight.{style.Name}";
    }

    /// <summary>Ombre portée d'une élévation (<see cref="System.Windows.Media.Effects.DropShadowEffect"/>, rien en contraste élevé).</summary>
    public static string Shadow(Elevation elevation)
    {
        ArgumentNullException.ThrowIfNull(elevation);
        return $"Kyf.Elevation.{elevation.Name}";
    }
}
