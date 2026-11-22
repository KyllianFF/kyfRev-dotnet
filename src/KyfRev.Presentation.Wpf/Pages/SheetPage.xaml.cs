using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Shell;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue d'une fiche ouverte pour la lire et la réviser : son plan, son texte et ses passages.</summary>
internal sealed partial class SheetPage : UserControl
{
    private const double ListenLabelWidth = 520;
    private const double ShareLabelWidth = 1000;
    private const int WordCardLength = 600;

    private IReadOnlyList<SpokenBlock> _spoken = [];
    private object? _pressedOn;

    public SheetPage()
    {
        InitializeComponent();
        // Les raccourcis marqués prennent les touches choisies dans les paramètres.
        KyfRev.Presentation.Wpf.Shell.ShortcutMap.Follow(this);
        Ribbon.ChooseImages = EditorView.ChooseImages;
        AddPanelsToRibbon();
        Unloaded += (_, _) => (DataContext as SheetPageViewModel)?.ReadAloud?.CloseCommand.Execute(null);
        EditorView.CoveredTextClicked = () => (DataContext as SheetPageViewModel)?.CloseOverlayCommand.Execute(null);
        EditorView.ZoomAsked = step => ZoomSheet(step);
        WriteToggle.MouseEnter += (_, _) => WarmUpEditor();
        WriteToggle.GotKeyboardFocus += (_, _) => WarmUpEditor();
        RibbonRow.SizeChanged += (_, _) =>
        {
            if (RibbonRow.IsVisible && RibbonRow.ActualHeight > 0)
            {
                _ribbonPush = RibbonRow.ActualHeight + RibbonRow.Margin.Bottom;
            }
        };
        // Seulement quand la fiche change de place ou de largeur (un volet à côté d'elle) : pas à chaque mise en page.
        SheetArea.SizeChanged += (_, _) => FollowCenter();
        EditorView.TextReady += (_, _) => AlignReadingText();
        EditorView.TextShown += (_, _) =>
        {
            // L'éditeur prend la place : la fiche lue, alignée sur lui un instant plus tôt, n'a plus à l'être.
            _moves = 0;
            Transitions.Rest(SheetArea);
            _shownEditor = EditorView.DataContext;
            ShowReadingText(DataContext as SheetPageViewModel);
        };
        SheetText.PageCountChanged += (_, _) => ShowPageCount();
        EditorView.VisualPagesChanged += (_, _) => ShowPageCount();
        EditorView.MaskImage = (picture, question, apply) => (DataContext as SheetPageViewModel)?.EditMasksWhileWriting(picture, question.Title, apply, question.Crop);
        SizeChanged += (_, e) =>
        {
            // Quand la place manque, « Écouter » se réduit à son icône : les autres boutons restent lisibles.
            ListenLabel.Visibility = e.NewSize.Width < ListenLabelWidth ? Visibility.Collapsed : Visibility.Visible;

            // « Partager » aussi, plutôt que de faire passer les modes à la ligne.
            ShareLabel.Visibility = e.NewSize.Width < ShareLabelWidth ? Visibility.Collapsed : Visibility.Visible;

            // Et les boutons du rattachement passent sous leur consigne, plutôt que de l'écraser.
            DockPanel.SetDock(RebindButtons, e.NewSize.Width < ListenLabelWidth ? Dock.Bottom : Dock.Right);
            RebindButtons.Margin = e.NewSize.Width < ListenLabelWidth ? new Thickness(0, 8, 0, 0) : default;
            if (DataContext is SheetPageViewModel page)
            {
                page.PageWidth = e.NewSize.Width;
            }
        };
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is SheetPageViewModel previous)
            {
                previous.PropertyChanged -= OnPageChanged;
                previous.Sharing?.PropertyChanged -= OnSharingChanged;
                previous.Masks?.PropertyChanged -= OnMasksChanged;
                if (previous.ReadAloud is { } reader)
                {
                    reader.PropertyChanged -= OnReadAloudChanged;
                    reader.CloseCommand.Execute(null);
                }
            }
            if (e.NewValue is SheetPageViewModel page)
            {
                page.PageWidth = ActualWidth;
                page.PropertyChanged += OnPageChanged;
                // Une autre fiche : son mode est le point de départ, il ne s'anime pas.
                _mode = -1;
                // Ses volets aussi : la fiche ne glisse pas pour les rejoindre.
                _panes = (page.ShowsOutline, page.ShowsPassages || page.ShowsBacklinks);
                _center = double.NaN;
                _paneCame = false;
                _paneLeaves = false;
                ShowMode(page);
                ShowReadingText(page);
                page.Sharing?.PropertyChanged += OnSharingChanged;
                page.Masks?.PropertyChanged += OnMasksChanged;
                page.Print?.PropertyChanged += OnPrintChanged;
                ApplySheetZoom(page);
                page.ReadAloud?.PropertyChanged += OnReadAloudChanged;
                UpdateListenToggle(page);
                ShowWantedPassage(page);
            }
        };
    }

    /// <summary>De combien le ruban descend en paraissant, quand on passe à Écrire.</summary>
    internal const double RibbonDrop = 8;

    /// <summary>Le mode montré : 0 Lire, 1 Réviser, 2 Écrire ; −1 avant le premier.</summary>
    private int _mode = -1;

    /// <summary>En lecture ou en révision, prépare l'éditeur avant qu'on ne le demande.</summary>
    internal void WarmUpEditor()
    {
        if (DataContext is SheetPageViewModel { IsReading: true })
        {
            EditorView.WarmUp();
        }
    }

    /// <summary>L'éditeur dont le texte est à l'écran.</summary>
    private object? _shownEditor;

    /// <summary>
    /// Le texte mis en forme se voit en lecture et en révision. En passant à l'écriture, il reste à l'écran, inerte, tant que
    /// l'éditeur n'a pas montré le sien : l'éditeur a sa propre fenêtre, qui met un instant à s'ouvrir, et il n'y a ainsi
    /// pas de blanc entre les deux.
    /// </summary>
    internal void ShowReadingText(SheetPageViewModel? page)
    {
        var held = page is { IsWriting: true } && !ReferenceEquals(page.Editor, _shownEditor);
        SheetText.Visibility = page is { IsReading: true } || held ? Visibility.Visible : Visibility.Collapsed;
        SheetText.IsHitTestVisible = !held;
    }

    /// <summary>
    /// Lire, Réviser, Écrire. Entre Lire et Réviser, le texte est le même, au même endroit : rien ne bouge, rien ne
    /// s'estompe. Vers Écrire, le ruban descend en fondu, une fois l'éditeur ouvert (l'éditeur a sa propre fenêtre, qu'on
    /// ne peut ni estomper ni déplacer). D'Écrire vers Lire ou Réviser, le texte mis en forme revient en fondu, sans bouger.
    /// </summary>
    /// <returns>Vrai si une transition est prévue.</returns>
    internal bool ShowMode(SheetPageViewModel page)
    {
        var mode = page.IsWriting ? 2 : page.IsRevising ? 1 : 0;
        var before = _mode;
        _mode = mode;
        if (before < 0 || before == mode || (before != 2 && mode != 2))
        {
            return false;
        }
        if (mode != 2)
        {
            // Le ruban est parti d'un coup : ce qui était sous lui remonte en glissant.
            Transitions.Rest(SheetArea);
            Transitions.FollowOnceShown(Body, _ribbonPush);
            return Transitions.EnterOnceShown(SheetText, KyfRev.DesignSystem.DesignKeys.NormalDuration);
        }
        if (!Transitions.EnterOnceShown(Ribbon, KyfRev.DesignSystem.DesignKeys.NormalDuration, rise: -RibbonDrop))
        {
            return false;
        }
        FollowRibbon();
        return true;
    }

    /// <summary>Le milieu de la fiche dans le corps de la page, à la dernière mise en page ; rien tant qu'elle n'en a pas eu.</summary>
    private double _center = double.NaN;

    /// <summary>Les volets en colonne que la page montrait à la dernière annonce : celui de gauche, et la colonne de droite (passages ou liens).</summary>
    private (bool Left, bool Right) _panes;

    /// <summary>Un volet vient de paraître à côté de la fiche : le déplacement que la mise en page lui donne est à accompagner.</summary>
    private bool _paneCame;

    /// <summary>Un volet s'en va : la fiche glisse déjà vers la place qu'il lui rendra.</summary>
    private bool _paneLeaves;

    /// <summary>Ce qui est centré dans la colonne de la fiche lue, et glisse avec elle : pas la barre de défilement.</summary>
    internal IEnumerable<FrameworkElement> Centered() => SheetText.Centered.Append(SheetHead);

    /// <summary>
    /// Un volet en colonne paraît ou s'en va à côté de la fiche lue : le texte glisse en même temps que lui, au lieu de sauter
    /// à sa nouvelle place. Quand le volet paraît, la mise en page a déjà déplacé le texte : il repart de là où il était
    /// (voir <see cref="FollowCenter"/>). Quand il s'en va, il ne rend sa place qu'une fois parti : le texte glisse d'avance
    /// vers celle qu'il aura. Pas en écriture : la fenêtre de l'éditeur ne se déplace pas ainsi.
    /// </summary>
    /// <returns>Vrai si le texte va glisser.</returns>
    internal bool FollowPane(SheetPageViewModel page)
    {
        // Le modèle annonce ses volets à chaque changement de largeur : seul un volet qui a vraiment changé compte.
        var before = _panes;
        _panes = (page.ShowsOutline, page.ShowsPassages || page.ShowsBacklinks);
        if (before == _panes || page.IsWriting || !IsLoaded || !SheetArea.IsVisible)
        {
            return false;
        }
        if (_paneLeaves)
        {
            // Un autre changement pendant qu'un volet s'en allait : la fiche reprend la place que la mise en page lui donne.
            _paneLeaves = false;
            foreach (var element in Centered())
            {
                Transitions.Rest(element);
            }
        }
        if ((_panes.Left && !before.Left) || (_panes.Right && !before.Right))
        {
            _paneCame = true;
            // Sans effet sur la mise en page : il n'y aura rien à accompagner.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => _paneCame = false);
            return true;
        }
        // Un volet parti rend sa colonne : la fiche se recentre d'une demi-colonne de son côté.
        var toX = 0.0;
        if (before.Left && OutlinePane.IsVisible)
        {
            toX -= (OutlinePane.ActualWidth + OutlinePane.Margin.Left + OutlinePane.Margin.Right) / 2;
        }
        if (before.Right && SidePanes.IsVisible)
        {
            toX += (SidePanes.ActualWidth + SidePanes.Margin.Left + SidePanes.Margin.Right) / 2;
        }
        foreach (var element in Centered())
        {
            _paneLeaves |= Transitions.ShiftAsideOnceShown(element, toX);
        }
        return _paneLeaves;
    }

    /// <summary>
    /// La mise en page vient de passer : si le milieu de la fiche a bougé parce qu'un volet a paru, le texte repart de là où
    /// il était ; si c'est parce qu'un volet est parti, il y est déjà (voir <see cref="FollowPane"/>), et retrouve sa place
    /// sans décalage. Une fenêtre qu'on redimensionne ne fait rien glisser.
    /// </summary>
    internal bool FollowCenter()
    {
        if (!SheetArea.IsVisible || SheetArea.ActualWidth <= 0)
        {
            return false;
        }
        var center = SheetArea.TranslatePoint(new Point(SheetArea.ActualWidth / 2, 0), Body).X;
        var before = _center;
        _center = center;
        if (double.IsNaN(before) || Math.Abs(center - before) < 0.5)
        {
            return false;
        }
        if (_paneLeaves)
        {
            _paneLeaves = false;
            foreach (var element in Centered())
            {
                Transitions.Rest(element);
            }
            return false;
        }
        if (!_paneCame)
        {
            return false;
        }
        _paneCame = false;
        var glides = false;
        foreach (var element in Centered())
        {
            glides |= Transitions.FollowAsideOnceShown(element, before - center);
        }
        return glides;
    }

    /// <summary>De combien le ruban pousse ce qui est sous lui : sa hauteur et sa marge.</summary>
    private double _ribbonPush;

    /// <summary>Les mouvements en cours que l'éditeur attend pour se montrer.</summary>
    private int _moves;

    /// <summary>
    /// Le ruban paraît et pousse ce qui est sous lui : la fiche descend avec lui, au lieu de sauter de sa hauteur. L'éditeur,
    /// qui a sa propre fenêtre et ne glisse pas, reste caché le temps de ce trajet.
    /// </summary>
    private void FollowRibbon()
    {
        Hold();
        void Follow(object? sender, EventArgs e)
        {
            if (DataContext is not SheetPageViewModel { IsWriting: true })
            {
                LayoutUpdated -= Follow;
                Settle();
                return;
            }
            if (!RibbonRow.IsVisible || RibbonRow.ActualHeight <= 0)
            {
                return;
            }
            LayoutUpdated -= Follow;
            Transitions.FollowOnceShown(Body, -(RibbonRow.ActualHeight + RibbonRow.Margin.Bottom), done: Settle);
        }
        LayoutUpdated += Follow;
        Follow(this, EventArgs.Empty);
    }

    /// <summary>Un mouvement commence : l'éditeur attend.</summary>
    private void Hold()
    {
        _moves++;
        EditorView.HoldText();
    }

    /// <summary>Un mouvement est fini : au dernier, l'éditeur peut se montrer.</summary>
    private void Settle()
    {
        _moves = Math.Max(0, _moves - 1);
        if (_moves == 0)
        {
            EditorView.ReleaseText();
        }
    }

    /// <summary>Le plus grand décalage qu'un alignement se permet : au-delà, les deux vues ne se ressemblent pas assez.</summary>
    internal const double MaximumAlignment = 240;

    /// <summary>De combien la fiche lue s'est décalée la dernière fois pour s'aligner sur l'éditeur (pour les essais).</summary>
    internal double LastAlignment { get; private set; }

    /// <summary>
    /// Le texte de l'éditeur est chargé, encore caché : la fiche lue glisse jusqu'à poser sa première ligne là où sera celle
    /// de l'éditeur, son titre passant sous le ruban. Le relais se fait alors sans que le texte change de place. Seulement
    /// si la lecture est en haut de la fiche : plus bas, l'éditeur s'ouvre de toute façon sur le début.
    /// </summary>
    /// <returns>Vrai si la fiche lue s'aligne.</returns>
    internal bool AlignReadingText()
    {
        if (DataContext is not SheetPageViewModel { IsWriting: true } || SheetText.Visibility != Visibility.Visible
            || EditorView.Visual is not { TextOffset: { } offset } visual || SheetText.FirstTextTop() is not { } reading
            || PresentationSource.FromVisual(this)?.RootVisual is not System.Windows.Media.Visual root || !IsDescendantOf(root))
        {
            return false;
        }
        // La page web compte en pixels de l'écran ; la fiche, dans la taille de l'interface.
        var scale = TransformToAncestor(root).TransformBounds(new Rect(0, 0, 1, 1)).Height;
        var editorTop = visual.TransformToAncestor(this).Transform(default).Y + (offset / Math.Max(scale, 0.01));
        var readingTop = SheetText.TransformToAncestor(this).Transform(new Point(0, reading)).Y;
        var shift = editorTop - readingTop;
        if (Math.Abs(shift) > MaximumAlignment)
        {
            return false;
        }
        Hold();
        LastAlignment = shift;
        return Transitions.ShiftTo(SheetArea, shift, done: Settle);
    }

    /// <summary>Le texte mis en forme a sa propre fenêtre : tout ce qui le recouvre le lui dit.</summary>
    private void OnPageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SheetPageViewModel.ShowsOutline) or nameof(SheetPageViewModel.ShowsPassages) or nameof(SheetPageViewModel.ShowsBacklinks)
            && sender is SheetPageViewModel beside)
        {
            FollowPane(beside);
        }
        if (e.PropertyName is nameof(SheetPageViewModel.IsWriting) or nameof(SheetPageViewModel.IsRevising) && sender is SheetPageViewModel switched)
        {
            ShowMode(switched);
        }
        if (e.PropertyName is nameof(SheetPageViewModel.IsWriting) or nameof(SheetPageViewModel.IsReading) && sender is SheetPageViewModel shown)
        {
            ShowReadingText(shown);
        }
        if (e.PropertyName is nameof(SheetPageViewModel.Reading) or nameof(SheetPageViewModel.IsReading) or nameof(SheetPageViewModel.IsRevising) && sender is SheetPageViewModel listened)
        {
            listened.ReadAloud?.CloseCommand.Execute(null);
            UpdateListenToggle(listened);
        }
        if (e.PropertyName == nameof(SheetPageViewModel.Reading) && sender is SheetPageViewModel opened)
        {
            ShowWantedPassage(opened);
        }
        if (e.PropertyName is nameof(SheetPageViewModel.Reading) or nameof(SheetPageViewModel.IsRevising) && sender is SheetPageViewModel { IsReading: true, Holes.IsActive: true })
        {
            // Entrée et Espace affichent le trou suivant : ils doivent aller au texte, et non au bouton « Réviser ».
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => SheetText.Focus());
        }
        if (e.PropertyName is nameof(SheetPageViewModel.IsWriting) or nameof(SheetPageViewModel.IsReading))
        {
            ShowPageCount();
        }
        if (e.PropertyName == nameof(SheetPageViewModel.SheetZoom) && sender is SheetPageViewModel zoomed)
        {
            ApplySheetZoom(zoomed);
        }
        if (e.PropertyName is nameof(SheetPageViewModel.IsEditing) or nameof(SheetPageViewModel.IsDeleting)
            or nameof(SheetPageViewModel.ShowsOutlineOverlay) or nameof(SheetPageViewModel.ShowsPassagesOverlay))
        {
            UpdateCovered((SheetPageViewModel)sender!);
        }
    }

    /// <summary>Les fenêtres de partage se posent aussi sur le texte mis en forme.</summary>
    private void OnSharingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SheetSharingViewModel.IsShowing) && DataContext is SheetPageViewModel page)
        {
            UpdateCovered(page);
        }
    }

    /// <summary>Un cran de zoom de la fiche, en plus ou en moins.</summary>
    private void ZoomSheet(int step)
    {
        if (DataContext is SheetPageViewModel page)
        {
            (step > 0 ? page.ZoomSheetInCommand : page.ZoomSheetOutCommand).Execute(null);
        }
    }

    /// <summary>Ctrl+molette sur la fiche, en lecture, en révision ou dans le code Markdown : son zoom, et non le défilement.</summary>
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Delta != 0)
        {
            ZoomSheet(e.Delta);
            e.Handled = true;
        }
    }

    /// <summary>La lecture suit le zoom de la fiche : le texte, ses images et ses schémas grandissent ensemble.</summary>
    private void ApplySheetZoom(SheetPageViewModel page)
    {
        SheetText.LayoutTransform = page.SheetZoom == 1 ? System.Windows.Media.Transform.Identity : new System.Windows.Media.ScaleTransform(page.SheetZoom, page.SheetZoom);
    }

    /// <summary>Le nombre de pages est celui du texte montré : lu, ou mis en forme pour l'écriture.</summary>
    private void ShowPageCount()
    {
        if (DataContext is SheetPageViewModel { PageSetup: { } setup } page)
        {
            setup.Pages = page.IsWriting ? EditorView.VisualPages : SheetText.PageCount;
        }
    }

    /// <summary>« Ajuster à la largeur » : le zoom auquel la feuille occupe toute la largeur libre, au dixième en dessous.</summary>
    private void OnFitPageWidth(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel { PageSetup.IsPaged: true } page && SheetText.Parent is Grid { Parent: FrameworkElement around })
        {
            // Le plan et les passages, quand ils sont ouverts à côté du texte, prennent leur part de la largeur.
            var beside = around is DockPanel dock
                ? dock.Children.OfType<FrameworkElement>().Where(child => child != SheetText.Parent && child.IsVisible && DockPanel.GetDock(child) is Dock.Left or Dock.Right).Sum(child => child.ActualWidth + child.Margin.Left + child.Margin.Right)
                : 0;
            var room = around.ActualWidth - beside;
            if (room > 0)
            {
                page.SheetZoom = Math.Floor(room / page.TextWidth * 10) / 10;
            }
        }
    }

    /// <summary>La fenêtre d'impression, ouverte en écriture, se pose elle aussi sur le texte mis en forme.</summary>
    private void OnPrintChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PrintViewModel.IsOpen) && DataContext is SheetPageViewModel page && ReferenceEquals(sender, page.Print))
        {
            UpdateCovered(page);
        }
    }

    /// <summary>La fenêtre des masques, ouverte en écriture, se pose elle aussi sur le texte mis en forme.</summary>
    private void OnMasksChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageMaskEditorViewModel.IsOpen) && DataContext is SheetPageViewModel page && ReferenceEquals(sender, page.Masks))
        {
            UpdateCovered(page);
        }
    }

    private void UpdateCovered(SheetPageViewModel page) =>
        EditorView.PageCovered = page.IsEditing || page.IsDeleting || page.ShowsOutlineOverlay || page.ShowsPassagesOverlay || page.Sharing is { IsShowing: true } || page.Masks is { IsOpen: true } || page.Print is { IsOpen: true };

    /// <summary>La réponse à la confirmation (révision en avance, J0, archivage) part vers la barre de révision.</summary>
    private void OnConfirmationClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetPageViewModel { Review: { } review })
        {
            review.AnswerCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    /// <summary>Revenir au texte referme le volet ouvert par-dessus.</summary>
    private void OnTextPressed(object sender, MouseButtonEventArgs e)
    {
        TextPressed(e.OriginalSource);
    }

    /// <summary>L'appui sur le texte : retient ce qu'il vise, et referme le volet ouvert par-dessus.</summary>
    internal void TextPressed(object? source)
    {
        _pressedOn = source;
        if (DataContext is SheetPageViewModel page)
        {
            page.CloseOverlayCommand.Execute(null);
        }
    }

    private void OnOutlineEntryClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page && ((FrameworkElement)sender).DataContext is OutlineEntry entry)
        {
            page.CloseOverlayCommand.Execute(null);
            if (page.Editor is { } editor)
            {
                editor.GoToHeading(entry.Title, page.Outline.TakeWhile(other => !ReferenceEquals(other, entry)).Count(other => other.Title == entry.Title));
            }
            else
            {
                SheetText.Show(entry.Heading);
            }
        }
    }

    private void OnPassageEntryClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page && ((FrameworkElement)sender).DataContext is PassageEntry entry)
        {
            ShowPassage(page, entry);
        }
    }

    /// <summary>On écoute ce qu'on lit. Pendant une révision à trous, la voix dirait ce qui est masqué.</summary>
    private void UpdateListenToggle(SheetPageViewModel page) =>
        ListenToggle.Visibility = page is { ReadAloud: not null, IsReading: true, Holes.IsActive: false } ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// À l'écriture, en barre courte, le plan et les passages passent sous le « ⋯ » du ruban, après l'affichage : l'en-tête
    /// tient sur une ligne. Ils suivent la page, quelle qu'elle soit.
    /// </summary>
    private void AddPanelsToRibbon()
    {
        var visible = new BooleanToVisibilityConverter();
        var separator = new Separator();
        separator.SetBinding(VisibilityProperty, new Binding("DataContext.HasPanels") { Source = this, Converter = visible, FallbackValue = Visibility.Collapsed });
        var outline = new MenuItem { Header = ReadingText.Outline, IsCheckable = true };
        outline.SetBinding(MenuItem.IsCheckedProperty, new Binding("DataContext.IsOutlineOn") { Source = this, Mode = BindingMode.TwoWay });
        outline.SetBinding(VisibilityProperty, new Binding("DataContext.HasOutline") { Source = this, Converter = visible, FallbackValue = Visibility.Collapsed });
        var passages = new MenuItem { IsCheckable = true };
        passages.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding("DataContext.PassagesLabel") { Source = this });
        passages.SetBinding(MenuItem.IsCheckedProperty, new Binding("DataContext.IsPassagesOn") { Source = this, Mode = BindingMode.TwoWay });
        passages.SetBinding(VisibilityProperty, new Binding("DataContext.HasPassages") { Source = this, Converter = visible, FallbackValue = Visibility.Collapsed });
        Ribbon.ViewMenu.Items.Add(separator);
        Ribbon.ViewMenu.Items.Add(outline);
        Ribbon.ViewMenu.Items.Add(passages);
        var share = new MenuItem { Header = SharingText.Share };
        share.SetBinding(MenuItem.CommandProperty, new Binding("DataContext.RequestShareCommand") { Source = this });
        share.SetBinding(VisibilityProperty, new Binding("DataContext.CanShare") { Source = this, Converter = visible, FallbackValue = Visibility.Collapsed });
        Ribbon.ViewMenu.Items.Add(share);
    }

    /// <summary>« ⋯ » de l'en-tête étroit : le plan et les passages, dans un menu ouvert sous le bouton.</summary>
    private void OnHeaderMore(object sender, RoutedEventArgs e)
    {
        HeaderMenu.PlacementTarget = HeaderMoreButton;
        HeaderMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        HeaderMenu.IsOpen = true;
    }

    private void OnListenToggle(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SheetPageViewModel { ReadAloud: { } reader })
        {
            return;
        }
        if (reader.IsOpen)
        {
            reader.CloseCommand.Execute(null);
        }
        else
        {
            _spoken = SheetText.SpokenBlocks();
            reader.Open([.. _spoken.Select(block => block.Text)]);
        }
    }

    /// <summary>Le bloc en cours ressort dans le texte.</summary>
    private void OnReadAloudChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReadAloudViewModel.Current) && sender is ReadAloudViewModel reader)
        {
            SheetText.MarkSpoken(reader.Current is { } index && index < _spoken.Count ? _spoken[index].Block : null);
        }
    }

    private void OnRateMenu(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel { ReadAloud: { } reader })
        {
            OpenChoices((Button)sender, ReadAloudViewModel.Rates, reader.Rate, rate => rate.Label, rate => reader.Rate = rate);
        }
    }

    private void OnVoiceMenu(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel { ReadAloud: { } reader })
        {
            OpenChoices((Button)sender, reader.Voices, reader.Voice, ReadingText.ShortVoice, voice => reader.Voice = voice);
        }
    }

    /// <summary>Un menu de choix sous son bouton, celui du moment coché.</summary>
    private static void OpenChoices<T>(Button button, IReadOnlyList<T> choices, T? current, Func<T, string> label, Action<T> choose)
    {
        var menu = new ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var choice in choices)
        {
            var item = new MenuItem { Header = label(choice), IsCheckable = true, IsChecked = EqualityComparer<T>.Default.Equals(choice, current) };
            item.Click += (_, _) => choose(choice);
            menu.Items.Add(item);
        }
        button.ContextMenu = menu;
        menu.IsOpen = choices.Count > 0;
    }

    /// <summary>La fiche s'ouvre sur le passage demandé : elle peut être lue avant ou après que la vue la montre.</summary>
    private void ShowWantedPassage(SheetPageViewModel page)
    {
        if (page is { Reading: not null, RangeToShow: { } range })
        {
            page.RangeToShow = null;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => SheetText.Select(range.Start, range.End));
        }
        if (page is { Reading: not null, PassageToShow: { } wanted })
        {
            page.PassageToShow = null;
            if (page.Passages.FirstOrDefault(entry => entry.Annotation.Id == wanted) is { } passage)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => ShowPassage(page, passage));
            }
        }
    }

    private void ShowPassage(SheetPageViewModel page, PassageEntry entry)
    {
        page.CloseOverlayCommand.Execute(null);
        // Afficher un passage masqué, c'est d'abord le découvrir : demander à le voir vaut pour réponse.
        page.Holes.RevealPassage(entry.Annotation.Id);
        page.ActivateCommand.Execute(entry.Annotation.Id);
        if (entry.Position is { } position && page.IsReading)
        {
            SheetText.Show(position.Start);
        }
    }

    /// <summary>Touche H : surligne la sélection, comme dans la 0.x.</summary>
    private void OnTextKey(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && Keyboard.Modifiers == ModifierKeys.None && SheetText.RevealNextHole())
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.None && DataContext is SheetPageViewModel page && SelectedPosition() is { } position)
        {
            e.Handled = true;
            page.HighlightCommand.Execute(position);
        }
    }

    /// <summary>Un clic sur un passage surligné le fait ressortir dans le panneau.</summary>
    private void OnTextReleased(object sender, MouseButtonEventArgs e) => TextReleased(e.OriginalSource);

    /// <summary>Le relâchement sur le texte.</summary>
    internal void TextReleased(object? source)
    {
        // Le texte prend la souris dès l'appui : au relâchement, seul l'appui dit ce qui est visé. Tirer pour sélectionner n'est
        // pas un clic.
        var target = _pressedOn ?? source;
        _pressedOn = null;
        if (SheetText.Selection is { IsEmpty: false })
        {
            return;
        }
        // Le texte ne change qu'après : il ne rend la souris que si sa mise en page est encore valable à ce moment-là, et le
        // clic suivant serait perdu.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => TextClicked(target));
    }

    private void TextClicked(object? target)
    {
        if (SheetText.RevealHoleAt(target))
        {
            return;
        }
        if (DataContext is not SheetPageViewModel page)
        {
            return;
        }
        if (SheetText.AnnotationsAt(target) is [var id, ..])
        {
            page.ActivateCommand.Execute(id);
        }
        else if (SheetText.WordAt(target) is { } word)
        {
            ShowWord(page, word);
        }
    }

    /// <summary>La définition d'un mot marqué, montrée là où il a été cliqué, avec ce qu'on peut en faire.</summary>
    internal void ShowWord(SheetPageViewModel page, DefinedWordMark word)
    {
        var panel = new StackPanel { MaxWidth = 380 };
        var term = new TextBlock { Text = word.Term, TextWrapping = TextWrapping.Wrap };
        term.SetResourceReference(StyleProperty, "Kyf.Text.BodyStrong");
        panel.Children.Add(term);
        var explanation = word.Explanation.Length > WordCardLength
            ? string.Concat(word.Explanation.AsSpan(0, WordCardLength).TrimEnd(), "\u2026")
            : word.Explanation;
        var body = new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        body.SetResourceReference(StyleProperty, "Kyf.Text.Body");
        panel.Children.Add(body);
        var source = new TextBlock { Text = ReadingText.DefinedIn(word.SheetTitle), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        source.SetResourceReference(StyleProperty, "Kyf.Text.Caption");
        panel.Children.Add(source);
        var actions = new WrapPanel { Margin = new Thickness(-6, 8, 0, 0) };
        var see = WordButton(ReadingText.SeeWordDefinition(word.Term), () => page.OpenWordDefinitionCommand.Execute(word));
        actions.Children.Add(see);
        actions.Children.Add(WordButton(ReadingText.StopMarkingWord(word.Term), () => page.StopMarkingWordCommand.Execute(word)));
        panel.Children.Add(actions);
        var card = new Card { Content = panel, Padding = new Thickness(14, 12, 14, 10) };
        card.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && WordCard is { } open)
            {
                open.IsOpen = false;
                e.Handled = true;
            }
        };
        WordCard = new Popup { Child = card, StaysOpen = false, Placement = PlacementMode.MousePoint, PlacementTarget = SheetText, AllowsTransparency = true };
        WordCard.IsOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => see.Focus());
    }

    /// <summary>La carte d'un mot marqué, ouverte ou refermée ; rien avant le premier clic sur un mot.</summary>
    internal Popup? WordCard { get; private set; }

    private Button WordButton(string text, Action action)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 4, 2) };
        button.SetResourceReference(StyleProperty, "Kyf.Button.Subtle");
        button.Click += (_, _) =>
        {
            WordCard?.IsOpen = false;
            action();
        };
        return button;
    }

    /// <summary>Les entrées du clic droit sur un mot marqué.</summary>
    private List<Control> WordMenuItems(SheetPageViewModel page, DefinedWordMark word) =>
    [
        ActionMenus.Quick(ReadingText.ShowDefinition, Icons.Show, () => ShowWord(page, word)),
        Item(ReadingText.SeeWordDefinition(word.Term), () => page.OpenWordDefinitionCommand.Execute(word)),
        Item(ReadingText.StopMarkingWord(word.Term), () => page.StopMarkingWordCommand.Execute(word)),
    ];

    /// <summary>Le menu du texte : ce qu'on peut faire de la sélection, ou du passage visé.</summary>
    private void OnTextMenu(object sender, ContextMenuEventArgs e)
    {
        Fill(TextMenu, TextMenuItems(e.OriginalSource), e);
    }

    private void OnPassageEntryMenu(object sender, ContextMenuEventArgs e)
    {
        var button = (FrameworkElement)sender;
        if (DataContext is SheetPageViewModel page && button.DataContext is PassageEntry entry)
        {
            Fill(button.ContextMenu!, PassageMenuItems(page, entry), e);
        }
    }

    private static void Fill(ContextMenu menu, IReadOnlyList<Control> items, ContextMenuEventArgs e) => ActionMenus.Fill(menu, items, e);

    private static MenuItem Item(string header, Action action, string? shortcut = null) => ActionMenus.Item(header, action, shortcut);

    /// <summary>La position sélectionnée, si elle peut devenir un passage.</summary>
    private TextPosition? SelectedPosition() =>
        SheetText.CurrentSelection() is { } position && position.End - position.Start <= SheetPageViewModel.PassageMaximum ? position : null;

    internal IReadOnlyList<Control> TextMenuItems(object? source)
    {
        if (DataContext is not SheetPageViewModel page)
        {
            return [];
        }
        if (SelectedPosition() is { } position)
        {
            if (page.IsRebinding)
            {
                return [Item(ReadingText.RebindHere, () => page.RebindAtCommand.Execute(position)), Item(ReadingText.RebindCancel, () => page.CancelRebindingCommand.Execute(null))];
            }
            var guided = page.Guided.IsActive ? [Item(ReadingText.GuidedTakeFor(page.Guided.Extension), () => Take(page, position)), new Separator()] : new List<Control>();
            return
            [
                .. guided,
                ActionMenus.Quick(ReadingText.Copy, Icons.Copy, ApplicationCommands.Copy, SheetText),
                ActionMenus.Quick(ReadingText.Highlight, Icons.Highlighter, () => page.HighlightCommand.Execute(position)),
                ActionMenus.Quick(ReadingText.Annotate, Icons.Comment, () => page.StartAnnotatingCommand.Execute(position)),
                ActionMenus.Quick(ReadingText.Define, Icons.Dictionary, () => page.StartDefiningCommand.Execute(position)),
                .. ExtensionItems(page, position),
                new Separator(),
                .. ListenItems(page, SheetText.Selection?.Start),
                ActionMenus.Item(DesignText.SelectAll, ApplicationCommands.SelectAll, SheetText, "Ctrl+A"),
                new Separator(),
                SheetSubmenu(page),
            ];
        }
        var entry = page.Passages.FirstOrDefault(candidate => SheetText.AnnotationsAt(source).Contains(candidate.Annotation.Id));
        if (entry is not null)
        {
            return [.. PassageMenuItems(page, entry), new Separator(), SheetSubmenu(page)];
        }
        // Une image : on peut y masquer des zones à retrouver en révision.
        var picture = source as Image ?? ((source as Reading.ImageMaskOverlay)?.Parent as Panel)?.Children.OfType<Image>().FirstOrDefault();
        if (picture is { Tag: int imageIndex } && page.CanMaskImages)
        {
            return
            [
                ActionMenus.Quick(ReadingText.Copy, Icons.Copy, () => CopyPicture(picture)),
                ActionMenus.Quick(ImageMaskText.Action, Icons.Edit, () => _ = page.EditMasksAsync(imageIndex, picture.Source)),
                new Separator(),
                SheetSubmenu(page),
            ];
        }
        var listen = ListenItems(page, (source as TextElement)?.ContentStart);
        if (SheetText.WordAt(source) is { } word)
        {
            return [.. WordMenuItems(page, word), new Separator(), .. listen, .. listen.Count > 0 ? [new Separator()] : new List<Control>(), SheetSubmenu(page)];
        }
        // Le fond de la fiche : le menu de la fiche, le même que partout, le mode en cours marqué ; « Écouter d'ici » à la fin.
        return [.. SheetItems(page), .. listen.Count > 0 ? [new Separator(), .. listen] : new List<Control>()];
    }

    /// <summary>Le menu de la fiche entière (rangée 23A) ; rien tant qu'elle n'est pas lue.</summary>
    private static List<Control> SheetItems(SheetPageViewModel page) =>
        page.MenuTarget is { } target ? SheetMenu.Items(target, new OpenedSheetMenu(page), page.Mode) : [];

    /// <summary>« Fiche › » : le menu de la fiche entière, sous ce qu'on vise.</summary>
    private static MenuItem SheetSubmenu(SheetPageViewModel page) => ActionMenus.Submenu(ReadingText.SheetSubmenu, SheetItems(page));

    /// <summary>Copie l'image visée, telle qu'elle s'affiche.</summary>
    private static void CopyPicture(Image picture)
    {
        if (picture.Source is System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            Clipboard.SetImage(bitmap);
        }
    }

    /// <summary>Les entrées que les extensions ajoutent au clic droit d'un texte sélectionné, avec ce texte et sa place.</summary>
    private List<Control> ExtensionItems(SheetPageViewModel page, TextPosition position)
    {
        if (page.Reading is not { } reading || TryFindResource(ExtensionPages.ResourceKey) is not ExtensionPages pages)
        {
            return [];
        }
        var context = new Dictionary<string, string>
        {
            ["id"] = page.SheetId,
            ["titre"] = reading.Title,
            ["dossier"] = reading.Folder,
            ["texte"] = reading.Map.Text[position.Start..position.End],
            ["debut"] = position.Start.ToString(CultureInfo.InvariantCulture),
            ["fin"] = position.End.ToString(CultureInfo.InvariantCulture),
        };
        var entries = pages.MenuEntries(ExtensionMenuZone.Selection, context);
        return entries.Count == 0 ? [] : [new Separator(), .. entries.Select(entry => Item(entry.Label, entry.Run))];
    }

    /// <summary>« Prendre la sélection » : le texte sélectionné part à l'extension qui l'attend.</summary>
    private void OnGuidedTake(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page)
        {
            if (SheetText.CurrentSelection() is { } position && position.End > position.Start)
            {
                Take(page, position);
            }
            else
            {
                page.Guided.NeedsSelection();
            }
        }
    }

    private static void Take(SheetPageViewModel page, TextPosition position)
    {
        if (page.Reading is { } reading)
        {
            page.Guided.Take(page.SheetId, reading.Title, position.Start, position.End, reading.Map.Text[position.Start..position.End]);
        }
    }

    /// <summary>« Écouter d'ici », là où l'écoute a un sens.</summary>
    private List<Control> ListenItems(SheetPageViewModel page, TextPointer? place) =>
        page is { ReadAloud: not null, IsReading: true, Holes.IsActive: false } && place is not null
            ? [Item(ReadingText.ListenFromHere, () => ListenFrom(page.ReadAloud, place))]
            : [];

    /// <summary>Ouvre le lecteur et lit à partir du bloc qui contient cet endroit.</summary>
    internal void ListenFrom(ReadAloudViewModel reader, TextPointer place)
    {
        _spoken = SheetText.SpokenBlocks();
        reader.Open([.. _spoken.Select(block => block.Text)]);
        var index = _spoken.ToList().FindIndex(block => block.Block.ContentEnd.CompareTo(place) >= 0);
        reader.PlayFrom(Math.Max(index, 0));
    }

    internal IReadOnlyList<Control> PassageMenuItems(SheetPageViewModel page, PassageEntry entry)
    {
        var items = new List<Control>();
        if (entry.Position is not null)
        {
            items.Add(ActionMenus.Quick(entry.IsDefinition ? ReadingText.ShowDefinition : ReadingText.ShowPassage, Icons.Show, () => ShowPassage(page, entry)));
        }
        items.Add(ActionMenus.Quick(entry.IsDefinition ? ReadingText.EditDefinition : ReadingText.EditComment, Icons.Comment, () => page.StartEditingCommand.Execute(entry)));
        items.Add(new Separator());
        if (entry.IsCandidate)
        {
            items.Add(Item(ReadingText.ConfirmPosition, () => page.ConfirmPositionCommand.Execute(entry)));
            items.Add(Item(ReadingText.KeepWithoutPosition, () => page.KeepWithoutPositionCommand.Execute(entry)));
        }
        items.Add(Item(entry.State == KyfRev.Application.Model.AnchorState.Anchored ? ReadingText.RebindOther : ReadingText.RebindLost, () => page.StartRebindingCommand.Execute(entry)));
        items.Add(new Separator());
        items.Add(Item(entry.IsDefinition ? ReadingText.DeleteDefinition : ReadingText.DeletePassage, () => page.RequestDeleteCommand.Execute(entry)));
        return items;
    }

    private void OnEditorClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page)
        {
            page.AnswerEditorCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    /// <summary>Le passage en attente prend pour place la sélection ; sans sélection, on le rappelle.</summary>
    private void OnRebindHere(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page)
        {
            RebindAtSelection(page);
        }
    }

    private void RebindAtSelection(SheetPageViewModel page)
    {
        if (SelectedPosition() is { } position)
        {
            page.RebindAtCommand.Execute(position);
        }
        else
        {
            page.AskForRebindingSelection();
        }
    }

    private void OnRebindClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.AnswerRebindingCommand.Execute(e.Choice switch
        {
            DialogChoice.Primary => false,
            DialogChoice.Secondary => true,
            _ => (bool?)null,
        });

    private void OnRebindConfirmCancel(object sender, RoutedEventArgs e) => RebindConfirm.Close(DialogChoice.None);

    private void OnLinkClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.Editor?.AnswerLinkCommand.Execute(e.Choice == DialogChoice.Primary);

    private void OnHistoryClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.Editor?.CloseHistoryCommand.Execute(null);

    private void OnConflictCompareClosed(object sender, DialogClosedEventArgs e)
    {
        if ((DataContext as SheetPageViewModel)?.Editor is { IsComparingConflict: true } editor)
        {
            editor.CompareConflictCommand.Execute(null);
        }
    }

    private void OnNetworkChoiceClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.Editor?.AnswerNetworkChoiceCommand.Execute(e.Choice == DialogChoice.Primary);

    private void OnDiagramEditorClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.Editor?.AnswerDiagramEditorCommand.Execute(e.Choice == DialogChoice.Primary);

    /// <summary>« Imprimer » : le travail part ; s'il est refusé, la fenêtre reste ouverte et dit pourquoi.</summary>
    private void OnPrintClosed(object sender, DialogClosedEventArgs e)
    {
        if ((DataContext as SheetPageViewModel)?.Print is not { } print)
        {
            return;
        }
        if (e.Choice == DialogChoice.Primary && !print.Print())
        {
            PrintDialog.SetCurrentValue(Dialog.IsOpenProperty, true);
            return;
        }
        print.Close();
    }

    private void OnGalleryClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.Editor?.AnswerGalleryCommand.Execute(e.Choice == DialogChoice.Primary);

    private void OnFormulaClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.Editor?.AnswerFormulaCommand.Execute(e.Choice == DialogChoice.Primary);

    private void OnDeleteClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page)
        {
            page.AnswerDeleteCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }
}

/// <summary>Le retrait d'un titre du plan, selon son niveau.</summary>
public sealed class IndentConverter : IValueConverter
{
    private const double Step = 12;

    private IndentConverter()
    {
    }

    /// <summary>L'unique instance.</summary>
    public static IndentConverter Instance { get; } = new();

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new Thickness((int)value * Step, 0, 0, 0);

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

}

/// <summary>Le menu de la fiche ouverte : elle change de mode, s'imprime et se partage elle-même ; le reste par les actions communes.</summary>
internal sealed class OpenedSheetMenu(SheetPageViewModel page) : ISheetMenuHost
{
    public bool CanManage => page.SheetActions is not null;

    public bool CanShare => page.CanShare;

    public void Read(SheetTarget sheet) => page.IsReadOn = true;

    public void Revise(SheetTarget sheet) => page.IsReviseOn = true;

    public void Write(SheetTarget sheet) => page.IsWriteOn = true;

    public void Print(SheetTarget sheet) => page.RequestPrintCommand.Execute(null);

    public void Share(SheetTarget sheet) => page.RequestShareCommand.Execute(null);

    public void SaveAsTemplate(SheetTarget sheet) => page.RequestSaveAsTemplateCommand.Execute(null);

    public void PostponeTomorrow(SheetTarget sheet) => page.SheetActions?.PostponeTomorrow(sheet);

    public void Archive(SheetTarget sheet) => page.SheetActions?.Archive(sheet);

    public void Restart(SheetTarget sheet) => page.SheetActions?.Restart(sheet);

    public void ToggleReminders(SheetTarget sheet) => page.SheetActions?.ToggleReminders(sheet);

    public void Rename(SheetTarget sheet) => page.SheetActions?.Rename(sheet);

    public void Tags(SheetTarget sheet) => page.SheetActions?.Tags(sheet);

    public void Move(SheetTarget sheet) => page.SheetActions?.Move(sheet);

    public void ToggleNote(SheetTarget sheet) => page.SheetActions?.ToggleNote(sheet);

    public void Trash(SheetTarget sheet) => page.SheetActions?.Trash(sheet);

    public IReadOnlyList<MenuContribution> Extensions(SheetTarget sheet) => page.SheetActions?.Extensions(sheet) ?? [];
}
