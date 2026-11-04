using System.Windows;
using System.Windows.Controls;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue « Santé du coffre ».</summary>
internal sealed partial class HealthPage : UserControl
{
    public HealthPage()
    {
        InitializeComponent();
    }

    /// <summary>Le menu d'une ligne : son action, et de quoi copier ce qu'elle dit.</summary>
    private void OnRowMenu(object sender, ContextMenuEventArgs e)
    {
        var border = (FrameworkElement)sender;
        ActionMenus.Fill(border.ContextMenu!, border.DataContext is HealthRow row ? RowMenuItems(row) : [], e);
    }

    /// <summary>Les entrées du menu de cette ligne.</summary>
    internal IReadOnlyList<Control> RowMenuItems(HealthRow row)
    {
        if (DataContext is not HealthPageViewModel page)
        {
            return [];
        }
        var copy = ActionMenus.Quick(DesignText.Copy, Icons.Copy, () => page.CopyRow(row));
        return row is { Action: { } action, ActionLabel: { } label }
            ? [ActionMenus.Quick(label, row.IsSubtle ? Icons.Document : Icons.Sync, () => action.Execute(null), action.CanExecute(null)), copy]
            : [copy];
    }
}
