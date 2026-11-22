using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace KyfRev.DesignSystem.Controls;

/// <summary>Gravité d'une notification.</summary>
public enum NotificationSeverity
{
    /// <summary>Information.</summary>
    Info,

    /// <summary>Réussite.</summary>
    Success,

    /// <summary>Avertissement.</summary>
    Warning,

    /// <summary>Erreur : annoncée tout de suite par le lecteur d'écran.</summary>
    Critical,
}

/// <summary>Où une notification se montre.</summary>
public enum NotificationPlacement
{
    /// <summary>Une réussite qui s'efface d'elle-même passe en pastille, s'il y a une page où la poser ; le reste, à sa place.</summary>
    Auto,

    /// <summary>À sa place, dans la page.</summary>
    Inline,

    /// <summary>En pastille, par-dessus la page, s'il y a une page où la poser.</summary>
    Toast,
}

/// <summary>
/// Notification : une icône selon la gravité, un titre et un message, qu'on peut fermer. Le
/// lecteur d'écran l'annonce dès qu'elle paraît (sans attendre pour une erreur).
/// En pastille (rangée 24A, <see cref="IsToast"/>) : elle ne prend plus de place dans la page ; elle se pose par-dessus, au
/// bas de <see cref="ToastAreaProperty"/>, avec un trait qui raccourcit le temps qui lui reste et s'arrête au survol.
/// </summary>
[TemplatePart(Name = CloseButtonPart, Type = typeof(ButtonBase))]
[TemplatePart(Name = ToastPart, Type = typeof(Popup))]
[TemplatePart(Name = ToastPillPart, Type = typeof(FrameworkElement))]
[TemplatePart(Name = CountdownPart, Type = typeof(FrameworkElement))]
public class Notification : Control
{
    /// <summary>La fenêtre flottante de la pastille, dans le modèle.</summary>
    public const string ToastPart = "PART_Toast";

    /// <summary>La pastille elle-même, dans le modèle.</summary>
    public const string ToastPillPart = "PART_Pill";

    /// <summary>Le trait qui raccourcit, dans le modèle.</summary>
    public const string CountdownPart = "PART_Countdown";

    /// <summary>Entre la pastille et le bas de la page, et entre deux pastilles.</summary>
    internal const double ToastGap = 16;

    /// <summary>Le temps d'une confirmation, en pastille.</summary>
    internal static readonly TimeSpan ToastLifetime = TimeSpan.FromSeconds(3);

    /// <summary>Le temps d'une confirmation longue, en pastille.</summary>
    internal static readonly TimeSpan LongToastLifetime = TimeSpan.FromSeconds(4);

    /// <summary>Au-delà de ce nombre de caractères, une confirmation est longue.</summary>
    internal const int LongToastLength = 60;

    /// <summary>Propriété <see cref="Placement"/>.</summary>
    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.Register(nameof(Placement), typeof(NotificationPlacement), typeof(Notification), new PropertyMetadata(NotificationPlacement.Auto, (notification, _) => ((Notification)notification).UpdateToast()));

    /// <summary>
    /// La page où se posent les pastilles : héritée, posée par la fenêtre principale sur la place de ses pages. Sans elle (une
    /// page seule, un essai), toute notification reste à sa place.
    /// </summary>
    public static readonly DependencyProperty ToastAreaProperty =
        DependencyProperty.RegisterAttached("ToastArea", typeof(FrameworkElement), typeof(Notification), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (element, _) => (element as Notification)?.UpdateToast()));

    private static readonly DependencyPropertyKey IsToastPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsToast), typeof(bool), typeof(Notification), new PropertyMetadata(false, (notification, _) => ((Notification)notification).OnToastChanged()));

    /// <summary>Propriété <see cref="IsToast"/>.</summary>
    public static readonly DependencyProperty IsToastProperty = IsToastPropertyKey.DependencyProperty;

    /// <summary>Lit <see cref="ToastAreaProperty"/>.</summary>
    public static FrameworkElement? GetToastArea(DependencyObject element) => (FrameworkElement?)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(ToastAreaProperty);

    /// <summary>Écrit <see cref="ToastAreaProperty"/>.</summary>
    public static void SetToastArea(DependencyObject element, FrameworkElement? value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(ToastAreaProperty, value);

    /// <inheritdoc cref="NotificationPlacement"/>
    public NotificationPlacement Placement
    {
        get => (NotificationPlacement)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>Elle se montre en pastille, par-dessus la page : elle ne prend aucune place dans la mise en page.</summary>
    public bool IsToast => (bool)GetValue(IsToastProperty);

    /// <summary>Les pastilles à l'écran, de la plus ancienne (en bas) à la plus récente (au-dessus).</summary>
    private static readonly List<Notification> ShownToasts = [];

    private Popup? _toast;
    private FrameworkElement? _pill;
    private FrameworkElement? _countdown;
    private Window? _window;

    /// <summary>Depuis quand le temps court, et ce qu'il en restait alors : le survol l'arrête, la sortie le reprend.</summary>
    private readonly Stopwatch _running = new();
    private TimeSpan _remaining;
    private bool _held;
    /// <summary>Bouton de fermeture dans le modèle.</summary>
    public const string CloseButtonPart = "PART_CloseButton";

    /// <summary>Propriété <see cref="Severity"/>.</summary>
    public static readonly DependencyProperty SeverityProperty =
        DependencyProperty.Register(nameof(Severity), typeof(NotificationSeverity), typeof(Notification), new PropertyMetadata(NotificationSeverity.Info, (notification, _) =>
        {
            ((Notification)notification).UpdateGlyph();
            ((Notification)notification).UpdateToast();
            ((Notification)notification).Renew();
        }));

    /// <summary>Propriété <see cref="Title"/>.</summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(Notification), new PropertyMetadata(string.Empty));

    /// <summary>Propriété <see cref="Message"/>.</summary>
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(Notification), new PropertyMetadata(string.Empty, (notification, _) => ((Notification)notification).OnMessageChanged()));

    private static readonly DependencyPropertyKey ShownMessagePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ShownMessage), typeof(string), typeof(Notification), new PropertyMetadata(string.Empty));

    /// <summary>Propriété <see cref="ShownMessage"/>.</summary>
    public static readonly DependencyProperty ShownMessageProperty = ShownMessagePropertyKey.DependencyProperty;

    /// <summary>
    /// Le message qu'on lit à l'écran : <see cref="Message"/>, sauf pendant que la notification s'efface. Une page vide
    /// souvent son message au moment de la fermer : elle part avec ce qu'elle disait, pas vide.
    /// </summary>
    public string ShownMessage => (string)GetValue(ShownMessageProperty);

    /// <summary>Propriété <see cref="IsClosable"/>.</summary>
    public static readonly DependencyProperty IsClosableProperty =
        DependencyProperty.Register(nameof(IsClosable), typeof(bool), typeof(Notification), new PropertyMetadata(true));

    /// <summary>Propriété <see cref="IsOpen"/>.</summary>
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Notification), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (notification, e) => ((Notification)notification).OnOpenChanged((bool)e.NewValue)));

    /// <summary>Propriété <see cref="StaysOpen"/>.</summary>
    public static readonly DependencyProperty StaysOpenProperty =
        DependencyProperty.Register(nameof(StaysOpen), typeof(bool), typeof(Notification), new PropertyMetadata(false, (notification, _) =>
        {
            ((Notification)notification).UpdateToast();
            ((Notification)notification).Renew();
        }));

    private static readonly DependencyPropertyKey IsLeavingPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsLeaving), typeof(bool), typeof(Notification), new PropertyMetadata(false));

    /// <summary>Propriété <see cref="IsLeaving"/>.</summary>
    public static readonly DependencyProperty IsLeavingProperty = IsLeavingPropertyKey.DependencyProperty;

    /// <summary>Fermée ou lue, la notification s'efface encore : elle garde sa place un instant, sans plus rien recevoir.</summary>
    public bool IsLeaving => (bool)GetValue(IsLeavingProperty);

    /// <summary>
    /// La notification n'a plus à être montrée (fermée, ou lue jusqu'au bout) : si elle est à l'écran, elle s'efface avant
    /// de rendre sa place.
    /// </summary>
    private void Leave()
    {
        // Jamais vue : elle n'a pas à s'effacer sous les yeux. Une page qui s'ouvre pose ses avis ouverts, puis ses liaisons
        // les ferment ; sans cette garde, ils paraissaient vides un instant avant de s'estomper.
        if (!_wasSeen || !IsVisible || IsLeaving)
        {
            return;
        }
        SetValue(IsLeavingPropertyKey, true);
        Transitions.Exit(this, () =>
        {
            SetValue(IsLeavingPropertyKey, false);
            SetValue(ShownMessagePropertyKey, Message);
        });
    }

    private void OnMessageChanged()
    {
        if (string.IsNullOrEmpty(Message) && IsVisible)
        {
            // Vidé : si c'est pour la fermer, la fermeture suit dans l'instant, et elle garde son texte en s'effaçant.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, SettleMessage);
        }
        else
        {
            SetValue(ShownMessagePropertyKey, Message);
        }
        Renew();
    }

    private void SettleMessage()
    {
        if (IsLeaving)
        {
            return;
        }
        if (string.IsNullOrEmpty(Title + Message))
        {
            // Plus rien à dire, sans avoir été fermée : elle s'efface de même, avec ce qu'elle disait.
            Leave();
            if (IsLeaving)
            {
                return;
            }
        }
        SetValue(ShownMessagePropertyKey, Message);
    }

    private static readonly DependencyPropertyKey IsExpiredPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsExpired), typeof(bool), typeof(Notification), new PropertyMetadata(false));

    /// <summary>Propriété <see cref="IsExpired"/>.</summary>
    public static readonly DependencyProperty IsExpiredProperty = IsExpiredPropertyKey.DependencyProperty;

    /// <summary>Quand elle paraît, la zone qui défile autour d'elle l'amène à la vue : un résultat se voit, même au bas d'une fenêtre étroite.</summary>
    public static readonly DependencyProperty ScrollsIntoViewProperty =
        DependencyProperty.Register(nameof(ScrollsIntoView), typeof(bool), typeof(Notification), new PropertyMetadata(false));

    private static readonly DependencyPropertyKey GlyphPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(Glyph), typeof(string), typeof(Notification), new PropertyMetadata(Icons.Of(NotificationSeverity.Info)));

    /// <summary>Propriété <see cref="Glyph"/>.</summary>
    public static readonly DependencyProperty GlyphProperty = GlyphPropertyKey.DependencyProperty;

    /// <summary>Événement <see cref="Closed"/>.</summary>
    public static readonly RoutedEvent ClosedEvent =
        EventManager.RegisterRoutedEvent(nameof(Closed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Notification));

    /// <summary>Le temps de lire le plus court des messages.</summary>
    internal static readonly TimeSpan ShortestLifetime = TimeSpan.FromSeconds(5);

    /// <summary>Au-delà, un message de réussite n'a plus à rester.</summary>
    internal static readonly TimeSpan LongestLifetime = TimeSpan.FromSeconds(12);

    /// <summary>Tant que la souris ou le clavier est sur la notification, elle reste ; on y revient voir à ce rythme.</summary>
    private static readonly TimeSpan HeldCheck = TimeSpan.FromSeconds(1);

    private ButtonBase? _closeButton;
    private DispatcherTimer? _timer;

    /// <summary>Une notification ; le lecteur d'écran l'annonce dès qu'elle paraît.</summary>
    public Notification()
    {
        IsVisibleChanged += (_, e) => OnAppearing((bool)e.NewValue);
        Loaded += (_, _) => Follow(Window.GetWindow(this));
        Unloaded += (_, _) => Follow(null);
    }

    static Notification()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Notification), new FrameworkPropertyMetadata(typeof(Notification)));
        // En pastille, elle ne prend aucune place : pas même sa marge.
        MarginProperty.OverrideMetadata(typeof(Notification), new FrameworkPropertyMetadata(default(Thickness), FrameworkPropertyMetadataOptions.AffectsMeasure, null, (notification, margin) => ((Notification)notification).IsToast ? default(Thickness) : margin));
    }

    /// <summary>Fermée par l'utilisateur (le bouton « Fermer »).</summary>
    public event RoutedEventHandler Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    /// <summary>Gravité.</summary>
    public NotificationSeverity Severity
    {
        get => (NotificationSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    /// <summary>Titre ; facultatif.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Message.</summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>Bouton « Fermer » proposé ; oui par défaut.</summary>
    public bool IsClosable
    {
        get => (bool)GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    /// <summary>Affichée ; une notification fermée disparaît sans laisser de place.</summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="ScrollsIntoViewProperty"/>
    public bool ScrollsIntoView
    {
        get => (bool)GetValue(ScrollsIntoViewProperty);
        set => SetValue(ScrollsIntoViewProperty, value);
    }

    /// <summary>
    /// Une réussite s'efface d'elle-même après le temps de la lire ; « vrai » la garde, pour le résultat d'un long travail
    /// qu'on retrouve en revenant. Une erreur, un avertissement et une information restent toujours.
    /// </summary>
    public bool StaysOpen
    {
        get => (bool)GetValue(StaysOpenProperty);
        set => SetValue(StaysOpenProperty, value);
    }

    /// <summary>Lue, elle s'est effacée d'elle-même ; un nouveau message la fait reparaître.</summary>
    public bool IsExpired => (bool)GetValue(IsExpiredProperty);

    /// <summary>Icône de la gravité.</summary>
    public string Glyph => (string)GetValue(GlyphProperty);

    /// <summary>Le temps laissé à un message avant de s'effacer ; rien : celui que demande sa longueur.</summary>
    internal TimeSpan? Lifetime { get; set; }

    /// <summary>
    /// Le temps de lire <paramref name="message"/> : quatre secondes, plus une par quinzaine de caractères, entre
    /// <see cref="ShortestLifetime"/> et <see cref="LongestLifetime"/>.
    /// </summary>
    internal static TimeSpan LifetimeOf(string? message)
    {
        var wanted = TimeSpan.FromSeconds(4 + ((message?.Length ?? 0) / 15.0));
        return wanted < ShortestLifetime ? ShortestLifetime : wanted > LongestLifetime ? LongestLifetime : wanted;
    }

    /// <summary>Le temps d'une confirmation en pastille : trois secondes, quatre si elle est longue.</summary>
    internal static TimeSpan ToastLifetimeOf(string? message) => (message?.Length ?? 0) > LongToastLength ? LongToastLifetime : ToastLifetime;

    /// <inheritdoc/>
    public override void OnApplyTemplate()
    {
        _closeButton?.Click -= OnClose;
        if (_pill is not null)
        {
            _pill.MouseEnter -= OnPillEntered;
            _pill.MouseLeave -= OnPillLeft;
            _pill.SizeChanged -= OnPillResized;
        }
        _toast?.CustomPopupPlacementCallback = null;
        base.OnApplyTemplate();
        _closeButton = GetTemplateChild(CloseButtonPart) as ButtonBase;
        _closeButton?.Click += OnClose;
        _toast = GetTemplateChild(ToastPart) as Popup;
        _pill = GetTemplateChild(ToastPillPart) as FrameworkElement;
        _countdown = GetTemplateChild(CountdownPart) as FrameworkElement;
        // Une transformation du modèle est figée : celle du trait, qu'on anime, est la sienne.
        _countdown?.RenderTransform = new ScaleTransform();
        _toast?.CustomPopupPlacementCallback = PlaceToast;
        if (_pill is not null)
        {
            _pill.MouseEnter += OnPillEntered;
            _pill.MouseLeave += OnPillLeft;
            _pill.SizeChanged += OnPillResized;
        }
        ShowToast();
    }

    /// <summary>Pastille ou à sa place : selon ce qu'elle dit, et s'il y a une page où la poser.</summary>
    private void UpdateToast()
    {
        var wanted = GetToastArea(this) is not null && Placement switch
        {
            NotificationPlacement.Toast => true,
            NotificationPlacement.Inline => false,
            _ => Severity == NotificationSeverity.Success && !StaysOpen,
        };
        SetValue(IsToastPropertyKey, wanted);
    }

    private void OnToastChanged()
    {
        CoerceValue(MarginProperty);
        // Son modèle change (voir le style) : la pastille se montre, ou se cache, une fois le nouveau en place.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            ShowToast();
            Count();
        });
    }

    /// <summary>La fenêtre où elle est : une pastille ne se montre que tant que cette fenêtre est au premier plan.</summary>
    private void Follow(Window? window)
    {
        if (ReferenceEquals(window, _window))
        {
            return;
        }
        if (_window is not null)
        {
            _window.Activated -= OnWindowChanged;
            _window.Deactivated -= OnWindowChanged;
            _window.LocationChanged -= OnWindowChanged;
            _window.SizeChanged -= OnWindowResized;
        }
        _window = window;
        if (_window is not null)
        {
            _window.Activated += OnWindowChanged;
            _window.Deactivated += OnWindowChanged;
            _window.LocationChanged += OnWindowChanged;
            _window.SizeChanged += OnWindowResized;
        }
        ShowToast();
    }

    private void OnWindowChanged(object? sender, EventArgs e) => ShowToast();

    private void OnWindowResized(object sender, SizeChangedEventArgs e) => ShowToast();

    /// <summary>
    /// Ouvre ou ferme sa fenêtre flottante. Elle est toujours au premier plan, au-dessus des autres applications : elle ne se
    /// montre donc que tant que la fenêtre de kyfRev l'est aussi, et reparaît quand on y revient.
    /// </summary>
    private void ShowToast()
    {
        if (_toast is null)
        {
            return;
        }
        var shown = IsToast && IsVisible && _window is { WindowState: not WindowState.Minimized } window && IsForeground(window);
        if (shown)
        {
            if (!ShownToasts.Contains(this))
            {
                ShownToasts.Add(this);
            }
            _toast.PlacementTarget = GetToastArea(this);
            _pill?.MaxWidth = Math.Max(160, Math.Min(560, (GetToastArea(this)?.ActualWidth ?? 600) - (2 * ToastGap)));
            _toast.IsOpen = true;
        }
        else
        {
            _toast.IsOpen = false;
            ShownToasts.Remove(this);
        }
        PlaceToasts();
    }

    /// <summary>Les pastilles d'une même page se rangent l'une au-dessus de l'autre : elles se recalent toutes.</summary>
    private static void PlaceToasts()
    {
        foreach (var toast in ShownToasts.ToList())
        {
            if (toast._toast is { IsOpen: true } popup)
            {
                // Le seul moyen de demander à une fenêtre flottante de se replacer : la décaler, puis la remettre.
                var offset = popup.HorizontalOffset;
                popup.HorizontalOffset = offset + 1;
                popup.HorizontalOffset = offset;
            }
        }
    }

    private void OnPillResized(object sender, SizeChangedEventArgs e) => PlaceToasts();

    /// <summary>Au bas de la page, au milieu ; au-dessus des pastilles déjà là.</summary>
    internal CustomPopupPlacement[] PlaceToast(Size popupSize, Size targetSize, Point offset)
    {
        // Sa hauteur avec ses marges (la place de l'ombre) : celle de sa fenêtre flottante.
        var own = _toast?.Child is FrameworkElement child && child.DesiredSize.Height > 0 ? child.DesiredSize.Height : 0;
        // Les tailles reçues sont en pixels de l'écran ; celles des pastilles, en pixels de WPF.
        var scale = own > 0 ? popupSize.Height / own : 1;
        var below = 0d;
        foreach (var toast in ShownToasts)
        {
            if (ReferenceEquals(toast, this))
            {
                break;
            }
            if (ReferenceEquals(GetToastArea(toast), GetToastArea(this)) && toast._toast?.Child is FrameworkElement other)
            {
                below += other.DesiredSize.Height - ToastGap;
            }
        }
        var x = (targetSize.Width - popupSize.Width) / 2;
        var y = targetSize.Height - popupSize.Height - (below * scale);
        return [new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.None)];
    }

    /// <summary>La pastille la plus récente se ferme, comme son ✕ ; faux s'il n'y en a aucune (Échap sert alors à autre chose).</summary>
    public static bool CloseNewestToast()
    {
        // Une pastille qui s'efface déjà ne compte plus.
        if (ShownToasts.LastOrDefault(toast => toast.IsOpen && !toast.IsLeaving && !toast.IsExpired) is not { } newest)
        {
            return false;
        }
        newest.Close();
        return true;
    }

    /// <summary>La fenêtre est au premier plan ; les essais, dont la fenêtre ne l'est pas toujours, le disent eux-mêmes.</summary>
    internal static Func<Window, bool> IsForeground { get; set; } = window => window.IsActive;

    /// <summary>Les pastilles à l'écran (pour les essais).</summary>
    internal static IReadOnlyList<Notification> Toasts => ShownToasts;

    /// <summary>La souris sur la pastille : son temps s'arrête, et le trait avec lui.</summary>
    private void OnPillEntered(object sender, MouseEventArgs e) => Hold(true);

    /// <summary>La souris repartie : le temps reprend où il en était.</summary>
    private void OnPillLeft(object sender, MouseEventArgs e) => Hold(false);

    /// <summary>Arrête ou reprend le temps d'une pastille qui s'efface d'elle-même.</summary>
    internal void Hold(bool held)
    {
        if (held == _held)
        {
            return;
        }
        _held = held;
        if (_timer is null || !IsToast)
        {
            return;
        }
        if (held)
        {
            if (!_timer.IsEnabled)
            {
                return;
            }
            _timer.Stop();
            _running.Stop();
            _remaining = _remaining > _running.Elapsed ? _remaining - _running.Elapsed : TimeSpan.Zero;
            _running.Reset();
            if (_countdown?.RenderTransform is ScaleTransform scale)
            {
                var now = scale.ScaleX;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.ScaleX = now;
            }
        }
        else if (_remaining > TimeSpan.Zero && !IsLeaving && !IsExpired && IsVisible)
        {
            Run(_remaining);
        }
    }

    /// <summary>Le temps court pour <paramref name="left"/>, et le trait raccourcit d'autant, jusqu'à rien.</summary>
    private void Run(TimeSpan left)
    {
        _remaining = left;
        _running.Restart();
        _timer!.Interval = left;
        _timer.Start();
        if (IsToast && _countdown is not null)
        {
            var scale = _countdown.RenderTransform as ScaleTransform ?? new ScaleTransform();
            _countdown.RenderTransform = scale;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale.ScaleX, 0, left));
        }
    }

    /// <summary>Ferme la notification, comme le bouton « Fermer ». Une liaison de <see cref="IsOpen"/> est gardée.</summary>
    public void Close()
    {
        SetCurrentValue(IsOpenProperty, false);
        RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// La notification a été à l'écran assez longtemps pour être vue (l'application a repris la main depuis qu'elle a paru) :
    /// fermée, elle s'efface en fondu. Sinon elle est retirée d'un coup, sans avoir été montrée.
    /// </summary>
    private bool _wasSeen;

    private void OnAppearing(bool isVisible)
    {
        if (!isVisible)
        {
            _wasSeen = false;
        }
        if (isVisible)
        {
            // Elle monte un peu, en fondu (rangée 18C) ; d'un coup quand les animations sont réduites. C'est son cadre qui
            // bouge, pas elle : sa place reste celle que la mise en page lui donne, et la ramener à la vue la montre entière.
            ApplyTemplate();
            ShowToast();
            // En pastille, c'est elle qui monte de 8 px en paraissant, dans sa fenêtre flottante.
            Transitions.Enter(IsToast && _pill is not null ? _pill : VisualTreeHelper.GetChildrenCount(this) > 0 && VisualTreeHelper.GetChild(this, 0) is FrameworkElement frame ? frame : this, IsToast ? DesignKeys.FastDuration : DesignKeys.NormalDuration, rise: IsToast ? 8 : Transitions.DialogRise, kind: MotionKinds.Surfaces);
            // Après la mise en page et le focus : un lecteur d'écran coupe ce qu'il dit quand le focus change.
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, Appear);
        }
        else
        {
            ShowToast();
        }
        Count();
    }

    /// <summary>Un nouveau message, une autre gravité, une réouverture : la notification reparaît, et son temps repart.</summary>
    private void Renew()
    {
        if (IsLeaving)
        {
            if (!IsOpen || string.IsNullOrEmpty(Title + Message))
            {
                // Elle s'en va, et il n'y a rien de nouveau à dire : elle finit de s'effacer.
                return;
            }
            // Elle s'effaçait : elle revient entière, et s'annonce de nouveau.
            // Dans cet ordre : à aucun moment elle n'est ni en train de partir ni ouverte, sans quoi elle disparaîtrait un instant.
            Transitions.StopExit(this);
            SetValue(ShownMessagePropertyKey, Message);
            SetValue(IsExpiredPropertyKey, false);
            SetValue(IsLeavingPropertyKey, false);
            OnAppearing(IsVisible);
            return;
        }
        SetValue(IsExpiredPropertyKey, false);
        Count();
    }
    /// <summary>Lance le compte d'une réussite visible, et arrête celui de toute autre.</summary>
    private void Count()
    {
        _timer?.Stop();
        _running.Reset();
        var counts = IsVisible && !IsLeaving && !IsExpired && !StaysOpen && Severity == NotificationSeverity.Success;
        _countdown?.Visibility = counts && IsToast ? Visibility.Visible : Visibility.Collapsed;
        if (!counts)
        {
            return;
        }
        _timer ??= new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
        _timer.Tick -= OnLifetimeOver;
        _timer.Tick += OnLifetimeOver;
        if (_countdown?.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.ScaleX = 1;
        }
        var lifetime = Lifetime ?? (IsToast ? ToastLifetimeOf(Title + Message) : LifetimeOf(Title + Message));
        if (_held && IsToast)
        {
            // Sous la souris : il attend qu'elle parte pour courir.
            _remaining = lifetime;
            return;
        }
        Run(lifetime);
    }

    private void OnLifetimeOver(object? sender, EventArgs e)
    {
        if (!IsToast && (IsMouseOver || IsKeyboardFocusWithin))
        {
            // On la lit encore, ou on va la fermer : elle attend.
            _timer!.Interval = HeldCheck < (Lifetime ?? HeldCheck) ? HeldCheck : Lifetime ?? HeldCheck;
            return;
        }
        Expire();
    }

    /// <summary>Ouverte, elle reparaît entière et son temps repart ; fermée, elle s'efface avant de rendre sa place.</summary>
    private void OnOpenChanged(bool isOpen)
    {
        if (isOpen)
        {
            Renew();
            return;
        }
        Leave();
        _timer?.Stop();
        if (!IsLeaving)
        {
            SetValue(IsExpiredPropertyKey, false);
        }
    }

    /// <summary>Le message a eu son temps : il s'efface, sans toucher à ce qui l'a ouvert.</summary>
    internal void Expire()
    {
        _timer?.Stop();
        Leave();
        SetValue(IsExpiredPropertyKey, true);
    }

    /// <summary>La notification paraît : elle se montre si elle le doit, et s'annonce comme une région active, si un client d'UI Automation écoute.</summary>
    private void Appear()
    {
        if (!IsVisible || IsLeaving)
        {
            return;
        }
        _wasSeen = true;
        if (ScrollsIntoView && !IsToast)
        {
            BringIntoView();
        }
        if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
        {
            UIElementAutomationPeer.CreatePeerForElement(this).RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }

    private void UpdateGlyph() => SetValue(GlyphPropertyKey, Icons.Of(Severity));

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new AutomationPeers.NotificationPeer(this);
}
