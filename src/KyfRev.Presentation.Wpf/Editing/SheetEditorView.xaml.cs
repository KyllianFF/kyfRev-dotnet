using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using Microsoft.Win32;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// La vue de l'écriture : le ruban, le code à gauche et son rendu à droite. Quitter la vue, ou fermer la fenêtre,
/// enregistre ce qui ne l'est pas encore.
/// </summary>
internal sealed partial class SheetEditorView : UserControl
{
    private SheetEditorViewModel? _page;
    private Window? _window;

    public SheetEditorView()
    {
        InitializeComponent();
        Source.KeyFilter = (key, modifiers) => _page?.TakeKey(key, modifiers) == true;
        Source.ImagePasted += content => _ = _page?.InsertImageAsync("image.png", content);
        Source.ImageFilesDropped += (paths, position) => _ = _page?.InsertImageFilesAsync(paths, position);
        Source.TextArea.LostKeyboardFocus += (_, _) => _ = _page?.SaveAsync();
        Ribbon.ChooseImages = OnChooseImages;
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
        };
        Unloaded += (_, _) =>
        {
            _window?.Closing -= OnWindowClosing;
            _window = null;
            _ = _page?.SaveAsync();
        };
    }

    /// <summary>Demande les images à insérer ; à défaut, le dialogue de Windows.</summary>
    internal Func<IReadOnlyList<string>>? PickImages { get; set; }

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
        if (_page is { } page)
        {
            page.EditorWidth = ActualWidth > 0 ? ActualWidth : double.PositiveInfinity;
            page.PropertyChanged += OnPageChanged;
            Source.Spelling = page.Spelling;
            page.Attach(Source);
            _ = page.RefreshPreviewAsync();
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => Source.TextArea.Focus());
        }
    }

    /// <summary>La liste des fiches s'ouvre sous « [[ ».</summary>
    private void OnPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SheetEditorViewModel.HasProposals) or nameof(SheetEditorViewModel.Proposals) && _page?.HasProposals == true)
        {
            var point = Source.CaretPoint;
            ProposalsPopup.HorizontalOffset = point.X;
            ProposalsPopup.VerticalOffset = point.Y + 4;
            ProposalsList.ScrollIntoView(_page?.ActiveProposal);
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

    private void OnLinkClosed(object sender, DialogClosedEventArgs e) =>
        _page?.AnswerLinkCommand.Execute(e.Choice == DialogChoice.Primary);

    private void OnChooseImages()
    {
        var paths = (PickImages ?? AskImages)();
        if (paths.Count > 0)
        {
            _ = _page?.InsertImageFilesAsync(paths);
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
