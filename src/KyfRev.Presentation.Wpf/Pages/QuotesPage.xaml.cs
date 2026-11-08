using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>La vue « Citations » : des cartes, autant de colonnes que la largeur en permet.</summary>
internal sealed partial class QuotesPage : UserControl
{
    /// <summary>Largeur d'une carte.</summary>
    public static readonly DependencyProperty CardWidthProperty = DependencyProperty.Register(
        nameof(CardWidth), typeof(double), typeof(QuotesPage), new PropertyMetadata(420d));

    private const double WantedCardWidth = 420;
    private const double CardMargin = 8;
    private const double PageMargins = 32;
    private const double ScrollBarRoom = 16;

    public QuotesPage()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Arrange();
        Loaded += (_, _) => Arrange();
    }

    /// <inheritdoc cref="CardWidthProperty"/>
    public double CardWidth
    {
        get => (double)GetValue(CardWidthProperty);
        set => SetValue(CardWidthProperty, value);
    }

    private void Arrange()
    {
        var available = Math.Max(0, ActualWidth - PageMargins - ScrollBarRoom);
        var columns = Math.Max(1, (int)(available / (WantedCardWidth + CardMargin)));
        CardWidth = Math.Max(120, (available / columns) - CardMargin);
    }

    /// <summary>
    /// « Afficher toutes les citations » disparaît avec son panneau, et le focus avec lui : il va au champ de recherche, qui
    /// reste (au bouton « Nouvelle citation » s'il n'y a plus de citation, donc plus de champ).
    /// </summary>
    private void OnShowAllClick(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => (SearchBox.IsVisible ? SearchBox : (UIElement)NewQuote).Focus());

    /// <summary>La réponse au formulaire part vers la vue.</summary>
    private void OnFormClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is QuotesPageViewModel page)
        {
            page.AnswerFormCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    /// <summary>La réponse à la mise à la corbeille part vers la vue.</summary>
    private void OnTrashClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is QuotesPageViewModel page)
        {
            page.AnswerTrashCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }
}
