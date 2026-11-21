using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Les transitions de l'interface : ce qui apparaît (une page, une fenêtre de dialogue) arrive en fondu, sur les durées du
/// design system. Quand les animations sont réduites, ces durées sont nulles et rien ne bouge.
/// </summary>
public static partial class Transitions
{
    /// <summary>La montée, en pixels, d'une fenêtre de dialogue qui apparaît.</summary>
    public const double DialogRise = 8;

    /// <summary>
    /// Pour les essais : les transitions se terminent aussitôt, et les menus s'ouvrent sans fondu. Une capture prise juste
    /// après une navigation montre alors la vue finie, pas une vue à demi apparue.
    /// </summary>
    public static bool Skip { get; set; }

    /// <summary>
    /// Fait apparaître <paramref name="element"/> : en fondu, et en montant de <paramref name="rise"/> pixels s'il y en a.
    /// </summary>
    /// <param name="element">Ce qui apparaît.</param>
    /// <param name="durationKey">La durée, par sa clé du design system (<see cref="DesignKeys.FastDuration"/>, <see cref="DesignKeys.NormalDuration"/>).</param>
    /// <param name="rise">De combien de pixels l'élément monte en apparaissant ; 0 : il ne bouge pas.</param>
    /// <param name="shift">De combien de pixels il glisse de côté en apparaissant (négatif : il vient de la gauche) ; 0 : il ne glisse pas.</param>
    /// <param name="kind">La famille de l'animation : coupée dans les Paramètres, l'élément est montré tel quel.</param>
    /// <param name="delay">Le temps qu'il attend, invisible, avant d'apparaître : son tour dans une cascade.</param>
    /// <returns>Vrai si une transition a commencé ; faux si l'élément est montré tel quel (animations réduites, essais).</returns>
    public static bool Enter(FrameworkElement element, string durationKey = DesignKeys.NormalDuration, double rise = 0, double shift = 0, MotionKinds kind = MotionKinds.Views, TimeSpan delay = default)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!Moves(element, durationKey, out var duration, kind))
        {
            // Une transition en cours s'arrête là : l'élément est entier, à sa place.
            element.BeginAnimation(UIElement.OpacityProperty, null);
            if (element.RenderTransform is TranslateTransform { IsFrozen: false } still)
            {
                still.BeginAnimation(TranslateTransform.YProperty, null);
                still.BeginAnimation(TranslateTransform.XProperty, null);
            }
            return false;
        }
        // À la fin, l'animation rend la main : l'opacité et la place de l'élément redeviennent celles qu'il avait.
        element.BeginAnimation(UIElement.OpacityProperty, Arrival(0, 1, duration, delay));
        if (rise != 0 || shift != 0)
        {
            var move = new TranslateTransform();
            element.RenderTransform = move;
            move.BeginAnimation(TranslateTransform.YProperty, Arrival(rise, 0, duration, delay));
            move.BeginAnimation(TranslateTransform.XProperty, Arrival(shift, 0, duration, delay));
        }
        return true;
    }

    /// <summary>
    /// Le trajet d'une arrivée, de <paramref name="from"/> à <paramref name="to"/>. Avec un délai, l'élément est tenu à son
    /// point de départ le temps d'attendre son tour, puis fait le même trajet : sans cela, il serait entier avant d'arriver.
    /// </summary>
    private static AnimationTimeline Arrival(double from, double to, TimeSpan duration, TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            return new DoubleAnimation(from, to, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop };
        }
        var frames = new DoubleAnimationUsingKeyFrames { Duration = delay + duration, FillBehavior = FillBehavior.Stop };
        frames.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        frames.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(delay)));
        frames.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(delay + duration), Ease));
        return frames;
    }

    /// <summary>
    /// Quand part une transition lancée pendant que l'application est occupée (ouvrir l'éditeur, changer de vue) : une fois la
    /// nouvelle vue mise en page et dessinée, avant le travail de fond. Plus tôt, elle serait finie avant la première image ;
    /// plus tard (l'application au repos), la mise en forme d'une longue fiche la retarderait de plusieurs secondes.
    /// </summary>
    internal const System.Windows.Threading.DispatcherPriority WhenShown = System.Windows.Threading.DispatcherPriority.Input;

    /// <summary>La décélération douce de tout ce qui arrive : sans rebond.</summary>
    internal static IEasingFunction Ease { get; } = Frozen(new CubicEase { EasingMode = EasingMode.EaseOut });

    /// <summary>L'accélération de ce qui part : il s'en va de plus en plus vite, sans traîner.</summary>
    internal static IEasingFunction Leave { get; } = Frozen(new CubicEase { EasingMode = EasingMode.EaseIn });

    // Le numéro du départ en cours de chaque élément.
    private static readonly DependencyProperty ExitRunProperty =
        DependencyProperty.RegisterAttached("ExitRun", typeof(int), typeof(Transitions), new PropertyMetadata(0));

    /// <summary>
    /// Arrête le départ en cours de <paramref name="element"/> (voir <see cref="Exit"/>) : il est là, entier, à sa place, et
    /// la suite prévue à son départ n'est pas appelée.
    /// </summary>
    public static void StopExit(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(ExitRunProperty, (int)element.GetValue(ExitRunProperty) + 1);
        element.BeginAnimation(UIElement.OpacityProperty, null);
        Rest(element);
    }

    /// <summary>
    /// Fait partir <paramref name="element"/> : il s'efface, en accélérant, et descend ou glisse de quelques pixels si on le
    /// demande. Il reste effacé jusqu'à ce que <paramref name="done"/> le retire de l'écran, puis retrouve son opacité.
    /// </summary>
    /// <param name="element">Ce qui part.</param>
    /// <param name="done">Appelé une fois l'élément effacé ; tout de suite s'il n'y a pas de transition.</param>
    /// <param name="durationKey">La durée, par sa clé du design system : un départ est court.</param>
    /// <param name="drop">De combien de pixels il descend en partant.</param>
    /// <param name="shift">De combien de pixels il glisse de côté en partant (négatif : vers la gauche).</param>
    /// <param name="kind">La famille de l'animation.</param>
    /// <returns>Vrai si une transition a commencé.</returns>
    public static bool Exit(FrameworkElement element, Action done, string durationKey = DesignKeys.FastDuration, double drop = 0, double shift = 0, MotionKinds kind = MotionKinds.Surfaces)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(done);
        var run = (int)element.GetValue(ExitRunProperty) + 1;
        element.SetValue(ExitRunProperty, run);
        if (!Moves(element, durationKey, out var duration, kind))
        {
            done();
            return false;
        }
        var fade = new DoubleAnimation(element.Opacity, 0, duration) { EasingFunction = Leave, FillBehavior = FillBehavior.HoldEnd };
        fade.Completed += (_, _) =>
        {
            // Une animation retirée va quand même à son terme : un départ arrêté, ou remplacé, ne prévient plus personne.
            if ((int)element.GetValue(ExitRunProperty) != run)
            {
                return;
            }
            done();
            // Retiré de l'écran, il redevient entier, à sa place, pour la prochaine fois.
            element.BeginAnimation(UIElement.OpacityProperty, null);
            Rest(element);
        };
        if (drop != 0 || shift != 0)
        {
            var move = new TranslateTransform();
            element.RenderTransform = move;
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, drop, duration) { EasingFunction = Leave, FillBehavior = FillBehavior.HoldEnd });
            move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, shift, duration) { EasingFunction = Leave, FillBehavior = FillBehavior.HoldEnd });
        }
        element.BeginAnimation(UIElement.OpacityProperty, fade);
        return true;
    }

    /// <summary>
    /// La durée de <paramref name="durationKey"/> pour cet élément, et s'il y a lieu de bouger : non quand les animations sont
    /// réduites (durée nulle), hors de tout thème, ou pendant les essais.
    /// </summary>
    internal static bool Moves(FrameworkElement element, string durationKey, out TimeSpan duration, MotionKinds kind = MotionKinds.Views)
    {
        // Une famille coupée dans les Paramètres ne bouge pas : c'est le même chemin que les animations réduites.
        if (element.TryFindResource(DesignKeys.MotionOff) is MotionKinds off && (off & kind) != 0)
        {
            duration = TimeSpan.Zero;
            return false;
        }
        duration = element.TryFindResource(durationKey) is Duration { HasTimeSpan: true } found ? found.TimeSpan : TimeSpan.Zero;
        return !Skip && duration > TimeSpan.Zero;
    }

    /// <summary>
    /// La durée d'une transition pour cet élément, à donner à ce qui s'anime hors de WPF (la page de l'éditeur) : nulle
    /// quand il n'y a pas lieu de bouger.
    /// </summary>
    public static TimeSpan DurationOf(FrameworkElement element, string durationKey = DesignKeys.NormalDuration, MotionKinds kind = MotionKinds.Views)
    {
        ArgumentNullException.ThrowIfNull(element);
        return Moves(element, durationKey, out var duration, kind) ? duration : TimeSpan.Zero;
    }

    private static CubicEase Frozen(CubicEase ease)
    {
        ease.Freeze();
        return ease;
    }
}
