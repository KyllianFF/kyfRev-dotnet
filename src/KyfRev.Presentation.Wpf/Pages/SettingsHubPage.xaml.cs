using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La vue « Paramètres » : la liste de ses sections à gauche tant que la place le permet ; en fenêtre étroite, une liste
/// déroulante au-dessus de la section ouverte. La recherche montre ses résultats à la place de la liste des sections.
/// </summary>
internal sealed partial class SettingsHubPage : UserControl
{
    /// <summary>En deçà, la liste des sections ne tient plus à côté de leur contenu.</summary>
    internal const double SideListMinimum = 760;

    private SettingsHubViewModel? _followed;
    private bool _resultHadFocus;

    public SettingsHubPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width);
        Loaded += (_, _) => Follow(DataContext as SettingsHubViewModel);
        Unloaded += (_, _) => Follow(null);
        DataContextChanged += (_, e) => Follow(IsLoaded ? e.NewValue as SettingsHubViewModel : null);
    }

    private void Arrange(double width)
    {
        var wide = width >= SideListMinimum;
        Side.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        NarrowTop.Visibility = SectionChoice.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
        // La liste déroulante nomme déjà la section : son titre ne se répète pas en dessous.
        SectionTitle.Visibility = Side.Visibility;
        // « Rétablir » : à droite du titre, ou sous la ligne qui décrit la section quand la place manque.
        Grid.SetColumn(RestoreButton, wide ? 1 : 0);
        Grid.SetRow(RestoreButton, wide ? 0 : 1);
        RestoreButton.Margin = wide ? new Thickness(12, 0, 0, 0) : new Thickness(0, 8, 0, 0);
    }

    /// <summary>La vue suit son modèle tant qu'elle est montrée, pas au-delà : le modèle lui survit.</summary>
    private void Follow(SettingsHubViewModel? hub)
    {
        if (ReferenceEquals(hub, _followed))
        {
            return;
        }
        _followed?.PropertyChanging -= OnHubChanging;
        _followed?.PropertyChanged -= OnHubChanged;
        _followed = hub;
        _followed?.PropertyChanging += OnHubChanging;
        _followed?.PropertyChanged += OnHubChanged;
    }

    /// <summary>
    /// Un résultat choisi au clavier disparaît avec la liste des résultats : le clavier reprend sur la section qu'il
    /// vient d'ouvrir, au lieu de se perdre.
    /// </summary>
    private void OnHubChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsHubViewModel.Query) && sender is SettingsHubViewModel { IsSearching: false } && _resultHadFocus)
        {
            _resultHadFocus = false;
            Dispatcher.BeginInvoke(FocusSection, DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// Juste avant que la recherche change : un résultat a-t-il le focus ? Après, il sera trop tard pour le savoir, la liste
    /// des résultats se vide avant que le changement soit annoncé.
    /// </summary>
    private void OnHubChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsHubViewModel.Query))
        {
            _resultHadFocus = HasFocus(SideResults) || HasFocus(NarrowResults);
        }
    }

    /// <summary>L'élément qui a le focus dans la fenêtre est dans cette part de la vue.</summary>
    private bool HasFocus(Visual part) =>
        FocusManager.GetFocusedElement(FocusManager.GetFocusScope(this)) is Visual focused && (ReferenceEquals(focused, part) || focused.IsDescendantOf(part));

    private void FocusSection()
    {
        UIElement? target = !Side.IsVisible
            ? SectionChoice
            : _followed is { } hub && SectionList.ItemContainerGenerator.ContainerFromItem(hub.Selected) is { } row ? First<RadioButton>(row) : null;
        if (target is not null && !target.Focus())
        {
            // La fenêtre n'est pas devant : le clavier y reprendra là à son retour.
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(target), target);
        }
    }

    private static T? First<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T found || (found = First<T>(child)!) is not null)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>La liste déroulante choisit une section comme le fait un clic dans la liste de gauche.</summary>
    private void OnSectionChosen(object sender, SelectionChangedEventArgs e)
    {
        if (SectionChoice.SelectedItem is SettingsTab { IsSelected: false } tab)
        {
            tab.IsSelected = true;
        }
    }
}
