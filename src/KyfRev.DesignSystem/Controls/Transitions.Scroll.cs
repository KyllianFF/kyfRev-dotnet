using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Le défilement accompagné (rangée 20D des maquettes) : aller à un titre, à un passage, au trou suivant ne saute plus, la
/// vue y glisse. De loin, elle saute d'abord tout près : on ne voit pas défiler toute la fiche.
/// </summary>
public static partial class Transitions
{
    /// <summary>De combien de hauteurs de vue on glisse au plus ; au-delà, la vue saute d'abord tout près.</summary>
    public const double MostScrolledViews = 1.5;

    /// <summary>Accélère puis ralentit : la vue part et arrive en douceur.</summary>
    internal static IEasingFunction ScrollEase { get; } = Frozen(new CubicEase { EasingMode = EasingMode.EaseInOut });

    // Le défilement qu'une animation fait avancer. Sans valeur (pas d'animation) : rien ne défile.
    private static readonly DependencyProperty GlidingOffsetProperty =
        DependencyProperty.RegisterAttached("GlidingOffset", typeof(double), typeof(Transitions), new PropertyMetadata(double.NaN, (scroller, e) =>
        {
            if (e.NewValue is double offset && !double.IsNaN(offset))
            {
                ((ScrollViewer)scroller).ScrollToVerticalOffset(offset);
            }
        }));

    // Le numéro du trajet en cours de chaque vue : un trajet arrêté ou remplacé va quand même à son terme, sans plus rien faire.
    private static readonly DependencyProperty ScrollRunProperty =
        DependencyProperty.RegisterAttached("ScrollRun", typeof(int), typeof(Transitions), new PropertyMetadata(0));

    private static int NextScroll(ScrollViewer scroller)
    {
        var run = (int)scroller.GetValue(ScrollRunProperty) + 1;
        scroller.SetValue(ScrollRunProperty, run);
        scroller.BeginAnimation(GlidingOffsetProperty, null);
        return run;
    }

    /// <summary>
    /// Fait défiler <paramref name="scroller"/> jusqu'à <paramref name="offset"/>, en glissant ; d'un coup si les animations
    /// sont coupées ou s'il y est presque.
    /// </summary>
    /// <param name="scroller">La vue qui défile.</param>
    /// <param name="offset">Où arriver.</param>
    /// <param name="done">Appelé une fois arrivé ; tout de suite s'il n'y a pas de trajet.</param>
    /// <param name="durationKey">La durée, par sa clé du design system.</param>
    /// <param name="kind">La famille de l'animation.</param>
    /// <returns>Vrai si la vue glisse.</returns>
    public static bool ScrollTo(ScrollViewer scroller, double offset, Action? done = null, string durationKey = DesignKeys.NormalDuration, MotionKinds kind = MotionKinds.Views)
    {
        ArgumentNullException.ThrowIfNull(scroller);
        // Un trajet en cours s'arrête là où il en est.
        var run = NextScroll(scroller);
        var target = Math.Clamp(offset, 0, Math.Max(0, scroller.ScrollableHeight));
        var from = scroller.VerticalOffset;
        if (Math.Abs(target - from) < 1 || !Moves(scroller, durationKey, out var duration, kind))
        {
            scroller.ScrollToVerticalOffset(offset);
            done?.Invoke();
            return false;
        }
        var most = Math.Max(1, scroller.ViewportHeight) * MostScrolledViews;
        if (Math.Abs(target - from) > most)
        {
            from = target - (Math.Sign(target - from) * most);
        }
        var glide = new DoubleAnimation(from, target, duration) { EasingFunction = ScrollEase, FillBehavior = FillBehavior.Stop };
        glide.Completed += (_, _) =>
        {
            if ((int)scroller.GetValue(ScrollRunProperty) != run)
            {
                return;
            }
            scroller.ScrollToVerticalOffset(target);
            done?.Invoke();
        };
        scroller.BeginAnimation(GlidingOffsetProperty, glide);
        return true;
    }

    /// <summary>Arrête le défilement accompagné de <paramref name="scroller"/> : l'utilisateur a repris la main (molette, barre).</summary>
    public static void StopScroll(ScrollViewer scroller)
    {
        ArgumentNullException.ThrowIfNull(scroller);
        NextScroll(scroller);
    }
}
