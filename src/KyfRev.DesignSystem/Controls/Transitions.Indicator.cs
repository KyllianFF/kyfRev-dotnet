using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Changer d'onglet (rangée 18B des maquettes) : un seul repère pour toute une rangée d'onglets, qui glisse de l'onglet
/// quitté à l'onglet choisi et prend sa largeur ; on voit d'où l'on vient. Dans une colonne de sections (les
/// Paramètres), le repère est un trait à gauche, qui monte ou descend.
/// </summary>
public static partial class Transitions
{
    /// <summary>Sur le panneau qui porte des onglets (des boutons à bascule) : un repère glisse sous celui qui est choisi.</summary>
    public static readonly DependencyProperty TabIndicatorProperty =
        DependencyProperty.RegisterAttached("TabIndicator", typeof(bool), typeof(Transitions), new PropertyMetadata(false, OnTabIndicator));

    /// <summary>Où est le repère : en bas des onglets (par défaut), ou à gauche des sections d'une colonne.</summary>
    public static readonly DependencyProperty TabIndicatorSideProperty =
        DependencyProperty.RegisterAttached("TabIndicatorSide", typeof(Dock), typeof(Transitions), new PropertyMetadata(Dock.Bottom));

    /// <summary>
    /// Vrai sous un panneau qui a son repère : chaque onglet y cache le sien (son modèle le lit). Hérité par tout ce que
    /// le panneau contient.
    /// </summary>
    public static readonly DependencyProperty HasSharedIndicatorProperty =
        DependencyProperty.RegisterAttached("HasSharedIndicator", typeof(bool), typeof(Transitions), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    private static readonly DependencyProperty IndicatorProperty =
        DependencyProperty.RegisterAttached("Indicator", typeof(TabIndicatorAdorner), typeof(Transitions));

    /// <summary>Lit <see cref="TabIndicatorProperty"/>.</summary>
    public static bool GetTabIndicator(DependencyObject element) => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(TabIndicatorProperty);

    /// <summary>Écrit <see cref="TabIndicatorProperty"/>.</summary>
    public static void SetTabIndicator(DependencyObject element, bool value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(TabIndicatorProperty, value);

    /// <summary>Lit <see cref="TabIndicatorSideProperty"/>.</summary>
    public static Dock GetTabIndicatorSide(DependencyObject element) => (Dock)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(TabIndicatorSideProperty);

    /// <summary>Écrit <see cref="TabIndicatorSideProperty"/>.</summary>
    public static void SetTabIndicatorSide(DependencyObject element, Dock value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(TabIndicatorSideProperty, value);

    /// <summary>Lit <see cref="HasSharedIndicatorProperty"/>.</summary>
    public static bool GetHasSharedIndicator(DependencyObject element) => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(HasSharedIndicatorProperty);

    /// <summary>Écrit <see cref="HasSharedIndicatorProperty"/>.</summary>
    public static void SetHasSharedIndicator(DependencyObject element, bool value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(HasSharedIndicatorProperty, value);

    /// <summary>Où est le repère d'un panneau d'onglets : sous quel onglet, dans les coordonnées du panneau ; aucun si rien n'est choisi.</summary>
    public static Rect? IndicatedTab(FrameworkElement panel) => IndicatorOf(panel ?? throw new ArgumentNullException(nameof(panel)))?.Target;

    /// <summary>Le repère d'un panneau d'onglets, s'il en a un (pour les essais).</summary>
    internal static TabIndicatorAdorner? IndicatorOf(FrameworkElement panel) => panel.GetValue(IndicatorProperty) as TabIndicatorAdorner;

    private static void OnTabIndicator(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement panel)
        {
            return;
        }
        panel.Loaded -= OnIndicatorLoaded;
        panel.Unloaded -= OnIndicatorUnloaded;
        if (e.NewValue is not true)
        {
            panel.ClearValue(HasSharedIndicatorProperty);
            OnIndicatorUnloaded(panel, new RoutedEventArgs());
            return;
        }
        panel.SetValue(HasSharedIndicatorProperty, true);
        panel.Loaded += OnIndicatorLoaded;
        panel.Unloaded += OnIndicatorUnloaded;
        if (panel.IsLoaded)
        {
            OnIndicatorLoaded(panel, new RoutedEventArgs());
        }
    }

    private static void OnIndicatorLoaded(object sender, RoutedEventArgs e)
    {
        var panel = (FrameworkElement)sender;
        if (IndicatorOf(panel) is not null || AdornerLayer.GetAdornerLayer(panel) is not { } layer)
        {
            return;
        }
        var indicator = new TabIndicatorAdorner(panel);
        layer.Add(indicator);
        panel.SetValue(IndicatorProperty, indicator);
    }

    private static void OnIndicatorUnloaded(object sender, RoutedEventArgs e)
    {
        var panel = (FrameworkElement)sender;
        if (IndicatorOf(panel) is { } indicator)
        {
            indicator.Detach();
            AdornerLayer.GetAdornerLayer(panel)?.Remove(indicator);
            panel.ClearValue(IndicatorProperty);
        }
    }
}

/// <summary>Le repère qui glisse sous l'onglet choisi d'un panneau ; il ne prend jamais le pointeur.</summary>
internal sealed class TabIndicatorAdorner : Adorner
{
    /// <summary>Épaisseur du trait sous un onglet, comme celui que chaque onglet dessinait.</summary>
    internal const double LineThickness = 2;

    /// <summary>Épaisseur du trait à gauche d'une section.</summary>
    internal const double SideThickness = 3;

    /// <summary>Le retrait de la pastille d'une section depuis le bord de son cadre.</summary>
    internal const double SideInset = 3;

    /// <summary>La part de la hauteur de la section que tient sa pastille.</summary>
    internal const double SideShare = 0.45;

    /// <summary>Où commence le repère, le long de la rangée (ou de la colonne).</summary>
    public static readonly DependencyProperty StartProperty =
        DependencyProperty.Register(nameof(Start), typeof(double), typeof(TabIndicatorAdorner), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Sa longueur : la largeur de l'onglet (ou la hauteur de la section).</summary>
    public static readonly DependencyProperty LengthProperty =
        DependencyProperty.Register(nameof(Length), typeof(double), typeof(TabIndicatorAdorner), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly FrameworkElement _panel;
    private Rect? _target;

    public TabIndicatorAdorner(FrameworkElement panel)
        : base(panel)
    {
        _panel = panel;
        IsHitTestVisible = false;
        _panel.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(OnChecked), handledEventsToo: true);
        _panel.AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(OnUnchecked), handledEventsToo: true);
        _panel.LayoutUpdated += OnLayout;
        _panel.IsVisibleChanged += OnVisible;
        Place(animate: false);
    }

    public double Start
    {
        get => (double)GetValue(StartProperty);
        set => SetValue(StartProperty, value);
    }

    public double Length
    {
        get => (double)GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    /// <summary>L'onglet sous lequel est le repère, dans les coordonnées du panneau ; aucun si rien n'est choisi.</summary>
    internal Rect? Target => _target;

    /// <summary>Le repère part : il ne suit plus le panneau.</summary>
    public void Detach()
    {
        _panel.RemoveHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(OnChecked));
        _panel.RemoveHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(OnUnchecked));
        _panel.LayoutUpdated -= OnLayout;
        _panel.IsVisibleChanged -= OnVisible;
    }

    private Dock Side => Transitions.GetTabIndicatorSide(_panel);

    // L'onglet qui vient d'être coché est le bon, même si l'ancien n'est pas encore décoché.
    private void OnChecked(object sender, RoutedEventArgs e) => Place(animate: true, e.OriginalSource as ToggleButton);

    private void OnUnchecked(object sender, RoutedEventArgs e) => Place(animate: true);

    private void OnVisible(object sender, DependencyPropertyChangedEventArgs e) => Place(animate: false);

    /// <summary>Après une mise en page (une fenêtre plus étroite, un onglet qui paraît) : le repère suit, sans glisser.</summary>
    private void OnLayout(object? sender, EventArgs e)
    {
        if (!HasAnimatedProperties)
        {
            Place(animate: false);
        }
    }

    /// <summary>Met le repère sous l'onglet choisi : en glissant depuis l'ancien, ou d'un coup.</summary>
    internal void Place(bool animate, ToggleButton? chosen = null)
    {
        chosen = chosen is { IsChecked: true, IsVisible: true } ? chosen : Chosen(_panel);
        var bounds = chosen is null || !_panel.IsVisible ? (Rect?)null : chosen.TransformToAncestor(_panel).TransformBounds(new Rect(chosen.RenderSize));
        // Rien n'a bougé : rien à redessiner. Redessiner demande une mise en page, qui rappellerait cette méthode, sans fin.
        if (Same(_target, bounds))
        {
            return;
        }
        var (start, length) = bounds is not { } b ? (0d, 0d) : Side == Dock.Left ? (b.Top, b.Height) : (b.Left, b.Width);
        _target = bounds;
        if (animate && Length > 0 && length > 0 && Transitions.Moves(_panel, DesignKeys.NormalDuration, out var duration))
        {
            BeginAnimation(StartProperty, new DoubleAnimation(Start, start, duration) { EasingFunction = Transitions.Ease, FillBehavior = FillBehavior.Stop });
            BeginAnimation(LengthProperty, new DoubleAnimation(Length, length, duration) { EasingFunction = Transitions.Ease, FillBehavior = FillBehavior.Stop });
        }
        else
        {
            BeginAnimation(StartProperty, null);
            BeginAnimation(LengthProperty, null);
        }
        Start = start;
        Length = length;
        InvalidateVisual();
    }

    /// <summary>Deux places du repère, à un demi-pixel près ; aucune et aucune sont la même.</summary>
    internal static bool Same(Rect? a, Rect? b) =>
        a is not { } x || b is not { } y
            ? a is null && b is null
            : Math.Abs(x.Left - y.Left) < 0.5 && Math.Abs(x.Top - y.Top) < 0.5 && Math.Abs(x.Width - y.Width) < 0.5 && Math.Abs(x.Height - y.Height) < 0.5;

    /// <summary>L'onglet choisi : le premier bouton à bascule coché et visible du panneau.</summary>
    internal static ToggleButton? Chosen(DependencyObject panel)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(panel); i++)
        {
            var child = VisualTreeHelper.GetChild(panel, i);
            if (child is ToggleButton { IsChecked: true, IsVisible: true } chosen)
            {
                return chosen;
            }
            if (child is not ToggleButton && Chosen(child) is { } inner)
            {
                return inner;
            }
        }
        return null;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (_target is not { } target || Length <= 0 || !_panel.IsVisible || _panel.TryFindResource(DesignKeys.Brush(Tokens.ColorRole.Primary)) is not Brush brush)
        {
            return;
        }
        // À gauche d'une section, une petite pastille arrondie posée dans son cadre, comme le menu de navigation de Windows : un
        // trait droit sur le bord coupait l'arrondi du cadre.
        var line = Side == Dock.Left
            ? new Rect(target.Left + SideInset, Start + (Length * (1 - SideShare) / 2), SideThickness, Length * SideShare)
            : new Rect(Start, target.Bottom - LineThickness, Length, LineThickness);
        var radius = Side == Dock.Left ? SideThickness / 2 : 1;
        drawingContext.DrawRoundedRectangle(brush, null, line, radius, radius);
    }
}
