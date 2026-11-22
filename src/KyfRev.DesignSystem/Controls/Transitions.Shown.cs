using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Montrer et cacher un bloc en le faisant glisser : un volet vient de son bord et y retourne, une barre du bas descend
/// pour se cacher. Le bloc est découpé à sa place pendant le trajet (il ne passe sur rien d'autre), et la mise en page ne
/// change qu'une fois : quand il prend sa place, ou quand il l'a quittée.
/// </summary>
public static partial class Transitions
{
    /// <summary>
    /// Montre (vrai) ou cache (faux) l'élément, à la place d'une liaison sur <see cref="UIElement.Visibility"/> : il glisse
    /// depuis <see cref="ShownFromProperty"/> en paraissant, et y retourne avant de céder sa place. Sans valeur, rien n'est
    /// décidé. Avant d'être à l'écran, ou les animations coupées, il est montré ou caché d'un coup.
    /// </summary>
    public static readonly DependencyProperty ShownProperty =
        DependencyProperty.RegisterAttached("Shown", typeof(bool?), typeof(Transitions), new PropertyMetadata(null, OnShown));

    /// <summary>Le bord d'où l'élément vient, et où il retourne : à gauche, à droite, en haut, en bas.</summary>
    public static readonly DependencyProperty ShownFromProperty =
        DependencyProperty.RegisterAttached("ShownFrom", typeof(Dock), typeof(Transitions), new PropertyMetadata(Dock.Bottom));

    // Le numéro du dernier changement : une fin de trajet d'un changement plus ancien ne décide plus de rien.
    private static readonly DependencyProperty ShownRunProperty =
        DependencyProperty.RegisterAttached("ShownRun", typeof(int), typeof(Transitions), new PropertyMetadata(0));

    /// <summary>Lit <see cref="ShownProperty"/>.</summary>
    public static bool? GetShown(DependencyObject element) => (bool?)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(ShownProperty);

    /// <summary>Écrit <see cref="ShownProperty"/>.</summary>
    public static void SetShown(DependencyObject element, bool? value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(ShownProperty, value);

    /// <summary>Lit <see cref="ShownFromProperty"/>.</summary>
    public static Dock GetShownFrom(DependencyObject element) => (Dock)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(ShownFromProperty);

    /// <summary>Écrit <see cref="ShownFromProperty"/>.</summary>
    public static void SetShownFrom(DependencyObject element, Dock value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(ShownFromProperty, value);

    // Combien de trajets l'élément a commencés.
    private static readonly DependencyProperty ShownSlidesProperty =
        DependencyProperty.RegisterAttached("ShownSlides", typeof(int), typeof(Transitions), new PropertyMetadata(0));

    /// <summary>Vrai tant que l'élément glisse pour paraître ou pour se cacher.</summary>
    internal static bool IsSliding(FrameworkElement element) => element.Clip is RectangleGeometry { HasAnimatedProperties: true };

    /// <summary>Combien de fois l'élément a glissé, pour paraître ou pour se cacher (pour les essais).</summary>
    internal static int SlidesOf(FrameworkElement element) => (int)element.GetValue(ShownSlidesProperty);

    private static void OnShown(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not FrameworkElement element || e.NewValue is not bool shown)
        {
            return;
        }
        var run = (int)element.GetValue(ShownRunProperty) + 1;
        element.SetValue(ShownRunProperty, run);
        var wasSliding = IsSliding(element);
        StopSlide(element);
        var fresh = IsFreshContext(element);
        // Pas encore à l'écran, une autre fiche dans la même vue (son état est le point de départ), ou sans animation : d'un coup.
        if (!element.IsLoaded || fresh || !Moves(element, DesignKeys.NormalDuration, out var duration, MotionKinds.Panels))
        {
            element.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            return;
        }
        if (!shown)
        {
            // Jamais mis en page, ou déjà caché : il n'y a rien à faire glisser.
            if (!element.IsVisible || element.RenderSize.Width <= 0 || element.RenderSize.Height <= 0)
            {
                element.Visibility = Visibility.Collapsed;
                return;
            }
            // Il reste là le temps que l'application montre ce qui change (ouvrir l'éditeur, par exemple) : lancé plus tôt,
            // le trajet serait fini avant la première image.
            element.Dispatcher.BeginInvoke(WhenShown, () =>
            {
                if ((int)element.GetValue(ShownRunProperty) != run)
                {
                    return;
                }
                Slide(element, duration, entering: false, () =>
                {
                    if ((int)element.GetValue(ShownRunProperty) == run)
                    {
                        element.Visibility = Visibility.Collapsed;
                        StopSlide(element);
                    }
                });
            });
            return;
        }
        if (element.Visibility == Visibility.Visible && !wasSliding)
        {
            return;
        }
        // Il prend sa place, invisible, le temps d'une mise en page : sa taille dit de combien il glisse.
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 0, TimeSpan.Zero) { FillBehavior = FillBehavior.HoldEnd });
        element.Visibility = Visibility.Visible;
        element.Dispatcher.BeginInvoke(WhenShown, () =>
        {
            if ((int)element.GetValue(ShownRunProperty) != run)
            {
                return;
            }
            if (element.RenderSize.Width <= 0 || element.RenderSize.Height <= 0)
            {
                StopSlide(element);
                return;
            }
            Slide(element, duration, entering: true, null);
        });
    }

    // Le contexte de données pour lequel l'élément a été montré ou caché en dernier, et s'il vient de changer.
    private static readonly DependencyProperty ShownContextProperty =
        DependencyProperty.RegisterAttached("ShownContext", typeof(object), typeof(Transitions), new PropertyMetadata(null));

    private static readonly DependencyProperty ShownSwappedProperty =
        DependencyProperty.RegisterAttached("ShownSwapped", typeof(bool?), typeof(Transitions), new PropertyMetadata(null));

    /// <summary>
    /// Vrai si l'élément vient de recevoir un autre contexte de données (une autre fiche dans la même vue) : ce qu'il montre
    /// alors est un point de départ, pas un changement à animer.
    /// </summary>
    private static bool IsFreshContext(FrameworkElement element)
    {
        if (element.GetValue(ShownSwappedProperty) is null)
        {
            // Première fois : on suit désormais le contexte de l'élément.
            element.SetValue(ShownSwappedProperty, false);
            element.SetValue(ShownContextProperty, element.DataContext);
            element.DataContextChanged += OnShownContextChanged;
            return false;
        }
        return element.GetValue(ShownSwappedProperty) is true || !ReferenceEquals(element.GetValue(ShownContextProperty), element.DataContext);
    }

    private static void OnShownContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        element.SetValue(ShownContextProperty, e.NewValue);
        // Les liaisons relisent leur valeur dans la foulée, avant ou après ce gestionnaire : jusqu'à la mise en page suivante,
        // ce que l'élément montre est son point de départ.
        element.SetValue(ShownSwappedProperty, true);
        element.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => element.SetValue(ShownSwappedProperty, false));
    }

    /// <summary>
    /// Fait paraître l'élément, déjà montré, en glissant depuis le bord donné (un volet de dialogue qui s'ouvre) : une fois
    /// mis en page, puisque sa taille dit de combien il glisse. Les fenêtres coupées dans les Paramètres, il est là d'un coup.
    /// </summary>
    internal static void SlideIn(FrameworkElement element, Dock from)
    {
        SetShownFrom(element, from);
        StopSlide(element);
        if (!Moves(element, DesignKeys.NormalDuration, out var duration, MotionKinds.Surfaces))
        {
            return;
        }
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 0, TimeSpan.Zero) { FillBehavior = FillBehavior.HoldEnd });
        element.Dispatcher.BeginInvoke(WhenShown, () =>
        {
            if (element.RenderSize.Width <= 0 || element.RenderSize.Height <= 0)
            {
                StopSlide(element);
                return;
            }
            Slide(element, duration, entering: true, null);
        });
    }

    /// <summary>Fait glisser l'élément sur toute sa longueur, depuis son bord ou vers lui, en fondu, découpé à sa place.</summary>
    private static void Slide(FrameworkElement element, TimeSpan duration, bool entering, Action? done)
    {
        element.SetValue(ShownSlidesProperty, SlidesOf(element) + 1);
        var size = element.RenderSize;
        var (dx, dy) = GetShownFrom(element) switch
        {
            Dock.Left => (-size.Width, 0d),
            Dock.Right => (size.Width, 0d),
            Dock.Top => (0d, -size.Height),
            _ => (0d, size.Height),
        };
        var whole = new Rect(size);
        // Parti : ce qui reste de lui à sa place, dans ses propres coordonnées, est un bord sans épaisseur.
        var gone = new Rect(dx < 0 ? size.Width : 0, dy < 0 ? size.Height : 0, dx != 0 ? 0 : size.Width, dy != 0 ? 0 : size.Height);
        var fill = entering ? FillBehavior.Stop : FillBehavior.HoldEnd;
        // Ce qui arrive décélère ; ce qui part accélère.
        var ease = entering ? Ease : Leave;
        DoubleAnimation Along(double away) => new(entering ? away : 0, entering ? 0 : away, duration) { EasingFunction = ease, FillBehavior = fill };

        var move = new TranslateTransform();
        var clip = new RectangleGeometry(whole);
        element.RenderTransform = move;
        element.Clip = clip;
        var reveal = new RectAnimation(entering ? gone : whole, entering ? whole : gone, duration) { EasingFunction = ease, FillBehavior = fill };
        reveal.Completed += (_, _) =>
        {
            if (done is not null)
            {
                done();
            }
            else if (ReferenceEquals(element.Clip, clip))
            {
                StopSlide(element);
            }
        };
        move.BeginAnimation(TranslateTransform.XProperty, Along(dx));
        move.BeginAnimation(TranslateTransform.YProperty, Along(dy));
        clip.BeginAnimation(RectangleGeometry.RectProperty, reveal);
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(entering ? 0 : 1, entering ? 1 : 0, duration) { EasingFunction = ease, FillBehavior = fill });
    }

    /// <summary>L'élément redevient entier, à sa place : plus de découpe, plus de décalage, son opacité d'origine.</summary>
    private static void StopSlide(FrameworkElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        if (element.Clip is RectangleGeometry { IsFrozen: false })
        {
            element.ClearValue(UIElement.ClipProperty);
        }
        if (element.RenderTransform is TranslateTransform { IsFrozen: false })
        {
            element.ClearValue(UIElement.RenderTransformProperty);
        }
    }
}
