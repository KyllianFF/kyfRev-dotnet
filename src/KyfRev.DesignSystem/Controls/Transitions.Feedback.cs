using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Le retour au toucher (rangée 18A des maquettes) : les couleurs d'un contrôle passent de l'une à l'autre au lieu de
/// sauter, un bouton s'enfonce un instant sous le clic, une coche ou un rond paraît en grandissant, le rond d'un
/// interrupteur glisse. Les styles le demandent par des propriétés attachées ; les déclencheurs des styles ne changent
/// pas : la transition ne fait que les suivre, et rien ne bouge quand les animations sont réduites.
/// </summary>
public static partial class Transitions
{
    /// <summary>
    /// Sur une bordure (fond, contour) ou une forme (remplissage, trait) d'un modèle : quand un déclencheur change sa
    /// couleur, elle y passe en fondu, sur la durée rapide.
    /// </summary>
    public static readonly DependencyProperty SmoothBrushesProperty =
        DependencyProperty.RegisterAttached("SmoothBrushes", typeof(bool), typeof(Transitions), new PropertyMetadata(false, OnSmoothBrushes));

    /// <summary>Sur un bouton : l'échelle où il descend sous le clic (0,97 : 97 %) ; 0 : il ne bouge pas.</summary>
    public static readonly DependencyProperty PressScaleProperty =
        DependencyProperty.RegisterAttached("PressScale", typeof(double), typeof(Transitions), new PropertyMetadata(0d, OnPressScale));

    /// <summary>Sur un élément qui paraît (une coche, le point d'un bouton radio) : il grandit en fondu jusqu'à sa taille.</summary>
    public static readonly DependencyProperty PopsInProperty =
        DependencyProperty.RegisterAttached("PopsIn", typeof(bool), typeof(Transitions), new PropertyMetadata(false, OnPopsIn));

    /// <summary>Sur un élément que la mise en page déplace (le rond d'un interrupteur) : il glisse de l'ancienne place à la nouvelle.</summary>
    public static readonly DependencyProperty GlidesProperty =
        DependencyProperty.RegisterAttached("Glides", typeof(bool), typeof(Transitions), new PropertyMetadata(false, OnGlides));

    // Ce que la transition suit : la valeur affichée de chaque pinceau, liée à l'élément lui-même, et la dernière valeur
    // de base vue (celle que les styles et les déclencheurs donnent).
    private static readonly DependencyProperty WatchedBackgroundProperty = Watched("WatchedBackground", Border.BackgroundProperty);
    private static readonly DependencyProperty WatchedBorderBrushProperty = Watched("WatchedBorderBrush", Border.BorderBrushProperty);
    private static readonly DependencyProperty WatchedFillProperty = Watched("WatchedFill", Shape.FillProperty);
    private static readonly DependencyProperty WatchedStrokeProperty = Watched("WatchedStroke", Shape.StrokeProperty);
    private static readonly DependencyProperty LastBrushesProperty =
        DependencyProperty.RegisterAttached("LastBrushes", typeof(Dictionary<DependencyProperty, Brush?>), typeof(Transitions));
    private static readonly DependencyProperty WatchedPressedProperty =
        DependencyProperty.RegisterAttached("WatchedPressed", typeof(bool), typeof(Transitions), new PropertyMetadata(false, OnPressed));
    private static readonly DependencyProperty PlaceProperty =
        DependencyProperty.RegisterAttached("Place", typeof(Point?), typeof(Transitions));

    /// <summary>Lit <see cref="SmoothBrushesProperty"/>.</summary>
    public static bool GetSmoothBrushes(DependencyObject element) => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(SmoothBrushesProperty);

    /// <summary>Écrit <see cref="SmoothBrushesProperty"/>.</summary>
    public static void SetSmoothBrushes(DependencyObject element, bool value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(SmoothBrushesProperty, value);

    /// <summary>Lit <see cref="PressScaleProperty"/>.</summary>
    public static double GetPressScale(DependencyObject element) => (double)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(PressScaleProperty);

    /// <summary>Écrit <see cref="PressScaleProperty"/>.</summary>
    public static void SetPressScale(DependencyObject element, double value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(PressScaleProperty, value);

    /// <summary>Lit <see cref="PopsInProperty"/>.</summary>
    public static bool GetPopsIn(DependencyObject element) => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(PopsInProperty);

    /// <summary>Écrit <see cref="PopsInProperty"/>.</summary>
    public static void SetPopsIn(DependencyObject element, bool value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(PopsInProperty, value);

    /// <summary>Lit <see cref="GlidesProperty"/>.</summary>
    public static bool GetGlides(DependencyObject element) => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(GlidesProperty);

    /// <summary>Écrit <see cref="GlidesProperty"/>.</summary>
    public static void SetGlides(DependencyObject element, bool value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(GlidesProperty, value);

    /// <summary>
    /// Fait grandir <paramref name="element"/> en fondu, de <paramref name="from"/> à sa taille, autour de
    /// <paramref name="origin"/> (0,5 ; 0,5 : son centre ; 0 ; 0 : son coin haut gauche).
    /// </summary>
    /// <returns>Vrai si une transition a commencé ; faux si l'élément est montré tel quel.</returns>
    public static bool Grow(FrameworkElement element, double from, Point origin, string durationKey = DesignKeys.FastDuration)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!Moves(element, durationKey, out var duration) || !OwnsScale(element, out var scale))
        {
            return false;
        }
        element.RenderTransformOrigin = origin;
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
        var grow = new DoubleAnimation(from, 1, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        return true;
    }

    private static DependencyProperty Watched(string name, DependencyProperty followed) =>
        DependencyProperty.RegisterAttached(name, typeof(Brush), typeof(Transitions), new PropertyMetadata(null, (element, _) => Follow((FrameworkElement)element, followed)));

    private static void OnSmoothBrushes(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true || element is not FrameworkElement framework)
        {
            return;
        }
        framework.SetValue(LastBrushesProperty, new Dictionary<DependencyProperty, Brush?>());
        var (first, second) = framework switch
        {
            Border => (Border.BackgroundProperty, Border.BorderBrushProperty),
            Shape => (Shape.FillProperty, Shape.StrokeProperty),
            _ => (null, null),
        };
        if (first is null || second is null)
        {
            return;
        }
        BindingOperations.SetBinding(framework, first == Border.BackgroundProperty ? WatchedBackgroundProperty : WatchedFillProperty, new Binding { Source = framework, Path = new PropertyPath(first) });
        BindingOperations.SetBinding(framework, second == Border.BorderBrushProperty ? WatchedBorderBrushProperty : WatchedStrokeProperty, new Binding { Source = framework, Path = new PropertyPath(second) });
    }

    /// <summary>
    /// La valeur affichée d'un pinceau a changé. Si c'est sa valeur de base (un déclencheur, un thème), la couleur passe de
    /// celle qu'on voyait à la nouvelle ; si ce n'est que l'animation qui avance, rien à faire.
    /// </summary>
    private static void Follow(FrameworkElement element, DependencyProperty property)
    {
        if (element.GetValue(LastBrushesProperty) is not Dictionary<DependencyProperty, Brush?> last)
        {
            return;
        }
        var target = element.GetAnimationBaseValue(property) as Brush;
        if (!last.TryGetValue(property, out var previous))
        {
            // La première valeur : celle de départ, sans transition.
            last[property] = target;
            return;
        }
        if (Equals(previous, target))
        {
            return;
        }
        last[property] = target;
        var shown = element.GetValue(property) as Brush;
        if (Moves(element, DesignKeys.FastDuration, out var duration) && shown is SolidColorBrush && target is SolidColorBrush)
        {
            element.BeginAnimation(property, new BrushTransition { From = shown, Duration = duration, FillBehavior = FillBehavior.Stop });
        }
        else
        {
            element.BeginAnimation(property, null);
        }
    }

    private static void OnPressScale(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is ButtonBase button && BindingOperations.GetBinding(button, WatchedPressedProperty) is null)
        {
            BindingOperations.SetBinding(button, WatchedPressedProperty, new Binding { Source = button, Path = new PropertyPath(ButtonBase.IsPressedProperty) });
        }
    }

    /// <summary>Le bouton est enfoncé ou relâché : il descend à son échelle, vite, et revient un peu moins vite.</summary>
    private static void OnPressed(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        var button = (ButtonBase)element;
        var depth = GetPressScale(button);
        var pressed = e.NewValue is true;
        if (depth <= 0 || depth >= 1 || !OwnsScale(button, out var scale))
        {
            return;
        }
        if (!Moves(button, pressed ? DesignKeys.PressDuration : DesignKeys.FastDuration, out var duration))
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            return;
        }
        button.RenderTransformOrigin = new Point(0.5, 0.5);
        var move = new DoubleAnimation(pressed ? depth : 1, duration) { EasingFunction = Ease, FillBehavior = pressed ? FillBehavior.HoldEnd : FillBehavior.Stop };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, move);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, move);
    }

    /// <summary>
    /// L'échelle de l'élément : la sienne s'il n'a pas d'autre transformation, sinon rien (on ne remplace pas une
    /// transformation posée par quelqu'un d'autre).
    /// </summary>
    private static bool OwnsScale(UIElement element, out ScaleTransform scale)
    {
        switch (element.RenderTransform)
        {
            case ScaleTransform { IsFrozen: false } own:
                scale = own;
                return true;
            case null:
            case MatrixTransform { Matrix.IsIdentity: true }:
            case TranslateTransform { X: 0, Y: 0 }:
                scale = new ScaleTransform();
                element.RenderTransform = scale;
                return true;
            default:
                scale = new ScaleTransform();
                return false;
        }
    }

    private static void OnPopsIn(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is FrameworkElement framework)
        {
            framework.IsVisibleChanged -= OnPopVisible;
            if (e.NewValue is true)
            {
                framework.IsVisibleChanged += OnPopVisible;
            }
        }
    }

    private static void OnPopVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Grow((FrameworkElement)sender, 0.6, new Point(0.5, 0.5));
        }
    }

    private static void OnGlides(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement framework)
        {
            return;
        }
        framework.LayoutUpdated -= GlideHandlers.Of(framework);
        if (e.NewValue is true)
        {
            framework.LayoutUpdated += GlideHandlers.Of(framework);
        }
    }

    /// <summary>
    /// Après une mise en page : si l'élément n'est plus à la même place dans son parent (le rond d'un interrupteur passé
    /// de gauche à droite), il part de l'ancienne et glisse jusqu'à la nouvelle.
    /// </summary>
    private static void Glide(FrameworkElement element)
    {
        if (!element.IsVisible || VisualTreeHelper.GetParent(element) is not Visual parent || !element.IsDescendantOf(parent))
        {
            return;
        }
        var place = element.TransformToAncestor(parent).Transform(default);
        // La place sans le glissement en cours : celle que la mise en page donne.
        if (element.RenderTransform is TranslateTransform current)
        {
            place.Offset(-current.X, -current.Y);
        }
        var before = (Point?)element.GetValue(PlaceProperty);
        element.SetValue(PlaceProperty, place);
        if (before is not { } old || (Math.Abs(old.X - place.X) < 0.5 && Math.Abs(old.Y - place.Y) < 0.5))
        {
            return;
        }
        if (!Moves(element, DesignKeys.FastDuration, out var duration))
        {
            return;
        }
        var move = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
        if (move.IsFrozen)
        {
            move = new TranslateTransform();
        }
        element.RenderTransform = move;
        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(old.X - place.X, 0, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(old.Y - place.Y, 0, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
    }

    /// <summary>Le gestionnaire de mise en page de chaque élément qui glisse, pour pouvoir le retirer.</summary>
    private static class GlideHandlers
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, EventHandler> Handlers = [];

        public static EventHandler Of(FrameworkElement element) => Handlers.GetValue(element, owner => (_, _) => Glide(owner));
    }
}

/// <summary>Une couleur qui passe d'un pinceau uni à un autre ; la destination est la valeur de base de la propriété.</summary>
internal sealed class BrushTransition : AnimationTimeline
{
    /// <summary>Le pinceau de départ ; sinon, celui que la propriété avait.</summary>
    public Brush? From { get; init; }

    public override Type TargetPropertyType => typeof(Brush);

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        ArgumentNullException.ThrowIfNull(animationClock);
        var progress = Transitions.Ease.Ease(animationClock.CurrentProgress ?? 1);
        return Between(From ?? defaultOriginValue as Brush, defaultDestinationValue as Brush, progress) ?? defaultDestinationValue;
    }

    /// <summary>La couleur à <paramref name="progress"/> (de 0 à 1) entre deux pinceaux unis ; sinon, l'un ou l'autre.</summary>
    internal static Brush? Between(Brush? from, Brush? to, double progress)
    {
        if (progress >= 1 || from is not SolidColorBrush a || to is not SolidColorBrush b)
        {
            return progress >= 0.5 ? to : from;
        }
        // Vers ou depuis le transparent : seule l'opacité change, la teinte reste celle de la couleur visible (pas de voile blanc en thème sombre).
        var (ca, cb) = (a.Color.A == 0 ? b.Color with { A = 0 } : a.Color, b.Color.A == 0 ? a.Color with { A = 0 } : b.Color);
        byte Mix(byte x, byte y) => (byte)Math.Round(x + ((y - x) * progress));
        // Entre deux couleurs des jetons : la couleur de passage se fait comme elles, par TokenColor.
        var brush = new SolidColorBrush(new Tokens.TokenColor(Mix(ca.A, cb.A), Mix(ca.R, cb.R), Mix(ca.G, cb.G), Mix(ca.B, cb.B)).ToColor());
        brush.Freeze();
        return brush;
    }

    protected override Freezable CreateInstanceCore() => new BrushTransition { From = From };
}
