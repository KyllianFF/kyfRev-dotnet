using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <remarks>
/// L'animation d'ouverture : jouée une fois par lancement, par-dessus toute la fenêtre, barre de titre comprise. La première
/// fois, sur l'accueil, elle prend son temps et porte le logotype en tête de l'accueil ; ensuite elle est plus courte. Un
/// clic ou une touche la passe, et rien ne joue quand les animations sont réduites.
/// </remarks>
public sealed partial class ShellWindow
{
    /// <summary>L'animation d'ouverture en cours ; rien quand elle n'a pas joué ou qu'elle a laissé la place.</summary>
    internal OpeningView? Opening { get; private set; }

    /// <summary>La fenêtre a été dessinée à l'écran une première fois.</summary>
    internal bool IsRendered { get; private set; }

    /// <summary>
    /// Pose l'animation d'ouverture, à appeler avant de montrer la fenêtre : elle en est alors la première image, rien de
    /// l'interface ne paraît avant elle. Elle ne commence qu'une fois la fenêtre à l'écran, pour être vue dès son début. La
    /// première ouverture est celle où l'accueil se montre sans que l'animation longue ait jamais été vue sur cet ordinateur.
    /// </summary>
    /// <param name="animated">Pour les essais : joue même quand les transitions sont coupées pour eux, ou ne joue pas.</param>
    /// <returns>Vrai si l'animation a commencé ; faux quand les animations sont réduites, ou celle-ci coupée dans les Paramètres.</returns>
    public bool PlayOpening(bool? animated = null)
    {
        var moves = animated ?? (!Transitions.Skip && TryFindResource(DesignKeys.NormalDuration) is Duration { HasTimeSpan: true } duration && duration.TimeSpan > TimeSpan.Zero
            && !(TryFindResource(DesignKeys.MotionOff) is MotionKinds off && off.HasFlag(MotionKinds.Opening)));
        if (!moves || Opening is not null)
        {
            return false;
        }
        var memory = _shell.FirstUse;
        var first = _shell.Current.Page is WelcomePageViewModel && memory is not null && !memory.Has(FirstUse.OpeningSeen);
        // Avant que la fenêtre ne soit montrée, sa largeur est celle qu'elle demande.
        var width = Root.ActualWidth > 0 ? Root.ActualWidth : double.IsNaN(Width) ? MinWidth : Width;
        var opening = new OpeningView(first ? OpeningPace.First : OpeningPace.Usual, OpeningView.MarkFor(width, first))
        {
            // La liste du jour se lit encore : le logotype reste, plutôt qu'une vue vide.
            StillLoading = () => _shell.Current.Page is DashboardPageViewModel { IsLoading: true },
            Landing = first ? () => Descendants(Root).OfType<WelcomePage>().FirstOrDefault()?.FindName("Brand") as Logotype : null,
        };
        Panel.SetZIndex(opening, int.MaxValue);
        // Sur toute la fenêtre, barre de titre comprise : rien de l'interface ne se voit avant que l'animation ne laisse la place.
        Grid.SetRowSpan(opening, Frame.RowDefinitions.Count);
        opening.Finished += (_, _) =>
        {
            Frame.Children.Remove(opening);
            PreviewKeyDown -= OnKeyDuringOpening;
            ContentRendered -= OnFirstRender;
            Opening = null;
        };
        Opening = opening;
        Frame.Children.Add(opening);
        PreviewKeyDown += OnKeyDuringOpening;
        if (first)
        {
            memory!.Remember(FirstUse.OpeningSeen);
        }
        if (IsRendered)
        {
            opening.Play();
        }
        else
        {
            ContentRendered += OnFirstRender;
        }
        return true;
    }

    /// <summary>La fenêtre est à l'écran : l'animation commence, dès sa première image.</summary>
    private void OnFirstRender(object? sender, EventArgs e)
    {
        ContentRendered -= OnFirstRender;
        Opening?.Play();
    }

    /// <summary>
    /// Une touche passe l'animation. Elle s'arrête là, pour ne pas activer un bouton que l'on ne voit pas encore ; sauf dans
    /// un champ (le mot de passe d'un coffre chiffré), où l'on tape déjà, et sauf un raccourci de Windows ou de l'application.
    /// </summary>
    private void OnKeyDuringOpening(object sender, KeyEventArgs e)
    {
        if (Opening is not { } opening)
        {
            return;
        }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return;
        }
        opening.Skip();
        var typing = Keyboard.FocusedElement is TextBoxBase or PasswordBox;
        var shortcut = e.Key == Key.System || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0;
        e.Handled = !typing && !shortcut;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }
}
