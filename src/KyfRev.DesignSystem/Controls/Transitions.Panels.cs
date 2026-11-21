using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

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
    public static bool EnterOnceShown(FrameworkElement element, string durationKey = DesignKeys.NormalDuration, double rise = 0, double shift = 0)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!Moves(element, durationKey, out _))
        {
            return Enter(element, durationKey, rise, shift);
        }
        // Invisible en attendant ; la transition, en partant, prend la place de cette attente.
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 0, TimeSpan.Zero) { FillBehavior = FillBehavior.HoldEnd });
        element.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => Enter(element, durationKey, rise, shift));
        return true;
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
        if (Math.Abs(from - to) < 0.5 || !Moves(panel, DesignKeys.NormalDuration, out var duration))
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
