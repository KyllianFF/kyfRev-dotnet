using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using KyfRev.DesignSystem.Styles;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.DesignSystem;

/// <summary>
/// Toutes les ressources du design system : échelles, couleurs du thème, durées, styles des
/// contrôles et des composants, réunies dans <see cref="Dictionary"/>. L'application l'ajoute à
/// ses ressources ; changer <see cref="Theme"/> ou <see cref="ReducedMotion"/> met l'interface à
/// jour sans la recharger.
/// </summary>
public sealed class DesignResources
{
    private ResourceDictionary _theme;
    private ResourceDictionary _motion;
    private ThemeKind _themeKind;
    private bool _reducedMotion;

    /// <summary>Thème et animations choisis.</summary>
    public DesignResources(ThemeKind theme, bool reducedMotion)
    {
        _themeKind = theme;
        _reducedMotion = reducedMotion;
        _theme = ThemeDictionary(theme);
        _motion = MotionDictionary(reducedMotion);
        Dictionary.MergedDictionaries.Add(Scales());
        Dictionary.MergedDictionaries.Add(_theme);
        Dictionary.MergedDictionaries.Add(_motion);
        Dictionary.MergedDictionaries.Add(new Foundations());
        Dictionary.MergedDictionaries.Add(new Buttons());
        Dictionary.MergedDictionaries.Add(new Fields());
        Dictionary.MergedDictionaries.Add(new Menus());
        Dictionary.MergedDictionaries.Add(new ScrollBars());
        Dictionary.MergedDictionaries.Add(new Components());
    }

    /// <summary>Les ressources, à fusionner dans celles de l'application.</summary>
    public ResourceDictionary Dictionary { get; } = [];

    /// <summary>Thème en vigueur. En contraste élevé, les couleurs actuelles de Windows sont relues.</summary>
    public ThemeKind Theme
    {
        get => _themeKind;
        set
        {
            _themeKind = value;
            _theme = Replace(_theme, ThemeDictionary(value));
        }
    }

    /// <summary>Animations réduites : durées nulles, squelettes immobiles.</summary>
    public bool ReducedMotion
    {
        get => _reducedMotion;
        set
        {
            _reducedMotion = value;
            _motion = Replace(_motion, MotionDictionary(value));
        }
    }

    /// <summary>Couleurs d'un thème : une couleur et un pinceau figé par rôle, et les ombres.</summary>
    internal static ResourceDictionary ThemeDictionary(ThemeKind theme)
    {
        var dictionary = new ResourceDictionary();
        foreach (var role in Enum.GetValues<ColorRole>())
        {
            var color = theme == ThemeKind.HighContrast
                ? ThemeColors.Current(ThemeColors.HighContrast(role))
                : ThemeColors.For(theme)[role].ToColor();
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            dictionary[DesignKeys.Color(role)] = color;
            dictionary[DesignKeys.Brush(role)] = brush;
        }
        foreach (var elevation in Elevation.All)
        {
            // Contraste élevé : pas d'ombre, le contour suffit.
            dictionary[DesignKeys.Shadow(elevation)] = theme == ThemeKind.HighContrast ? null : Shadow(elevation);
        }
        return dictionary;
    }

    /// <summary>Durées et animations.</summary>
    internal static ResourceDictionary MotionDictionary(bool reducedMotion) => new()
    {
        [DesignKeys.AnimationsEnabled] = !reducedMotion,
        [DesignKeys.FastDuration] = new Duration(Motion.Effective(Motion.Fast, reducedMotion)),
        [DesignKeys.NormalDuration] = new Duration(Motion.Effective(Motion.Normal, reducedMotion)),
    };

    private static ResourceDictionary Scales()
    {
        var dictionary = new ResourceDictionary
        {
            [DesignKeys.UiFont] = new FontFamily(Typography.UiFamily),
            [DesignKeys.DisplayFont] = new FontFamily(Typography.DisplayFamily),
            [DesignKeys.CodeFont] = new FontFamily(Typography.CodeFamily),
            [DesignKeys.IconFont] = new FontFamily(Typography.IconFamily),
            [DesignKeys.ControlRadius] = new CornerRadius(Radii.Control),
            [DesignKeys.CardRadius] = new CornerRadius(Radii.Card),
            [DesignKeys.DialogRadius] = new CornerRadius(Radii.Dialog),
        };
        foreach (var style in Typography.Ramp)
        {
            dictionary[DesignKeys.FontSize(style)] = style.Size;
            dictionary[DesignKeys.LineHeight(style)] = style.LineHeight;
        }
        return dictionary;
    }

    private static DropShadowEffect Shadow(Elevation elevation)
    {
        var shadow = new DropShadowEffect
        {
            Color = Palette.Shadow.ToColor(),
            BlurRadius = elevation.BlurRadius,
            ShadowDepth = elevation.Depth,
            Direction = 270,
            Opacity = elevation.Opacity,
        };
        shadow.Freeze();
        return shadow;
    }

    private ResourceDictionary Replace(ResourceDictionary current, ResourceDictionary next)
    {
        Dictionary.MergedDictionaries[Dictionary.MergedDictionaries.IndexOf(current)] = next;
        return next;
    }
}
