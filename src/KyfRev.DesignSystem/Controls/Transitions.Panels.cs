using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Ce qui change de place dans la fenêtre : un ruban qui paraît quand la page a fini de s'ouvrir, un menu de côté qui se
/// réduit, s'élargit ou se masque. Rien ici ne refait la mise en page à chaque image : la mise en page change une fois,
/// d'un coup, et seuls le rendu (une découpe, une échelle, un décalage) et l'opacité sont animés. C'est ce qui garde ces
/// transitions légères, même au-dessus d'une longue fiche.
/// </summary>
public static partial class Transitions
{
    /// <summary>
    /// Fait apparaître <paramref name="element"/> comme <see cref="Enter"/>, mais seulement une fois l'écran prêt : il reste
    /// invisible jusque-là. Quand ce qui le montre occupe l'interface un instant (une page qui ouvre son éditeur), une
    /// transition lancée tout de suite serait finie avant la première image.
    /// </summary>
    /// <returns>Vrai si une transition est prévue ; faux si l'élément est montré tel quel (animations réduites, essais).</returns>
    public static bool EnterOnceShown(FrameworkElement element, string durationKey = DesignKeys.NormalDuration, double rise = 0, double shift = 0, MotionKinds kind = MotionKinds.Views)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!Moves(element, durationKey, out _, kind))
        {
            return Enter(element, durationKey, rise, shift, kind);
        }
        // Invisible en attendant ; la transition, en partant, prend la place de cette attente.
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 0, TimeSpan.Zero) { FillBehavior = FillBehavior.HoldEnd });
        element.Dispatcher.BeginInvoke(WhenShown, () => Enter(element, durationKey, rise, shift, kind));
        return true;
    }

    /// <summary>
    /// Fait suivre un élément que la mise en page vient de déplacer de haut en bas (un bloc paru au-dessus de lui l'a poussé,
    /// ou parti, l'a laissé remonter) : il part de là où il était, <paramref name="fromY"/> pixels plus loin, et rejoint sa
    /// place. Comme <see cref="EnterOnceShown"/>, le trajet ne commence qu'une fois la nouvelle vue affichée ; d'ici là,
    /// l'élément reste où il était.
    /// </summary>
    /// <param name="element">Ce qui a été déplacé.</param>
    /// <param name="fromY">D'où il part, depuis sa nouvelle place : négatif s'il a été poussé vers le bas.</param>
    /// <param name="durationKey">La durée, par sa clé du design system.</param>
    /// <param name="kind">La famille de l'animation.</param>
    /// <param name="done">Appelé une fois l'élément à sa place ; tout de suite s'il n'y a pas de trajet.</param>
    /// <returns>Vrai si un trajet est prévu.</returns>
    public static bool FollowOnceShown(FrameworkElement element, double fromY, string durationKey = DesignKeys.NormalDuration, MotionKinds kind = MotionKinds.Views, Action? done = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (Math.Abs(fromY) < 0.5 || !Moves(element, durationKey, out var duration, kind))
        {
            Rest(element);
            done?.Invoke();
            return false;
        }
        var move = new TranslateTransform(0, fromY);
        element.RenderTransform = move;
        element.Dispatcher.BeginInvoke(WhenShown, () =>
        {
            if (!ReferenceEquals(element.RenderTransform, move))
            {
                // Un autre trajet a pris le relais.
                done?.Invoke();
                return;
            }
            var glide = new DoubleAnimation(fromY, 0, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop };
            glide.Completed += (_, _) =>
            {
                if (ReferenceEquals(element.RenderTransform, move))
                {
                    element.ClearValue(UIElement.RenderTransformProperty);
                }
                done?.Invoke();
            };
            move.Y = 0;
            move.BeginAnimation(TranslateTransform.YProperty, glide);
        });
        return true;
    }

    /// <summary>
    /// Décale un élément de <paramref name="toY"/> pixels, en glissant, et l'y laisse jusqu'à <see cref="Rest"/> : le temps
    /// qu'un autre prenne sa place, exactement là.
    /// </summary>
    /// <returns>Vrai si l'élément glisse ; faux s'il ne bouge pas (animations coupées) : il reste alors à sa place.</returns>
    public static bool ShiftTo(FrameworkElement element, double toY, string durationKey = DesignKeys.FastDuration, MotionKinds kind = MotionKinds.Views, Action? done = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (Math.Abs(toY) < 0.5 || !Moves(element, durationKey, out var duration, kind))
        {
            done?.Invoke();
            return false;
        }
        var move = new TranslateTransform();
        element.RenderTransform = move;
        var shift = new DoubleAnimation(0, toY, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.HoldEnd };
        shift.Completed += (_, _) => done?.Invoke();
        move.BeginAnimation(TranslateTransform.YProperty, shift);
        return true;
    }

    /// <summary>
    /// Le jumeau de <see cref="FollowOnceShown"/> pour un élément que la mise en page vient de déplacer de côté (un volet
    /// paru à côté de lui) : il part de là où il était, <paramref name="fromX"/> pixels plus loin, et rejoint sa place en
    /// même temps que le volet glisse.
    /// </summary>
    /// <param name="element">Ce qui a été déplacé.</param>
    /// <param name="fromX">D'où il part, depuis sa nouvelle place : négatif s'il a été poussé vers la droite.</param>
    /// <param name="durationKey">La durée, par sa clé du design system.</param>
    /// <param name="kind">La famille de l'animation.</param>
    /// <returns>Vrai si un trajet est prévu.</returns>
    public static bool FollowAsideOnceShown(FrameworkElement element, double fromX, string durationKey = DesignKeys.NormalDuration, MotionKinds kind = MotionKinds.Panels)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (Math.Abs(fromX) < 0.5 || !Moves(element, durationKey, out var duration, kind))
        {
            Rest(element);
            return false;
        }
        var move = new TranslateTransform(fromX, 0);
        element.RenderTransform = move;
        element.Dispatcher.BeginInvoke(WhenShown, () =>
        {
            if (!ReferenceEquals(element.RenderTransform, move))
            {
                // Un autre trajet a pris le relais.
                return;
            }
            var glide = new DoubleAnimation(fromX, 0, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop };
            glide.Completed += (_, _) =>
            {
                if (ReferenceEquals(element.RenderTransform, move))
                {
                    element.ClearValue(UIElement.RenderTransformProperty);
                }
            };
            move.X = 0;
            move.BeginAnimation(TranslateTransform.XProperty, glide);
        });
        return true;
    }

    /// <summary>
    /// Décale un élément de <paramref name="toX"/> pixels de côté, en glissant, une fois l'écran prêt, et l'y laisse jusqu'à
    /// <see cref="Rest"/> : un volet s'en va à côté de lui, et il rejoint d'avance la place que la mise en page lui donnera
    /// quand le volet l'aura rendue. Il suit la courbe de ce qui part, pour bouger avec le volet.
    /// </summary>
    /// <returns>Vrai si l'élément va glisser ; faux s'il ne bouge pas (animations coupées).</returns>
    public static bool ShiftAsideOnceShown(FrameworkElement element, double toX, string durationKey = DesignKeys.NormalDuration, MotionKinds kind = MotionKinds.Panels)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (Math.Abs(toX) < 0.5 || !Moves(element, durationKey, out var duration, kind))
        {
            return false;
        }
        var move = new TranslateTransform();
        element.RenderTransform = move;
        element.Dispatcher.BeginInvoke(WhenShown, () =>
        {
            if (ReferenceEquals(element.RenderTransform, move))
            {
                move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, toX, duration) { EasingFunction = Leave, FillBehavior = FillBehavior.HoldEnd });
            }
        });
        return true;
    }

    /// <summary>Remet un élément à sa place : plus de trajet, plus de décalage.</summary>
    public static void Rest(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.RenderTransform is TranslateTransform { IsFrozen: false } move)
        {
            move.BeginAnimation(TranslateTransform.XProperty, null);
            move.BeginAnimation(TranslateTransform.YProperty, null);
            element.ClearValue(UIElement.RenderTransformProperty);
        }
    }

    /// <summary>
    /// Un panneau de côté vient de changer de largeur dans la mise en page (de <paramref name="from"/> à
    /// <paramref name="to"/>, 0 : masqué) : à l'écran, son bord glisse de l'ancienne largeur à la nouvelle. Élargi, il se
    /// découvre peu à peu ; réduit, un rideau de sa couleur (<paramref name="curtain"/>, posé à son bord, par-dessus la
    /// page) se retire. Ce qui le suit (<paramref name="follower"/>, la page) glisse avec le bord, s'il peut être déplacé.
    /// </summary>
    /// <param name="panel">Le panneau, déjà à sa nouvelle largeur.</param>
    /// <param name="curtain">Le rideau : un rectangle de la couleur du panneau, dans un canevas qui couvre panneau et page, caché hors transition.</param>
    /// <param name="follower">Ce qui longe le panneau ; aucun s'il contient une fenêtre native, que le rendu ne déplace pas.</param>
    /// <param name="from">La largeur d'avant.</param>
    /// <param name="to">La largeur de maintenant.</param>
    /// <returns>Vrai si une transition a commencé.</returns>
    public static bool Resize(FrameworkElement panel, FrameworkElement curtain, FrameworkElement? follower, double from, double to)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(curtain);
        if (Math.Abs(from - to) < 0.5 || !Moves(panel, DesignKeys.NormalDuration, out var duration, MotionKinds.Panels))
        {
            StopResize(panel, curtain, follower);
            return false;
        }
        StopResize(panel, curtain, follower);
        if (to > from)
        {
            // Élargi : le panneau, entier dans la mise en page, se découvre depuis son ancienne largeur.
            var clip = new RectangleGeometry(new Rect(0, 0, from, Tall));
            var reveal = new RectAnimation(new Rect(0, 0, from, Tall), new Rect(0, 0, to, Tall), duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop };
            reveal.Completed += (_, _) =>
            {
                if (ReferenceEquals(panel.Clip, clip))
                {
                    panel.Clip = null;
                }
            };
            panel.Clip = clip;
            clip.BeginAnimation(RectangleGeometry.RectProperty, reveal);
        }
        else
        {
            // Réduit : le rideau tient la place que le panneau vient de quitter, et se retire vers son nouveau bord.
            var scale = new ScaleTransform(1, 1);
            var retreat = new DoubleAnimation(1, 0, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop };
            retreat.Completed += (_, _) =>
            {
                if (ReferenceEquals(curtain.RenderTransform, scale))
                {
                    curtain.Visibility = Visibility.Collapsed;
                }
            };
            System.Windows.Controls.Canvas.SetLeft(curtain, to);
            curtain.Width = from - to;
            curtain.Height = Math.Max(panel.ActualHeight, (VisualTreeHelper.GetParent(curtain) as FrameworkElement)?.ActualHeight ?? 0);
            curtain.RenderTransformOrigin = new Point(0, 0);
            curtain.RenderTransform = scale;
            curtain.Visibility = Visibility.Visible;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, retreat);
        }
        if (follower is not null)
        {
            var move = new TranslateTransform();
            follower.RenderTransform = move;
            move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(from - to, 0, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
        }
        return true;
    }

    /// <summary>Plus haut que toute fenêtre : la découpe du panneau ne dépend pas de sa hauteur.</summary>
    private const double Tall = 100_000;

    /// <summary>Une transition de largeur en cours s'arrête là : le panneau entier, le rideau retiré, la page à sa place.</summary>
    private static void StopResize(FrameworkElement panel, FrameworkElement curtain, FrameworkElement? follower)
    {
        panel.Clip = null;
        curtain.Visibility = Visibility.Collapsed;
        curtain.RenderTransform = Transform.Identity;
        if (follower?.RenderTransform is TranslateTransform { IsFrozen: false } moving)
        {
            moving.BeginAnimation(TranslateTransform.XProperty, null);
        }
    }
}
