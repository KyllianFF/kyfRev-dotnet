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
    private const int NonClientCalcSize = 0x0083;
    private const int NonClientHitTest = 0x0084;
    private const int NonClientLeftButtonDown = 0x00A1;
    private const int NonClientLeftButtonUp = 0x00A2;
    private const int NonClientMouseLeave = 0x02A2;
    private const int NonClientRightButtonDown = 0x00A4;
    private const int OnCaption = 2;
    private const int OnMaximizeButton = 9;

    /// <summary><c>WVR_REDRAW</c> : la réponse à <c>WM_NCCALCSIZE</c> que donne le cadre, la fenêtre redessinée en entier.</summary>
    private const int RedrawWhole = 0x0300;

    /// <summary>L'épaisseur du bord qui redimensionne la fenêtre : celle du cadre (<c>ResizeBorderThickness</c>).</summary>
    internal const double ResizeBorder = 6;

    /// <summary>Ce qu'un coin prend sur chacun de ses deux bords : la diagonale se trouve sans viser au pixel.</summary>
    internal const double ResizeCorner = 16;

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
    private WindowState _lastState;
    private ResizeMode _windowedResize = ResizeMode.CanResize;

    /// <summary>La fenêtre de la coquille <paramref name="shell"/>.</summary>
    public ShellWindow(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        _shell = shell;
        InitializeComponent();
        // Les raccourcis marqués prennent les touches choisies dans les paramètres.
        KyfRev.Presentation.Wpf.Shell.ShortcutMap.Follow(this);
        DataContext = shell;
        shell.Appearance.Attach(this);
        shell.PropertyChanged += OnShellChanged;
        // Une vue qui en remplace une autre : celle qu'on quitte s'efface, celle qui arrive vient du côté où l'on va.
        // Le thème bascule en fondu : l'image de l'ancien s'efface par-dessus le nouveau.
        shell.Appearance.ThemeChanging += (_, _) => FadeTheme();
        shell.PropertyChanging += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.Current))
            {
                LeavePage();
            }
        };
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.Current))
            {
                EnterPage();
            }
        };
        // MouseUp remonte jusqu'à la liste, même quand l'élément cliqué l'a déjà traité.
        PaletteResults.AddHandler(MouseUpEvent, new MouseButtonEventHandler(OnPaletteClick), handledEventsToo: true);
        MoreMenu.AddHandler(MenuItem.ClickEvent, new RoutedEventHandler(OnOverflowClick));
        Root.SizeChanged += (_, e) => shell.WindowWidth = e.NewSize.Width;
        // Le menu réduit, élargi ou masqué : son bord glisse de l'ancienne largeur à la nouvelle.
        Sidebar.LayoutUpdated += (_, _) => FollowSidebar();
        // Le guide prend la place que la fenêtre lui laisse : ni plus large, ni plus haut qu'elle.
        Root.SizeChanged += (_, e) => (GuidePane.Width, GuidePane.Height) = GuideSize(e.NewSize);
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
        ContentRendered += (_, _) => IsRendered = true;
        // Le coffre ouvert, l'îlot de ses schémas démarre en arrière-plan : la vue s'affichera dès le premier clic.
        WarmUp = new Schemas.SchemaWarmUp(WarmHost, () => shell.MainNavigation.Concat(shell.SecondaryNavigation)
            .Select(entry => entry.Page).OfType<Pages.SchemasPageViewModel>().FirstOrDefault());
        shell.WorkspaceOpening += (_, _) => WarmUp.Schedule();
    }

    /// <summary>La préparation de l'îlot des schémas, en arrière-plan.</summary>
    internal Schemas.SchemaWarmUp WarmUp { get; }

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

    /// <summary>La taille du guide dans l'aide, pour une fenêtre de cette taille : 800 × 440 au plus, et ce qui reste autour de l'aide au moins.</summary>
    internal static (double Width, double Height) GuideSize(Size window) =>
        (Math.Clamp(window.Width - 96, 240, 800), Math.Clamp(window.Height - 300, 160, 440));

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
        // Rendue depuis la barre des tâches, la fenêtre pouvait garder la mise en page d'une taille d'avant : une grande bande vide
        // en bas, jusqu'à la navigation suivante. Le cadre est recalculé, puis le contenu remis à la taille de la fenêtre.
        if (_lastState == WindowState.Minimized && WindowState != WindowState.Minimized)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Refill);
        }
        _lastState = WindowState;
    }

    /// <summary>La largeur du menu à la dernière mise en page (0 : masqué) ; négative avant la première.</summary>
    private double _sidebarWidth = -1;

    /// <summary>
    /// Après une mise en page : si le menu a changé de largeur (barre, colonne d'icônes, masqué), son bord glisse de
    /// l'ancienne à la nouvelle, et la page avec lui. La mise en page, elle, a déjà changé, une seule fois. Une page qui
    /// montre une fenêtre native (l'éditeur d'Écrire, les Schémas) ne glisse pas : le rendu ne déplace pas ces fenêtres.
    /// </summary>
    /// <returns>Vrai si une transition a commencé.</returns>
    /// <summary>De combien la vue quittée recule en s'effaçant, et de combien celle qui arrive vient de côté.</summary>
    internal const double PageLeaveShift = 10;

    /// <inheritdoc cref="PageLeaveShift"/>
    internal const double PageEnterShift = 14;

    /// <summary>
    /// Le nombre de points de l'image de la vue quittée, au plus. La dessiner prend une quinzaine de millisecondes par million
    /// de points, avant que la vue ne change : sur un très grand écran, elle est dessinée moins fine (elle s'efface en un
    /// dixième de seconde) plutôt que de retarder le changement.
    /// </summary>
    internal const double PageGhostPixels = 1_500_000;

    /// <summary>La place, dans le menu, de la vue qu'on quitte ; −1 si elle n'y est pas (une fiche ouverte, par exemple).</summary>
    private int _leftPlace = -1;

    /// <summary>La place d'une vue dans le menu, principal puis secondaire.</summary>
    private int PlaceOf(NavigationItem? item) =>
        item is null ? -1 : _shell.MainNavigation.Concat(_shell.SecondaryNavigation).ToList().IndexOf(item);

    /// <summary>
    /// Le thème va changer : l'image de la fenêtre telle qu'elle est, dans l'ancien thème, est posée par-dessus et s'efface
    /// pendant que les nouvelles couleurs prennent place dessous. Une fenêtre native (l'éditeur, les schémas) passe devant
    /// cette image : elle change de couleurs d'elle-même.
    /// </summary>
    /// <returns>Vrai si le fondu a commencé.</returns>
    internal bool FadeTheme()
    {
        Transitions.StopExit(ThemeGhost);
        ThemeGhost.Visibility = Visibility.Collapsed;
        ThemeGhost.Source = null;
        if (!IsLoaded || Frame.ActualWidth < 1 || Frame.ActualHeight < 1
            || Transitions.DurationOf(Frame, KyfRev.DesignSystem.DesignKeys.FastDuration) == TimeSpan.Zero)
        {
            return false;
        }
        ThemeGhost.Source = Portrait(Frame);
        ThemeGhost.Width = Frame.ActualWidth;
        ThemeGhost.Height = Frame.ActualHeight;
        ThemeGhost.Visibility = Visibility.Visible;
        return Transitions.Exit(
            ThemeGhost,
            () =>
            {
                ThemeGhost.Visibility = Visibility.Collapsed;
                ThemeGhost.Source = null;
            },
            KyfRev.DesignSystem.DesignKeys.FastDuration,
            kind: KyfRev.DesignSystem.MotionKinds.Views);
    }

    /// <summary>L'image d'un élément tel qu'il est à l'écran, moins fine s'il est très grand (voir <see cref="PageGhostPixels"/>).</summary>
    private System.Windows.Media.Imaging.RenderTargetBitmap Portrait(FrameworkElement element)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var fine = GhostScale(element.ActualWidth * dpi.DpiScaleX * element.ActualHeight * dpi.DpiScaleY);
        var picture = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX * fine), (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY * fine), dpi.PixelsPerInchX * fine, dpi.PixelsPerInchY * fine, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(element.RenderSize));
        }
        picture.Render(drawing);
        picture.Freeze();
        return picture;
    }

    /// <summary>
    /// La vue courante va changer : son image est gardée par-dessus, le temps de s'effacer. Pas pour une vue qui montre une
    /// fenêtre native (l'éditeur, les schémas) : son image n'aurait pas leur contenu.
    /// </summary>
    internal bool LeavePage()
    {
        _leftPlace = PlaceOf(_shell.Current);
        PageGhost.Visibility = Visibility.Collapsed;
        PageGhost.Source = null;
        if (!IsLoaded || Page.ActualWidth < 1 || Page.ActualHeight < 1 || HostsNativeWindow(Page)
            || Transitions.DurationOf(Page, KyfRev.DesignSystem.DesignKeys.FastDuration) == TimeSpan.Zero)
        {
            return false;
        }
        PageGhost.Source = Portrait(Page);
        PageGhost.Width = Page.ActualWidth;
        PageGhost.Height = Page.ActualHeight;
        PageGhost.Visibility = Visibility.Visible;
        return true;
    }

    /// <summary>La finesse de l'image d'une vue de <paramref name="pixels"/> points : entière tant qu'elle tient dans <see cref="PageGhostPixels"/>.</summary>
    internal static double GhostScale(double pixels) => pixels <= PageGhostPixels ? 1 : Math.Sqrt(PageGhostPixels / pixels);

    /// <summary>
    /// La vue courante vient de changer : l'image de l'ancienne s'efface en reculant, la nouvelle arrive du côté où l'on va
    /// (vers le bas du menu : par la droite ; vers le haut : par la gauche ; hors du menu : sur place).
    /// </summary>
    internal int EnterPage()
    {
        var place = PlaceOf(_shell.Current);
        var direction = _leftPlace < 0 || place < 0 ? 0 : Math.Sign(place - _leftPlace);
        if (PageGhost.Visibility == Visibility.Visible)
        {
            Transitions.Exit(
                PageGhost,
                () =>
                {
                    PageGhost.Visibility = Visibility.Collapsed;
                    PageGhost.Source = null;
                },
                KyfRev.DesignSystem.DesignKeys.FastDuration,
                shift: -PageLeaveShift * direction,
                kind: KyfRev.DesignSystem.MotionKinds.Views);
        }
        Transitions.Enter(Page, KyfRev.DesignSystem.DesignKeys.FastDuration, shift: PageEnterShift * direction);
        return direction;
    }

    internal bool FollowSidebar()
    {
        var width = Sidebar.Visibility == Visibility.Visible ? Sidebar.ActualWidth : 0;
        var before = _sidebarWidth;
        if (Math.Abs(width - before) < 0.5)
        {
            return false;
        }
        _sidebarWidth = width;
        // La première largeur est celle qu'on trouve en ouvrant : elle ne s'anime pas.
        return before >= 0 && IsLoaded && Transitions.Resize(Sidebar, MenuCurtain, HostsNativeWindow(Page) ? null : Page, before, width);
    }

    /// <summary>Vrai si une fenêtre native (un moteur web) se montre sous cet élément.</summary>
    internal static bool HostsNativeWindow(DependencyObject root)
    {
        if (root is HwndHost { IsVisible: true })
        {
            return true;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is not UIElement { IsVisible: false } && HostsNativeWindow(child))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Le cadre recalculé par Windows, et le contenu remis à la taille de la fenêtre.</summary>
    internal void Refill()
    {
        _shell.Appearance.RefreshFrame(new WindowInteropHelper(this).Handle);
        FitFrame();
        InvalidateMeasure();
        Root.InvalidateMeasure();
        UpdateLayout();
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
            case NonClientCalcSize when wParam != 0 && !_shell.IsFullScreen && _shell.Appearance.LeaveRoomForHiddenTaskbar(window, lParam):
                // Agrandie, avec une barre des tâches qui se masque : la zone cliente s'arrête juste avant son bord, sans quoi la barre ne
                // se montre plus. Comme le cadre l'aurait fait, la fenêtre est redessinée en entier.
                handled = true;
                return RedrawWhole;
            case NonClientHitTest:
                // Les bords d'abord : un bouton de la barre de titre se déclare « contenu » et l'emporte sur le cadre, si bien que le
                // coin supérieur droit, sous « Fermer », ne redimensionnait plus la fenêtre.
                if (ResizeEdgeAt(lParam) is var edge and not 0)
                {
                    MaximizeButton.Tag = null;
                    handled = true;
                    return edge;
                }
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

    /// <summary>La fenêtre se redimensionne par son cadre : ni agrandie, ni en plein écran, ni de taille fixe.</summary>
    internal bool ResizesByItsFrame =>
        WindowState == WindowState.Normal && !_shell.IsFullScreen && ResizeMode is not (ResizeMode.NoResize or ResizeMode.CanMinimize);

    /// <summary>
    /// Ce qu'un contenu qui a sa propre fenêtre (le moteur web) doit laisser au cadre d'une fenêtre de taille <paramref name="window"/>,
    /// d'après sa place <paramref name="bounds"/> : posé sur un bord, il prendrait la souris au cadre, et la fenêtre ne se
    /// redimensionnerait plus par ce bord.
    /// </summary>
    internal static Thickness FrameInset(Rect bounds, Size window) => new(
        Math.Max(0, ResizeBorder - bounds.Left),
        Math.Max(0, ResizeBorder - bounds.Top),
        Math.Max(0, bounds.Right - (window.Width - ResizeBorder)),
        Math.Max(0, bounds.Bottom - (window.Height - ResizeBorder)));

    /// <summary>Le bord ou le coin de la fenêtre sous ce point de l'écran, tant qu'elle se redimensionne ; 0 ailleurs.</summary>
    private int ResizeEdgeAt(nint position)
    {
        if (!ResizesByItsFrame || PresentationSource.FromVisual(this) is null)
        {
            return 0;
        }
        var point = PointFromScreen(new Point(unchecked((short)(position & 0xFFFF)), unchecked((short)((position >> 16) & 0xFFFF))));
        return ResizeEdge(point, new Size(ActualWidth, ActualHeight));
    }

    /// <summary>
    /// Ce que Windows doit comprendre d'un point de la fenêtre de taille <paramref name="window"/> : un de ses quatre bords
    /// (HTLEFT 10, HTRIGHT 11, HTTOP 12, HTBOTTOM 15), un de ses coins (HTTOPLEFT 13, HTTOPRIGHT 14, HTBOTTOMLEFT 16,
    /// HTBOTTOMRIGHT 17), ou 0 quand le point n'est pas sur le cadre.
    /// </summary>
    internal static int ResizeEdge(Point point, Size window)
    {
        if (point.X < 0 || point.Y < 0 || point.X > window.Width || point.Y > window.Height)
        {
            return 0;
        }
        var left = point.X < ResizeBorder;
        var right = point.X >= window.Width - ResizeBorder;
        var top = point.Y < ResizeBorder;
        var bottom = point.Y >= window.Height - ResizeBorder;
        if (!left && !right && !top && !bottom)
        {
            return 0;
        }
        // Sur un bord, près d'un coin : la diagonale.
        var nearLeft = point.X < ResizeCorner;
        var nearRight = point.X >= window.Width - ResizeCorner;
        var nearTop = point.Y < ResizeCorner;
        var nearBottom = point.Y >= window.Height - ResizeCorner;
        return (nearTop, nearBottom, nearLeft, nearRight) switch
        {
            (true, _, true, _) => 13,
            (true, _, _, true) => 14,
            (_, true, true, _) => 16,
            (_, true, _, true) => 17,
            _ => left ? 10 : right ? 11 : top ? 12 : 15,
        };
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

    /// <summary>Le délai avant de refermer le menu montré au survol : le temps d'aller du bouton au menu.</summary>
    private static readonly TimeSpan PeekGrace = TimeSpan.FromMilliseconds(250);

    private System.Windows.Threading.DispatcherTimer? _peekTimer;

    /// <summary>Menu masqué, survoler son bouton le montre par-dessus la page.</summary>
    private void OnMenuButtonEnter(object sender, MouseEventArgs e)
    {
        if (_shell.IsFocusMode && _shell.CanToggleMenu && !_shell.IsWelcome)
        {
            _peekTimer?.Stop();
            OpenPeek();
        }
    }

    /// <summary>La souris quitte le bouton ou le menu : il se referme si elle n'est plus sur aucun des deux.</summary>
    private void OnMenuPeekLeave(object sender, MouseEventArgs e)
    {
        if (!IsMenuPeekOpen)
        {
            return;
        }
        _peekTimer ??= new System.Windows.Threading.DispatcherTimer(PeekGrace, System.Windows.Threading.DispatcherPriority.Background, (_, _) => ClosePeekIfLeft(), Dispatcher);
        _peekTimer.Stop();
        _peekTimer.Start();
    }

    private void ClosePeekIfLeft()
    {
        _peekTimer?.Stop();
        if (!ExitFocusButton.IsMouseOver && !MenuPeekCard.IsMouseOver)
        {
            ClosePeek();
        }
    }

    /// <summary>Un clic sur le bouton rend le menu pour de bon : celui du survol n'a plus lieu d'être.</summary>
    private void OnMenuButtonClick(object sender, RoutedEventArgs e) => ClosePeek();

    private void ClosePeek()
    {
        _peekTimer?.Stop();
        if (_peekWindow is { IsVisible: true } peek)
        {
            peek.Hide();
        }
    }

    /// <summary>La largeur du menu montré au survol, celle de la barre latérale.</summary>
    private const double PeekWidth = 248;

    /// <summary>L'épaisseur du contour que Windows dessine autour d'une fenêtre, en pixels de l'écran.</summary>
    private const int WindowOutline = 1;

    private Window? _peekWindow;

    /// <summary>Le menu montré au survol est à l'écran.</summary>
    internal bool IsMenuPeekOpen => _peekWindow is { IsVisible: true };

    /// <summary>
    /// Pose le menu dans sa fenêtre, contre le bord gauche de la page et sur toute sa hauteur. La fenêtre ne prend pas le
    /// clavier en s'ouvrant : survoler un bouton ne doit pas interrompre une frappe.
    /// </summary>
    private void OpenPeek()
    {
        if (_peekWindow is null)
        {
            MenuPeek.Child = null;
            _peekWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                // Une fenêtre sans fond ni cadre : Windows n'en arrondit pas les angles et ne lui donne pas d'ombre, la carte seule se voit.
                AllowsTransparency = true,
                Background = null,
                Owner = this,
                Content = MenuPeekCard,
                DataContext = _shell,
                Title = ShellText.MainNavigation,
            };
            _peekWindow.Resources.MergedDictionaries.Add(Resources);
        }
        // En pixels de l'écran : le coin haut gauche de la page et son bas, ramenés dans ce qui se voit de la fenêtre. Windows
        // garde autour d'elle une bordure invisible pour la redimensionner, où le menu dépasserait de quelques pixels.
        var top = Workspace.PointToScreen(default);
        var bottom = Workspace.PointToScreen(new Point(0, Workspace.ActualHeight));
        var visible = _shell.Appearance.VisibleBounds(new WindowInteropHelper(this).Handle);
        if (!visible.IsEmpty)
        {
            // Un pixel en retrait : Windows dessine le contour de la fenêtre sur son dernier pixel, que le menu ne doit pas couvrir.
            top.X = Math.Max(top.X, visible.Left + WindowOutline);
            bottom.Y = Math.Min(bottom.Y, visible.Bottom - WindowOutline);
        }
        if (PresentationSource.FromVisual(this)?.CompositionTarget is { } target)
        {
            top = target.TransformFromDevice.Transform(top);
            bottom = target.TransformFromDevice.Transform(bottom);
        }
        _peekWindow.Left = top.X;
        _peekWindow.Top = top.Y;
        _peekWindow.Width = PeekWidth * _shell.Zoom;
        _peekWindow.Height = Math.Max(bottom.Y - top.Y, 1);
        _peekWindow.Show();
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.Current))
        {
            // Une vue choisie dans le menu montré au survol le referme.
            ClosePeek();
        }
        if (e.PropertyName is nameof(ShellViewModel.IsFocusMode) or nameof(ShellViewModel.IsFullScreen) or nameof(ShellViewModel.Layout))
        {
            ClosePeek();
        }
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
        if (BrandMark.FrameFor(AppIcon.Width * VisualTreeHelper.GetDpi(this).DpiScaleX) is { } frame)
        {
            AppIcon.Source = frame;
        }
    }

    private void OnPaletteClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && ItemsControl.ContainerFromElement(PaletteResults, e.OriginalSource as DependencyObject) is ListBoxItem)
        {
            _shell.RunSelected();
        }
    }

    /// <summary>La réponse à la capture rapide part vers la coquille.</summary>
    private void OnCaptureClosed(object sender, DialogClosedEventArgs e) => _ = _shell.AnswerCaptureCommand.ExecuteAsync(e.Choice == DialogChoice.Primary);

    private void OnUpdateClosed(object sender, DialogClosedEventArgs e) => _ = _shell.Updates?.AnswerAsync(e.Choice == DialogChoice.Primary);

    /// <summary>La note se tape dès que la fenêtre de capture paraît.</summary>
    private void OnCaptureBoxShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () => CaptureBox.Focus());
        }
    }
}
