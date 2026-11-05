using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace KyfRev.Presentation.Wpf.Extensions;

/// <summary>La vue d'une extension, en éléments natifs.</summary>
internal sealed partial class ExtensionPage : UserControl
{
    private static readonly TimeSpan ReleaseCheck = TimeSpan.FromMilliseconds(30);

    private ExtensionPageViewModel? _page;
    private List<(string Action, Control Control)> _shown = [];
    private DispatcherTimer? _waitingForRelease;

    public ExtensionPage()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            _page?.PropertyChanged -= OnPageChanged;
            _page = e.NewValue as ExtensionPageViewModel;
            _page?.PropertyChanged += OnPageChanged;
            Show();
        };
    }

    /// <summary>Le contrôle à sa place n'existe plus : l'action qui vient de servir, ou la première qui reste.</summary>
    private static Control? Next(List<(string Action, Control Control)> shown, string? lastAction) =>
        shown.FirstOrDefault(item => item.Action == lastAction).Control ?? shown.FirstOrDefault().Control;

    private void OnPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExtensionPageViewModel.Content))
        {
            ShowOnceReleased();
        }
    }

    /// <summary>
    /// Un champ se valide en perdant le focus, donc dès qu'on appuie sur un bouton de la vue. Si le rendu remplaçait ce bouton avant le
    /// relâchement, le clic serait perdu : le nouveau rendu attend que la souris soit relâchée.
    /// </summary>
    private void ShowOnceReleased()
    {
        if (Mouse.LeftButton != MouseButtonState.Pressed || !IsMouseOver)
        {
            Show();
            return;
        }
        _waitingForRelease ??= new DispatcherTimer(ReleaseCheck, DispatcherPriority.Background, (_, _) =>
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed)
            {
                _waitingForRelease?.Stop();
                _waitingForRelease = null;
                Show();
            }
        }, Dispatcher);
    }

    private void Show()
    {
        if (_page?.Content is not { } content)
        {
            Body.Content = null;
            return;
        }
        var page = _page;
        var builder = new ExtensionContentBuilder(action => _ = page.ActAsync(action));
        var hadFocus = IsKeyboardFocusWithin;
        var place = _shown.FindIndex(item => item.Control.IsKeyboardFocusWithin);
        var action = place >= 0 ? _shown[place].Action : null;
        Body.Content = builder.Build(content);
        var shown = builder.Interactive;
        _shown = shown;
        if (hadFocus)
        {
            GiveFocus(place >= 0 && place < shown.Count && shown[place].Action == action ? shown[place].Control : Next(shown, page.LastAction));
        }
    }

    /// <summary>
    /// Le rendu remplace tout : le focus revient, une fois le nouveau contenu en place. Seulement s'il était ici : une extension qui se
    /// redessine d'elle-même ne le prend pas à ce qu'on fait ailleurs.
    /// </summary>
    private void GiveFocus(Control? target)
    {
        if (target is null)
        {
            Keyboard.ClearFocus();
            return;
        }
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            target.Focus();
            if (target is TextBox box)
            {
                box.CaretIndex = box.Text.Length;
            }
        });
    }
}
