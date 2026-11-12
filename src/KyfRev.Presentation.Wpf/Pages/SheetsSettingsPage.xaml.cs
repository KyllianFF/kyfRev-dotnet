using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La section « Feuilles » des paramètres : les réglages à gauche, ce qu'ils donnent à droite ; en fenêtre étroite, tout
/// se suit sur une colonne.
/// </summary>
internal sealed partial class SheetsSettingsPage : UserControl
{
    /// <summary>En deçà, l'aperçu ne tient plus à côté des réglages.</summary>
    internal const double TwoColumns = 900;

    public SheetsSettingsPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width);
    }

    private void Arrange(double width)
    {
        var wide = width >= TwoColumns;
        Grid.SetColumn(Side, wide ? 1 : 0);
        Grid.SetRow(Side, wide ? 0 : 1);
        Grid.SetColumnSpan(Main, wide ? 1 : 2);
        Grid.SetColumnSpan(Side, wide ? 1 : 2);
        Main.Margin = wide ? new Thickness(0, 0, 12, 0) : default;
    }

    /// <summary>Entrée, dans le champ des marges : la valeur tapée s'applique, sans quitter le champ.</summary>
    private void OnMarginKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CustomMarginBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            CustomMarginBox.SelectAll();
            e.Handled = true;
        }
    }
}

/// <summary>L'onglet « Raccourcis et menu » des paramètres : il prend les touches tapées pendant qu'un raccourci les attend.</summary>
internal sealed partial class ShortcutsSettingsPage : UserControl
{
    public ShortcutsSettingsPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Sections.Columns = e.NewSize.Width < 820 ? 1 : 2;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (DataContext is ShortcutsSettingsViewModel page && page.IsWaiting)
        {
            // Alt fait de la touche une touche « système » : c'est elle qu'on veut, pas « System ».
            e.Handled = page.Take(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
        }
    }
}
