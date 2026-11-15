using System.Windows;
using System.Windows.Controls;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Le glossaire du coffre, comme les autres vues : le titre, le compte et « Nouvelle définition » en haut, la recherche, le
/// dossier, l'ordre et le filtre sur une ligne, puis les définitions en cartes, autant de colonnes que la largeur en permet. En
/// fenêtre étroite, une colonne de cartes, la recherche sur toute la largeur et le bouton raccourci à « Nouvelle ».
/// </summary>
internal sealed partial class DefinitionsPage : UserControl
{
    /// <summary>La largeur d'une carte : autant de colonnes que la place en permet.</summary>
    public static readonly DependencyProperty CardWidthProperty = DependencyProperty.Register(
        nameof(CardWidth), typeof(double), typeof(DefinitionsPage), new PropertyMetadata(360d));

    private const double SmallestCard = 300;
    private const double CardMargin = 12;
    private const double ScrollBar = 18;

    /// <summary>En deçà, la vue passe à une colonne et resserre ses marges.</summary>
    internal const double NarrowPage = 560;

    /// <summary>La nouvelle définition prend au plus deux colonnes de cartes : ses champs restent lisibles d'un coup d'œil.</summary>
    private const int DraftColumns = 2;

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

    /// <summary>Le menu d'une définition : l'ouvrir, la modifier, la mettre à la corbeille.</summary>
    private void OnRowMenu(object sender, ContextMenuEventArgs e)
    {
        var card = (FrameworkElement)sender;
        ActionMenus.Fill(card.ContextMenu!, card.DataContext is DefinitionRow row ? RowMenuItems(row) : [], e);
    }

    /// <summary>Les entrées du menu de cette définition ; aucune pendant sa modification.</summary>
    internal IReadOnlyList<Control> RowMenuItems(DefinitionRow row)
    {
        if (row.IsEditing || DataContext is not DefinitionsPageViewModel page)
        {
            return [];
        }
        return
        [
            ActionMenus.Quick(DefinitionsText.SeeInSheet, Icons.Document, () => row.OpenCommand.Execute(null)),
            ActionMenus.Quick(DefinitionsText.Edit, Icons.Edit, () => row.EditCommand.Execute(null)),
            ActionMenus.Item(row.IsMarkedEverywhere ? DefinitionsText.MarkHereOnly : DefinitionsText.MarkEverywhere, () => row.IsMarkedEverywhere = !row.IsMarkedEverywhere),
            new Separator(),
            ActionMenus.Item(ReadingText.DeleteDefinition, () => page.RequestDelete(row)),
        ];
    }

    /// <summary>La réponse à la mise à la corbeille part vers la vue.</summary>
    private void OnDeleteClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is DefinitionsPageViewModel page)
        {
            page.AnswerDeleteCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    /// <summary>« … » d'une carte : le même menu que le clic droit, sous le bouton.</summary>
    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DefinitionRow row } button && RowMenuItems(row) is { Count: > 0 } items)
        {
            var menu = new ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            foreach (var item in items)
            {
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }
    }

    /// <summary>Les colonnes de cartes selon la largeur ; en fenêtre étroite, une seule, et les champs l'un sous l'autre.</summary>
    private void Arrange()
    {
        var narrow = ActualWidth < NarrowPage;
        Layout.Margin = narrow ? new Thickness(16, 14, 16, 0) : new Thickness(32, 22, 32, 0);
        NewLabel.Text = narrow ? DefinitionsText.NewShort : DefinitionsText.NewDefinition;
        var available = Math.Max(0, ActualWidth - Layout.Margin.Left - Layout.Margin.Right - ScrollBar);
        SearchBox.Width = narrow ? available : Math.Min(340, available);
        var columns = Math.Max(1, (int)((available + CardMargin) / (SmallestCard + CardMargin)));
        CardWidth = Math.Max(120, ((available + CardMargin) / columns) - CardMargin);
        DraftCard.Width = (CardWidth * Math.Min(DraftColumns, columns)) + (CardMargin * (Math.Min(DraftColumns, columns) - 1));
        var stacked = columns < DraftColumns;
        Grid.SetColumn(ExplanationField, stacked ? 0 : 1);
        Grid.SetRow(ExplanationField, stacked ? 1 : 0);
        Grid.SetColumnSpan(TermField, stacked ? 2 : 1);
        Grid.SetColumnSpan(ExplanationField, stacked ? 2 : 1);
        TermField.Margin = stacked ? new Thickness(0, 0, 0, 10) : new Thickness(0, 0, 10, 0);
    }
}
