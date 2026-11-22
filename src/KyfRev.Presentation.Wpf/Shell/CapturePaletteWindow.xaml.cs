using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// La fenêtre de la palette de capture (rangée 25B) : au-dessus de tout, sans barre ni bouton dans la barre des tâches. Elle
/// paraît au-dessus de kyfRev s'il est à l'écran, sinon au haut de l'écran, et se cache dès qu'on va ailleurs.
/// </summary>
public sealed partial class CapturePaletteWindow : Window
{
    /// <summary>Le temps de lire ce qui vient d'être fait, avant que la palette ne se cache.</summary>
    internal static readonly TimeSpan DoneShown = TimeSpan.FromMilliseconds(900);

    private readonly CapturePaletteViewModel _palette;
    private readonly DispatcherTimer _leaving;

    /// <summary>Une fenêtre pour cette palette.</summary>
    public CapturePaletteWindow(CapturePaletteViewModel palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        InitializeComponent();
        _palette = palette;
        DataContext = palette;
        _leaving = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = DoneShown };
        _leaving.Tick += (_, _) =>
        {
            _leaving.Stop();
            Hide();
        };
        palette.Finished = saysDone =>
        {
            if (saysDone)
            {
                _leaving.Start();
            }
            else
            {
                Hide();
            }
        };
    }

    /// <summary>La palette s'ouvre : au-dessus de <paramref name="application"/> s'il est à l'écran, sinon au haut de l'écran.</summary>
    public void ShowNear(Window? application)
    {
        _leaving.Stop();
        _palette.Open();
        if (application is { IsVisible: true, WindowState: not WindowState.Minimized })
        {
            var width = application.WindowState == WindowState.Maximized ? SystemParameters.WorkArea.Width : application.ActualWidth;
            var left = application.WindowState == WindowState.Maximized ? SystemParameters.WorkArea.Left : application.Left;
            var top = application.WindowState == WindowState.Maximized ? SystemParameters.WorkArea.Top : application.Top;
            var height = application.WindowState == WindowState.Maximized ? SystemParameters.WorkArea.Height : application.ActualHeight;
            Left = left + ((width - Width) / 2);
            Top = top + (height * 0.15);
        }
        else
        {
            var area = SystemParameters.WorkArea;
            Left = area.Left + ((area.Width - Width) / 2);
            Top = area.Top + (area.Height * 0.22);
        }
        Show();
        Activate();
        Input.Focus();
        // Elle descend un peu en paraissant (140 ms) ; d'un coup avec les animations réduites.
        Transitions.Enter(Frame, DesignKeys.FastDuration, rise: -8, kind: MotionKinds.Surfaces);
    }

    /// <summary>Les touches : flèches pour choisir, Entrée pour faire, Ctrl+Entrée pour une fiche, Échap pour fermer.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                break;
            case Key.Down:
                _palette.Move(1);
                break;
            case Key.Up:
                _palette.Move(-1);
                break;
            case Key.Enter when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                _palette.RunNewSheetCommand.Execute(null);
                break;
            case Key.Enter:
                _palette.RunSelectedCommand.Execute(null);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    /// <summary>Un clic sur un choix le fait.</summary>
    private void OnChoiceClicked(object sender, MouseButtonEventArgs e)
    {
        for (var element = e.OriginalSource as DependencyObject; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is ListBoxItem { DataContext: CaptureChoice choice })
            {
                _palette.RunCommand.Execute(choice);
                e.Handled = true;
                return;
            }
        }
    }

    /// <summary>On va ailleurs : la palette se cache, sauf pendant qu'elle écrit ou qu'elle dit ce qu'elle a fait.</summary>
    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!_palette.IsWorking && !_leaving.IsEnabled)
        {
            Hide();
        }
    }
}
