using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Les listes qui se remplissent et se vident (rangée 20C des maquettes). Une liste qu'on voit pour la première fois arrive
/// en courte cascade, ses premières lignes l'une après l'autre. Une ligne retirée s'efface à sa place, et celles d'en dessous
/// remontent en glissant au lieu de sauter. Comme pour les lignes nouvelles, une ligne se reconnaît à sa clé
/// (<see cref="NewcomerKeyProperty"/>) : les listes de l'application sont reconstruites entières à chaque changement.
/// </summary>
public static partial class Transitions
{
    /// <summary>Combien de lignes arrivent l'une après l'autre quand une liste paraît ; les suivantes arrivent ensemble, juste après.</summary>
    public const int CascadeRows = 6;

    /// <summary>Combien de lignes une cascade anime au plus : au-delà, elles sont hors de l'écran.</summary>
    public const int MostCascadedRows = 80;

    /// <summary>L'écart entre deux lignes de la cascade.</summary>
    public static readonly TimeSpan CascadeStep = TimeSpan.FromMilliseconds(35);

    /// <summary>Au-delà, ce n'est plus un retrait mais une liste qui change : rien ne s'anime.</summary>
    public const int MostLeavers = 3;

    /// <summary>L'espace entre deux lignes qu'on admet en plus de la hauteur d'une ligne retirée, quand on vérifie de combien les suivantes ont monté.</summary>
    private const double RowSpacingAllowance = 32;

    /// <summary>Une ligne à la dernière fois que la liste a été regardée : sa clé, sa place dans la liste, et elle-même.</summary>
    private sealed record RowPlace(object Key, Rect Place, WeakReference<FrameworkElement> Row);

    /// <summary>La liste à la dernière fois qu'elle a été regardée : ses lignes à l'écran, dans l'ordre, et combien elle en comptait en tout.</summary>
    private sealed class ListPlaces
    {
        public List<RowPlace> Rows { get; } = [];

        public int Items { get; set; }
    }

    private static readonly ConditionalWeakTable<ItemsControl, ListPlaces> Places = [];

    // Les images des lignes qui viennent d'être retirées, prises avant que rien ne soit redessiné, en attendant la mise en page.
    private static readonly ConditionalWeakTable<ItemsControl, List<(Rect Place, ImageSource Picture)>> Leavers = [];

    private static readonly DependencyProperty PlacesPendingProperty =
        DependencyProperty.RegisterAttached("PlacesPending", typeof(bool), typeof(Transitions), new PropertyMetadata(false));

    /// <summary>
    /// Les lignes d'une liste qu'on voit pour la première fois arrivent en fondu, en montant un peu : les premières l'une après
    /// l'autre, les suivantes ensemble avec la dernière d'entre elles. Aucune ligne n'est là avant celles du haut : une
    /// liste dont le bas serait déjà affiché pendant que le haut arrive paraîtrait trouée. Les lignes sous le bas de la
    /// fenêtre ne s'animent pas.
    /// </summary>
    /// <returns>Combien de lignes arrivent.</returns>
    internal static int Cascade(ItemsControl list)
    {
        if (!list.IsVisible)
        {
            return 0;
        }
        var window = Window.GetWindow(list);
        var shown = 0;
        for (var index = 0; index < list.Items.Count && shown < MostCascadedRows; index++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement { IsVisible: true } row)
            {
                continue;
            }
            if (window is not null && shown >= CascadeRows && row.TranslatePoint(default, window).Y > window.ActualHeight)
            {
                break;
            }
            if (!Enter(row, DesignKeys.NormalDuration, rise: NewcomerRise, kind: MotionKinds.Lists, delay: CascadeStep * Math.Min(shown, CascadeRows - 1)))
            {
                break;
            }
            shown++;
        }
        return shown;
    }

    /// <summary>Retient où est chaque ligne de la liste, pour savoir d'où elle part si une autre est retirée.</summary>
    internal static int Place(ItemsControl list)
    {
        var places = Places.GetValue(list, _ => new ListPlaces());
        places.Rows.Clear();
        places.Items = list.Items.Count;
        foreach (var (item, key) in Keyed(list.Items, GetNewcomerKey(list)))
        {
            if (list.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement { IsVisible: true } row)
            {
                places.Rows.Add(new RowPlace(key, new Rect(LaidAt(row, list), row.RenderSize), new WeakReference<FrameworkElement>(row)));
            }
        }
        return places.Rows.Count;
    }

    /// <summary>
    /// La place que la mise en page donne à une ligne, dans le repère de la liste : sans le décalage d'un trajet en cours
    /// (une ligne qui arrive en montant, ou qui remonte encore après un retrait).
    /// </summary>
    private static Point LaidAt(FrameworkElement row, ItemsControl list)
    {
        var offset = VisualTreeHelper.GetOffset(row);
        var origin = VisualTreeHelper.GetParent(row) is UIElement parent ? parent.TranslatePoint(default, list) : default;
        return new Point(origin.X + offset.X, origin.Y + offset.Y);
    }

    /// <summary>La liste a défilé ou changé de taille : ses lignes ne sont plus où on les a vues. On les regarde de nouveau, une fois le calme revenu.</summary>
    private static void PlaceLater(ItemsControl list)
    {
        if ((bool)list.GetValue(PlacesPendingProperty))
        {
            return;
        }
        list.SetValue(PlacesPendingProperty, true);
        list.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            list.SetValue(PlacesPendingProperty, false);
            if (GetNewcomerKey(list).Length > 0)
            {
                Place(list);
            }
        });
    }

    /// <summary>
    /// Les lignes qu'on voyait et qui ne sont plus dans la liste, si c'est un retrait et rien d'autre : une à trois lignes en
    /// moins, toutes parmi celles qu'on voyait. Sinon (une autre liste, un filtre, un tri), rien.
    /// </summary>
    private static List<RowPlace> Gone(ItemsControl list, ListPlaces before)
    {
        // D'abord le compte, qui ne coûte rien : une liste qu'on reconstruit ligne à ligne passe ici à chaque ligne.
        var fewer = before.Items - list.Items.Count;
        if (before.Rows.Count == 0 || fewer is < 1 or > MostLeavers)
        {
            return [];
        }
        var kept = Keyed(list.Items, GetNewcomerKey(list)).Select(entry => entry.Key).ToHashSet();
        var gone = before.Rows.Where(place => !kept.Contains(place.Key)).ToList();
        return gone.Count == fewer ? gone : [];
    }

    /// <summary>
    /// La liste vient de changer, et rien n'a encore été redessiné : les lignes qui viennent d'en partir (trois au plus) sont
    /// prises en image, telles qu'on les voyait, pour s'effacer à leur place.
    /// </summary>
    /// <returns>Combien de lignes retirées ont été prises en image.</returns>
    internal static int Portray(ItemsControl list)
    {
        Leavers.Remove(list);
        if (!Places.TryGetValue(list, out var before) || !list.IsVisible || !Moves(list, DesignKeys.FastDuration, out _, MotionKinds.Lists))
        {
            return 0;
        }
        var dpi = VisualTreeHelper.GetDpi(list);
        var pictures = new List<(Rect Place, ImageSource Picture)>();
        foreach (var place in Gone(list, before))
        {
            if (!place.Row.TryGetTarget(out var row) || place.Place.Width < 1 || place.Place.Height < 1)
            {
                continue;
            }
            var picture = new RenderTargetBitmap(
                (int)Math.Ceiling(place.Place.Width * dpi.DpiScaleX), (int)Math.Ceiling(place.Place.Height * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            picture.Render(row);
            picture.Freeze();
            pictures.Add((place.Place, picture));
        }
        if (pictures.Count > 0)
        {
            Leavers.Add(list, pictures);
        }
        return pictures.Count;
    }

    /// <summary>
    /// La liste a perdu une à trois lignes, et rien d'autre n'a changé : leur image s'efface à leur place, et les lignes d'en
    /// dessous, que la mise en page vient de remonter, repartent de là où elles étaient. Si la liste a bougé entre-temps
    /// (elle a défilé, la fenêtre a changé de taille), rien ne s'anime : on ne ferait que la secouer.
    /// </summary>
    /// <returns>Combien de lignes glissent.</returns>
    internal static int Close(ItemsControl list)
    {
        Leavers.TryGetValue(list, out var pictures);
        Leavers.Remove(list);
        if (!Places.TryGetValue(list, out var before) || !list.IsVisible || Gone(list, before) is not { Count: > 0 } gone)
        {
            return 0;
        }
        var first = gone.Min(place => place.Place.Top);
        var most = gone.Sum(place => place.Place.Height + RowSpacingAllowance);
        var was = before.Rows.ToDictionary(place => place.Key, place => place.Place);
        var moves = new List<(FrameworkElement Row, double From)>();
        foreach (var (item, key) in Keyed(list.Items, GetNewcomerKey(list)))
        {
            if (!was.TryGetValue(key, out var place) || list.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement { IsVisible: true } row)
            {
                continue;
            }
            var at = LaidAt(row, list);
            var rise = place.Top - at.Y;
            if (Math.Abs(place.Left - at.X) >= 0.5 || rise < -0.5 || rise > most || (place.Top < first && rise >= 0.5))
            {
                // La liste n'est plus où on l'a vue : ce n'est pas le retrait qui a déplacé ses lignes.
                return 0;
            }
            if (rise >= 0.5)
            {
                moves.Add((row, rise));
            }
        }
        if (pictures is not null && AdornerLayer.GetAdornerLayer(list) is { } layer)
        {
            foreach (var (place, picture) in pictures)
            {
                var ghost = new LeaverAdorner(list, place, picture);
                layer.Add(ghost);
                Exit(ghost, () => layer.Remove(ghost), DesignKeys.FastDuration, kind: MotionKinds.Lists);
            }
        }
        var glides = 0;
        foreach (var (row, from) in moves)
        {
            if (FollowOnceShown(row, from, DesignKeys.NormalDuration, MotionKinds.Lists))
            {
                glides++;
            }
        }
        return glides;
    }

    /// <summary>L'image d'une ligne retirée, posée sur la liste à la place qu'elle avait, le temps de s'effacer. Rien ne s'y clique.</summary>
    internal sealed class LeaverAdorner : Adorner
    {
        private readonly Rect _place;
        private readonly ImageSource _picture;

        public LeaverAdorner(UIElement list, Rect place, ImageSource picture)
            : base(list)
        {
            _place = place;
            _picture = picture;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext) => drawingContext.DrawImage(_picture, _place);
    }

    /// <summary>Le gestionnaire « la liste a défilé ou changé de taille » de chaque liste, pour pouvoir le retirer.</summary>
    private static class PlaceHandlers
    {
        private static readonly ConditionalWeakTable<ItemsControl, ScrollChangedEventHandler> Scrolled = [];
        private static readonly ConditionalWeakTable<ItemsControl, SizeChangedEventHandler> Resized = [];

        public static ScrollChangedEventHandler ScrolledOf(ItemsControl list) => Scrolled.GetValue(list, owner => (_, _) => PlaceLater(owner));

        public static SizeChangedEventHandler ResizedOf(ItemsControl list) => Resized.GetValue(list, owner => (_, _) => PlaceLater(owner));
    }
}
