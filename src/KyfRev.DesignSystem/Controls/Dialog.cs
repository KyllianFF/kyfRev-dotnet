using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace KyfRev.DesignSystem.Controls;

/// <summary>Bouton par lequel un dialogue s'est fermé.</summary>
public enum DialogChoice
{
    /// <summary>Fermé par Échap.</summary>
    None,

    /// <summary>Bouton principal.</summary>
    Primary,

    /// <summary>Bouton secondaire (« Annuler »).</summary>
    Secondary,
}

/// <summary>Données de l'événement <see cref="Dialog.Closed"/>.</summary>
public sealed class DialogClosedEventArgs(RoutedEvent routedEvent, object source, DialogChoice choice) : RoutedEventArgs(routedEvent, source)
{
    /// <summary>Bouton choisi.</summary>
    public DialogChoice Choice { get; } = choice;
}

/// <summary>
/// Dialogue posé sur un voile, au-dessus de la page. Le focus y entre à l'ouverture, n'en sort pas
/// au clavier, et revient à la fermeture à ce qui le tenait ; Échap le ferme, Entrée choisit le
/// bouton principal. Pendant un travail lancé de là (<see cref="IsWorking"/>), le focus attend sur le
/// bouton secondaire ; le travail fini, il revient au bouton principal. En volet (<see cref="IsSidePanel"/>), il glisse
/// depuis le bord droit et laisse voir la page ; sous <see cref="NarrowWidth"/> de large, il vient du bas.
/// </summary>
[TemplatePart(Name = PrimaryButtonPart, Type = typeof(ButtonBase))]
[TemplatePart(Name = SecondaryButtonPart, Type = typeof(ButtonBase))]
[TemplatePart(Name = CloseButtonPart, Type = typeof(ButtonBase))]
[TemplatePart(Name = ScrimPart, Type = typeof(UIElement))]
public class Dialog : ContentControl
{
    /// <summary>Bouton principal dans le modèle.</summary>
    public const string PrimaryButtonPart = "PART_PrimaryButton";

    /// <summary>Bouton secondaire dans le modèle.</summary>
    public const string SecondaryButtonPart = "PART_SecondaryButton";

    /// <summary>La croix d'un volet, dans le modèle.</summary>
    public const string CloseButtonPart = "PART_CloseButton";

    /// <summary>Le voile, dans le modèle : un clic dessus referme un volet.</summary>
    public const string ScrimPart = "PART_Scrim";

    /// <summary>Plus étroit, un volet vient du bas et prend toute la largeur, comme sur un téléphone.</summary>
    public const double NarrowWidth = 600;

    /// <summary>S'ouvre en volet contre le bord droit, la page restant visible à côté, au lieu d'une carte au milieu.</summary>
    public static readonly DependencyProperty IsSidePanelProperty =
        DependencyProperty.Register(nameof(IsSidePanel), typeof(bool), typeof(Dialog), new PropertyMetadata(false, (dialog, _) => ((Dialog)dialog).UpdateFromBottom()));

    private static readonly DependencyPropertyKey IsFromBottomPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsFromBottom), typeof(bool), typeof(Dialog), new PropertyMetadata(false));

    /// <summary>Propriété <see cref="IsFromBottom"/>.</summary>
    public static readonly DependencyProperty IsFromBottomProperty = IsFromBottomPropertyKey.DependencyProperty;

    /// <summary>Propriété <see cref="Title"/>.</summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(Dialog), new PropertyMetadata(string.Empty));

    /// <summary>La largeur la plus grande de la carte : 560 par défaut, plus pour un contenu qui en a besoin.</summary>
    public static readonly DependencyProperty CardMaxWidthProperty =
        DependencyProperty.Register(nameof(CardMaxWidth), typeof(double), typeof(Dialog), new PropertyMetadata(560d));

    /// <summary>La carte prend toute la largeur permise, pour un contenu qui s'y étend (une comparaison, une galerie).</summary>
    public static readonly DependencyProperty IsWideProperty =
        DependencyProperty.Register(nameof(IsWide), typeof(bool), typeof(Dialog), new PropertyMetadata(false));

    /// <summary>Propriété <see cref="IsDestructive"/>.</summary>
    public static readonly DependencyProperty IsDestructiveProperty =
        DependencyProperty.Register(nameof(IsDestructive), typeof(bool), typeof(Dialog), new PropertyMetadata(false));

    /// <summary>Propriété <see cref="IsOpen"/>.</summary>
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Dialog), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (dialog, e) => ((Dialog)dialog).OnIsOpenChanged((bool)e.NewValue)));

    /// <summary>Propriété <see cref="PrimaryButtonText"/>.</summary>
    public static readonly DependencyProperty PrimaryButtonTextProperty =
        DependencyProperty.Register(nameof(PrimaryButtonText), typeof(string), typeof(Dialog), new PropertyMetadata(DesignText.Ok));

    /// <summary>Propriété <see cref="SecondaryButtonText"/>.</summary>
    public static readonly DependencyProperty SecondaryButtonTextProperty =
        DependencyProperty.Register(nameof(SecondaryButtonText), typeof(string), typeof(Dialog), new PropertyMetadata(DesignText.Cancel));

    /// <summary>Le bouton principal répond : faux, par exemple, tant qu'il manque un choix ou qu'une action est en cours.</summary>
    public static readonly DependencyProperty IsPrimaryEnabledProperty =
        DependencyProperty.Register(nameof(IsPrimaryEnabled), typeof(bool), typeof(Dialog), new PropertyMetadata(true));

    /// <summary>
    /// Un travail lancé depuis le dialogue est en cours : ses champs sont désactivés, le focus attend donc sur le bouton
    /// secondaire, qu'Entrée ne déclenche pas (un second appui n'y met pas fin). Il revient au bouton principal à la fin.
    /// </summary>
    public static readonly DependencyProperty IsWorkingProperty =
        DependencyProperty.Register(nameof(IsWorking), typeof(bool), typeof(Dialog), new PropertyMetadata(false, (dialog, _) => ((Dialog)dialog).OnIsWorkingChanged()));

    /// <summary>Événement <see cref="Closed"/>.</summary>
    public static readonly RoutedEvent ClosedEvent =
        EventManager.RegisterRoutedEvent(nameof(Closed), RoutingStrategy.Bubble, typeof(EventHandler<DialogClosedEventArgs>), typeof(Dialog));

    private ButtonBase? _primaryButton;
    private ButtonBase? _secondaryButton;
    private ButtonBase? _closeButton;
    private UIElement? _scrim;

    /// <summary>Ce qui tenait le focus à l'ouverture.</summary>
    private IInputElement? _focusBefore;

    static Dialog()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Dialog), new FrameworkPropertyMetadata(typeof(Dialog)));
    }

    /// <summary>Fermé, avec le bouton choisi.</summary>
    public event EventHandler<DialogClosedEventArgs> Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    /// <summary>Titre.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="IsWideProperty"/>
    public bool IsWide
    {
        get => (bool)GetValue(IsWideProperty);
        set => SetValue(IsWideProperty, value);
    }

    /// <inheritdoc cref="CardMaxWidthProperty"/>
    public double CardMaxWidth
    {
        get => (double)GetValue(CardMaxWidthProperty);
        set => SetValue(CardMaxWidthProperty, value);
    }

    /// <summary>Le bouton principal fait ce qui ne s'annule pas : il le montre, et Entrée ne le déclenche pas.</summary>
    public bool IsDestructive
    {
        get => (bool)GetValue(IsDestructiveProperty);
        set => SetValue(IsDestructiveProperty, value);
    }

    /// <inheritdoc cref="IsSidePanelProperty"/>
    public bool IsSidePanel
    {
        get => (bool)GetValue(IsSidePanelProperty);
        set => SetValue(IsSidePanelProperty, value);
    }

    /// <summary>Un volet dans une place trop étroite : il vient du bas, sur toute la largeur.</summary>
    public bool IsFromBottom => (bool)GetValue(IsFromBottomProperty);

    /// <summary>Ouvert ; fermé par défaut.</summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Libellé du bouton principal (« OK » par défaut).</summary>
    public string PrimaryButtonText
    {
        get => (string)GetValue(PrimaryButtonTextProperty);
        set => SetValue(PrimaryButtonTextProperty, value);
    }

    /// <inheritdoc cref="IsPrimaryEnabledProperty"/>
    public bool IsPrimaryEnabled
    {
        get => (bool)GetValue(IsPrimaryEnabledProperty);
        set => SetValue(IsPrimaryEnabledProperty, value);
    }

    /// <inheritdoc cref="IsWorkingProperty"/>
    public bool IsWorking
    {
        get => (bool)GetValue(IsWorkingProperty);
        set => SetValue(IsWorkingProperty, value);
    }

    /// <summary>Libellé du bouton secondaire (« Annuler » par défaut) ; vide, il n'est pas proposé.</summary>
    public string SecondaryButtonText
    {
        get => (string)GetValue(SecondaryButtonTextProperty);
        set => SetValue(SecondaryButtonTextProperty, value);
    }

    /// <inheritdoc/>
    public override void OnApplyTemplate()
    {
        _primaryButton?.Click -= OnPrimary;
        _secondaryButton?.Click -= OnSecondary;
        _closeButton?.Click -= OnCloseButton;
        _scrim?.MouseLeftButtonDown -= OnScrimPressed;
        base.OnApplyTemplate();
        _primaryButton = GetTemplateChild(PrimaryButtonPart) as ButtonBase;
        _secondaryButton = GetTemplateChild(SecondaryButtonPart) as ButtonBase;
        _closeButton = GetTemplateChild(CloseButtonPart) as ButtonBase;
        _scrim = GetTemplateChild(ScrimPart) as UIElement;
        _primaryButton?.Click += OnPrimary;
        _secondaryButton?.Click += OnSecondary;
        _closeButton?.Click += OnCloseButton;
        _scrim?.MouseLeftButtonDown += OnScrimPressed;
    }

    /// <summary>
    /// Ferme le dialogue avec le choix donné. Une liaison de <see cref="IsOpen"/> est gardée, même à
    /// sens unique : le modèle de vue reçoit le choix par <see cref="Closed"/>.
    /// </summary>
    public void Close(DialogChoice choice)
    {
        SetCurrentValue(IsOpenProperty, false);
        RaiseEvent(new DialogClosedEventArgs(ClosedEvent, this, choice));
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (IsOpen && e.Key == Key.Escape)
        {
            Close(DialogChoice.None);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private static readonly DependencyPropertyKey IsLeavingPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsLeaving), typeof(bool), typeof(Dialog), new PropertyMetadata(false));

    /// <summary>Propriété <see cref="IsLeaving"/>.</summary>
    public static readonly DependencyProperty IsLeavingProperty = IsLeavingPropertyKey.DependencyProperty;

    /// <summary>Fermé, le dialogue s'efface encore : il reste à l'écran un instant, sans plus rien recevoir.</summary>
    public bool IsLeaving => (bool)GetValue(IsLeavingProperty);

    private void OnIsOpenChanged(bool isOpen)
    {
        if (isOpen)
        {
            // Rouvert pendant qu'il s'effaçait : il est là, entier.
            Transitions.StopExit(this);
            SetValue(IsLeavingPropertyKey, false);
            // Rouvert aussitôt après un refus, il a encore le focus : ce qu'on retient reste ce d'où l'on venait.
            if (!IsKeyboardFocusWithin)
            {
                _focusBefore = Keyboard.FocusedElement;
            }
            // Sa carte arrive en fondu, en montant un peu : elle s'ouvre, elle ne surgit pas.
            // À sa première ouverture, son modèle n'est pas encore en place : il l'est ici.
            ApplyTemplate();
            if (GetTemplateChild("Card") is FrameworkElement card)
            {
                // Un volet glisse depuis son bord : la droite, ou le bas quand la place est étroite.
                if (IsSidePanel)
                {
                    Transitions.SlideIn(card, IsFromBottom ? Dock.Bottom : Dock.Right);
                }
                else
                {
                    Transitions.Enter(card, DesignKeys.NormalDuration, Transitions.DialogRise, kind: MotionKinds.Surfaces);
                }
            }
            // Une fois le dialogue affiché, le focus va au premier élément qui le prend.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, FocusFirst);
        }
        else
        {
            // Il s'efface, avec son voile, au lieu de disparaître d'un coup ; le focus, lui, repart tout de suite.
            SetValue(IsLeavingPropertyKey, true);
            Transitions.Exit(this, () => SetValue(IsLeavingPropertyKey, false));
            Dispatcher.BeginInvoke(DispatcherPriority.Input, GiveFocusBack);
        }
    }

    /// <summary>Le focus resté dans le dialogue revient à ce qui le tenait ; s'il est déjà allé ailleurs, il y reste.</summary>
    private void GiveFocusBack()
    {
        if (IsOpen)
        {
            return;
        }
        var before = _focusBefore;
        _focusBefore = null;
        // Fermé, le dialogue s'efface encore un instant : le focus resté dedans est perdu autant que s'il avait disparu.
        var within = IsKeyboardFocusWithin;
        if ((KeyboardFocus.IsLost() || within) && before?.Focus() != true && within)
        {
            // Rien à qui le rendre : il ne reste pas sur un bouton qui s'efface, que la touche Entrée actionnerait encore.
            Keyboard.ClearFocus();
        }
    }

    private void OnIsWorkingChanged()
    {
        if (IsOpen)
        {
            // Une fois les boutons et les champs à jour : ce qui avait le focus vient d'être désactivé, ou masqué.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, SettleFocus);
        }
    }

    /// <summary>Le focus reste dans le dialogue : il attend le travail sur le bouton secondaire, et sa fin sur le principal.</summary>
    private void SettleFocus()
    {
        if (!IsOpen)
        {
            return;
        }
        var button = IsWorking ? _secondaryButton : _primaryButton;
        if (button is { IsEnabled: true, IsVisible: true })
        {
            button.Focus();
        }
        else
        {
            FocusFirst();
        }
    }

    private void FocusFirst() => MoveFocus(new TraversalRequest(FocusNavigationDirection.First));

    /// <inheritdoc/>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateFromBottom();
    }

    private void UpdateFromBottom() => SetValue(IsFromBottomPropertyKey, IsSidePanel && ActualWidth > 0 && ActualWidth < NarrowWidth);

    /// <summary>Un clic à côté d'un volet le referme, comme Échap ; à côté d'une fenêtre au milieu, il ne fait rien.</summary>
    private void OnScrimPressed(object sender, MouseButtonEventArgs e)
    {
        if (IsOpen && IsSidePanel)
        {
            Close(DialogChoice.None);
            e.Handled = true;
        }
    }

    private void OnCloseButton(object sender, RoutedEventArgs e) => Close(DialogChoice.None);

    private void OnPrimary(object sender, RoutedEventArgs e) => Close(DialogChoice.Primary);

    private void OnSecondary(object sender, RoutedEventArgs e) => Close(DialogChoice.Secondary);

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new AutomationPeers.DialogPeer(this);
}
