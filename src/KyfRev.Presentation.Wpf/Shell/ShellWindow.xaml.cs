using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    private const int NonClientRightButtonDown = 0x00A4;
    private const int OnCaption = 2;
    private const int OnMaximizeButton = 9;

    private readonly ShellViewModel _shell;
    private const double PaletteEdge = 16;
    private const double PaletteMinWidth = 360;
    private const double PaletteMinHeight = 120;
    private const double PaletteMaxHeight = 520;

    /// <summary>Ce qui entoure la liste de la recherche, en hauteur : les marges de sa carte, et la ligne d'aide.</summary>
    private const double PaletteAround = 120;

    private IInputElement? _focusBeforePalette;
    private bool _paletteLeft;
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
        PreviewMouseDown += OnClickAnywhere;
        Deactivated += (_, _) => _shell.ClosePalette();
        Loaded += (_, _) => ShowIcon();
        DpiChanged += (_, _) => ShowIcon();
        LocationChanged += (_, _) =>
        {
            FitFrame();
            FitScreen();
        };
        Loaded += (_, _) => FocusNavigation();
    }

    /// <inheritdoc/>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        // Coupée, une conversion laisse un dossier temporaire, le coffre en clair s'il s'agit d'un déchiffrement : la page
        // demande de laisser kyfRev ouvert jusqu'au bout.
        e.Cancel = e.Cancel || _shell.Current.Page is Pages.ConversionPageViewModel { IsConverting: true };
        base.OnClosing(e);
    }

    /// <inheritdoc/>
    protected override void OnSourceInitialized(EventArgs e)
    {
        // Après la base : ce filtre passe donc avant celui du cadre, qui répondrait à sa place.
        base.OnSourceInitialized(e);
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(OnWindowMessage);
        FitScreen();
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
        FitScreen();
    }

    /// <summary>
    /// Donne à la coquille l'espace de travail de l'écran où se trouve la fenêtre, en pixels indépendants : le zoom ne dépasse pas
    /// ce que cet écran peut tenir, la fenêtre gardant sa largeur minimale.
    /// </summary>
    internal void FitScreen()
    {
        var area = _shell.Appearance.WorkArea(new WindowInteropHelper(this).Handle);
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }
        var dpi = VisualTreeHelper.GetDpi(this);
        _shell.FitScreen(area.Width / dpi.DpiScaleX, area.Height / dpi.DpiScaleY);
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
            case NonClientLeftButtonDown or NonClientRightButtonDown when _shell.IsPaletteOpen && wParam == OnCaption:
                // Un clic dans la barre de titre, hors de ses boutons : la recherche se ferme, comme sur un clic ailleurs. Un bord tiré pour
                // redimensionner la fenêtre la garde ouverte : la liste suit le champ.
                _shell.ClosePalette();
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
        if (e.PropertyName == nameof(ShellViewModel.SelectedResult) && _shell.SelectedResult is { } selected)
        {
            // Aux flèches, la ligne choisie reste à l'écran : la liste peut être plus haute que sa place.
            PaletteResults.ScrollIntoView(selected);
        }
        if (e.PropertyName != nameof(ShellViewModel.IsPaletteOpen))
        {
            return;
        }
        if (_shell.IsPaletteOpen)
        {
            _focusBeforePalette = Keyboard.FocusedElement;
            LayoutUpdated += OnLayoutUpdated;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                PlacePalette();
                PaletteQuery.Focus();
            });
            return;
        }
        LayoutUpdated -= OnLayoutUpdated;
        // L'élément d'avant n'est gardé que le temps de la recherche : retenu, il garderait en vie une vue fermée depuis
        // (l'éditeur d'une fiche, son texte, son historique), même après le verrouillage du coffre.
        var before = _focusBeforePalette;
        _focusBeforePalette = null;
        if (_paletteLeft)
        {
            return;
        }
        if (before is UIElement { IsVisible: true } previous)
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

    /// <summary>
    /// La liste se place sous le champ de la barre de titre, aussi large que lui (360 pixels au moins), sans sortir de la fenêtre.
    /// </summary>
    internal void PlacePalette()
    {
        if (!_shell.IsPaletteOpen || !SearchHost.IsVisible || !Palette.IsVisible)
        {
            return;
        }
        var left = SearchHost.TranslatePoint(default, Palette).X;
        var room = Math.Max(0, Palette.ActualWidth - (2 * PaletteEdge));
        var width = Math.Min(Math.Max(SearchHost.ActualWidth, PaletteMinWidth), room);
        left = Math.Clamp(left, PaletteEdge, Math.Max(PaletteEdge, Palette.ActualWidth - PaletteEdge - width));
        PaletteCard.Width = width;
        PaletteCard.Margin = new Thickness(left, 2, PaletteEdge, PaletteEdge);
        PaletteResults.MaxHeight = Math.Clamp(Palette.ActualHeight - PaletteAround, PaletteMinHeight, PaletteMaxHeight);
    }

    /// <summary>
    /// Le champ perd le focus pour autre chose que la liste : la recherche se ferme, comme un menu, et le focus reste où l'on a cliqué.
    /// Une fenêtre qui passe derrière une autre la garde ouverte.
    /// </summary>
    private void OnPaletteQueryLeft(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_shell.IsPaletteOpen || !IsActive || e.NewFocus is not DependencyObject target || PaletteCard.IsAncestorOf(target))
        {
            return;
        }
        _focusBeforePalette = null;
        _paletteLeft = true;
        _shell.ClosePalette();
        _paletteLeft = false;
    }

    /// <summary>Après chaque mise en page, la liste rejoint le champ, qui a pu bouger sans changer de taille.</summary>
    private void OnLayoutUpdated(object? sender, EventArgs e) => PlacePalette();

    /// <summary>Un clic ailleurs que dans le champ ou la liste ferme la recherche, comme un menu.</summary>
    private void OnClickAnywhere(object sender, MouseButtonEventArgs e)
    {
        if (_shell.IsPaletteOpen && e.OriginalSource is DependencyObject source && VisualOf(source) is { } target && !SearchHost.IsAncestorOf(target) && !PaletteCard.IsAncestorOf(target))
        {
            _shell.ClosePalette();
        }
    }

    /// <summary>Ce qu'on a cliqué, ou, pour un morceau de texte (un <c>Run</c>), le bloc qui le contient : seul un élément visuel a des ancêtres visuels.</summary>
    private static DependencyObject? VisualOf(DependencyObject source)
    {
        var element = source;
        while (element is ContentElement)
        {
            element = LogicalTreeHelper.GetParent(element);
        }
        return element;
    }

    /// <summary>L'icône de l'application, dans l'image de son fichier .ico la plus proche de la taille à l'écran : nette à toutes les échelles.</summary>
    internal void ShowIcon()
    {
        using var stream = typeof(ShellWindow).Assembly.GetManifestResourceStream("kyfrev-icone.ico");
        if (stream is null)
        {
            return;
        }
        var pixels = AppIcon.Width * VisualTreeHelper.GetDpi(this).DpiScaleX;
        var frames = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames;
        AppIcon.Source = frames.Where(frame => frame.PixelWidth >= pixels).OrderBy(frame => frame.PixelWidth).FirstOrDefault()
            ?? frames.OrderByDescending(frame => frame.PixelWidth).First();
    }

    private void OnPaletteClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && ItemsControl.ContainerFromElement(PaletteResults, e.OriginalSource as DependencyObject) is ListBoxItem)
        {
            _shell.RunSelected();
        }
    }
}
