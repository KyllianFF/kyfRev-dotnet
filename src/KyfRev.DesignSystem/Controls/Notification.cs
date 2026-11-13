using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

/// <summary>
/// Notification : une icône selon la gravité, un titre et un message, qu'on peut fermer. Le
/// lecteur d'écran l'annonce dès qu'elle paraît (sans attendre pour une erreur).
/// </summary>
[TemplatePart(Name = CloseButtonPart, Type = typeof(ButtonBase))]
public class Notification : Control
{
    /// <summary>Bouton de fermeture dans le modèle.</summary>
    public const string CloseButtonPart = "PART_CloseButton";

    /// <summary>Propriété <see cref="Severity"/>.</summary>
    public static readonly DependencyProperty SeverityProperty =
        DependencyProperty.Register(nameof(Severity), typeof(NotificationSeverity), typeof(Notification), new PropertyMetadata(NotificationSeverity.Info, (notification, _) =>
        {
            ((Notification)notification).UpdateGlyph();
            ((Notification)notification).Renew();
        }));

    /// <summary>Propriété <see cref="Title"/>.</summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(Notification), new PropertyMetadata(string.Empty));

    /// <summary>Propriété <see cref="Message"/>.</summary>
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(Notification), new PropertyMetadata(string.Empty, (notification, _) => ((Notification)notification).Renew()));

    /// <summary>Propriété <see cref="IsClosable"/>.</summary>
    public static readonly DependencyProperty IsClosableProperty =
        DependencyProperty.Register(nameof(IsClosable), typeof(bool), typeof(Notification), new PropertyMetadata(true));

    /// <summary>Propriété <see cref="IsOpen"/>.</summary>
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Notification), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (notification, _) => ((Notification)notification).Renew()));

    /// <summary>Propriété <see cref="StaysOpen"/>.</summary>
    public static readonly DependencyProperty StaysOpenProperty =
        DependencyProperty.Register(nameof(StaysOpen), typeof(bool), typeof(Notification), new PropertyMetadata(false, (notification, _) => ((Notification)notification).Renew()));

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
    }

    static Notification()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Notification), new FrameworkPropertyMetadata(typeof(Notification)));
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

    /// <inheritdoc/>
    public override void OnApplyTemplate()
    {
        _closeButton?.Click -= OnClose;
        base.OnApplyTemplate();
        _closeButton = GetTemplateChild(CloseButtonPart) as ButtonBase;
        _closeButton?.Click += OnClose;
    }

    /// <summary>Ferme la notification, comme le bouton « Fermer ». Une liaison de <see cref="IsOpen"/> est gardée.</summary>
    public void Close()
    {
        SetCurrentValue(IsOpenProperty, false);
        RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnAppearing(bool isVisible)
    {
        if (isVisible)
        {
            // Après la mise en page et le focus : un lecteur d'écran coupe ce qu'il dit quand le focus change.
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, Appear);
        }
        Count();
    }

    /// <summary>Un nouveau message, une autre gravité, une réouverture : la notification reparaît, et son temps repart.</summary>
    private void Renew()
    {
        SetValue(IsExpiredPropertyKey, false);
        Count();
    }

    /// <summary>Lance le compte d'une réussite visible, et arrête celui de toute autre.</summary>
    private void Count()
    {
        _timer?.Stop();
        if (!IsVisible || IsExpired || StaysOpen || Severity != NotificationSeverity.Success)
        {
            return;
        }
        _timer ??= new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
        _timer.Tick -= OnLifetimeOver;
        _timer.Tick += OnLifetimeOver;
        _timer.Interval = Lifetime ?? LifetimeOf(Title + Message);
        _timer.Start();
    }

    private void OnLifetimeOver(object? sender, EventArgs e)
    {
        if (IsMouseOver || IsKeyboardFocusWithin)
        {
            // On la lit encore, ou on va la fermer : elle attend.
            _timer!.Interval = HeldCheck < (Lifetime ?? HeldCheck) ? HeldCheck : Lifetime ?? HeldCheck;
            return;
        }
        Expire();
    }

    /// <summary>Le message a eu son temps : il s'efface, sans toucher à ce qui l'a ouvert.</summary>
    internal void Expire()
    {
        _timer?.Stop();
        SetValue(IsExpiredPropertyKey, true);
    }

    /// <summary>La notification paraît : elle se montre si elle le doit, et s'annonce comme une région active, si un client d'UI Automation écoute.</summary>
    private void Appear()
    {
        if (!IsVisible)
        {
            return;
        }
        if (ScrollsIntoView)
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
