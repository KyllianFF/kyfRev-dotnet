using System.Windows;
using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>L'onglet « Général » de la vue « Paramètres » : deux colonnes quand la place le permet, l'aperçu tout près des réglages sinon.</summary>
internal sealed partial class GeneralSettingsPage : UserControl
{
    private const double TwoColumns = 860;

    public GeneralSettingsPage()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Arrange();
        Loaded += (_, _) => Arrange();
    }

    private static void Place(UIElement card, int row, int column)
    {
        Grid.SetRow(card, row);
        Grid.SetColumn(card, column);
    }

    private void Arrange()
    {
        var wide = ActualWidth >= TwoColumns;
        SecondColumn.Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        AppearanceCard.Margin = wide ? new Thickness(0, 0, 8, 8) : new Thickness(0, 0, 0, 8);
        Grid.SetRowSpan(AppearanceCard, wide ? 2 : 1);
        Place(AccessibilityCard, wide ? 0 : 2, wide ? 1 : 0);
        Place(PreviewCard, 1, wide ? 1 : 0);
    }
}
