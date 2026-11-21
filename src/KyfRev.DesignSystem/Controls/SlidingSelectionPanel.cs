using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Une colonne de choix (le menu de gauche, les sections des Paramètres) dont la sélection glisse d'un choix à l'autre
/// (rangée 18B des maquettes) : le cadre du choix, sa couleur et son trait à gauche, est dessiné par la colonne, derrière
/// les choix, et va de l'ancien au nouveau. Les choix ne dessinent plus le leur (<see cref="Transitions.HasSlidingSelectionProperty"/>).
/// </summary>
public sealed class SlidingSelectionPanel : StackPanel
{
    /// <summary>L'épaisseur du trait à gauche du choix.</summary>
    internal const double BarThickness = 3;

    /// <summary>Où commence le cadre, de haut en bas.</summary>
    public static readonly DependencyProperty SelectionTopProperty =
        DependencyProperty.Register(nameof(SelectionTop), typeof(double), typeof(SlidingSelectionPanel), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Sa hauteur : celle du choix.</summary>
    public static readonly DependencyProperty SelectionHeightProperty =
        DependencyProperty.Register(nameof(SelectionHeight), typeof(double), typeof(SlidingSelectionPanel), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Son opacité : il paraît en fondu quand la sélection arrive d'une autre colonne.</summary>
    public static readonly DependencyProperty SelectionOpacityProperty =
        DependencyProperty.Register(nameof(SelectionOpacity), typeof(double), typeof(SlidingSelectionPanel), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    private Rect? _target;
    private bool _sliding;
    private int _run;

    /// <summary>Ce que le cadre a fait en dernier : rien, sauté, paru en fondu, ou glissé (pour les essais).</summary>
    internal SelectionMove LastMove { get; private set; }

    /// <summary>Une colonne dont la sélection glisse.</summary>
    public SlidingSelectionPanel()
    {
        Transitions.SetHasSlidingSelection(this, true);
        AddHandler(ListBoxItem.SelectedEvent, new RoutedEventHandler((_, e) => Place(animate: true, e.OriginalSource as FrameworkElement)), handledEventsToo: true);
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, e) => Place(animate: true, e.OriginalSource as FrameworkElement)), handledEventsToo: true);
        // Un choix quitté n'efface pas le cadre tout de suite : le nouveau choix arrive juste après, et le cadre doit glisser de
        // l'un à l'autre. Si rien n'est choisi au tour suivant (la sélection est partie dans l'autre colonne), il s'efface.
        var left = new RoutedEventHandler((_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => Place(animate: false)));
        AddHandler(ListBoxItem.UnselectedEvent, left, handledEventsToo: true);
        AddHandler(ToggleButton.UncheckedEvent, left, handledEventsToo: true);
        // Cachée ou montrée, même pendant un glissement : le cadre suit tout de suite.
        IsVisibleChanged += (_, _) => Place(animate: false);
        // Après chaque mise en page, le cadre est là où est le choix : à la première ouverture (le choix est fait avant d'avoir
        // une taille), quand le menu se réduit ou s'élargit, quand un groupe se replie. Sans effet si rien n'a bougé ; pendant un
        // glissement, la place visée est déjà la bonne.
        LayoutUpdated += (_, _) => Place(animate: false);
    }

    /// <inheritdoc cref="SelectionTopProperty"/>
    public double SelectionTop
    {
        get => (double)GetValue(SelectionTopProperty);
        set => SetValue(SelectionTopProperty, value);
    }

    /// <inheritdoc cref="SelectionHeightProperty"/>
    public double SelectionHeight
    {
        get => (double)GetValue(SelectionHeightProperty);
        set => SetValue(SelectionHeightProperty, value);
    }

    /// <inheritdoc cref="SelectionOpacityProperty"/>
    public double SelectionOpacity
    {
        get => (double)GetValue(SelectionOpacityProperty);
        set => SetValue(SelectionOpacityProperty, value);
    }

    /// <summary>Le choix sous lequel est le cadre, dans les coordonnées de la colonne ; aucun si rien n'est choisi.</summary>
    public Rect? Selection => _target;

    /// <summary>Met le cadre sous le choix : en glissant depuis l'ancien, en fondu s'il n'y en avait pas, ou d'un coup.</summary>
    internal void Place(bool animate, FrameworkElement? chosen = null)
    {
        chosen = chosen is ListBoxItem { IsSelected: true, IsVisible: true } or ToggleButton { IsChecked: true, IsVisible: true } ? chosen : Chosen(this);
        // Un choix qui n'a pas encore de taille (choisi avant sa première mise en page) n'a pas encore de place : elle viendra
        // à la mise en page suivante.
        var bounds = chosen is null || !IsVisible || !chosen.IsDescendantOf(this) || chosen.RenderSize.Height <= 0 || chosen.RenderSize.Width <= 0
            ? (Rect?)null
            : chosen.TransformToAncestor(this).TransformBounds(new Rect(chosen.RenderSize));
        // Rien n'a bougé : rien à redessiner (redessiner demande une mise en page, qui rappellerait cette méthode, sans fin).
        if (TabIndicatorAdorner.Same(_target, bounds))
        {
            return;
        }
        var before = _target;
        _target = bounds;
        if (bounds is not { } b)
        {
            BeginAnimation(SelectionTopProperty, null);
            BeginAnimation(SelectionHeightProperty, null);
            InvalidateVisual();
            return;
        }
        var canMove = Transitions.Moves(this, DesignKeys.NormalDuration, out var duration);
        // Un glissement que la mise en page rattrape en route (le choix a changé de taille) continue vers la nouvelle place.
        if (canMove && before is not null && (animate || _sliding))
        {
            var run = ++_run;
            _sliding = true;
            var slide = new DoubleAnimation(SelectionTop, b.Top, duration) { EasingFunction = Transitions.Ease, FillBehavior = FillBehavior.Stop };
            slide.Completed += (_, _) => _sliding = _sliding && run != _run;
            BeginAnimation(SelectionTopProperty, slide);
            BeginAnimation(SelectionHeightProperty, new DoubleAnimation(SelectionHeight, b.Height, duration) { EasingFunction = Transitions.Ease, FillBehavior = FillBehavior.Stop });
            LastMove = SelectionMove.Slid;
        }
        else
        {
            // La mise en page a déplacé le choix, ou il vient d'être fait : le cadre y est d'un coup, de haut en bas. S'il vient
            // d'être fait dans une colonne qui n'en avait pas (la sélection arrive de l'autre colonne du menu), il paraît en fondu.
            _run++;
            _sliding = false;
            var fades = canMove && animate;
            BeginAnimation(SelectionTopProperty, null);
            BeginAnimation(SelectionHeightProperty, null);
            BeginAnimation(SelectionOpacityProperty, fades ? new DoubleAnimation(0, 1, duration) { EasingFunction = Transitions.Ease, FillBehavior = FillBehavior.Stop } : null);
            LastMove = fades ? SelectionMove.Faded : SelectionMove.Jumped;
        }
        SelectionTop = b.Top;
        SelectionHeight = b.Height;
        InvalidateVisual();
    }

    /// <summary>Le choix de la colonne : la ligne choisie d'une liste, ou le bouton coché d'un groupe.</summary>
    internal static FrameworkElement? Chosen(DependencyObject panel)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(panel); i++)
        {
            var child = VisualTreeHelper.GetChild(panel, i);
            if (child is ListBoxItem { IsSelected: true, IsVisible: true } or ToggleButton { IsChecked: true, IsVisible: true })
            {
                return (FrameworkElement)child;
            }
            if (child is not ListBoxItem and not ToggleButton && Chosen(child) is { } inner)
            {
                return inner;
            }
        }
        return null;
    }

    /// <summary>Le cadre du choix, sous les choix : la couleur d'un choix actif, et son trait à gauche, pris dans l'arrondi.</summary>
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        ArgumentNullException.ThrowIfNull(dc);
        if (_target is not { } target || SelectionHeight <= 0
            || TryFindResource(DesignKeys.Brush(ColorRole.NavigationActiveBackground)) is not Brush fill
            || TryFindResource(DesignKeys.Brush(ColorRole.Primary)) is not Brush bar)
        {
            return;
        }
        var radius = TryFindResource(DesignKeys.ControlRadius) is CornerRadius corner ? corner.TopLeft : 0;
        var frame = new Rect(target.Left, SelectionTop, target.Width, SelectionHeight);
        var shape = new RectangleGeometry(frame, radius, radius);
        shape.Freeze();
        dc.PushOpacity(SelectionOpacity);
        dc.DrawGeometry(fill, null, shape);
        dc.PushClip(shape);
        dc.DrawRectangle(bar, null, new Rect(frame.Left, frame.Top, BarThickness, frame.Height));
        dc.Pop();
        dc.Pop();
    }
}

/// <summary>Comment le cadre d'un choix a rejoint sa place.</summary>
internal enum SelectionMove
{
    /// <summary>Il n'a pas encore bougé.</summary>
    None,

    /// <summary>D'un coup : la mise en page l'a déplacé, ou les animations sont réduites.</summary>
    Jumped,

    /// <summary>En fondu, à sa place : il n'y avait pas de choix avant dans cette colonne.</summary>
    Faded,

    /// <summary>En glissant depuis l'ancien choix.</summary>
    Slid,
}
