using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// L'arrivée d'une page quand on change de vue : elle monte de quelques pixels et se pose, comme les pages des Paramètres de
/// Windows ou de PowerToys. Un seul mouvement, sur la page entière ; rien d'autre ne s'anime pendant qu'elle arrive.
/// </summary>
public static partial class Transitions
{
    /// <summary>
    /// De combien une page monte en arrivant. PowerToys part de 140 pixels ; ici la montée est légère : le décalage que
    /// Windows donne à un contenu qui entre dans une page déjà là.
    /// </summary>
    public const double PageRise = 28;

    /// <summary>
    /// La courbe d'arrivée d'une page, celle de Windows (0,1 ; 0,9 ; 0,2 ; 1) : elle fait les deux tiers du chemin en un
    /// dixième du temps, puis se pose longuement. C'est elle, plus que la distance, qui donne l'impression de fluidité.
    /// </summary>
    internal static KeySpline PageEase { get; } = FrozenSpline();

    private static KeySpline FrozenSpline()
    {
        var spline = new KeySpline(0.1, 0.9, 0.2, 1);
        spline.Freeze();
        return spline;
    }

    /// <summary>
    /// Vrai sur une page qui arrive, et sur tout ce qu'elle contient : ce qui s'animerait de son côté en paraissant (une
    /// liste en cascade) s'en abstient, pour qu'il n'y ait qu'un mouvement à l'écran.
    /// </summary>
    public static readonly DependencyProperty IsArrivingProperty =
        DependencyProperty.RegisterAttached("IsArriving", typeof(bool), typeof(Transitions), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Lit <see cref="IsArrivingProperty"/>.</summary>
    public static bool GetIsArriving(DependencyObject element) => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(IsArrivingProperty);

    // Le numéro de la dernière arrivée de chaque page : une arrivée remplacée par une autre ne touche plus à rien.
    private static readonly DependencyProperty ArrivalProperty =
        DependencyProperty.RegisterAttached("Arrival", typeof(int), typeof(Transitions), new PropertyMetadata(0));

    /// <summary>
    /// Fait arriver <paramref name="page"/>, dont le contenu vient de changer : elle reste invisible le temps de se mettre en
    /// page (sans cela, le mouvement commencerait pendant que l'application est occupée et sauterait des images), puis monte
    /// de <see cref="PageRise"/> pixels en se posant, et paraît en fondu sur le tout début du trajet.
    /// </summary>
    /// <param name="page">Ce qui porte la vue.</param>
    /// <param name="unless">
    /// Demandé une fois la vue mise en page : vrai si elle ne doit pas bouger (elle montre une fenêtre native, qui ne
    /// suivrait pas le mouvement). Elle est alors montrée telle quelle.
    /// </param>
    /// <returns>Vrai si une arrivée est prévue ; faux si la page est montrée telle quelle (animations coupées).</returns>
    public static bool Arrive(FrameworkElement page, Func<bool>? unless = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        var arrival = (int)page.GetValue(ArrivalProperty) + 1;
        page.SetValue(ArrivalProperty, arrival);
        // Une arrivée en cours s'arrête là : la page est entière, à sa place.
        page.BeginAnimation(UIElement.OpacityProperty, null);
        Rest(page);
        page.ClearValue(IsArrivingProperty);
        if (!Moves(page, DesignKeys.PageDuration, out var duration, MotionKinds.Views))
        {
            return false;
        }
        page.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 0, TimeSpan.Zero) { FillBehavior = FillBehavior.HoldEnd });
        page.SetValue(IsArrivingProperty, true);
        page.Dispatcher.BeginInvoke(WhenShown, () =>
        {
            if ((int)page.GetValue(ArrivalProperty) != arrival)
            {
                return;
            }
            if (unless?.Invoke() == true)
            {
                page.BeginAnimation(UIElement.OpacityProperty, null);
                page.ClearValue(IsArrivingProperty);
                return;
            }
            var move = new TranslateTransform();
            page.RenderTransform = move;
            // Tenue en bas au départ, puis la montée : à la fin, l'animation rend la main et la page est à sa place.
            var rise = new DoubleAnimationUsingKeyFrames { Duration = duration, FillBehavior = FillBehavior.Stop };
            rise.KeyFrames.Add(new DiscreteDoubleKeyFrame(PageRise, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            rise.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(duration), PageEase));
            rise.Completed += (_, _) =>
            {
                if (ReferenceEquals(page.RenderTransform, move))
                {
                    page.ClearValue(UIElement.RenderTransformProperty);
                }
                if ((int)page.GetValue(ArrivalProperty) == arrival)
                {
                    page.ClearValue(IsArrivingProperty);
                }
            };
            move.BeginAnimation(TranslateTransform.YProperty, rise);
            // Le fondu ne dure que le début du trajet : la page est entière bien avant de se poser.
            var fade = Moves(page, DesignKeys.FastDuration, out var brief, MotionKinds.Views) ? brief : duration;
            page.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, fade) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
        });
        return true;
    }
}
