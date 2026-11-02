using System.Windows;
using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Le glossaire du coffre : les dossiers à gauche, les définitions en cartes à droite.</summary>
internal sealed partial class DefinitionsPage : UserControl
{
    /// <summary>La largeur d'une carte : autant de colonnes que la place en permet.</summary>
    public static readonly DependencyProperty CardWidthProperty = DependencyProperty.Register(
        nameof(CardWidth), typeof(double), typeof(DefinitionsPage), new PropertyMetadata(360d));

    private const double SmallestCard = 340;
    private const double CardMargin = 8;
    private const double ScrollBar = 18;
    private const double NarrowPage = 720;

    public DefinitionsPage()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Arrange();
        Loaded += (_, _) =>
        {
            Arrange();
            if (DataContext is DefinitionsPageViewModel page)
            {
                page.LoadCommand.Execute(null);
            }
        };
    }

    /// <inheritdoc cref="CardWidthProperty"/>
    public double CardWidth
    {
        get => (double)GetValue(CardWidthProperty);
        set => SetValue(CardWidthProperty, value);
    }

    /// <summary>Quand la place manque, les dossiers passent au-dessus des définitions.</summary>
    private void Arrange()
    {
        var narrow = ActualWidth < NarrowPage;
        FoldersColumn.Width = narrow ? new GridLength(0) : new GridLength(260);
        FoldersRow.Height = narrow ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        Grid.SetColumnSpan(FoldersPanel, narrow ? 2 : 1);
        Grid.SetRow(ListPanel, narrow ? 1 : 0);
        Grid.SetRowSpan(ListPanel, narrow ? 1 : 2);
        Grid.SetColumn(ListPanel, narrow ? 0 : 1);
        Grid.SetColumnSpan(ListPanel, narrow ? 2 : 1);
        FoldersPanel.Margin = narrow ? new Thickness(0, 0, 0, 8) : new Thickness(0, 0, 16, 0);
        FolderList.MaxHeight = narrow ? 120 : double.PositiveInfinity;
        var available = Math.Max(0, ActualWidth - Layout.Margin.Left - Layout.Margin.Right - (narrow ? 0 : 276) - ScrollBar);
        var columns = Math.Max(1, (int)(available / (SmallestCard + CardMargin)));
        CardWidth = Math.Max(120, (available / columns) - CardMargin);
    }
}
