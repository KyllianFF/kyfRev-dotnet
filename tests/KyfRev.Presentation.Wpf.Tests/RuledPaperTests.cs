using System.Globalization;
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
/// Le texte posé sur la réglure d'un papier, en lecture : chaque ligne de texte sur une ligne du papier, une ligne vide
/// entre deux paragraphes, et, après un tableau ou un bloc de code, le texte qui reprend sur la première ligne entière.
/// Les essais regardent l'image elle-même : où est l'encre du texte, où sont les lignes.
/// </summary>
/// <remarks>
/// Avec la variable d'environnement <c>KYFREV_CAPTURES</c> (un dossier), chaque essai y écrit l'image qu'il a regardée.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class RuledPaperTests
{
    /// <summary>Des capitales seulement, là où l'essai mesure : rien ne descend sous la ligne de base.</summary>
    private const string Body = """
        # MITOSE

        HIHI PREMIER PARAGRAPHE AVEC ASSEZ DE MOTS POUR PASSER À LA LIGNE SUIVANTE ET MONTRER DEUX LIGNES POSÉES SUR LE PAPIER.

        HIHI SECOND PARAGRAPHE.\
        LIGNE VENUE APRÈS MAJ+ENTRÉE.

        ## PHASES

        - PROPHASE
        - ANAPHASE

        | Phase | Durée |
        | --- | --- |
        | Prophase | longue |
        | Métaphase | courte |

        TEXTE VENU SOUS LE TABLEAU.

        ```
        ligne de code
        ```

        TEXTE VENU SOUS LE CODE.

        > CITATION.

        DERNIER PARAGRAPHE.
        """;

    [STATestMethod]
    [DataRow(PaperStyle.LargeSquares, false, ReadingFont.Sans)]
    [DataRow(PaperStyle.LargeSquares, true, ReadingFont.Sans)]
    [DataRow(PaperStyle.Lines, false, ReadingFont.Serif)]
    [DataRow(PaperStyle.Lines, true, ReadingFont.Sans)]
    [DataRow(PaperStyle.SmallSquares, false, ReadingFont.Sans)]
    [DataRow(PaperStyle.SmallSquares, true, ReadingFont.Sans)]
    [DataRow(PaperStyle.Dots, false, ReadingFont.Sans)]
    [DataRow(PaperStyle.Dots, true, ReadingFont.Sans)]
    [DataRow(PaperStyle.Graph, false, ReadingFont.Typewriter)]
    [DataRow(PaperStyle.Graph, true, ReadingFont.Sans)]
    [DataRow(PaperStyle.LargeSquares, true, ReadingFont.Handwriting)]
    [DataRow(PaperStyle.Dots, false, ReadingFont.Marker)]
    public void Every_line_of_text_sits_on_a_line_of_the_paper(PaperStyle style, bool paged, ReadingFont font)
    {
        var step = PaperStyles.LineStepPixels(style);
        using var shown = new Shown(style, paged ? new PageLayout(PageFormat.A5) : null, font, Body);
        var viewer = shown.Viewer;
        Assert.AreEqual(step, viewer.WritingStep, 0.001);
        Assert.AreEqual(style, viewer.PagePaper.Ruling);

        var image = shown.Capture($"lecture-{PaperStyles.Name(style)}-{(paged ? "a5" : "sans-fin")}-{font}");
        var lines = shown.WritingLines(image.PixelHeight);
        Assert.IsGreaterThan(8, lines.Count, "la feuille montre ses lignes d'écriture");

        // Chaque bloc de texte : le bas de l'encre de chacune de ses lignes est posé sur une ligne du papier.
        var blocks = viewer.Document!.Blocks.ToList();
        var measured = 0;
        var astray = new List<string>();
        // Une police manuscrite ne pose pas toutes ses lettres sur la ligne de base : on n'y regarde que les premières,
        // des « H » et des « I », et on leur laisse un peu de jeu.
        var written = font is ReadingFont.Handwriting or ReadingFont.Marker;
        var (reach, play) = written ? (34d, 3.2) : (200d, 1.6);
        foreach (var paragraph in Texts(blocks).Where(paragraph => Math.Abs((paragraph.LineHeight / step) - Math.Round(paragraph.LineHeight / step)) < 0.01))
        {
            if (written && !Start(paragraph).StartsWith("HIHI", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (var (top, bottom, left, _) in shown.LineBoxes(paragraph).Take(written ? 1 : int.MaxValue))
            {
                var ink = image.LowestInk(top, bottom, left, left + reach, shown.Ink);
                if (ink is not { } row)
                {
                    continue;
                }
                var nearest = lines.MinBy(line => Math.Abs(line - (row + 1)));
                if (Math.Abs(nearest - (row + 1)) > play)
                {
                    astray.Add(string.Create(CultureInfo.InvariantCulture, $"« {Start(paragraph)[..Math.Min(18, Start(paragraph).Length)]} » haut {top:0.0}, bas des lettres {row + 1}, ligne {nearest:0}"));
                }
                measured++;
            }
        }
        Assert.IsEmpty(astray, $"Des lignes de texte ne sont pas posées sur le papier (pas {step:0.00}, ligne de base {viewer.WritingBaseline:0.00}) : {string.Join(" ; ", astray)}");
        Assert.IsGreaterThanOrEqualTo(written ? 2 : 9, measured, "titres, paragraphes, liste et citation ont été regardés");

        // Entrée laisse une ligne vide, Maj+Entrée n'en laisse pas.
        var paragraphs = blocks.OfType<Paragraph>().Where(paragraph => Start(paragraph).StartsWith("HIHI", StringComparison.Ordinal)).ToList();
        var first = shown.LineBoxes(paragraphs[0]).ToList();
        var second = shown.LineBoxes(paragraphs[1]).ToList();
        Assert.IsGreaterThanOrEqualTo(2, first.Count, "le premier paragraphe passe à la ligne");
        Assert.AreEqual(step, first[1].Top - first[0].Top, 0.6, "deux lignes d'un paragraphe : un pas");
        Assert.AreEqual(2 * step, second[0].Top - first[^1].Top, 0.6, "un nouveau paragraphe : une ligne laissée vide");
        Assert.AreEqual(step, second[1].Top - second[0].Top, 0.6, "Maj+Entrée : la ligne du dessous");

        // Le tableau et le code prennent la hauteur qu'ils veulent ; le texte qui les suit reprend sur une ligne entière.
        var table = blocks.OfType<Table>().Single();
        Assert.IsNotNull(table.Background, "un tableau a un fond plein sur un papier réglé");
        Assert.AreNotEqual(step, table.LineHeight, "un tableau garde l'interligne du texte");
    }

    [STATestMethod]
    public void Without_a_ruled_paper_the_text_keeps_its_own_spacing()
    {
        using var plain = new Shown(PaperStyle.Plain, null, ReadingFont.Sans, Body);
        Assert.AreEqual(0, plain.Viewer.WritingStep);
        var document = plain.Viewer.Document!;
        Assert.AreEqual(LineStackingStrategy.MaxHeight, document.LineStackingStrategy);
        Assert.AreEqual(ReadingComfort.Default.Size * ReadingComfort.Default.LineSpacing, document.LineHeight, 0.001);
        Assert.AreEqual(12, document.Blocks.OfType<Paragraph>().First(paragraph => Start(paragraph).StartsWith("HIHI", StringComparison.Ordinal)).Margin.Bottom);
        Assert.IsNull(document.Blocks.OfType<Table>().Single().Background);
        Assert.IsTrue(document.Blocks.All(block => block.Margin.Top is 0 or 24 or 16), "rien n'est ajouté au-dessus des blocs");

        // Ce qui s'imprime ne suit jamais un papier : même réglé, il garde l'interligne du texte.
        using var printed = new Shown(PaperStyle.LargeSquares, new PageLayout(PageFormat.A5), ReadingFont.Sans, Body, drawsPaper: false);
        Assert.AreEqual(0, printed.Viewer.WritingStep);
        Assert.AreEqual(LineStackingStrategy.MaxHeight, printed.Viewer.Document!.LineStackingStrategy);
    }

    [STATestMethod]
    public void Changing_the_font_or_the_paper_rebuilds_the_text_that_is_shown()
    {
        using var shown = new Shown(PaperStyle.Lines, null, ReadingFont.Sans, Body);
        var viewer = shown.Viewer;
        var before = viewer.Document!;
        Assert.AreNotEqual("Georgia", before.FontFamily.Source);

        // La police choisie dans les paramètres : le texte se refait avec elle, titres compris, toujours sur les lignes.
        shown.Use(ReadingFont.Serif);
        Assert.AreNotSame(before, viewer.Document);
        Assert.AreEqual("Georgia", viewer.Document!.FontFamily.Source);
        Assert.AreEqual("Georgia", viewer.Document.Blocks.OfType<Paragraph>().First(paragraph => Start(paragraph) == "PHASES").FontFamily.Source);
        Assert.AreEqual(PaperStyles.LineStepPixels(PaperStyle.Lines), viewer.Document.LineHeight, 0.001);

        // Le même réglage ne refait rien.
        var serif = viewer.Document;
        shown.Use(ReadingFont.Serif);
        Assert.AreSame(serif, viewer.Document);

        // Un papier d'un autre pas : les lignes du texte changent de hauteur avec lui ; sans papier, elles reprennent leur interligne.
        viewer.Paper = new SheetPaper(new PaperChoices(IsOn: true, Endless: PaperStyle.Dots), PageLayout.Default, SheetPageLayouts.None);
        shown.Settle();
        Assert.AreEqual(PaperStyles.LineStepPixels(PaperStyle.Dots), viewer.Document!.LineHeight, 0.001);
        Assert.AreEqual(PaperStyles.LineStepPixels(PaperStyle.Dots), viewer.WritingStep, 0.001);
        viewer.Paper = new SheetPaper(new PaperChoices(IsOn: false, Endless: PaperStyle.Dots), PageLayout.Default, SheetPageLayouts.None);
        shown.Settle();
        Assert.AreEqual(0, viewer.WritingStep);
        Assert.AreEqual(LineStackingStrategy.MaxHeight, viewer.Document!.LineStackingStrategy);
    }

    [STATestMethod]
    public void A_quote_cut_between_two_sheets_says_so_and_a_figure_keeps_its_place()
    {
        var quote = string.Join("\n>\n", Enumerable.Range(1, 30).Select(index => $"> CITATION {index}."));
        var body = "- SEUL\n\n$$\nE = mc^2\n$$\n\nTEXTE.\n\n" + quote + "\n\nFIN.\n";
        using var shown = new Shown(PaperStyle.LargeSquares, new PageLayout(PageFormat.A6), ReadingFont.Sans, body);
        var viewer = shown.Viewer;
        var step = PaperStyles.LineStepPixels(PaperStyle.LargeSquares);

        Assert.IsGreaterThan(1, viewer.PageCount);
        Assert.IsGreaterThan(0, viewer.PageBands.Continued, "la citation coupée dit « suite page suivante », puis « suite »");
        Assert.AreEqual(viewer.Document!.LineHeight * (viewer.Document.FontFamily.Baseline / viewer.Document.FontFamily.LineSpacing), viewer.WritingBaseline, 0.001);
        Assert.IsTrue(viewer.Document.Blocks.OfType<BlockUIContainer>().Any(), "la formule a sa place à elle, qui n'est pas un nombre entier de lignes");

        // La formule prend la hauteur qu'elle veut ; le texte qui la suit est sur une ligne, comme celui qui la précède.
        var image = shown.Capture("lecture-citation-coupee-a6");
        var lines = shown.WritingLines(image.PixelHeight);
        foreach (var text in new[] { "SEUL", "TEXTE." })
        {
            var paragraph = Texts(viewer.Document.Blocks).First(candidate => Start(candidate).EndsWith(text, StringComparison.Ordinal));
            var (top, bottom, left, _) = shown.LineBoxes(paragraph).First();
            var row = image.LowestInk(top, bottom, left, left + 120, shown.Ink);
            Assert.IsNotNull(row, text);
            Assert.AreEqual(lines.MinBy(line => Math.Abs(line - (row.Value + 1))), row.Value + 1, 1.6, $"« {text} » est posé sur une ligne (pas {step:0.00})");
        }
    }

    [STATestMethod]
    public void The_preview_and_the_swatch_draw_the_sheet_they_are_given()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var comfort = ReadingComfort.Of(new MachinePreferences { Font = ReadingFont.Typewriter });
        var landscape = new PageLayout(PageFormat.A5, Landscape: true);
        var preview = new SheetPaperPreview { Width = 300, Height = 400, Layout = landscape, Paper = PaperStyle.Graph, Comfort = comfort, Title = "Les organites", Text = "Le noyau.\n\nLes mitochondries." };
        var swatch = new PaperSwatch { Width = 90, Height = 46, Paper = PaperStyle.Lines };
        var tiny = new SheetPaperPreview { Width = 20, Height = 20, Layout = landscape };
        var host = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Children = { preview, swatch, tiny } };
        host.Resources.MergedDictionaries.Add(design.Dictionary);
        var window = new Window { Content = host, Left = -10000, Top = -10000, Width = 400, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        TestShell.Flush();
        try
        {
            Assert.AreEqual((landscape, PaperStyle.Graph, comfort, "Les organites"), (preview.Layout, preview.Paper, preview.Comfort, preview.Title));
            StringAssert.StartsWith(preview.Text, "Le noyau.", StringComparison.Ordinal);
            Assert.AreEqual(PaperStyle.Lines, swatch.Paper);

            // La page entière, centrée, à l'échelle : ses proportions sont celles de la feuille, et son pas celui du papier.
            Assert.AreEqual(21 / 14.8, preview.Sheet.Width / preview.Sheet.Height, 0.02);
            var scale = preview.Sheet.Width / landscape.WidthPixels;
            Assert.AreEqual(PaperStyles.LineStepPixels(PaperStyle.Graph) * scale, preview.Step, 0.2);
            host.Capture("apercu-feuille");

            // Sans papier, plus de pas ; sans fin, la feuille sort par le bas de l'aperçu.
            preview.Paper = null;
            preview.Layout = PageLayout.Default;
            TestShell.Flush();
            Assert.AreEqual(0, preview.Step);
            Assert.AreEqual(preview.ActualHeight, preview.Sheet.Height);

            // Trop petit pour rien montrer : rien n'est dessiné.
            Assert.AreEqual(default, tiny.Sheet);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void A_line_of_text_takes_one_step_and_a_big_title_two()
    {
        var large = PaperStyles.LineStepPixels(PaperStyle.LargeSquares);
        var small = PaperStyles.LineStepPixels(PaperStyle.SmallSquares);
        Assert.AreEqual(30.08, large, 0.001, "8 mm, au trois-centième de pouce");
        Assert.AreEqual(large, PaperStyles.LineStepPixels(PaperStyle.Lines));
        Assert.AreEqual(37.76, small, 0.001, "10 mm : une ligne sur deux des petits carreaux");
        Assert.AreEqual(small, PaperStyles.LineStepPixels(PaperStyle.Dots));
        Assert.AreEqual(small, PaperStyles.LineStepPixels(PaperStyle.Graph));
        Assert.AreEqual(0, PaperStyles.LineStepPixels(PaperStyle.Plain));
        Assert.AreEqual(0, (large * 300 / 96) % 1, 1e-9, "un nombre entier de l'unité de la mise en page");

        Assert.AreEqual(1, ReadingComfort.Slots(17, large));
        Assert.AreEqual(1, ReadingComfort.Slots(21, large));
        Assert.AreEqual(2, ReadingComfort.Slots(28, large), "un grand titre prend deux lignes des grands carreaux");
        Assert.AreEqual(1, ReadingComfort.Slots(28, small));
        Assert.AreEqual(1, ReadingComfort.Slots(17, 0));
        var ruled = ReadingComfort.Default with { Step = large };
        Assert.IsTrue(ruled.IsRuled);
        Assert.AreEqual(large, ruled.LineHeight);
        Assert.AreEqual(2 * large, ruled.HeadingLineHeight(28));
        Assert.AreEqual(28 * 1.3, ReadingComfort.Default.HeadingLineHeight(28), 0.001);
    }

    [TestMethod]
    public void A_text_block_starts_on_the_next_whole_line_and_a_free_block_where_it_falls()
    {
        const double step = 30;
        // Un paragraphe, un tableau (libre), puis trois blocs de texte : celui qui suit le tableau descend à la ligne
        // entière la plus proche, le suivant y est déjà, et un titre de deux pas se décale de ce que demande sa police.
        var boxes = new[]
        {
            new PageBox(0, 30, Snap: 0),
            new PageBox(60, 53),
            new PageBox(125, 30, Snap: 0),
            new PageBox(185, 30, Snap: 0),
            new PageBox(245, 60, Snap: 6),
            new PageBox(305, 30, Snap: 0),
        };
        var endless = PageBreaks.Compute(boxes, double.PositiveInfinity, 0, step);
        CollectionAssert.AreEqual(new[] { 0d, 0, 25, 0, 6, 24 }, endless.Placements.Select(placement => placement.Spacer).ToArray(), "25 pour atteindre 150 ; 6 pour le titre ; 24 pour retrouver une ligne après lui");
        Assert.AreEqual((1, 0, 0d), (endless.Pages, endless.Breaks.Count, endless.Remaining), "une feuille sans fin ne change jamais de page");

        // Sans pas, rien ne bouge ; et à une demi-unité près d'une ligne, un bloc y est.
        Assert.IsTrue(PageBreaks.Compute(boxes, double.PositiveInfinity, 0).Placements.All(placement => placement.Spacer == 0));
        var near = PageBreaks.Compute([new PageBox(0, 30, Snap: 0), new PageBox(59.7, 30, Snap: 0), new PageBox(120.3, 30, Snap: 0)], double.PositiveInfinity, 0, step);
        Assert.IsTrue(near.Placements.All(placement => placement.Spacer == 0));

        // Sur une feuille sans fin, un saut de page ne fait rien : le texte se cale seulement.
        var marked = PageBreaks.Compute([new PageBox(0, 30, Snap: 0), new PageBox(30, 1, PageBoxKind.Break), new PageBox(31, 30, Snap: 0)], double.PositiveInfinity, 0, step);
        Assert.AreEqual((1, 29d), (marked.Pages, marked.Placements[2].Spacer));

        // En pages : un bloc qui ne tiendrait plus une fois calé commence la page suivante, sur sa première ligne.
        var paged = PageBreaks.Compute([new PageBox(0, 30, Snap: 0), new PageBox(30, 32), new PageBox(62, 30, Snap: 0)], usable: 100, between: 50, step);
        Assert.AreEqual(2, paged.Pages);
        Assert.AreEqual(88, paged.Placements[2].Spacer, "à 62, il lui faudrait 90 : il dépasserait 100, et commence donc à 150");
        Assert.AreEqual(150, paged.Breaks.Single().NextTop);
    }

    [TestMethod]
    public void Each_paper_has_its_recommended_font()
    {
        Assert.AreEqual(ReadingFont.Sans, PaperStyles.RecommendedFont(PaperStyle.Plain));
        Assert.AreEqual(ReadingFont.Sans, PaperStyles.RecommendedFont(PaperStyle.SmallSquares));
        Assert.AreEqual(ReadingFont.Handwriting, PaperStyles.RecommendedFont(PaperStyle.LargeSquares));
        Assert.AreEqual(ReadingFont.Serif, PaperStyles.RecommendedFont(PaperStyle.Lines));
        Assert.AreEqual(ReadingFont.Marker, PaperStyles.RecommendedFont(PaperStyle.Dots));
        Assert.AreEqual(ReadingFont.Typewriter, PaperStyles.RecommendedFont(PaperStyle.Graph));

        Assert.AreEqual("Segoe Print", ReadingComfort.Of(new MachinePreferences { Font = ReadingFont.Handwriting }).Family!.Source);
        Assert.IsTrue(ReadingComfort.Of(new MachinePreferences { Font = ReadingFont.Handwriting }).HeadingsFollow);
        Assert.AreEqual("Kyf.Font.Code", ReadingComfort.Of(new MachinePreferences { Font = ReadingFont.Typewriter }).FamilyKey);
        Assert.IsTrue(ReadingComfort.Of(new MachinePreferences { Font = ReadingFont.Serif }).HeadingsFollow, "les titres suivent la police choisie, comme dans l'écriture");
        Assert.IsFalse(ReadingComfort.Of(new MachinePreferences()).HeadingsFollow);
        Assert.AreEqual("var(--police)", ReadingComfort.Of(new MachinePreferences()).CssFamily);
        Assert.AreEqual("Georgia, serif", ReadingComfort.Of(new MachinePreferences { Font = ReadingFont.Serif }).CssFamily);
    }

    /// <summary>
    /// Les paragraphes de texte : ceux du document, des listes et des citations ; ni tableau, ni code. Le titre de la
    /// page, lui, n'écrit pas sur les lignes : sa hauteur de ligne n'est pas un nombre entier de pas.
    /// </summary>
    private static IEnumerable<Paragraph> Texts(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph { Padding.Top: 0, Tag: null } paragraph when Start(paragraph).Length > 0:
                    yield return paragraph;
                    break;
                case List list:
                    foreach (var paragraph in list.ListItems.SelectMany(item => Texts(item.Blocks)))
                    {
                        yield return paragraph;
                    }
                    break;
                case Section section:
                    foreach (var paragraph in Texts(section.Blocks))
                    {
                        yield return paragraph;
                    }
                    break;
            }
        }
    }

    private static string Start(Paragraph paragraph) => new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim();

    /// <summary>Une vue de lecture montrée dans une fenêtre hors de l'écran, avec son papier, sa feuille et sa police.</summary>
    private sealed class Shown : IDisposable
    {
        private readonly Window _window;
        private readonly Border _host;

        public Shown(PaperStyle style, PageLayout? layout, ReadingFont font, string body, bool drawsPaper = true)
        {
            var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
            var choices = new PaperChoices(IsOn: true, Endless: style, A4: style, A5: style, A6: style);
            var width = layout is null ? 640 : layout.WidthPixels + SystemParameters.VerticalScrollBarWidth + 8;
            Viewer = new SheetDocumentViewer { Width = width, Height = 900, DrawsPaper = drawsPaper, Paper = new SheetPaper(choices, PageLayout.Default, SheetPageLayouts.None), PageLayout = layout };
            // En haut à gauche de la fenêtre : l'image d'un élément garde le décalage qu'il a dans son parent.
            _host = new Border { Child = Viewer, Width = width, Height = 900, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            _host.Resources.MergedDictionaries.Add(design.Dictionary);
            _host.Resources[ReadingComfort.ResourceKey] = ReadingComfort.Of(new MachinePreferences { Font = font });
            _host.SetResourceReference(Border.BackgroundProperty, "Kyf.Brush.Background");
            Ink = ((SolidColorBrush)design.Dictionary["Kyf.Brush.Text"]).Color;
            // Dans un canevas, et une fenêtre plus petite que la vue : un canevas ne rogne pas ce qu'il porte, si bien que
            // l'image ne dépend ni de la fenêtre ni de l'écran du poste (celui de la CI est plus petit que la vue).
            _window = new Window { Content = new Canvas { Children = { _host } }, Left = -10000, Top = -10000, Width = 400, Height = 300, ShowActivated = false, ShowInTaskbar = false };
            _window.Show();
            Viewer.Reading = SheetReading.Of(
                new OpenedSheet("f_1", "Mitose", "fiches/Biologie/Mitose.md", "Biologie", [], body.ReplaceLineEndings("\n"), 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []),
                _ => Outcome.Refused<byte[]>("non"));
            Viewer.CompleteDocument();
            for (var pass = 0; pass < 14; pass++)
            {
                TestShell.Flush();
                Viewer.UpdateLayout();
            }
        }

        public SheetDocumentViewer Viewer { get; }

        /// <summary>Change la police des fiches, comme le font les paramètres : par les ressources.</summary>
        public void Use(ReadingFont font)
        {
            _host.Resources[ReadingComfort.ResourceKey] = ReadingComfort.Of(new MachinePreferences { Font = font });
            Settle();
        }

        /// <summary>Laisse le texte se construire et se caler.</summary>
        public void Settle()
        {
            Viewer.CompleteDocument();
            for (var pass = 0; pass < 14; pass++)
            {
                TestShell.Flush();
                Viewer.UpdateLayout();
            }
        }

        /// <summary>La couleur du texte : c'est elle que l'essai cherche dans l'image.</summary>
        public Color Ink { get; }

        /// <summary>L'image de la vue ; écrite dans le dossier des captures s'il est donné.</summary>
        public RenderTargetBitmap Capture(string name) => _host.Capture(name);

        /// <summary>Les lignes d'écriture des feuilles dessinées, en rangées de l'image.</summary>
        public List<double> WritingLines(int height)
        {
            var layer = Viewer.PagePaper;
            var lines = new List<double>();
            foreach (var sheet in layer.Sheets)
            {
                for (var y = sheet.Y + layer.WritingLine; y < sheet.Bottom; y += Viewer.WritingStep)
                {
                    var row = layer.TranslatePoint(new Point(0, y), _host).Y;
                    if (row >= 0 && row < height)
                    {
                        lines.Add(Math.Round(row));
                    }
                }
            }
            return lines;
        }

        /// <summary>Les lignes d'un paragraphe, là où elles sont dans l'image : haut, bas, gauche, droite.</summary>
        public IEnumerable<(double Top, double Bottom, double Left, double Right)> LineBoxes(Paragraph paragraph)
        {
            var scope = Descendants<ScrollViewer>(Viewer).First().Content as UIElement ?? Viewer;
            // Ligne après ligne, depuis le premier caractère de chacune : le haut est celui de ses lettres.
            var pointer = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            for (var guard = 0; pointer is not null && pointer.CompareTo(paragraph.ContentEnd) < 0 && guard < 200; guard++)
            {
                var rect = pointer.GetCharacterRect(LogicalDirection.Forward);
                if (rect.IsEmpty || double.IsInfinity(rect.Top))
                {
                    yield break;
                }
                var origin = scope.TranslatePoint(new Point(rect.Left, rect.Top), _host);
                yield return (origin.Y, origin.Y + rect.Height, origin.X, origin.X + 200);
                pointer = pointer.GetLineStartPosition(1);
            }
        }

        public void Dispose() => _window.Close();

        private static IEnumerable<T> Descendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T found)
                {
                    yield return found;
                }
                foreach (var inner in Descendants<T>(child))
                {
                    yield return inner;
                }
            }
        }
    }
}

/// <summary>Ce que les essais lisent dans une image.</summary>
internal static class RuledPaperPixels
{
    /// <summary>
    /// L'image d'un élément montré ; écrite dans le dossier que donne <c>KYFREV_CAPTURES</c>, s'il est donné, pour la
    /// regarder.
    /// </summary>
    public static RenderTargetBitmap Capture(this FrameworkElement element, string name)
    {
        var (width, height) = (Math.Max(1, (int)element.ActualWidth), Math.Max(1, (int)element.ActualHeight));
        // À travers un pinceau, cadré sur l'élément : rendu tel quel, il garderait le décalage qu'il a dans son parent.
        var picture = new DrawingVisual();
        using (var drawing = picture.RenderOpen())
        {
            var brush = new VisualBrush(element) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, width, height) };
            drawing.DrawRectangle(brush, null, new Rect(0, 0, width, height));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(picture);
        if (Environment.GetEnvironmentVariable("KYFREV_CAPTURES") is { Length: > 0 } folder && Directory.Exists(folder))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(folder, name + ".png"));
            encoder.Save(file);
        }
        return bitmap;
    }

    /// <summary>
    /// Les rangées où le papier a un trait fort, lues dans une bande de colonnes où il n'y a pas de texte : celles dont la
    /// couleur s'écarte le plus du papier (les lignes d'écriture ; pas les interlignes légers).
    /// </summary>
    public static List<int> RuledRows(this BitmapSource image, double left, double right, Color paper)
    {
        var stride = image.PixelWidth * 4;
        var pixels = new byte[stride * image.PixelHeight];
        image.CopyPixels(pixels, stride, 0);
        var (from, to) = (Math.Max(0, (int)left), Math.Min(image.PixelWidth - 1, (int)right));
        var apart = new int[image.PixelHeight];
        for (var row = 0; row < image.PixelHeight; row++)
        {
            for (var column = from; column <= to; column++)
            {
                var at = (row * stride) + (column * 4);
                apart[row] = Math.Max(apart[row], Math.Abs(pixels[at] - paper.B) + Math.Abs(pixels[at + 1] - paper.G) + Math.Abs(pixels[at + 2] - paper.R));
            }
        }
        // Un trait qui tombe entre deux rangées s'y partage, deux fois plus pâle : sa force est celle des deux rangées
        // ensemble. Ne restent que les traits les plus forts : les lignes d'écriture, pas les interlignes ni le millimètre.
        var strength = new int[apart.Length];
        for (var row = 0; row < apart.Length; row++)
        {
            strength[row] = apart[row] + Math.Max(row > 0 ? apart[row - 1] : 0, row < apart.Length - 1 ? apart[row + 1] : 0);
        }
        // La référence est la force des lignes elles-mêmes, pas celle d'un bord de feuille, plus sombre et bien plus rare.
        var ranked = strength.OrderDescending().ToList();
        var reference = ranked.Count > 8 ? ranked[8] : 0;
        return [.. Enumerable.Range(0, apart.Length).Where(row => apart[row] > 8 && strength[row] >= 0.8 * reference)];
    }

    /// <summary>La rangée la plus basse où l'on trouve l'encre du texte, dans cette zone ; rien si elle est vide.</summary>
    public static int? LowestInk(this BitmapSource image, double top, double bottom, double left, double right, Color ink)
    {
        var stride = image.PixelWidth * 4;
        var pixels = new byte[stride * image.PixelHeight];
        image.CopyPixels(pixels, stride, 0);
        var (fromRow, toRow) = (Math.Max(0, (int)Math.Floor(top)), Math.Min(image.PixelHeight - 1, (int)Math.Ceiling(bottom) + 3));
        var (fromColumn, toColumn) = (Math.Max(0, (int)Math.Floor(left)), Math.Min(image.PixelWidth - 1, (int)Math.Ceiling(right)));
        for (var row = toRow; row >= fromRow; row--)
        {
            for (var column = fromColumn; column <= toColumn; column++)
            {
                var at = (row * stride) + (column * 4);
                if (Math.Abs(pixels[at] - ink.B) + Math.Abs(pixels[at + 1] - ink.G) + Math.Abs(pixels[at + 2] - ink.R) < 90)
                {
                    return row;
                }
            }
        }
        return null;
    }
}
