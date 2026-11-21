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
    /// <returns>Vrai si une transition a commencé ; faux si l'élément est montré tel quel (animations réduites, essais).</returns>
    public static bool Enter(FrameworkElement element, string durationKey = DesignKeys.NormalDuration, double rise = 0, double shift = 0, MotionKinds kind = MotionKinds.Views)
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
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
        if (rise != 0 || shift != 0)
        {
            var move = new TranslateTransform();
            element.RenderTransform = move;
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(rise, 0, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
            move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(shift, 0, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
        }
        return true;
    }

    /// <summary>
    /// Quand part une transition lancée pendant que l'application est occupée (ouvrir l'éditeur, changer de vue) : une fois la
    /// nouvelle vue mise en page et dessinée, avant le travail de fond. Plus tôt, elle serait finie avant la première image ;
    /// plus tard (l'application au repos), la mise en forme d'une longue fiche la retarderait de plusieurs secondes.
    /// </summary>
    internal const System.Windows.Threading.DispatcherPriority WhenShown = System.Windows.Threading.DispatcherPriority.Input;

    /// <summary>La décélération douce de toutes les transitions : sans rebond.</summary>
    internal static IEasingFunction Ease { get; } = Frozen(new CubicEase { EasingMode = EasingMode.EaseOut });

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
