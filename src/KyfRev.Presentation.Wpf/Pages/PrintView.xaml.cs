using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Le contenu de la fenêtre « Imprimer » : ses réglages, et l'aperçu. La fiche est mise en page une fois, en une longue
/// bande, par la même vue que la lecture, avec la feuille choisie ici ; l'aperçu en montre une feuille, et l'impression
/// les découpe toutes dans cette même bande : ce qui sort est ce qui est montré. Le thème clair s'y applique toujours, et
/// le papier est blanc : on n'imprime pas un fond.
/// </summary>
internal sealed partial class PrintView : UserControl
{
    /// <summary>La taille des numéros de page.</summary>
    private const double NumberSize = 11;

    private readonly SheetDocumentViewer _viewer = new() { ShowsWhole = true, DrawsPaper = false, IsHitTestVisible = false, Focusable = false, IsTabStop = false };
    private readonly Grid _stage = new() { IsHitTestVisible = false };
    private PrintViewModel? _model;

    public PrintView()
    {
        InitializeComponent();
        // Le texte s'imprime comme en thème clair, quel que soit celui de l'application, et sur du papier blanc.
        var paper = new SolidColorBrush(Palette.PrintPaper.ToColor());
        paper.Freeze();
        _stage.Resources.MergedDictionaries.Add(new DesignResources(ThemeKind.Light, reducedMotion: true).Dictionary);
        _stage.Resources["Kyf.Brush.Surface"] = paper;
        _stage.Resources["Kyf.Brush.Background"] = paper;
        Paper.Background = paper;
        PageNumber.SetValue(TextElement.ForegroundProperty, _stage.TryFindResource("Kyf.Brush.Text") as Brush);
        PageNumber.FontSize = NumberSize;
        _stage.Children.Add(_viewer);
        PageCanvas.Children.Add(_stage);
        _viewer.PagesShown += (_, _) => ShowPage();
        DataContextChanged += (_, e) => Follow(e.NewValue as PrintViewModel);
        IsVisibleChanged += (_, _) => ShowPage();
    }

    /// <summary>La vue qui met la fiche en page, pour les essais.</summary>
    internal SheetDocumentViewer Viewer => _viewer;

    private void Follow(PrintViewModel? model)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model.Paginator = null;
        }
        _model = model;
        if (model is not null)
        {
            model.PropertyChanged += OnModelChanged;
            model.Paginator = Paginate;
            ApplyLayout(model);
            _viewer.Reading = model.Reading;
            _ = WaitForPicturesAsync(model);
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PrintViewModel model)
        {
            return;
        }
        switch (e.PropertyName)
        {
            case nameof(PrintViewModel.Reading):
                _viewer.Reading = model.Reading;
                _ = WaitForPicturesAsync(model);
                break;
            case nameof(PrintViewModel.Layout):
                ApplyLayout(model);
                break;
            case nameof(PrintViewModel.CurrentPage) or nameof(PrintViewModel.ShowsNumbers) or nameof(PrintViewModel.IsCurrentPrinted):
                ShowPage();
                break;
        }
    }

    /// <summary>La feuille choisie : la vue prend sa largeur, et ses marges deviennent celles du texte.</summary>
    private void ApplyLayout(PrintViewModel model)
    {
        var layout = model.Layout;
        _viewer.Width = layout.WidthPixels + SystemParameters.VerticalScrollBarWidth;
        _viewer.MinHeight = layout.HeightPixels;
        _viewer.PageLayout = layout;
        PageCanvas.Width = layout.WidthPixels;
        PageCanvas.Height = layout.HeightPixels;
    }

    /// <summary>L'aperçu est prêt quand les images de la fiche sont là : avant, des pages bougeraient encore.</summary>
    private async Task WaitForPicturesAsync(PrintViewModel model)
    {
        var reading = model.Reading;
        if (reading is null)
        {
            return;
        }
        model.IsPreparing = true;
        _viewer.CompleteDocument();
        // Une image qui ne vient pas n'empêche pas d'imprimer le reste : on attend qu'elles aient fini, venues ou non.
        var pictures = _viewer.ImagesLoaded;
        await Task.WhenAny(pictures).ConfigureAwait(true);
        _ = pictures.Exception;
        if (ReferenceEquals(model.Reading, reading))
        {
            Settle();
            model.IsPreparing = false;
            ShowPage();
        }
    }

    /// <summary>Amène la mise en pages à son état stable : chaque mesure confirme la précédente.</summary>
    private void Settle()
    {
        _viewer.CompleteDocument();
        for (var pass = 0; pass < 6; pass++)
        {
            UpdateLayout();
            _viewer.Paginate();
        }
        UpdateLayout();
    }

    /// <summary>Montre la feuille en cours dans l'aperçu.</summary>
    private void ShowPage()
    {
        if (_model is not { } model || !IsVisible)
        {
            return;
        }
        var rects = _viewer.PageRects();
        if (model.TotalPages != rects.Count)
        {
            model.TotalPages = rects.Count;
        }
        NotPrintedText.Visibility = model.IsCurrentPrinted || model.IsPreparing ? Visibility.Hidden : Visibility.Visible;
        if (rects.Count == 0)
        {
            PageNumber.Text = string.Empty;
            return;
        }
        var index = Math.Clamp(model.CurrentPage, 1, rects.Count) - 1;
        var (sheet, _) = rects[index];
        Canvas.SetLeft(_stage, -sheet.X);
        Canvas.SetTop(_stage, -sheet.Y);
        Paper.Opacity = model.IsCurrentPrinted ? 1 : 0.45;
        PageNumber.Text = model.ShowsNumbers ? Number(index + 1, rects.Count) : string.Empty;
        PageNumber.Margin = new Thickness(0, 0, 0, NumberBottom(model.Layout));
    }

    private static string Number(int page, int total) => string.Create(CultureInfo.InvariantCulture, $"{page}/{total}");

    /// <summary>Le numéro se pose au milieu de la marge du bas.</summary>
    private static double NumberBottom(PageLayout layout) => Math.Max(4, (layout.MarginPixels - (NumberSize * 1.3)) / 2);

    /// <summary>Les pages à envoyer à l'imprimante : les feuilles demandées, découpées dans la bande mise en page.</summary>
    internal DocumentPaginator? Paginate(IReadOnlyList<int> pages, bool numbers)
    {
        if (_model is not { } model)
        {
            return null;
        }
        Settle();
        var rects = _viewer.PageRects();
        if (rects.Count == 0)
        {
            return null;
        }
        var layout = model.Layout;
        var size = new Size(layout.WidthPixels, layout.HeightPixels);
        var ink = _stage.TryFindResource("Kyf.Brush.Text") as Brush;
        var typeface = new Typeface(TextElement.GetFontFamily(_viewer), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var visuals = new List<Visual>();
        foreach (var page in pages.Where(page => page >= 1 && page <= rects.Count))
        {
            var (sheet, textOnly) = rects[page - 1];
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                var picture = new VisualBrush(_stage)
                {
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = sheet,
                    ViewportUnits = BrushMappingMode.Absolute,
                    Viewport = new Rect(size),
                    Stretch = Stretch.Fill,
                    TileMode = TileMode.None,
                };
                if (textOnly)
                {
                    // Une page découpée dans un texte plus haut qu'elle : ses marges restent blanches.
                    drawing.PushClip(new RectangleGeometry(new Rect(0, layout.MarginPixels, size.Width, size.Height - (2 * layout.MarginPixels))));
                }
                drawing.DrawRectangle(picture, null, new Rect(size));
                if (textOnly)
                {
                    drawing.Pop();
                }
                if (numbers && ink is not null)
                {
                    var text = new FormattedText(Number(page, rects.Count), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, NumberSize, ink, 1);
                    drawing.DrawText(text, new Point((size.Width - text.Width) / 2, size.Height - NumberBottom(layout) - text.Height));
                }
            }
            visuals.Add(visual);
        }
        return visuals.Count == 0 ? null : new SheetPaginator(visuals, size);
    }

    /// <summary>Des pages déjà dessinées, à la taille de la feuille.</summary>
    private sealed class SheetPaginator(IReadOnlyList<Visual> pages, Size size) : DocumentPaginator
    {
        public override bool IsPageCountValid => true;

        public override int PageCount => pages.Count;

        public override Size PageSize { get; set; } = size;

        public override IDocumentPaginatorSource? Source => null;

        public override DocumentPage GetPage(int pageNumber) =>
            new(pages[pageNumber], PageSize, new Rect(PageSize), new Rect(PageSize));
    }
}
