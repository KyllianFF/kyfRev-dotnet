using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue de la barre de révision.</summary>
internal sealed partial class ReviewBar : UserControl
{
    /// <summary>Le bouton dont le menu a ouvert le champ de date : le focus y revient.</summary>
    private Button? _menuButton;

    /// <summary>Les étapes montrées en dernier pour l'élément de la barre : ce qui change ensuite vient d'une révision.</summary>
    private IReadOnlyList<StepMark>? _steps;

    public ReviewBar()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ReviewBarViewModel previous)
            {
                previous.PropertyChanged -= OnBarChanged;
            }
            // Un autre élément : sa frise est le point de départ, elle ne s'anime pas.
            _steps = (e.NewValue as ReviewBarViewModel)?.Steps;
            if (e.NewValue is ReviewBarViewModel bar)
            {
                bar.PropertyChanged += OnBarChanged;
            }
        };
    }

    private void OnBarChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ReviewBarViewModel.Steps) || sender is not ReviewBarViewModel bar)
        {
            return;
        }
        var before = _steps;
        var now = bar.Steps;
        _steps = now;
        // Une fois la frise refaite et mise en page.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => MarkValidated(before, now));
    }

    /// <summary>
    /// La frise vient d'être refaite après une révision : chaque étape qui vient d'être validée se remplit en fondu, sa coche
    /// grandit, et le trait avance vers l'étape suivante.
    /// </summary>
    /// <returns>Combien d'étapes viennent d'être validées à l'écran.</returns>
    internal int MarkValidated(IReadOnlyList<StepMark>? before, IReadOnlyList<StepMark> now)
    {
        if (before is null || before.Count != now.Count || !Steps.IsVisible)
        {
            return 0;
        }
        var marked = 0;
        for (var index = 0; index < now.Count; index++)
        {
            if (!now[index].IsDone || before[index].IsDone || PartOf<Border>(index, "Point") is not { } point)
            {
                continue;
            }
            // Elle était l'étape en cours, au fond léger : elle prend la couleur pleine.
            Transitions.FadeBrush(point, point, Border.BackgroundProperty, TryFindResource("Kyf.Brush.NavigationActiveBackground") as System.Windows.Media.Brush);
            if (PartOf<FrameworkElement>(index, "Check") is { } check)
            {
                Transitions.Grow(check, 0.4, new Point(0.5, 0.5));
            }
            if (PartOf<FrameworkElement>(index + 1, "Link") is { } link)
            {
                Transitions.Stretch(link, 0, Dock.Left);
            }
            marked++;
        }
        return marked;
    }

    /// <summary>Une pièce du dessin d'une étape de la frise, par son nom ; rien si l'étape n'est pas à l'écran.</summary>
    internal T? PartOf<T>(int step, string name)
        where T : FrameworkElement
    {
        if (step < 0 || step >= Steps.Items.Count || Steps.ItemContainerGenerator.ContainerFromIndex(step) is not ContentPresenter presenter)
        {
            return null;
        }
        presenter.ApplyTemplate();
        return presenter.ContentTemplate?.FindName(name, presenter) as T;
    }

    /// <summary>« Reporter » et « ⋯ » ouvrent leur menu au-dessus du bouton, la barre étant en bas ; le clavier y entre.</summary>
    private void OnMenuButton(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        _menuButton = button;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Top;
        button.ContextMenu.IsOpen = true;
    }

    /// <summary>Le champ de date prend le focus dès qu'il paraît, et le rend quand il se referme.</summary>
    private void OnDayFormVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => ChosenDay.Focus());
        }
        else
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, GiveFocusBack);
        }
    }

    /// <summary>Un refus laisse le champ ouvert : la date reprend le focus, que son bouton a perdu le temps de l'essai.</summary>
    private void OnErrorVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue && DayForm.IsVisible)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (KeyboardFocus.IsLost() || DayForm.IsKeyboardFocusWithin)
                {
                    ChosenDay.Focus();
                }
            });
        }
    }

    /// <summary>Le focus resté dans le champ revient au bouton qui l'a ouvert ; archivée par cette révision, la barre n'a plus que la remise en révision.</summary>
    private void GiveFocusBack()
    {
        if (!IsVisible || !KeyboardFocus.IsLost())
        {
            return;
        }
        var opener = _menuButton ?? MoreButton;
        (opener.IsVisible ? opener : RestartArchived).Focus();
    }
}
