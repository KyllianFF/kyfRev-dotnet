using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Le rang 12 de la liste « Fluidité » : un bloc qui glisse (un volet, une barre, le texte qui suit un volet) est mis en image
/// le temps du trajet, au lieu d'être redessiné à chaque image ; il redevient lui-même à l'arrivée.
/// </summary>
public static partial class Transitions
{
    /// <summary>
    /// Au-delà de cette surface (en pixels indépendants), un bloc n'est pas mis en image : l'image coûterait plus en mémoire
    /// graphique qu'elle ne ferait gagner. 4 millions : un bloc de 2 560 × 1 560 au plus.
    /// </summary>
    internal const double LargestCachedArea = 4_000_000;

    // L'image posée pour les trajets (pour ne jamais retirer une image que l'élément avait déjà), et les trajets en cours.
    private static readonly DependencyProperty TripCacheProperty =
        DependencyProperty.RegisterAttached("TripCache", typeof(BitmapCache), typeof(Transitions), new PropertyMetadata(null));

    private static readonly DependencyProperty TripsProperty =
        DependencyProperty.RegisterAttached("Trips", typeof(int), typeof(Transitions), new PropertyMetadata(0));

    /// <summary>
    /// Met <paramref name="element"/> en image pendant <paramref name="trip"/>, s'il ne l'est pas déjà, puis le rend à
    /// lui-même quand le trajet finit. Un trajet qui en relance un autre garde l'image jusqu'à la fin du dernier.
    /// </summary>
    /// <returns>Vrai si l'élément est en image pour ce trajet.</returns>
    internal static bool CacheWhileMoving(UIElement element, Timeline trip)
    {
        var ours = element.GetValue(TripCacheProperty) as BitmapCache;
        if (element.CacheMode is { } existing && !ReferenceEquals(existing, ours))
        {
            // Une image voulue par l'élément lui-même : c'est la sienne, elle reste.
            return false;
        }
        var size = element.RenderSize;
        if (size.Width * size.Height > LargestCachedArea)
        {
            return false;
        }
        if (ours is null)
        {
            // À l'échelle de l'écran : un texte mis en image reste net sur un écran à 150 %.
            ours = new BitmapCache(Math.Max(1, VisualTreeHelper.GetDpi(element).DpiScaleX)) { SnapsToDevicePixels = true };
            element.SetValue(TripCacheProperty, ours);
            element.CacheMode = ours;
        }
        element.SetValue(TripsProperty, (int)element.GetValue(TripsProperty) + 1);
        var cache = ours;
        trip.Completed += (_, _) => EndTrip(element, cache);
        return true;
    }

    /// <summary>Le trajet est arrêté en route (l'élément remis à sa place) : il redevient lui-même tout de suite.</summary>
    internal static void EndTrips(UIElement element)
    {
        element.SetValue(TripsProperty, 0);
        if (element.GetValue(TripCacheProperty) is BitmapCache cache && ReferenceEquals(element.CacheMode, cache))
        {
            element.ClearValue(UIElement.CacheModeProperty);
        }
        element.ClearValue(TripCacheProperty);
    }

    /// <summary>Un trajet de plus vient de finir : au dernier, l'élément est rendu à lui-même.</summary>
    private static void EndTrip(UIElement element, BitmapCache cache)
    {
        var trips = Math.Max(0, (int)element.GetValue(TripsProperty) - 1);
        element.SetValue(TripsProperty, trips);
        if (trips == 0 && ReferenceEquals(element.CacheMode, cache))
        {
            element.ClearValue(UIElement.CacheModeProperty);
            element.ClearValue(TripCacheProperty);
        }
    }
}
