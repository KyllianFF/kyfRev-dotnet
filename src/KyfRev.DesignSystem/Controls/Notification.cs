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
        DependencyProperty.Register(nameof(Severity), typeof(NotificationSeverity), typeof(Notification), new PropertyMetadata(NotificationSeverity.Info, (notification, _) => ((Notification)notification).UpdateGlyph()));

    /// <summary>Propriété <see cref="Title"/>.</summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(Notification), new PropertyMetadata(string.Empty));

    /// <summary>Propriété <see cref="Message"/>.</summary>
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(Notification), new PropertyMetadata(string.Empty));

    /// <summary>Propriété <see cref="IsClosable"/>.</summary>
    public static readonly DependencyProperty IsClosableProperty =
        DependencyProperty.Register(nameof(IsClosable), typeof(bool), typeof(Notification), new PropertyMetadata(true));

    /// <summary>Propriété <see cref="IsOpen"/>.</summary>
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Notification), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

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

    private ButtonBase? _closeButton;

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

    /// <summary>Icône de la gravité.</summary>
    public string Glyph => (string)GetValue(GlyphProperty);

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
