using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Fenêtre principale. Le focus suit l'utilisateur : il entre dans la recherche quand elle
/// s'ouvre et revient d'où il venait quand elle se ferme ; au démarrage, il est sur le menu.
/// </summary>
public sealed partial class ShellWindow : Window
{
    private readonly ShellViewModel _shell;
    private IInputElement? _focusBeforePalette;

    /// <summary>La fenêtre de la coquille <paramref name="shell"/>.</summary>
    public ShellWindow(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        _shell = shell;
        InitializeComponent();
        DataContext = shell;
        shell.Appearance.Attach(this);
        shell.PropertyChanged += OnShellChanged;
        // MouseUp remonte jusqu'à la liste, même quand l'élément cliqué l'a déjà traité.
        PaletteResults.AddHandler(MouseUpEvent, new MouseButtonEventHandler(OnPaletteClick), handledEventsToo: true);
        Loaded += (_, _) => FocusNavigation();
    }

    /// <summary>Met le focus sur l'entrée choisie du menu.</summary>
    internal void FocusNavigation()
    {
        var list = _shell.SelectedSecondary is not null ? SecondaryNavigation : MainNavigation;
        if (list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem) is ListBoxItem item)
        {
            item.Focus();
        }
        else
        {
            list.Focus();
        }
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShellViewModel.IsPaletteOpen))
        {
            return;
        }
        if (_shell.IsPaletteOpen)
        {
            _focusBeforePalette = Keyboard.FocusedElement;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => PaletteQuery.Focus());
        }
        else if (_focusBeforePalette is UIElement { IsVisible: true } previous)
        {
            previous.Focus();
        }
        else
        {
            FocusNavigation();
        }
    }

    private void OnPaletteClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && ItemsControl.ContainerFromElement(PaletteResults, e.OriginalSource as DependencyObject) is ListBoxItem)
        {
            _shell.RunSelected();
        }
    }
}
