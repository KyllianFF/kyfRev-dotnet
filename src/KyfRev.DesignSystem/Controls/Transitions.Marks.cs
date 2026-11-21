using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Les marques qui changent (rangée 19C des maquettes) : une barre de graphique pousse et grandit, la part faite d'une
/// progression avance, une couleur posée sur un texte passe en fondu, un élément qui remplace un autre paraît en fondu.
/// </summary>
public static partial class Transitions
{
    /// <summary>
    /// Fait passer une couleur de <paramref name="from"/> à celle que <paramref name="property"/> a maintenant, en fondu :
    /// le fond d'un passage qu'on surligne, le texte d'un trou qu'on découvre.
    /// </summary>
    /// <param name="owner">L'élément à l'écran qui porte la cible : il dit si les animations sont permises.</param>
    /// <param name="target">Ce dont la couleur change (un élément, ou un morceau de texte).</param>
    /// <param name="property">La propriété de couleur.</param>
    /// <param name="from">La couleur d'avant ; rien : transparent.</param>
    /// <param name="durationKey">La durée, par sa clé du design system.</param>
    /// <param name="kind">La famille de l'animation.</param>
    /// <returns>Vrai si le fondu a commencé.</returns>
    public static bool FadeBrush(FrameworkElement owner, IAnimatable target, DependencyProperty property, Brush? from, string durationKey = DesignKeys.FastDuration, MotionKinds kind = MotionKinds.Feedback)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(property);
        if (!Moves(owner, durationKey, out var duration, kind))
        {
            target.BeginAnimation(property, null);
            return false;
        }
        target.BeginAnimation(property, new BrushTransition { From = from ?? BrushTransition.Clear, Duration = duration, FillBehavior = FillBehavior.Stop });
        return true;
    }

    /// <summary>
    /// Étire <paramref name="element"/> depuis l'un de ses bords : il part de <paramref name="from"/> fois sa taille et
    /// rejoint sa taille. Une barre qui pousse part de 0 ; une barre qui vient de changer, de son ancienne taille.
    /// </summary>
    /// <param name="element">Ce qui grandit ou rétrécit.</param>
    /// <param name="from">La part de sa taille d'où il part.</param>
    /// <param name="edge">Le bord qui ne bouge pas : le bas pour une barre verticale, la gauche pour une barre horizontale.</param>
    /// <param name="durationKey">La durée, par sa clé du design system.</param>
    /// <param name="kind">La famille de l'animation.</param>
    /// <returns>Vrai si l'élément s'étire.</returns>
    public static bool Stretch(FrameworkElement element, double from, Dock edge, string durationKey = DesignKeys.NormalDuration, MotionKinds kind = MotionKinds.Feedback)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (double.IsNaN(from) || double.IsInfinity(from) || Math.Abs(from - 1) < 0.01 || !Moves(element, durationKey, out var duration, kind))
        {
            return false;
        }
        var scale = new ScaleTransform(1, 1);
        element.RenderTransformOrigin = edge switch
        {
            Dock.Bottom => new Point(0.5, 1),
            Dock.Top => new Point(0.5, 0),
            Dock.Right => new Point(1, 0.5),
            _ => new Point(0, 0.5),
        };
        element.RenderTransform = scale;
        var along = edge is Dock.Left or Dock.Right ? ScaleTransform.ScaleXProperty : ScaleTransform.ScaleYProperty;
        scale.BeginAnimation(along, new DoubleAnimation(Math.Max(0, from), 1, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
        return true;
    }

    /// <summary>
    /// Sur une barre de graphique : le bord d'où elle pousse. Elle pousse en arrivant, et grandit ou rétrécit depuis son
    /// ancienne taille quand sa valeur change.
    /// </summary>
    public static readonly DependencyProperty GrowsFromProperty =
        DependencyProperty.RegisterAttached("GrowsFrom", typeof(Dock?), typeof(Transitions), new PropertyMetadata(null, OnGrowsFrom));

    /// <summary>Lit <see cref="GrowsFromProperty"/>.</summary>
    public static Dock? GetGrowsFrom(DependencyObject element) => (Dock?)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(GrowsFromProperty);

    /// <summary>Écrit <see cref="GrowsFromProperty"/>.</summary>
    public static void SetGrowsFrom(DependencyObject element, Dock? value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(GrowsFromProperty, value);

    private static void OnGrowsFrom(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not FrameworkElement bar)
        {
            return;
        }
        bar.SizeChanged -= OnBarResized;
        if (e.NewValue is Dock)
        {
            bar.SizeChanged += OnBarResized;
        }
    }

    private static void OnBarResized(object sender, SizeChangedEventArgs e)
    {
        var bar = (FrameworkElement)sender;
        if (GetGrowsFrom(bar) is not { } edge)
        {
            return;
        }
        var (was, now) = edge is Dock.Left or Dock.Right ? (e.PreviousSize.Width, e.NewSize.Width) : (e.PreviousSize.Height, e.NewSize.Height);
        if (now > 0)
        {
            Stretch(bar, was / now, edge);
        }
    }

    /// <summary>
    /// Sur la part faite d'une progression dessinée à la main (une barre étirée de 0 à 1) : la part, qui avance en glissant
    /// quand elle change. Remplace un <see cref="ScaleTransform"/> lié.
    /// </summary>
    public static readonly DependencyProperty FillProperty =
        DependencyProperty.RegisterAttached("Fill", typeof(double), typeof(Transitions), new PropertyMetadata(double.NaN, OnFill));

    /// <summary>Lit <see cref="FillProperty"/>.</summary>
    public static double GetFill(DependencyObject element) => (double)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(FillProperty);

    /// <summary>Écrit <see cref="FillProperty"/>.</summary>
    public static void SetFill(DependencyObject element, double value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(FillProperty, value);

    private static void OnFill(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not FrameworkElement bar || e.NewValue is not double part || double.IsNaN(part))
        {
            return;
        }
        if (bar.RenderTransform is not ScaleTransform { IsFrozen: false } scale)
        {
            scale = new ScaleTransform(1, 1);
            bar.RenderTransformOrigin = new Point(0, 0.5);
            bar.RenderTransform = scale;
        }
        var was = e.OldValue is double before && !double.IsNaN(before) ? before : part;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.ScaleX = part;
        if (was != part && bar.IsLoaded && bar.IsVisible && Moves(bar, DesignKeys.NormalDuration, out var duration, MotionKinds.Feedback))
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(was, part, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
        }
    }

    /// <summary>
    /// Sur un élément qui en remplace un autre au même endroit (« Passer » qui devient « Suivant ») : vrai, il est montré, et
    /// paraît en fondu s'il se montre une fois la vue à l'écran ; faux, il s'en va d'un coup, pour laisser la place à
    /// l'autre. Remplace une liaison sur <see cref="UIElement.Visibility"/>.
    /// </summary>
    public static readonly DependencyProperty SwappedInProperty =
        DependencyProperty.RegisterAttached("SwappedIn", typeof(bool?), typeof(Transitions), new PropertyMetadata(null, OnSwappedIn));

    /// <summary>Lit <see cref="SwappedInProperty"/>.</summary>
    public static bool? GetSwappedIn(DependencyObject element) => (bool?)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(SwappedInProperty);

    /// <summary>Écrit <see cref="SwappedInProperty"/>.</summary>
    public static void SetSwappedIn(DependencyObject element, bool? value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(SwappedInProperty, value);

    private static void OnSwappedIn(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not FrameworkElement element || e.NewValue is not bool shown)
        {
            return;
        }
        element.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        // La vue qui s'ouvre montre tout d'un coup : seul un échange, une fois à l'écran, se fait en fondu.
        if (shown && e.OldValue is false && element.IsLoaded)
        {
            Enter(element, DesignKeys.FastDuration, kind: MotionKinds.Views);
        }
    }
}
