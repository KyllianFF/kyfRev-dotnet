using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KyfRev.Application.Model;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Le contenu de la fenêtre « Imprimer » : ses réglages, et l'aperçu. La fiche est mise en page une fois, en une longue
/// bande, par la même vue que la lecture, avec la feuille choisie ici ; l'aperçu en montre une feuille, et l'impression
/// les découpe toutes dans cette même bande : ce qui sort est ce qui est montré. Le thème clair s'y applique toujours, et
/// le papier est blanc : on n'imprime pas un fond. Une image seule (un schéma) se pose sur une feuille, sous son titre. En noir
/// et blanc, l'aperçu et les pages sont en nuances de gris : ce qui sort est encore ce qui est montré.
/// </summary>
internal sealed partial class PrintView : UserControl
{
    /// <summary>La taille des numéros de page.</summary>
    private const double NumberSize = 11;

    /// <summary>La taille du titre d'une image imprimée seule.</summary>
    private const double PictureTitleSize = 21;

    /// <summary>La finesse d'une page imprimée en noir et blanc, en points par pouce.</summary>
    private const double GrayPrintDpi = 300;

    /// <summary>La finesse de l'aperçu en noir et blanc.</summary>
    private const double GrayPreviewDpi = 144;

    private readonly SheetDocumentViewer _viewer = new() { ShowsWhole = true, DrawsPaper = false, IsHitTestVisible = false, Focusable = false, IsTabStop = false };
    private readonly Grid _stage = new() { IsHitTestVisible = false };
    private PrintViewModel? _model;
    private (BitmapSource Source, BitmapSource Gray)? _grayPicture;

    /// <summary>Le papier, blanc, et l'encre du noir et blanc : des jetons du design system, pas des couleurs écrites ici.</summary>
    private readonly SolidColorBrush _paper = Frozen(Palette.PrintPaper);
    private readonly SolidColorBrush _ink = Frozen(Palette.PrintInk);

    public PrintView()
    {
        InitializeComponent();
        // Le texte s'imprime comme en thème clair, quel que soit celui de l'application, et sur du papier blanc.
        var paper = _paper;
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
        SizeChanged += (_, e) => Arrange(e.NewSize.Width);
    }

    /// <summary>En dessous, les réglages et l'aperçu ne tiennent plus côte à côte.</summary>
    internal const double StackedBelow = 640;

    /// <summary>Les réglages et l'aperçu sont l'un sous l'autre.</summary>
    internal bool IsStacked { get; private set; }

    /// <summary>
    /// Dispose la fenêtre selon sa largeur : côte à côte, chacun à sa hauteur ; ou, trop étroite, les réglages puis l'aperçu,
    /// l'ensemble défilant d'un bloc, pour que ni l'un ni l'autre ne sorte du cadre.
    /// </summary>
    private void Arrange(double width)
    {
        var stacked = width is > 0 and < StackedBelow;
        if (stacked == IsStacked)
        {
            return;
        }
        IsStacked = stacked;
        SettingsColumn.Width = stacked ? new GridLength(1, GridUnitType.Star) : new GridLength(340);
        PreviewColumn.Width = stacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        PreviewColumn.MinWidth = stacked ? 0 : 300;
        SettingsRow.Height = stacked ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        PreviewRow.Height = stacked ? GridLength.Auto : new GridLength(0);
        Grid.SetColumn(PreviewPane, stacked ? 0 : 1);
        Grid.SetRow(PreviewPane, stacked ? 1 : 0);
        PreviewPane.Margin = stacked ? new Thickness(0, 16, 0, 0) : default;
        SettingsPane.Margin = stacked ? default : new Thickness(0, 0, 16, 0);
        SettingsPane.VerticalScrollBarVisibility = stacked ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        Whole.VerticalScrollBarVisibility = stacked ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
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
            ShowPage();
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
                if (model.Picture is not null)
                {
                    ShowPage();
                }
                break;
            case nameof(PrintViewModel.CurrentPage) or nameof(PrintViewModel.ShowsNumbers) or nameof(PrintViewModel.IsCurrentPrinted)
                or nameof(PrintViewModel.Picture) or nameof(PrintViewModel.IsColor):
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
        if (model.Picture is { } picture)
        {
            ShowPicture(model, picture);
            return;
        }
        PageCanvas.Visibility = Visibility.Visible;
        var rects = _viewer.PageRects();
        if (model.TotalPages != rects.Count)
        {
            model.TotalPages = rects.Count;
        }
        NotPrintedText.Visibility = model.IsCurrentPrinted || model.IsPreparing ? Visibility.Hidden : Visibility.Visible;
        if (rects.Count == 0)
        {
            PageNumber.Text = string.Empty;
            HidePageImage();
            return;
        }
        var index = Math.Clamp(model.CurrentPage, 1, rects.Count) - 1;
        var (sheet, textOnly) = rects[index];
        Canvas.SetLeft(_stage, -sheet.X);
        Canvas.SetTop(_stage, -sheet.Y);
        Paper.Opacity = model.IsCurrentPrinted ? 1 : 0.45;
        PageNumber.Text = model.ShowsNumbers ? Number(index + 1, rects.Count) : string.Empty;
        PageNumber.Margin = new Thickness(0, 0, 0, NumberBottom(model.Layout));
        // En noir et blanc, la feuille montrée est celle qui sortira : grise, posée sur la feuille en couleur.
        if (model.IsGrayscale && !model.IsPreparing)
        {
            var layout = model.Layout;
            PageImage.Source = Grayed(SheetVisual(sheet, textOnly, layout, null, rects.Count), new Size(layout.WidthPixels, layout.HeightPixels), GrayPreviewDpi);
            PageImage.Visibility = Visibility.Visible;
        }
        else
        {
            HidePageImage();
        }
    }

    private void HidePageImage()
    {
        PageImage.Source = null;
        PageImage.Visibility = Visibility.Collapsed;
    }

    /// <summary>L'image seule : une feuille, celle qui sortira, sans numéro.</summary>
    private void ShowPicture(PrintViewModel model, BitmapSource picture)
    {
        if (model.TotalPages != model.Pictures.Count)
        {
            model.TotalPages = model.Pictures.Count;
        }
        PageCanvas.Visibility = Visibility.Hidden;
        NotPrintedText.Visibility = model.IsCurrentPrinted ? Visibility.Hidden : Visibility.Visible;
        Paper.Opacity = model.IsCurrentPrinted ? 1 : 0.45;
        PageNumber.Text = string.Empty;
        var page = new DrawingImage(PicturePage(model, model.PictureTitle, picture));
        page.Freeze();
        PageImage.Source = page;
        PageImage.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// La feuille d'une image seule : son titre en haut, dans les marges, puis l'image entière, centrée dans la place qui reste.
    /// Le titre reste du texte ; en noir et blanc, il est noir et l'image en nuances de gris.
    /// </summary>
    private DrawingGroup PicturePage(PrintViewModel model, string heading, BitmapSource picture)
    {
        var layout = model.Layout;
        var size = new Size(layout.WidthPixels, layout.HeightPixels);
        var margin = layout.MarginPixels;
        var width = Math.Max(1, size.Width - (2 * margin));
        var ink = model.IsColor && _stage.TryFindResource("Kyf.Brush.Text") is Brush text ? text : _ink;
        var typeface = new Typeface(TextElement.GetFontFamily(_viewer), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var title = new FormattedText(heading, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, PictureTitleSize, ink, 1)
        {
            MaxTextWidth = width,
            MaxLineCount = 2,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        var top = margin + title.Height + 12;
        var area = new Rect(margin, top, width, Math.Max(1, size.Height - top - margin));
        var scale = Math.Min(area.Width / Math.Max(1, picture.PixelWidth), area.Height / Math.Max(1, picture.PixelHeight));
        var drawn = new Size(picture.PixelWidth * scale, picture.PixelHeight * scale);
        var page = new DrawingGroup();
        using (var context = page.Open())
        {
            context.DrawRectangle(_paper, null, new Rect(size));
            context.DrawText(title, new Point(margin, margin));
            context.DrawImage(model.IsColor ? picture : GrayOf(picture), new Rect(area.X + ((area.Width - drawn.Width) / 2), area.Y, drawn.Width, drawn.Height));
        }
        page.Freeze();
        return page;
    }

    private static SolidColorBrush Frozen(TokenColor color)
    {
        var brush = new SolidColorBrush(color.ToColor());
        brush.Freeze();
        return brush;
    }

    /// <summary>L'image en nuances de gris, faite une fois par image.</summary>
    private BitmapSource GrayOf(BitmapSource picture)
    {
        if (_grayPicture is not { } known || !ReferenceEquals(known.Source, picture))
        {
            var gray = new FormatConvertedBitmap(picture, PixelFormats.Gray8, null, 0);
            gray.Freeze();
            _grayPicture = (picture, gray);
        }
        return _grayPicture.Value.Gray;
    }

    /// <summary>Une page rendue en points, puis en nuances de gris, sur fond blanc.</summary>
    private static FormatConvertedBitmap Grayed(Visual page, Size size, double dpi)
    {
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(size.Width * dpi / 96)), Math.Max(1, (int)Math.Ceiling(size.Height * dpi / 96)), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(page);
        var gray = new FormatConvertedBitmap(bitmap, PixelFormats.Gray8, null, 0);
        gray.Freeze();
        return gray;
    }

    /// <summary>Une page faite d'une image, à la taille de la feuille.</summary>
    private static DrawingVisual ImagePage(ImageSource image, Size size)
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(image, new Rect(size));
        }
        return visual;
    }

    private static string Number(int page, int total) => string.Create(CultureInfo.InvariantCulture, $"{page}/{total}");

    /// <summary>Le numéro se pose au milieu de la marge du bas.</summary>
    private static double NumberBottom(PageLayout layout) => Math.Max(4, (layout.MarginPixels - (NumberSize * 1.3)) / 2);

    /// <summary>
    /// Les pages à envoyer à l'imprimante : les feuilles demandées, découpées dans la bande mise en page ; ou la feuille de l'image
    /// seule. Elles se dessinent l'une après l'autre, quand l'imprimante les demande : en noir et blanc, chacune devient une image
    /// grise fine, et une seule à la fois est en mémoire.
    /// </summary>
    internal DocumentPaginator? Paginate(IReadOnlyList<int> pages, bool numbers)
    {
        if (_model is not { } model)
        {
            return null;
        }
        var layout = model.Layout;
        var size = new Size(layout.WidthPixels, layout.HeightPixels);
        if (!model.IsSheet)
        {
            // Une image par feuille, sous son titre : celles des feuilles demandées, dans l'ordre, dessinées quand on les demande.
            var pictures = model.Pictures;
            var wanted = pages.Where(page => page >= 1 && page <= pictures.Count).ToList();
            if (wanted.Count == 0)
            {
                return null;
            }
            return new SheetPaginator(wanted.Count, size, index =>
            {
                var picture = pictures[wanted[index] - 1];
                var visual = new DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    context.DrawDrawing(PicturePage(model, picture.Title, picture.Image));
                }
                return visual;
            });
        }
        Settle();
        var rects = _viewer.PageRects();
        var chosen = pages.Where(page => page >= 1 && page <= rects.Count).ToList();
        if (chosen.Count == 0)
        {
            return null;
        }
        var gray = model.IsGrayscale;
        return new SheetPaginator(chosen.Count, size, index =>
        {
            var page = chosen[index];
            var (sheet, textOnly) = rects[page - 1];
            var visual = SheetVisual(sheet, textOnly, layout, numbers ? page : null, rects.Count);
            return gray ? ImagePage(Grayed(visual, size, GrayPrintDpi), size) : visual;
        });
    }

    /// <summary>Une feuille de la fiche, découpée dans la bande mise en page, sur du blanc, avec son numéro si on le veut.</summary>
    private DrawingVisual SheetVisual(Rect sheet, bool textOnly, PageLayout layout, int? number, int total)
    {
        var size = new Size(layout.WidthPixels, layout.HeightPixels);
        var ink = _stage.TryFindResource("Kyf.Brush.Text") as Brush;
        var typeface = new Typeface(TextElement.GetFontFamily(_viewer), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(_paper, null, new Rect(size));
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
            if (number is { } page && ink is not null)
            {
                var text = new FormattedText(Number(page, total), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, NumberSize, ink, 1);
                drawing.DrawText(text, new Point((size.Width - text.Width) / 2, size.Height - NumberBottom(layout) - text.Height));
            }
        }
        return visual;
    }

    /// <summary>Des pages à la taille de la feuille, dessinées quand on les demande.</summary>
    private sealed class SheetPaginator(int count, Size size, Func<int, Visual> page) : DocumentPaginator
    {
        public override bool IsPageCountValid => true;

        public override int PageCount => count;

        public override Size PageSize { get; set; } = size;

        public override IDocumentPaginatorSource? Source => null;

        public override DocumentPage GetPage(int pageNumber) =>
            new(page(pageNumber), PageSize, new Rect(PageSize), new Rect(PageSize));
    }
}
