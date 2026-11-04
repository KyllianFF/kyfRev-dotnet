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
            _ = previous.SaveAsync();
        }
        _page = e.NewValue as SheetEditorViewModel;
        Visual?.Dispose();
        VisualHost.Content = null;
        if (_page is { } page)
        {
            page.EditorWidth = ActualWidth > 0 ? ActualWidth : double.PositiveInfinity;
            page.PropertyChanged += OnPageChanged;
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

    private VisualEditor CreateVisual(SheetEditorViewModel page)
    {
        var visual = new VisualEditor(page.Folder, page.ReadResource, page.Spelling, page.Environment.WebDataFolder);
        visual.ImageDropped += (name, content, position) => _ = page.InsertImageAsync(name, content, position);
        visual.Failed += (_, _) => _ = page.VisualFailedAsync();
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
        if (_shell is { } shell)
        {
            Visual?.SetZoom(shell.Zoom);
        }
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
