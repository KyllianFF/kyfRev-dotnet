using System.Windows;
using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Les réglages du coffre : une section par sujet, l'enregistrement toujours à portée.</summary>
internal sealed partial class SettingsPage : UserControl
{
    private const double TwoColumns = 860;
    private const double SideBySideBar = 560;

    public SettingsPage()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Arrange();
        Loaded += (_, _) =>
        {
            Arrange();
            if (DataContext is SettingsPageViewModel page)
            {
                page.LoadCommand.Execute(null);
            }
        };
    }

    private void Arrange()
    {
        var wide = ActualWidth >= TwoColumns;
        Sections.Columns = wide ? 2 : 1;
        CalendarCard.Margin = wide ? new Thickness(0, 0, 8, 8) : new Thickness(0, 0, 0, 8);

        // Quand la place manque, le bouton passe sous son explication plutôt que de s'y écraser.
        var sideBySide = ActualWidth >= SideBySideBar;
        Grid.SetRow(SaveSettings, sideBySide ? 0 : 1);
        Grid.SetColumn(SaveSettings, sideBySide ? 1 : 0);
        Grid.SetColumnSpan(SaveSettings, sideBySide ? 1 : 2);
        SaveSettings.Margin = sideBySide ? new Thickness(12, 0, 0, 0) : new Thickness(0, 8, 0, 0);
    }
}
