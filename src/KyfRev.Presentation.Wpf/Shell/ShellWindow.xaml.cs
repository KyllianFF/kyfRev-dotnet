using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Fenêtre principale. Sa largeur décide de la disposition du menu (<see cref="ShellLayout"/>).
/// Le focus suit l'utilisateur : il entre dans la recherche quand elle s'ouvre et revient d'où il
/// venait quand elle se ferme ; au démarrage, il est sur le menu. En plein écran, la fenêtre perd
/// son cadre et couvre tout l'écran ; elle retrouve ensuite son état. Elle dessine sa barre de titre ;
/// Windows garde ce qui fait une fenêtre, jusqu'au menu des dispositions sur le bouton « Agrandir ».
/// </summary>
public sealed partial class ShellWindow : Window
{
    private const int NonClientHitTest = 0x0084;
    private const int NonClientLeftButtonDown = 0x00A1;
    private const int NonClientLeftButtonUp = 0x00A2;
    private const int NonClientMouseLeave = 0x02A2;
    private const int OnMaximizeButton = 9;

    private readonly ShellViewModel _shell;
    private IInputElement? _focusBeforePalette;
    private WindowState _windowedState;
    private ResizeMode _windowedResize = ResizeMode.CanResize;

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
        MoreMenu.AddHandler(MenuItem.ClickEvent, new RoutedEventHandler(OnOverflowClick));
        Root.SizeChanged += (_, e) => shell.WindowWidth = e.NewSize.Width;
        LocationChanged += (_, _) => FitFrame();
        Loaded += (_, _) => FocusNavigation();
    }

    /// <inheritdoc/>
    protected override void OnSourceInitialized(EventArgs e)
    {
        // Après la base : ce filtre passe donc avant celui du cadre, qui répondrait à sa place.
        base.OnSourceInitialized(e);
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(OnWindowMessage);
    }

    /// <inheritdoc/>
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        var maximized = WindowState == WindowState.Maximized;
        var label = maximized ? ShellText.WindowRestore : ShellText.WindowMaximize;
        MaximizeIcon.Text = maximized ? Icons.WindowRestore : Icons.WindowMaximize;
        MaximizeButton.ToolTip = label;
        AutomationProperties.SetName(MaximizeButton, label);
        FitFrame();
    }

    /// <inheritdoc/>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        FitFrame();
    }

    /// <summary>
    /// Agrandie, Windows étend la fenêtre au-delà de l'écran de l'épaisseur de son cadre ; comme l'application dessine le sien, le contenu
    /// se retire d'autant, mesuré sur l'écran où elle se trouve. Le haut de la barre reste visible, le bas reste au-dessus de la barre des tâches.
    /// </summary>
    internal void FitFrame()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (WindowState != WindowState.Maximized || _shell.IsFullScreen || handle == 0)
        {
            Frame.Margin = default;
            return;
        }
        var overhang = _shell.Appearance.MaximizedOverhang(handle);
        var dpi = VisualTreeHelper.GetDpi(this);
        Frame.Margin = new Thickness(overhang.Left / dpi.DpiScaleX, overhang.Top / dpi.DpiScaleY, overhang.Right / dpi.DpiScaleX, overhang.Bottom / dpi.DpiScaleY);
    }

    /// <summary>
    /// Ce que Windows demande à la fenêtre. « Agrandir » se déclare comme le vrai bouton : sans cela, Windows 11 n'y ouvre pas son menu
    /// des dispositions.
    /// </summary>
    internal nint OnWindowMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        switch (message)
        {
            case NonClientHitTest:
                var over = IsOnMaximizeButton(lParam);
                MaximizeButton.Tag = over ? bool.TrueString : null;
                if (over)
                {
                    handled = true;
                    return OnMaximizeButton;
                }
                break;
            case NonClientLeftButtonDown when wParam == OnMaximizeButton:
                handled = true;
                break;
            case NonClientLeftButtonUp when wParam == OnMaximizeButton:
                handled = true;
                ToggleMaximized();
                break;
            case NonClientMouseLeave:
                MaximizeButton.Tag = null;
                break;
            default:
                break;
        }
        return 0;
    }

    private bool IsOnMaximizeButton(nint position)
    {
        if (!MaximizeButton.IsVisible)
        {
            return false;
        }
        var point = MaximizeButton.PointFromScreen(new Point(unchecked((short)(position & 0xFFFF)), unchecked((short)((position >> 16) & 0xFFFF))));
        return new Rect(0, 0, MaximizeButton.ActualWidth, MaximizeButton.ActualHeight).Contains(point);
    }

    private void ToggleMaximized()
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeClick(object sender, RoutedEventArgs e) => ToggleMaximized();

    private void OnCloseClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    /// <summary>Met le focus sur l'entrée choisie du menu (sur les onglets en fenêtre étroite).</summary>
    internal void FocusNavigation()
    {
        var list = _shell.Layout == NavigationLayout.Tabs ? TabNavigation
            : _shell.SelectedSecondary is not null ? SecondaryNavigation : MainNavigation;
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
        if (e.PropertyName == nameof(ShellViewModel.IsFullScreen))
        {
            ApplyFullScreen(_shell.IsFullScreen);
            return;
        }
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

    private void ApplyFullScreen(bool fullScreen)
    {
        if (fullScreen)
        {
            _windowedState = WindowState;
            _windowedResize = ResizeMode;
            WindowStyle = WindowStyle.None;
            // Sans cadre épais : Windows ferait déborder la fenêtre agrandie de l'épaisseur de ce cadre, tout autour de l'écran.
            ResizeMode = ResizeMode.NoResize;
            // Repasser par l'état normal fait couvrir aussi la barre des tâches.
            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = _windowedResize;
            WindowState = _windowedState;
        }
        // Du plein écran à une fenêtre déjà agrandie, l'état ne change pas : le cadre se recalcule quand même.
        FitFrame();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FitFrame);
    }

    /// <summary>« Plus » : les autres vues, dans un menu ouvert au-dessus de l'onglet.</summary>
    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        MoreMenu.PlacementTarget = MoreButton;
        MoreMenu.Placement = PlacementMode.Top;
        MoreMenu.IsOpen = true;
    }

    private void OnOverflowClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is MenuItem { DataContext: NavigationItem item })
        {
            _shell.Navigate(item);
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
