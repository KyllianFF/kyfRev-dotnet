using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// En lecture, sur une feuille sans fin : le texte garde la colonne choisie, et ce qui est plus large qu'elle (un tableau,
/// une image seule sur sa ligne, un schéma) déborde de chaque côté, centré sur elle, jusqu'à la largeur d'avant ce réglage.
/// </summary>
/// <remarks>
/// Avec la variable d'environnement <c>KYFREV_CAPTURES</c> (un dossier), chaque essai y écrit l'image qu'il a regardée.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class WideBlocksTests
{
    private const double Wide = SheetPageViewModel.TextMaxWidth;

    private const string Text = "Un paragraphe de texte ordinaire, assez long pour passer à la ligne dans la colonne de lecture et montrer où elle commence et où elle finit, quelle que soit la largeur de la fenêtre.";

    private const string WideTable = """
        | Période d'incubation | Mode de transmission | Symptômes principaux | Traitement |
        |---|---|---|---|
        | De deux à quatorze jours | Par gouttelettes | Fièvre, toux, fatigue | Repos, hydratation, suivi |
        | De un à trois jours | Par l'eau, les aliments | Nausées et crampes | Réhydratation orale |
        """;

    private const string FittedTable = """
        <!-- kyfrev tableau largeur=ajustee -->
        | Étape | Jour |
        |---|---|
        | J1 | demain |
        | J3 | dans trois jours |
        """;

    private const string Diagram = "```smartart cycle\nG1\nS\nG2\nM\n```";

    /// <summary>Du texte, un tableau large, un tableau ajusté, une image de 800 pixels et un schéma : en lecture comme en écriture.</summary>
    internal static string Body() => string.Join("\n\n", Text, WideTable, Text, FittedTable, $"![Une frise large](data:image/png;base64,{Convert.ToBase64String(Picture(800, 90))})", Text, Diagram, Text) + "\n";

    [STATestMethod]
    [DataRow(570, false)]
    [DataRow(680, false)]
    [DataRow(680, true)]
    [DataRow(820, false)]
    public void What_is_wider_than_the_text_column_breaks_out_of_it_centred(int column, bool dark)
    {
        using var ui = new UiThread();
        using var shown = new Shown(Body(), column, dark);
        var viewer = shown.Viewer;
        shown.Resize(1600);
        shown.WaitForPictures();
        shown.Capture($"debord-{column}-{(dark ? "sombre" : "clair")}");

        var bar = SystemParameters.VerticalScrollBarWidth;
        var document = Math.Max(column, Wide);
        var side = Math.Floor((1600 - bar - document) / 2);
        var inset = Math.Floor((document - column) / 2);
        Assert.AreEqual(side, viewer.EndlessSide, "le document garde la largeur d'avant, centré dans la vue");
        Assert.AreEqual(inset, viewer.TextInset);
        var centre = side + (document / 2);
        var blocks = viewer.Document!.Blocks.ToList();

        // Le texte : resserré sur la colonne choisie, au même endroit qu'avant dans la fenêtre.
        foreach (var paragraph in blocks.OfType<Paragraph>().Where(paragraph => Start(paragraph).StartsWith("Un paragraphe", StringComparison.Ordinal)))
        {
            Assert.AreEqual((inset, inset), (paragraph.Margin.Left, paragraph.Margin.Right));
            var (left, right) = Shown.Lines(paragraph);
            Assert.AreEqual(side + inset, left, 1, "le texte commence au bord de sa colonne");
            Assert.IsLessThanOrEqualTo(side + inset + column + 6, right, "et ne la dépasse pas, à une espace de fin de ligne près");
            Assert.IsGreaterThan(side + inset + column - 140, right, "il la remplit");
        }

        // Le tableau pleine largeur, plus large que la colonne : ce que ses colonnes demandent, autant de chaque côté.
        var wide = blocks.OfType<Table>().First(table => table.Tag is TableSizing { Fitted: false });
        var natural = ((TableSizing)wide.Tag).Natural.Sum();
        Assert.IsGreaterThan(680, natural, "l'essai porte sur un tableau plus large que la colonne confortable");
        Assert.IsLessThan(Wide, natural, "et qui tenait dans celle d'avant");
        var room = Math.Max(0, inset - Math.Ceiling(Math.Max(0, natural - column) / 2));
        Assert.AreEqual((room, room), (wide.Margin.Left, wide.Margin.Right));
        if (natural > column)
        {
            Assert.IsLessThan(inset, room, "il sort de la colonne de texte");
            for (var index = 0; index < 4; index++)
            {
                Assert.AreEqual(((TableSizing)wide.Tag).Natural[index] / DocumentBuilder.StarUnit, wide.Columns[index].Width.Value, 1e-6, "aucune colonne resserrée");
            }
        }
        var first = wide.RowGroups[0].Rows[0].Cells[0].ContentStart.GetCharacterRect(LogicalDirection.Forward).Left;
        Assert.AreEqual(side + room, first, 16, "le tableau commence où sa marge le dit, à la marge de sa case près");

        // Le tableau ajusté, plus étroit que le texte : aligné sur lui.
        var fitted = blocks.OfType<Table>().First(table => table.Tag is TableSizing { Fitted: true });
        Assert.AreEqual((inset, inset), (fitted.Margin.Left, fitted.Margin.Right));

        // L'image, seule sur sa ligne et large de 800 pixels : entière, centrée sur la colonne.
        var picture = Visuals(viewer).OfType<Image>().Single(image => image.Source is not null);
        Assert.AreEqual(800, picture.ActualWidth, 1, "l'image garde ses pixels");
        // Plus large que la colonne, elle déborde autant de chaque côté ; dans une colonne qui la contient, elle reste au bord du texte.
        var expected = column < 800 ? centre : side + inset + 400;
        Assert.AreEqual(expected, picture.TranslatePoint(new Point(picture.ActualWidth / 2, 0), viewer).X, 1.5);

        // Le schéma : ses 640 pixels, même dans la colonne étroite.
        var drawing = Visuals(viewer).OfType<DiagramView>().Single();
        Assert.AreEqual(640, drawing.ActualWidth, 1);
        Assert.AreEqual(centre, drawing.TranslatePoint(new Point(drawing.ActualWidth / 2, 0), viewer).X, 4, "centré sur la colonne, comme il l'a toujours été");

        // Une fenêtre qui n'a que la place du texte : rien ne déborde, tout suit la vue comme avant.
        shown.Resize(column - 40);
        Assert.AreEqual(0, viewer.TextInset);
        Assert.AreEqual(0, viewer.EndlessSide);
        Assert.IsTrue(blocks.All(block => block.Margin.Left == 0 && block.Margin.Right == 0), "les blocs retrouvent leurs marges");
        Assert.IsLessThan(column - 40, picture.ActualWidth, "l'image se réduit à la vue");
        shown.Capture($"debord-{column}-{(dark ? "sombre" : "clair")}-etroit");

        // Entre les deux : le document prend la vue, le texte sa colonne, le tableau ce qui reste.
        shown.Resize(column + 80);
        var between = Math.Floor((column + 80 - bar - column) / 2);
        Assert.AreEqual(column < Wide ? between : 0, viewer.TextInset);
        Assert.AreEqual(viewer.TextInset, blocks.OfType<Paragraph>().First().Margin.Left);
        Assert.IsLessThanOrEqualTo(viewer.TextInset, wide.Margin.Left);
    }

    [STATestMethod]
    public void On_a_paper_nothing_leaves_the_sheet_that_is_drawn()
    {
        using var ui = new UiThread();
        using var shown = new Shown(Body(), 680, dark: false, paper: PaperStyle.Lines);
        var viewer = shown.Viewer;
        shown.Resize(1600);
        shown.WaitForPictures();

        Assert.AreEqual(0, viewer.TextInset, "le papier a la largeur du texte : rien n'en sort");
        Assert.AreEqual(Math.Floor((1600 - SystemParameters.VerticalScrollBarWidth - 680) / 2), viewer.EndlessSide);
        Assert.IsTrue(viewer.Document!.Blocks.All(block => block.Margin.Left == 0), "aucun bloc n'est resserré ni élargi");
        Assert.IsLessThanOrEqualTo(680, Visuals(viewer).OfType<Image>().Single(image => image.Source is not null).ActualWidth);
        shown.Capture("debord-papier");

        // Le tableau ajusté : la place du texte se compte entre les marges du papier, ses colonnes n'y sont pas resserrées.
        var fitted = viewer.Document.Blocks.OfType<Table>().First(table => table.Tag is TableSizing { Fitted: true });
        var sizing = (TableSizing)fitted.Tag;
        Assert.AreEqual(sizing.Natural[0] / DocumentBuilder.StarUnit, fitted.Columns[0].Width.Value, 1e-6);
        Assert.AreEqual(sizing.Natural[1] / DocumentBuilder.StarUnit, fitted.Columns[1].Width.Value, 1e-6);
        Assert.AreEqual((680 - 56 - sizing.Natural.Sum()) / DocumentBuilder.StarUnit, fitted.Columns[2].Width.Value, 0.02, "le reste de la ligne, marges du papier déduites");
    }

    [STATestMethod]
    public void On_a_page_a_fitted_table_keeps_its_width_between_the_margins()
    {
        using var ui = new UiThread();
        var page = new PageLayout(PageFormat.A5);
        using var shown = new Shown(Text + "\n\n" + FittedTable + "\n\n" + WideTable + "\n\n" + Text + "\n", page.WidthPixels + 52, dark: false, paper: PaperStyle.Plain, layout: page);
        var viewer = shown.Viewer;
        shown.Resize(page.WidthPixels + 52);
        shown.Capture("debord-feuille-a5");

        Assert.AreEqual(0, viewer.TextInset, "une feuille a ses marges : rien n'en sort");
        var room = page.WidthPixels - (2 * page.MarginPixels);
        var fitted = viewer.Document!.Blocks.OfType<Table>().First(table => table.Tag is TableSizing { Fitted: true });
        var sizing = (TableSizing)fitted.Tag;
        Assert.IsLessThan(room, sizing.Natural.Sum());
        Assert.AreEqual(sizing.Natural[0] / DocumentBuilder.StarUnit, fitted.Columns[0].Width.Value, 1e-6, "ce que la colonne demande, pas moins");
        Assert.AreEqual((room - sizing.Natural.Sum()) / DocumentBuilder.StarUnit, fitted.Columns[2].Width.Value, 1e-6, "le reste de la feuille à la colonne vide");

        // Le tableau plus large que la feuille s'y partage, pour la place qu'elle a entre ses marges : chaque colonne garde
        // son plus long mot, aucune ne prend ce qu'elle demanderait sur une ligne.
        var wide = viewer.Document.Blocks.OfType<Table>().First(table => table.Tag is TableSizing { Fitted: false });
        var shared = (TableSizing)wide.Tag;
        Assert.IsGreaterThan(room, shared.Natural.Sum());
        Assert.AreEqual(shared.For(room).Sum() / DocumentBuilder.StarUnit, wide.Columns.Sum(column => column.Width.Value), 1e-6);
        Assert.IsLessThan(shared.Natural.Sum() / DocumentBuilder.StarUnit, wide.Columns.Sum(column => column.Width.Value));
        Assert.IsTrue(viewer.Document.Blocks.All(block => block.Margin.Left == 0));
    }

    [STATestMethod]
    public void The_rest_of_a_long_sheet_takes_its_place_as_it_arrives()
    {
        using var ui = new UiThread();
        // Assez de texte pour que le tableau n'arrive pas avec le premier écran.
        var body = string.Join("\n\n", Enumerable.Repeat(Text, 80)) + "\n\n" + WideTable + "\n\n" + Text + "\n";
        using var shown = new Shown(body, 680, dark: false, complete: false);
        var viewer = shown.Viewer;
        shown.Resize(1600, complete: false);
        Assert.IsTrue(viewer.HasPendingWork, "la suite de la fiche n'est pas encore là");
        Assert.IsFalse(viewer.Document!.Blocks.OfType<Table>().Any());

        UiThread.Until(() => !viewer.HasPendingWork);
        viewer.UpdateLayout();
        TestShell.Flush();

        Assert.IsFalse(viewer.HasPendingWork);
        var inset = viewer.TextInset;
        Assert.AreEqual(70, inset);
        var table = viewer.Document.Blocks.OfType<Table>().Single();
        var sizing = (TableSizing)table.Tag;
        var room = Math.Max(0, inset - Math.Ceiling((sizing.Natural.Sum() - 680) / 2));
        Assert.AreEqual(room, table.Margin.Left, "le tableau arrivé après coup déborde comme les autres");
        Assert.AreEqual(sizing.Natural[0] / DocumentBuilder.StarUnit, table.Columns[0].Width.Value, 1e-6, "et ses colonnes ont leurs largeurs");
        Assert.IsTrue(viewer.Document.Blocks.OfType<Paragraph>().All(paragraph => paragraph.Margin.Left == inset), "tout le texte, du début à la fin, est dans la colonne");
    }

    private static string Start(Paragraph paragraph) => new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;

    internal static byte[] Picture(int width, int height)
    {
        var pixels = new byte[width * height];
        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)(96 + (index % width * 96 / width));
        }
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var deeper in Visuals(child))
            {
                yield return deeper;
            }
        }
    }

    /// <summary>Une fiche montrée par la vue de lecture, comme dans la page d'une fiche : la colonne choisie, et la largeur des blocs larges.</summary>
    private sealed class Shown : IDisposable
    {
        private readonly Window _window;
        private readonly Border _host;

        public Shown(string body, double column, bool dark, PaperStyle? paper = null, bool complete = true, PageLayout? layout = null)
        {
            var design = new DesignResources(dark ? ThemeKind.Dark : ThemeKind.Light, reducedMotion: false);
            var style = paper ?? PaperStyle.Plain;
            var choices = new PaperChoices(IsOn: paper is not null, Endless: style, A4: style, A5: style, A6: style);
            Viewer = new SheetDocumentViewer
            {
                Width = 640,
                Height = 900,
                DrawsPaper = paper is not null,
                Paper = new SheetPaper(choices, PageLayout.Default, SheetPageLayouts.None),
                TextMaxWidth = column,
                WideMaxWidth = Wide,
                PageLayout = layout,
            };
            _host = new Border { Child = Viewer, Width = 640, Height = 900, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            _host.Resources.MergedDictionaries.Add(design.Dictionary);
            _host.Resources[ReadingComfort.ResourceKey] = ReadingComfort.Of(new MachinePreferences());
            _host.SetResourceReference(Border.BackgroundProperty, "Kyf.Brush.Background");
            _window = new Window { Content = new Canvas { Children = { _host } }, Left = -10000, Top = -10000, Width = 400, Height = 300, ShowActivated = false, ShowInTaskbar = false };
            _window.Show();
            Viewer.Reading = SheetReading.Of(
                new OpenedSheet("f_1", "Maladies", "fiches/Biologie/Maladies.md", "Biologie", [], body.ReplaceLineEndings("\n"), 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []),
                _ => Outcome.Refused<byte[]>("non"));
            if (complete)
            {
                Settle();
            }
        }

        public SheetDocumentViewer Viewer { get; }

        public void Resize(double width, bool complete = true)
        {
            _host.Width = width;
            Viewer.Width = width;
            if (complete)
            {
                Settle();
            }
            else
            {
                Viewer.UpdateLayout();
            }
        }

        public void Settle()
        {
            Viewer.CompleteDocument();
            for (var pass = 0; pass < 14; pass++)
            {
                TestShell.Flush();
                Viewer.UpdateLayout();
            }
        }

        /// <summary>Les images se lisent hors du fil de l'interface : la vue les attend, puis se recale.</summary>
        public void WaitForPictures()
        {
            UiThread.Until(() => Visuals(Viewer).OfType<Image>().Any(image => image.Source is not null));
            Settle();
        }

        /// <summary>Le bord gauche de la première ligne d'un paragraphe et le bord droit de sa plus longue ligne, dans la vue.</summary>
        public static (double Left, double Right) Lines(Paragraph paragraph)
        {
            var left = paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward).Left;
            var right = left;
            for (var position = paragraph.ContentStart.GetLineStartPosition(0); position is not null && position.CompareTo(paragraph.ContentEnd) < 0;)
            {
                var next = position.GetLineStartPosition(1, out var moved);
                var end = moved == 1 && next is not null ? next.GetPositionAtOffset(-1, LogicalDirection.Backward) ?? paragraph.ContentEnd : paragraph.ContentEnd;
                right = Math.Max(right, end.GetCharacterRect(LogicalDirection.Backward).Right);
                position = moved == 1 ? next : null;
            }
            return (left, right);
        }

        public void Capture(string name) => _host.Capture(name);

        public void Dispose() => _window.Close();
    }
}
