using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Win32;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// Le corps de l'écriture : le texte qui se modifie à sa place ou, sur demande, le code et son rendu. Quitter la
/// vue, ou fermer la fenêtre, enregistre ce qui ne l'est pas encore.
/// </summary>
internal sealed partial class SheetEditorView : UserControl
{
    private SheetEditorViewModel? _page;
    private Window? _window;
    private ShellViewModel? _shell;
    private bool _pageCovered;

    public SheetEditorView()
    {
        InitializeComponent();
        SetResourceReference(PaperProperty, Reading.SheetPaper.ResourceKey);
        Source.KeyFilter = (key, modifiers) => _page?.TakeKey(key, modifiers) == true;
        Source.ImagePasted += content => _ = _page?.InsertImageAsync("image.png", content);
        Source.ImageFilesDropped += (paths, position) => _ = _page?.InsertImageFilesAsync(paths, position);
        Source.TextArea.LostKeyboardFocus += (_, _) => _ = _page?.SaveAsync();
        Source.ClipboardBusy += (_, _) => _page?.Report(EditingText.ClipboardBusy);
        SizeChanged += (_, e) =>
        {
            if (_page is { } page)
            {
                page.EditorWidth = e.NewSize.Width;
            }
        };
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) =>
        {
            _window = Window.GetWindow(this);
            _window?.Closing += OnWindowClosing;
            _shell = _window?.DataContext as ShellViewModel;
            _shell?.PropertyChanged += OnShellChanged;
            if (_page is { IsVisualShown: true } page && Visual is null)
            {
                page.Attach(Source, () => CreateVisual(page));
            }
            UpdateCover();
            UpdateZoom();
        };
        Unloaded += (_, _) =>
        {
            _window?.Closing -= OnWindowClosing;
            _window = null;
            _shell?.PropertyChanged -= OnShellChanged;
            _shell = null;
            _ = LeaveAsync();
        };
    }

    /// <summary>Demande les images à insérer ; à défaut, le dialogue de Windows.</summary>
    internal Func<IReadOnlyList<string>>? PickImages { get; set; }

    /// <summary>Un dialogue ou un volet de la page recouvre le texte.</summary>
    internal bool PageCovered
    {
        get => _pageCovered;
        set
        {
            _pageCovered = value;
            UpdateCover();
        }
    }

    /// <summary>Un clic sur le texte recouvert par un volet.</summary>
    internal Action? CoveredTextClicked { get; set; }

    /// <summary>Le texte mis en forme, s'il a été ouvert.</summary>
    internal VisualEditor? Visual => VisualHost.Content as VisualEditor;

    /// <summary>Demande des images et les insère.</summary>
    internal void ChooseImages()
    {
        var paths = (PickImages ?? AskImages)();
        if (paths.Count > 0)
        {
            _ = _page?.InsertImageFilesAsync(paths);
        }
    }

    private static List<string> AskImages()
    {
        var dialog = new OpenFileDialog { Title = EditingText.ChooseImages, Filter = EditingText.ImageFilter, Multiselect = true };
        return dialog.ShowDialog() == true ? [.. dialog.FileNames] : [];
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_page is { } previous)
        {
            previous.PropertyChanged -= OnPageChanged;
            previous.ImageDialogAsked -= OnImageDialogAsked;
            _ = previous.SaveAsync();
        }
        _page = e.NewValue as SheetEditorViewModel;
        Visual?.Dispose();
        VisualHost.Content = null;
        if (_page is { } page)
        {
            page.EditorWidth = ActualWidth > 0 ? ActualWidth : double.PositiveInfinity;
            page.PropertyChanged += OnPageChanged;
            page.ImageDialogAsked += OnImageDialogAsked;
            Source.Spelling = page.Spelling;
            page.Attach(Source, () => CreateVisual(page));
            _ = page.RefreshPreviewAsync();
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => page.Surface?.Focus());
        }
    }

    /// <summary>La vue quitte la fenêtre : la dernière frappe s'enregistre, puis le moteur se ferme.</summary>
    private async Task LeaveAsync()
    {
        if (_page is { } page)
        {
            await page.SaveAsync().ConfigureAwait(true);
        }
        if (!IsLoaded)
        {
            Visual?.Dispose();
            VisualHost.Content = null;
        }
    }

    /// <summary>
    /// « Masquer des zones… » sur une image, en écriture : la page ouvre la fenêtre avec l'image lue dans le coffre, et le
    /// titre qu'elle rend s'écrit dans le texte mis en forme, qui s'enregistre comme toute autre frappe.
    /// </summary>
    internal Action<System.Windows.Media.ImageSource?, ImageMasksQuestion, Action<string?>>? MaskImage { get; set; }

    /// <summary>Ctrl+molette dans le texte mis en forme : la page change le zoom de la fiche d'un cran.</summary>
    internal Action<int>? ZoomAsked { get; set; }

    /// <summary>
    /// La largeur que le texte mis en forme d'une feuille sans fin ne dépasse pas : la vue prend toute la place, sa barre de
    /// défilement au bord de la fenêtre, et le texte reste une colonne centrée. Infinie par défaut.
    /// </summary>
    public static readonly DependencyProperty TextMaxWidthProperty = DependencyProperty.Register(
        nameof(TextMaxWidth), typeof(double), typeof(SheetEditorView), new PropertyMetadata(double.PositiveInfinity, (view, _) => ((SheetEditorView)view).UpdateZoom()));

    /// <inheritdoc cref="TextMaxWidthProperty"/>
    public double TextMaxWidth
    {
        get => (double)GetValue(TextMaxWidthProperty);
        set => SetValue(TextMaxWidthProperty, value);
    }

    /// <summary>Le zoom de la fiche (1 : taille normale) : le texte mis en forme et le code Markdown le suivent, en plus de la taille de l'interface.</summary>
    public static readonly DependencyProperty SheetZoomProperty = DependencyProperty.Register(
        nameof(SheetZoom), typeof(double), typeof(SheetEditorView), new PropertyMetadata(1d, (view, _) => ((SheetEditorView)view).UpdateZoom()));

    /// <summary>Le zoom de la fiche.</summary>
    public double SheetZoom
    {
        get => (double)GetValue(SheetZoomProperty);
        set => SetValue(SheetZoomProperty, value);
    }

    /// <summary>La mise en page de la fiche : le texte mis en forme prend sa feuille, et dit combien de pages il fait.</summary>
    public static readonly DependencyProperty PageSetupProperty = DependencyProperty.Register(
        nameof(PageSetup), typeof(Pages.PageSetupViewModel), typeof(SheetEditorView), new PropertyMetadata(null, (view, e) => ((SheetEditorView)view).OnPageSetupChanged(e.OldValue as Pages.PageSetupViewModel, e.NewValue as Pages.PageSetupViewModel)));

    /// <inheritdoc cref="PageSetupProperty"/>
    public Pages.PageSetupViewModel? PageSetup
    {
        get => (Pages.PageSetupViewModel?)GetValue(PageSetupProperty);
        set => SetValue(PageSetupProperty, value);
    }

    /// <summary>Le nombre de pages du texte mis en forme ; zéro sans feuille, ou tant qu'il n'est pas connu.</summary>
    internal int VisualPages { get; private set; }

    /// <summary>Le nombre de pages du texte mis en forme a changé.</summary>
    internal event EventHandler? VisualPagesChanged;

    private void OnPageSetupChanged(Pages.PageSetupViewModel? old, Pages.PageSetupViewModel? setup)
    {
        old?.PropertyChanged -= OnPageSetupPropertyChanged;
        setup?.PropertyChanged += OnPageSetupPropertyChanged;
        UpdatePage();
    }

    private void OnPageSetupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Pages.PageSetupViewModel.Layout))
        {
            UpdatePage();
        }
    }

    /// <summary>Ce que les préférences disent du papier : trouvé dans les ressources, suivi à chaque changement.</summary>
    public static readonly DependencyProperty PaperProperty = DependencyProperty.Register(
        nameof(Paper), typeof(Reading.SheetPaper), typeof(SheetEditorView), new PropertyMetadata(null, (view, _) => ((SheetEditorView)view).UpdatePage()));

    /// <inheritdoc cref="PaperProperty"/>
    public Reading.SheetPaper? Paper
    {
        get => (Reading.SheetPaper?)GetValue(PaperProperty);
        set => SetValue(PaperProperty, value);
    }

    private void UpdatePage()
    {
        var layout = PageSetup?.Layout ?? KyfRev.Application.Model.PageLayout.Default;
        Visual?.SetPage(layout);
        Visual?.SetPaper(Paper?.StyleOf(layout.Format) is { } style ? KyfRev.Application.Model.PaperStyles.Name(style) : null);
        PageSetup?.NotifyPaper();
    }

    /// <summary>L'onglet Image demande la fenêtre des masques ou celle du rognage, sur l'image choisie.</summary>
    private void OnImageDialogAsked(object? sender, ImageMasksQuestion question)
    {
        if (Visual is { } visual && ReferenceEquals(sender, _page) && _page is { } page)
        {
            _ = AskImageMasksAsync(visual, page, question);
        }
    }

    private async Task AskImageMasksAsync(VisualEditor visual, SheetEditorViewModel page, ImageMasksQuestion question)
    {
        if (MaskImage is not { } ask)
        {
            return;
        }
        var picture = await Task.Run(() => Framed(PictureOf(page.ReadResource(question.Path)), question)).ConfigureAwait(true);
        // Le texte mis en forme a pu être quitté pendant la lecture de l'image : la fenêtre ne s'ouvre pas sur un éditeur parti.
        if (!ReferenceEquals(Visual, visual) || !ReferenceEquals(_page, page))
        {
            return;
        }
        ask(picture, question, title =>
        {
            if (ReferenceEquals(Visual, visual))
            {
                visual.SetImageTitle(question.Position, title);
            }
        });
    }

    /// <summary>
    /// L'image telle que la fenêtre doit la montrer : entière pour la rogner ; pour y poser des masques, réduite à son
    /// cadre, puisque les masques se comptent sur ce qui se voit.
    /// </summary>
    private static System.Windows.Media.ImageSource? Framed(System.Windows.Media.Imaging.BitmapImage? picture, ImageMasksQuestion question)
    {
        if (picture is null || question.Crop || KyfRev.Rendering.ImageLooks.FromTitle(question.Title)?.Crop is not { } crop)
        {
            return picture;
        }
        var area = new Int32Rect(
            (int)Math.Round(crop.X * picture.PixelWidth), (int)Math.Round(crop.Y * picture.PixelHeight),
            Math.Max((int)Math.Round(crop.Width * picture.PixelWidth), 1), Math.Max((int)Math.Round(crop.Height * picture.PixelHeight), 1));
        area.Width = Math.Min(area.Width, picture.PixelWidth - area.X);
        area.Height = Math.Min(area.Height, picture.PixelHeight - area.Y);
        if (area.Width < 1 || area.Height < 1)
        {
            return picture;
        }
        var framed = new System.Windows.Media.Imaging.CroppedBitmap(picture, area);
        framed.Freeze();
        return framed;
    }

    /// <summary>L'image lue, prête à être montrée ; rien si ce n'en est pas une que WPF sait lire (un SVG, un fichier abîmé).</summary>
    private static System.Windows.Media.Imaging.BitmapImage? PictureOf(KyfRev.Application.Outcome<byte[]> content)
    {
        if (!content.Succeeded)
        {
            return null;
        }
        try
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new System.IO.MemoryStream(content.Value);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception problem) when (problem is NotSupportedException or System.IO.FileFormatException or ArgumentException or InvalidOperationException or System.IO.IOException or OverflowException)
        {
            return null;
        }
    }

    private VisualEditor CreateVisual(SheetEditorViewModel page)
    {
        var visual = new VisualEditor(page.Folder, page.ReadResource, page.Spelling, page.Environment.WebDataFolder);
        visual.ImageDropped += (name, content, position) => _ = page.InsertImageAsync(name, content, position);
        visual.ImageMasksAsked += (_, question) => _ = AskImageMasksAsync(visual, page, question);
        visual.ZoomAsked += (_, step) => ZoomAsked?.Invoke(step);
        visual.PagesChanged += (_, pages) =>
        {
            VisualPages = pages;
            VisualPagesChanged?.Invoke(this, EventArgs.Empty);
        };
        visual.SetPage(PageSetup?.Layout ?? KyfRev.Application.Model.PageLayout.Default);
        visual.SetPaper(Paper?.StyleOf((PageSetup?.Layout ?? KyfRev.Application.Model.PageLayout.Default).Format) is { } style ? KyfRev.Application.Model.PaperStyles.Name(style) : null);
        visual.Failed += (_, reason) =>
        {
            page.Environment.Report?.Invoke($"le texte mis en forme ne s'ouvre pas : {reason}");
            _ = page.VisualFailedAsync(visual.MissesEngine);
        };
        visual.Restarted += (_, reason) => page.Environment.Report?.Invoke($"moteur web du texte mis en forme : {reason}, relancé");
        visual.SnapshotClicked += (_, _) => CoveredTextClicked?.Invoke();
        VisualHost.Content = visual;
        UpdateZoom();
        return visual;
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.IsPaletteOpen) or nameof(ShellViewModel.IsHelpOpen))
        {
            UpdateCover();
        }
        else if (e.PropertyName == nameof(ShellViewModel.Zoom))
        {
            UpdateZoom();
        }
    }

    private void UpdateCover() =>
        _ = Visual?.SetCoveredAsync(_pageCovered || _shell is { IsPaletteOpen: true } or { IsHelpOpen: true } || _page is { IsLinkOpen: true } or { IsFormulaOpen: true } or { IsGalleryOpen: true } or { IsDiagramEditorOpen: true } or { IsNetworkChoiceOpen: true } or { IsHistoryOpen: true } or { IsComparingConflict: true });

    private void UpdateZoom()
    {
        // Le moteur web n'hérite pas de la taille de l'interface : il reçoit les deux zooms, multipliés.
        Visual?.SetZoom((_shell?.Zoom ?? 1) * SheetZoom);
        Visual?.TextWidth = TextMaxWidth;
        // Le code Markdown, lui, est déjà à la taille de l'interface : seul le zoom de la fiche s'y ajoute.
        Source.LayoutTransform = SheetZoom == 1 ? System.Windows.Media.Transform.Identity : new System.Windows.Media.ScaleTransform(SheetZoom, SheetZoom);
    }

    /// <summary>La liste des fiches s'ouvre sous « [[ ».</summary>
    private void OnPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SheetEditorViewModel.IsLinkOpen) or nameof(SheetEditorViewModel.IsFormulaOpen) or nameof(SheetEditorViewModel.IsGalleryOpen) or nameof(SheetEditorViewModel.IsDiagramEditorOpen) or nameof(SheetEditorViewModel.IsNetworkChoiceOpen) or nameof(SheetEditorViewModel.IsHistoryOpen) or nameof(SheetEditorViewModel.IsComparingConflict))
        {
            UpdateCover();
        }
        if (e.PropertyName is nameof(SheetEditorViewModel.HasProposals) or nameof(SheetEditorViewModel.Proposals) && _page is { HasProposals: true } page)
        {
            ProposalsPopup.PlacementTarget = page.IsMarkdownShown ? Source : VisualHost;
            var point = page.InputPoint;
            ProposalsPopup.HorizontalOffset = point.X;
            ProposalsPopup.VerticalOffset = point.Y + 4;
            ProposalsList.ScrollIntoView(page.ActiveProposal);
        }
    }

    private void OnProposalClicked(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is VaultSheet sheet)
        {
            e.Handled = true;
            _page?.ChooseProposalCommand.Execute(sheet);
        }
    }

    /// <summary>La fenêtre attend la fin de l'enregistrement : la dernière frappe n'est pas perdue.</summary>
    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_page is not { } page)
        {
            return;
        }
        var saved = page.SaveAsync();
        if (!saved.IsCompleted)
        {
            var frame = new DispatcherFrame();
            saved.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
    }
}
