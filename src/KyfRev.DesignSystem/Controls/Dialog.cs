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
/// bouton principal.
/// </summary>
[TemplatePart(Name = PrimaryButtonPart, Type = typeof(ButtonBase))]
[TemplatePart(Name = SecondaryButtonPart, Type = typeof(ButtonBase))]
public class Dialog : ContentControl
{
    /// <summary>Bouton principal dans le modèle.</summary>
    public const string PrimaryButtonPart = "PART_PrimaryButton";

    /// <summary>Bouton secondaire dans le modèle.</summary>
    public const string SecondaryButtonPart = "PART_SecondaryButton";

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

    /// <summary>Événement <see cref="Closed"/>.</summary>
    public static readonly RoutedEvent ClosedEvent =
        EventManager.RegisterRoutedEvent(nameof(Closed), RoutingStrategy.Bubble, typeof(EventHandler<DialogClosedEventArgs>), typeof(Dialog));

    private ButtonBase? _primaryButton;
    private ButtonBase? _secondaryButton;

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
        base.OnApplyTemplate();
        _primaryButton = GetTemplateChild(PrimaryButtonPart) as ButtonBase;
        _secondaryButton = GetTemplateChild(SecondaryButtonPart) as ButtonBase;
        _primaryButton?.Click += OnPrimary;
        _secondaryButton?.Click += OnSecondary;
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

    private void OnIsOpenChanged(bool isOpen)
    {
        if (isOpen)
        {
            // Rouvert aussitôt après un refus, il a encore le focus : ce qu'on retient reste ce d'où l'on venait.
            if (!IsKeyboardFocusWithin)
            {
                _focusBefore = Keyboard.FocusedElement;
            }
            // Une fois le dialogue affiché, le focus va au premier élément qui le prend.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => MoveFocus(new TraversalRequest(FocusNavigationDirection.First)));
        }
        else
        {
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
        if (KeyboardFocus.IsLost())
        {
            before?.Focus();
        }
    }

    private void OnPrimary(object sender, RoutedEventArgs e) => Close(DialogChoice.Primary);

    private void OnSecondary(object sender, RoutedEventArgs e) => Close(DialogChoice.Secondary);

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new AutomationPeers.DialogPeer(this);
}
