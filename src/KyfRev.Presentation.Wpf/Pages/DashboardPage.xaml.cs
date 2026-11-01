using System.Windows.Controls;
using System.Windows.Input;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue « Aujourd'hui » : le tableau de bord.</summary>
internal sealed partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Entrée ou Espace sur l'en-tête d'un groupe le replie ou le déplie. La liste est alors
    /// reconstruite : le focus est rendu à l'en-tête.
    /// </summary>
    private void OnItemsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && Keyboard.FocusedElement is ListBoxItem { DataContext: DashboardGroup group })
        {
            group.ToggleCommand.Execute(null);
            Items.ScrollIntoView(group);
            Items.UpdateLayout();
            (Items.ItemContainerGenerator.ContainerFromItem(group) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
    }
}
