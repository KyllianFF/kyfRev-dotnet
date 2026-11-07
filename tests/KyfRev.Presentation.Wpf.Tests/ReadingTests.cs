using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CsCheck;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Presentation.Wpf.Shell;
using KyfRev.Rendering;
using WpfMath.Controls;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La lecture d'une fiche : son document natif, ses trois volets, ses liens et ses images.</summary>
[TestClass]
public sealed class ReadingTests
{
    private const string Rich = """
        # Mitose

        ## Les causes

        Un *mot*, un **autre**, <u>souligné</u>, ~~barré~~, H<sub>2</sub>O, x<sup>2</sup> et `du code`.\
        Après un saut dur,
        puis un saut simple.

        > Une citation.

        3. trois
        4. quatre

        - un
          - deux

        | A | B | C | D |
        |:--|:-:|--:|---|
        | 1 | 2 | 3 | 4 |

        ---

        ### Plus loin

        ```python
        print(1)
        print(2)
        ```

        ```mermaid
        graph LR
        ```

        Une note[^n] et [une ancre](#fn1).

        [^n]: Son texte.
        """;

    private static readonly string[] MarkdownPieces =
    [
        "*", "_", "**", "~~", "`", "[", "]", "(", ")", "![", "](", "$", "$$", "\n", "\n\n", "> ", "- ", "1. ", "# ", "## ", "---", "|", "|---|", "<u>", "</u>", "<sup>", "</sup>",
        "&amp;", "\\", " ", "  ", "\t", "    ", "a", "é", "😀", "http://a.b/c", "x@y.z", "^[", "[^a]", "[^a]: ", "```", "x", "[r]: u", "[r]", "\r\n", "\\frac{1}{2}", "\\nope{",
    ];

    [STATestMethod]
    public void The_header_opens_the_text_and_a_repeated_title_is_not_shown_twice()
    {
        var built = Build(Rich);

        var first = (Paragraph)built.Document.Blocks.FirstBlock;
        Assert.AreEqual("Biologie  ·  #bio  ·  #partiel", TextOf(first));
        Assert.AreEqual("Mitose", TextOf((Paragraph)first.NextBlock));
        Assert.AreEqual("Les causes", TextOf((Paragraph)first.NextBlock.NextBlock));
    }

    [STATestMethod]
    public void A_preview_has_no_header_since_its_title_is_above()
    {
        var built = Build("Du texte.", showsHeader: false);

        Assert.AreEqual("Du texte.", TextOf((Paragraph)built.Document.Blocks.FirstBlock));
        Assert.AreEqual(1, built.Document.Blocks.Count);
    }

    [STATestMethod]
    public void Every_construction_gets_its_native_form()
    {
        var built = Build(Rich);
        var elements = LogicalDescendants(built.Document).ToList();

        Assert.AreEqual(1, elements.OfType<Italic>().Count());
        Assert.AreEqual(1, elements.OfType<Bold>().Count());
        Assert.AreEqual(1, elements.OfType<Underline>().Count());
        Assert.IsTrue(elements.OfType<Span>().Any(span => span.TextDecorations == TextDecorations.Strikethrough));
        Assert.IsTrue(elements.OfType<Span>().Any(span => span.BaselineAlignment == BaselineAlignment.Superscript));
        Assert.IsTrue(elements.OfType<Span>().Any(span => span.BaselineAlignment == BaselineAlignment.Subscript));
        Assert.AreEqual(1, elements.OfType<LineBreak>().Count());
        var lists = elements.OfType<List>().ToList();
        Assert.AreEqual(TextMarkerStyle.Decimal, lists[0].MarkerStyle);
        Assert.AreEqual(3, lists[0].StartIndex);
        Assert.AreEqual(TextMarkerStyle.Disc, lists[1].MarkerStyle);
        var table = elements.OfType<Table>().Single();
        Assert.AreEqual(4, table.Columns.Count, "quatre colonnes sur toute la largeur du texte : on n'écrit ni à gauche ni à droite d'un tableau");
        var alignments = table.RowGroups[1].Rows[0].Cells.Take(4).Select(cell => ((Paragraph)cell.Blocks.FirstBlock).TextAlignment).ToList();
        Assert.AreEqual("Left Center Right Left", string.Join(' ', alignments));
        Assert.AreEqual(FontWeights.SemiBold, table.RowGroups[0].Rows[0].Cells[0].FontWeight);
        Assert.IsGreaterThanOrEqualTo(1, elements.OfType<Section>().Count(section => section.BorderThickness.Left > 0));
    }

    [STATestMethod]
    public void A_line_break_shows_once_and_a_simple_one_reads_as_a_space()
    {
        var paragraph = (Paragraph)Build("Avant  \nAprès\nsuite", showsHeader: false).Document.Blocks.FirstBlock;

        Assert.AreEqual("Avant", ((Run)paragraph.Inlines.FirstInline).Text);
        Assert.IsInstanceOfType<LineBreak>(paragraph.Inlines.FirstInline.NextInline);
        Assert.AreEqual("Après suite", ((Run)paragraph.Inlines.LastInline).Text);
    }

    [STATestMethod]
    public void Code_keeps_its_lines_and_a_diagram_says_it_is_shown_as_text()
    {
        var built = Build("```python\nprint(1)\nprint(2)\n```\n\n```mermaid\ngraph LR\n```", showsHeader: false);
        var blocks = built.Document.Blocks.OfType<Paragraph>().ToList();

        Assert.AreEqual("print(1)\nprint(2)", string.Concat(LogicalDescendants(blocks[0]).OfType<Run>().Select(run => run.Text)));
        Assert.AreEqual(ReadingText.DiagramAsText, TextOf(blocks[1]));
        Assert.AreEqual("graph LR", TextOf(blocks[2]));
    }

    [STATestMethod]
    public void A_formula_is_drawn_and_one_the_engine_does_not_know_stays_readable()
    {
        var elements = LogicalDescendants(Build("$x^2$ et $\\nope{y}$\n\n$$\n\\frac{1}{2}\n$$\n\n$$\n\\nope{z}\n$$\n\n$\\tfrac{1}{2}+\\dfrac{1}{3}$", showsHeader: false).Document).ToList();

        var inline = elements.OfType<InlineUIContainer>().Select(container => ((Border)container.Child).Child).ToList();
        Assert.AreEqual("x^2", ((FormulaControl)inline[0]).Formula);
        Assert.AreEqual("$\\nope{y}$", ((TextBlock)inline[1]).Text);
        var blocks = elements.OfType<BlockUIContainer>().Select(container => ((Border)container.Child).Child).ToList();
        Assert.AreEqual("\\frac{1}{2}", ((FormulaControl)((Viewbox)blocks[0]).Child).Formula);
        Assert.AreEqual("$$\\nope{z}$$", ((TextBlock)blocks[1]).Text);
        Assert.AreEqual("\\frac{1}{2}+\\frac{1}{3}", ((FormulaControl)inline[2]).Formula, "Deux écritures courantes de KaTeX se dessinent comme une fraction.");
    }

    [STATestMethod]
    public void Many_formulas_are_readable_at_once_and_drawn_just_after()
    {
        using var ui = new UiThread();
        var builder = new DocumentBuilder(
            SheetReading.Of(Sheet(string.Concat(Enumerable.Range(1, 40).Select(i => $"$x_{{{i}}}$\n\n$$\ny_{{{i}}}\n$$\n\n"))), _ => Outcome.Refused<byte[]>("non")),
            _ => { },
            opensLinks: true,
            showsHeader: true);
        var document = builder.Build();
        var slots = LogicalDescendants(document).Select(element => element switch
        {
            InlineUIContainer inline => (Border)inline.Child,
            BlockUIContainer block => (Border)block.Child,
            _ => null,
        }).OfType<Border>().ToList();

        Assert.HasCount(80, slots);
        Assert.IsTrue(builder.HasPendingWork);
        Assert.IsTrue(slots.Any(slot => slot.Child is TextBlock { Text: "$x_{40}$" }));

        UiThread.Until(() => !builder.HasPendingWork);

        Assert.IsFalse(slots.Any(slot => slot.Child is TextBlock));
    }

    [STATestMethod]
    public void A_link_follows_its_target_and_never_navigates_by_itself()
    {
        var followed = new List<object?>();
        var built = Build("[web](https://exemple.org/a) [fiche](Autre.md) [note](#fn1) [cassé](image.png)", follow: followed);
        var links = LogicalDescendants(built.Document).OfType<Hyperlink>().ToList();

        Assert.HasCount(3, links);
        Assert.IsNull(links[0].NavigateUri);
        Assert.AreEqual("https://exemple.org/a", links[0].ToolTip);
        foreach (var link in links)
        {
            link.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
        }
        CollectionAssert.AreEqual(new object[] { new ExternalLink("https://exemple.org/a"), new SheetLink("fiches/Biologie/Autre.md"), new PageAnchor("fn1") }, followed);
        var broken = LogicalDescendants(built.Document).OfType<Span>().Single(span => span is not Hyperlink && span.ToolTip is string);
        Assert.AreEqual(ReadingText.BrokenLink, broken.ToolTip);
    }

    [STATestMethod]
    public void In_a_preview_only_links_inside_the_page_are_followed()
    {
        var built = Build("[web](https://exemple.org) [fiche](Autre.md) [note](#fn1)", opensLinks: false);

        var link = LogicalDescendants(built.Document).OfType<Hyperlink>().Single();
        Assert.AreEqual(new PageAnchor("fn1"), link.Tag);
    }

    [STATestMethod]
    public void A_link_inside_a_link_is_not_a_second_link()
    {
        var built = Build("[a<https://exemple.org>b](c.md)");

        Assert.AreEqual(1, LogicalDescendants(built.Document).OfType<Hyperlink>().Count());
    }

    [STATestMethod]
    public void An_image_of_the_vault_or_of_the_sheet_is_loaded_and_the_others_are_named()
    {
        using var ui = new UiThread();
        var reads = new List<string>();
        var built = Build(
            "![carte](carte.png) ![écrite](" + "data:image/png;base64," + Convert.ToBase64String(Png(8)) + ") ![géante](geante.png)\n\n"
            + "![web](https://exemple.org/a.png) ![loin](../../../a.png) ![ailleurs](ftp://exemple.org/a.png) ![svg](dessin.SVG)\n\n"
            + "![abîmée](abimee.png) ![absente](absente.png) ![sans base](data:image/png;xx)",
            read: path =>
            {
                reads.Add(path);
                return path switch
                {
                    "fiches/Biologie/carte.png" => Outcome.Of(Png(4)),
                    "fiches/Biologie/geante.png" => Outcome.Of(Png(2500)),
                    "fiches/Biologie/abimee.png" => Outcome.Of(new byte[] { 1, 2, 3 }),
                    _ => Outcome.Refused<byte[]>("absente"),
                };
            });

        UiThread.Until(() => built.Images.IsCompleted);

        var elements = LogicalDescendants(built.Document).ToList();
        var containers = elements.OfType<InlineUIContainer>().ToList();
        var images = containers.Select(container => container.Child).OfType<Image>().ToList();
        Assert.HasCount(3, images);
        Assert.AreEqual(4, ((BitmapSource)images[0].Source).PixelWidth);
        Assert.AreEqual("carte", System.Windows.Automation.AutomationProperties.GetName(images[0]));
        Assert.AreEqual(8, ((BitmapSource)images[1].Source).PixelWidth);
        Assert.AreEqual(2400, ((BitmapSource)images[2].Source).PixelWidth);
        Assert.AreEqual(3, containers.Select(container => container.Child).OfType<TextBlock>().Count(text => text.Text == ReadingText.UnreadableImage));
        var text = new TextRange(built.Document.ContentStart, built.Document.ContentEnd).Text;
        Assert.Contains("Image distante bloquée : https://exemple.org/a.png", text);
        Assert.Contains("Image hors du coffre : ../../../a.png", text);
        Assert.Contains($"{ReadingText.SvgLater} : dessin.SVG", text);
        Assert.Contains("Image hors du coffre (copiez-la dans la fiche) : ftp://exemple.org/a.png", text);
        Assert.DoesNotContain("https://exemple.org/a.png", reads);
    }

    [STATestMethod]
    public void A_sheet_nested_too_deeply_is_shown_entirely_and_laid_out()
    {
        var bodies = new[]
        {
            string.Concat(Enumerable.Repeat("> ", 90)) + "fin",
            string.Concat(Enumerable.Repeat("- ", 40)) + "fin",
            string.Concat(Enumerable.Repeat("*a ", 400)) + "fin" + string.Concat(Enumerable.Repeat("*", 400)),
            string.Concat(Enumerable.Repeat("<u>", 90)) + "fin" + string.Concat(Enumerable.Repeat("</u>", 90)),
            string.Concat(Enumerable.Repeat("[", 90)) + "fin" + string.Concat(Enumerable.Repeat("](b.md)", 90)),
        };
        foreach (var body in bodies)
        {
            var window = Host(body);
            try
            {
                Assert.Contains("fin", new TextRange(window.Viewer.Document.ContentStart, window.Viewer.Document.ContentEnd).Text);
            }
            finally
            {
                window.Window.Close();
            }
        }
    }

    [STATestMethod]
    public void Any_text_gives_a_document_that_lays_out()
    {
        var viewer = new SheetDocumentViewer { Width = 600, Height = 400 };
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 640, Height = 440, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            var sheet = Sheet("x");
            Gen.OneOfConst(MarkdownPieces).Array[0, 30].Select(string.Concat).Sample(
                body =>
                {
                    viewer.Reading = ReadingOf(sheet with { Body = body }, _ => Outcome.Refused<byte[]>("non"));
                    viewer.UpdateLayout();
                },
                iter: 300,
                threads: 1);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public async Task The_panels_fold_as_the_page_narrows_the_outline_first()
    {
        using var vault = new ReadingVault(Rich, passage: true);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        page.PageWidth = SheetPageViewModel.OutlineMinimum;
        Assert.IsTrue(page.ShowsOutline);
        Assert.IsTrue(page.ShowsPassages);

        page.PageWidth = SheetPageViewModel.OutlineMinimum - 1;
        Assert.IsFalse(page.ShowsOutline);
        Assert.IsFalse(page.IsOutlineOn);
        Assert.IsTrue(page.ShowsPassages);

        page.PageWidth = SheetPageViewModel.PassagesMinimum - 1;
        Assert.IsFalse(page.ShowsPassages);
        Assert.IsFalse(page.IsPassagesOn);
    }

    [TestMethod]
    public async Task The_same_buttons_hide_a_panel_or_open_it_over_the_text()
    {
        using var vault = new ReadingVault(Rich, passage: true);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        page.PageWidth = 2000;
        page.IsOutlineOn = false;
        Assert.IsFalse(page.ShowsOutline);
        page.IsPassagesOn = false;
        Assert.IsFalse(page.ShowsPassages);
        page.IsOutlineOn = true;
        page.IsPassagesOn = true;
        Assert.IsTrue(page.ShowsOutline && page.ShowsPassages);

        page.PageWidth = 600;
        page.IsOutlineOn = true;
        Assert.IsTrue(page.ShowsOutlineOverlay);
        Assert.IsTrue(page.IsOutlineOn);
        page.IsPassagesOn = true;
        Assert.IsFalse(page.ShowsOutlineOverlay);
        Assert.IsTrue(page.ShowsPassagesOverlay);
        page.IsOutlineOn = true;
        Assert.IsFalse(page.ShowsPassagesOverlay);
        page.CloseOverlayCommand.Execute(null);
        Assert.IsFalse(page.IsOutlineOn);
        page.IsPassagesOn = true;
        page.PageWidth = 2000;
        Assert.IsFalse(page.ShowsPassagesOverlay);
    }

    [TestMethod]
    public async Task A_sheet_without_headings_or_passages_gives_all_the_room_to_its_text()
    {
        using var vault = new ReadingVault("Rien que du texte.", passage: false);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        page.PageWidth = 2000;

        Assert.IsFalse(page.HasOutline);
        Assert.IsFalse(page.HasPassages);
        Assert.IsFalse(page.ShowsOutline);
        Assert.IsFalse(page.ShowsPassages);
    }

    [TestMethod]
    public async Task The_outline_and_the_passages_describe_the_sheet()
    {
        using var vault = new ReadingVault(Rich, passage: true);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        Assert.AreEqual("Les causes|Plus loin", string.Join('|', page.Outline.Select(entry => entry.Title)));
        Assert.AreEqual("0|1", string.Join('|', page.Outline.Select(entry => entry.Indent)));
        Assert.AreEqual("Passages · 2", page.PassagesLabel);
        var passage = page.Passages[0];
        Assert.IsTrue(passage.IsPassage);
        Assert.AreEqual("autre", passage.Title);
        Assert.AreEqual("Mon commentaire", passage.Detail);
        Assert.IsTrue(passage.HasDetail);
        Assert.IsFalse(passage.IsLost);
        var definition = page.Passages[1];
        Assert.IsTrue(definition.IsDefinition);
        Assert.AreEqual("mot", definition.Title);
        Assert.AreEqual("Son sens", definition.Detail);
        Assert.AreEqual(new TextPosition((int)passage.Annotation.Anchor.Start, (int)passage.Annotation.Anchor.End), passage.Position);
    }

    [TestMethod]
    public void Following_a_link_opens_the_browser_or_the_sheet_and_says_when_it_cannot()
    {
        using var vault = new ReadingVault(Rich, passage: false);
        var page = vault.Page();

        page.OpenLinkCommand.Execute(new ExternalLink("https://exemple.org"));
        Assert.AreEqual("https://exemple.org", vault.Links.Opened.Single());
        Assert.IsFalse(page.HasProblem);

        vault.Links.Works = false;
        page.OpenLinkCommand.Execute(new ExternalLink("https://exemple.org/b"));
        Assert.AreEqual(ReadingText.CannotOpenAddress, page.Problem);

        page.OpenLinkCommand.Execute(new SheetLink(vault.App.Sheets.Open(vault.SheetId).Value.Path));
        Assert.AreEqual(vault.SheetId, vault.OpenedSheets.Single());
        Assert.IsFalse(page.HasProblem);

        page.OpenLinkCommand.Execute(new SheetLink("fiches/Absente.md"));
        Assert.AreEqual(ReadingText.MissingLinkedSheet, page.Problem);
    }

    [STATestMethod]
    public void The_page_shows_the_panels_that_fit_and_keeps_back_and_review_in_view()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Long(), passage: true);
        var test = new TestShell();
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var window = ShowWide(test, page);
        try
        {
            Assert.AreEqual(Visibility.Visible, Named<ContentControl>(window, "OutlinePane").Visibility);
            Assert.AreEqual(Visibility.Visible, Named<ContentControl>(window, "PassagesPane").Visibility);
            var viewer = Named<SheetDocumentViewer>(window, "SheetText");
            var scroller = Scroller(viewer);
            var back = Named<Button>(window, "BackButton");
            var before = back.TranslatePoint(default, window);

            scroller.ScrollToEnd();
            TestShell.Flush();

            Assert.IsGreaterThan(0, scroller.VerticalOffset);
            Assert.AreEqual(before, back.TranslatePoint(default, window));
            Assert.AreEqual(0, scroller.ScrollableWidth, "Le texte ne défile jamais à l'horizontale.");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_outline_marks_the_part_at_the_top_of_the_view_from_the_top_to_the_end_of_the_sheet()
    {
        using var ui = new UiThread();
        var body = "## Focus : La notion de l'autorité\n\nUne phrase.\n\n## Définitions :\n\n" + Long() + "\n\n## Fin\n\nCourt.\n";
        using var vault = new ReadingVault(body, passage: false);
        var test = new TestShell();
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var window = ShowWide(test, page);
        try
        {
            var viewer = Named<SheetDocumentViewer>(window, "SheetText");
            UiThread.Until(() => !viewer.HasPendingWork);
            TestShell.Flush();
            var scroller = Scroller(viewer);
            scroller.ScrollToTop();
            TestShell.Flush();

            Assert.AreEqual("Focus : La notion de l'autorité", page.Outline.Single(entry => entry.IsActive).Title, "En haut de la fiche, la partie en cours est celle du haut, même si la suivante est proche.");

            scroller.ScrollToEnd();
            TestShell.Flush();
            Assert.AreEqual("Fin", page.Outline.Single(entry => entry.IsActive).Title, "Tout en bas, le dernier titre ne peut plus monter : il est la partie en cours.");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_list_number_is_never_cut_by_the_left_edge_of_the_text()
    {
        using var ui = new UiThread();
        var test = new TestShell();
        foreach (var comfort in new[] { ReadingComfort.Default, new ReadingComfort(21, 1.9, null), new ReadingComfort(21, 1.6, new FontFamily("Georgia")) })
        {
            foreach (var list in new[] { "1. un\n2. deux\n3. trois\n4. quatre\n", "9. neuf\n10. dix\n", "99. un\n100. deux\n" })
            {
                var ink = InkLeftOfTheText(test, list, comfort);
                Assert.IsGreaterThanOrEqualTo(0, ink, $"« {list.Split('\n')[^2]} » à {comfort.Size} px en {comfort.Family?.Source ?? "police de l'application"} : {ink} px à gauche du texte.");
            }
        }
    }

    /// <summary>
    /// Où commence l'encre d'une liste, par rapport au bord gauche du texte : négatif, elle déborderait, et le lecteur, sans marge
    /// à gauche, la couperait. La page est décalée pour voir ce qui dépasse.
    /// </summary>
    private static int InkLeftOfTheText(TestShell test, string markdown, ReadingComfort comfort)
    {
        const int shift = 60;
        const int width = 500;
        const int height = 300;
        var sheet = new OpenedSheet("f_1", "Essai", "fiches/Essai.md", "", [], markdown, 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []);
        var reading = SheetReading.Of(sheet, _ => Outcome.Refused<byte[]>("non"));
        var document = new DocumentBuilder(reading, _ => { }, opensLinks: true, showsHeader: false, comfort: comfort).Build();
        document.PagePadding = new Thickness(shift, 4, 0, 24);
        var host = new Border { Child = new FlowDocumentScrollViewer { Document = document, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }, Background = Brushes.White, Width = width, Height = height };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var window = new Window { Content = host, Left = -10000, Top = -10000, Width = width + 20, Height = height + 40, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            TestShell.Flush();
            var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            image.Render(host);
            var pixels = new byte[width * height * 4];
            image.CopyPixels(pixels, width * 4, 0);
            var left = width;
            for (var y = 0; y < height; y++)
            {
                for (var x = 3; x < left; x++)
                {
                    var i = ((y * width) + x) * 4;
                    if (pixels[i + 3] > 200 && pixels[i] < 100 && pixels[i + 1] < 100 && pixels[i + 2] < 100)
                    {
                        left = x;
                    }
                }
            }
            return left - shift;
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Clicking_the_outline_brings_the_part_to_the_top_and_marks_it()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Long(), passage: false);
        var test = new TestShell();
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var window = ShowWide(test, page);
        try
        {
            var viewer = Named<SheetDocumentViewer>(window, "SheetText");
            var scroller = Scroller(viewer);
            var entry = page.Outline[4];
            var button = Descendants(Named<ContentControl>(window, "OutlinePane")).OfType<Button>().First(candidate => candidate.DataContext == entry);

            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            TestShell.Flush();

            Assert.IsGreaterThan(0, scroller.VerticalOffset);
            Assert.IsTrue(entry.IsActive);
            Assert.AreEqual(1, page.Outline.Count(candidate => candidate.IsActive));
            var top = viewer.Document.Blocks.OfType<Paragraph>().First(block => TextOf(block) == entry.Title).ContentStart.GetCharacterRect(LogicalDirection.Forward).Top;
            Assert.AreEqual(8, top, 2, "Le titre arrive en haut du texte.");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Clicking_a_passage_brings_its_text_into_view_and_a_lost_one_changes_nothing()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Long() + "\n\nLe mot à trouver est tout au bout, une phrase à retirer aussi.", passage: false);
        var found = vault.Annotate("à trouver");
        vault.Annotate("une phrase à retirer", lost: true);
        var edited = vault.App.Sheets.Open(vault.SheetId).Value;
        Assert.IsTrue(vault.App.Editing.Save(vault.SheetId, edited.Body.Replace("une phrase à retirer", "rien", StringComparison.Ordinal), edited.Fingerprint, 1).Succeeded);
        var test = new TestShell();
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var window = ShowWide(test, page);
        try
        {
            var viewer = Named<SheetDocumentViewer>(window, "SheetText");
            var scroller = Scroller(viewer);
            var buttons = Descendants(Named<ContentControl>(window, "PassagesPane")).OfType<Button>().ToList();
            var lostButton = buttons.Single(button => ((PassageEntry)button.DataContext).IsLost);
            Assert.AreEqual(1, buttons.Count(button => ((PassageEntry)button.DataContext).IsPassage && !((PassageEntry)button.DataContext).IsLost));

            lostButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            TestShell.Flush();
            Assert.AreEqual(0, scroller.VerticalOffset);

            buttons.Single(button => ((PassageEntry)button.DataContext).Annotation.Id == found).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            TestShell.Flush();
            Assert.IsGreaterThan(0, scroller.VerticalOffset);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_note_call_brings_the_note_into_view()
    {
        var host = Host(Long() + "\n\nUne note[^n].\n\n" + Long() + "\n\n[^n]: Son texte.");
        try
        {
            var call = LogicalDescendants(host.Viewer.Document).OfType<Hyperlink>().First(link => link.Tag is PageAnchor { Identifier: "fn1" });
            call.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
            TestShell.Flush();

            Assert.IsGreaterThan(0, Scroller(host.Viewer).VerticalOffset);
        }
        finally
        {
            host.Window.Close();
        }
    }

    [STATestMethod]
    public void The_overlay_panel_appears_on_a_narrow_page_and_closes_when_a_part_is_chosen()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Long(), passage: true);
        var test = new TestShell();
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var window = Show(test, page, 760, 700);
        try
        {
            Assert.AreEqual(Visibility.Collapsed, Named<ContentControl>(window, "OutlinePane").Visibility);
            Named<ToggleButton>(window, "OutlineToggle").IsChecked = true;
            TestShell.Flush();
            var overlay = Named<ContentControl>(window, "OutlineOverlay");
            Assert.AreEqual(Visibility.Visible, overlay.Visibility);

            var button = Descendants(overlay).OfType<Button>().First();
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            TestShell.Flush();

            Assert.AreEqual(Visibility.Collapsed, overlay.Visibility);
            Assert.IsFalse(page.IsOutlineOn);

            page.IsPassagesOn = true;
            Assert.IsTrue(page.ShowsPassagesOverlay);
            Named<SheetDocumentViewer>(window, "SheetText").RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            });
            Assert.IsFalse(page.ShowsPassagesOverlay, "Revenir au texte referme le volet.");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Going_further_than_what_has_appeared_adds_the_rest_at_once()
    {
        using var ui = new UiThread();
        var host = Host(Words(20000) + "\n\n## La fin", flush: false);
        try
        {
            var heading = host.Viewer.Reading!.Outline[^1].Heading;
            Assert.IsTrue(host.Viewer.HasPendingWork || LogicalDescendants(host.Viewer.Document).OfType<Paragraph>().Any(block => TextOf(block) == "La fin"));
            host.Viewer.Show(heading);
            Assert.IsTrue(LogicalDescendants(host.Viewer.Document).OfType<Paragraph>().Any(block => TextOf(block) == "La fin"));
            TestShell.Flush();
            Assert.IsGreaterThan(0, Scroller(host.Viewer).VerticalOffset);

            var replaced = host.Viewer.Document;
            host.Viewer.Reading = SheetReading.Of(Sheet(Words(20000)), _ => Outcome.Refused<byte[]>("non"));
            Assert.AreNotSame(replaced, host.Viewer.Document);
            UiThread.Until(() => !host.Viewer.HasPendingWork);
        }
        finally
        {
            host.Window.Close();
        }
    }

    [STATestMethod]
    public void A_picture_that_does_not_decode_says_it_is_unreadable()
    {
        using var ui = new UiThread();
        var reading = SheetReading.Of(Sheet("![Un dessin](dessin.svg)\n"), _ => Outcome.Of(new byte[] { 1 }));
        var built = new DocumentBuilder(reading, _ => { }, opensLinks: false, showsHeader: false) { Pictures = new FakePictures([]) };
        var document = built.Build();
        built.Complete();

        UiThread.Until(() => built.Images.IsCompleted);

        Assert.IsFalse(built.Images.IsFaulted);
        Assert.AreEqual(ReadingText.UnreadableImage, LogicalDescendants(document).OfType<InlineUIContainer>().Select(container => container.Child).OfType<TextBlock>().Single().Text);
    }

    [STATestMethod]
    public void An_svg_of_the_vault_is_shown_as_a_picture()
    {
        using var ui = new UiThread();
        var reading = SheetReading.Of(Sheet("![Un dessin](dessin.svg)\n\n![Un autre](abime.svg)\n\n![Absent](absent.svg)\n"), path => path switch
        {
            "fiches/Biologie/dessin.svg" => Outcome.Of(new byte[] { 1 }),
            "fiches/Biologie/abime.svg" => Outcome.Of(new byte[] { 2 }),
            _ => Outcome.Refused<byte[]>("absente"),
        });
        var pictures = new FakePictures(Png(4));
        var built = new DocumentBuilder(reading, _ => { }, opensLinks: false, showsHeader: false) { Pictures = pictures };
        var document = built.Build();
        built.Complete();

        UiThread.Until(() => built.Images.IsCompleted);

        var containers = LogicalDescendants(document).OfType<InlineUIContainer>().ToList();
        var image = containers.Select(container => container.Child).OfType<Image>().Single();
        Assert.AreEqual(4, ((BitmapSource)image.Source).PixelWidth);
        Assert.AreEqual("Un dessin", System.Windows.Automation.AutomationProperties.GetName(image));
        Assert.AreEqual(12d, image.MaxWidth, "sa taille naturelle");
        Assert.AreEqual(2, containers.Select(container => container.Child).OfType<TextBlock>().Count(text => text.Text == ReadingText.UnreadableImage), "un SVG abîmé ou absent le dit");
    }

    [STATestMethod]
    public void A_viewer_with_nothing_to_show_stays_quiet()
    {
        var viewer = new SheetDocumentViewer();
        viewer.Show(0);
        viewer.Show(SheetRenderer.Render("# a", "b").Children.OfType<RenderElement>().First());
        Assert.IsNull(viewer.Document);

        viewer.Reading = ReadingOf(Sheet("Texte."), _ => Outcome.Refused<byte[]>("non"));
        Assert.IsNotNull(viewer.Document);
        viewer.Show(0);
        viewer.Show(SheetRenderer.Render("# a", "b").Children.OfType<RenderElement>().First());
        viewer.Reading = null;
        Assert.IsNull(viewer.Document);
        Assert.IsTrue(viewer.ImagesLoaded.IsCompleted);
    }

    [STATestMethod]
    public void A_long_sheet_is_displayed_within_the_budget()
    {
        var sheet = Sheet(Words(20000));
        var viewer = new SheetDocumentViewer { Width = 820, Height = 800 };
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 860, Height = 840, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            var best = double.MaxValue;
            for (var attempt = 0; attempt < 5 && best > 500; attempt++)
            {
                var watch = Stopwatch.StartNew();
                viewer.Reading = ReadingOf(sheet, _ => Outcome.Refused<byte[]>("non"));
                viewer.UpdateLayout();
                viewer.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, () => { });
                best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
            }
            Assert.IsLessThanOrEqualTo(500, best, $"Fiche de 20 000 mots : {best:F0} ms");

            UiThread.Until(() => !viewer.HasPendingWork);
            var text = new TextRange(viewer.Document.ContentStart, viewer.Document.ContentEnd).Text;
            Assert.AreEqual(viewer.Reading!.Map.Text.Count(c => c == 'P'), text.Count(c => c == 'P'), "Tout le texte finit par paraître.");
        }
        finally
        {
            window.Close();
        }
    }

    private static string Long() => string.Join("\n\n", Enumerable.Range(1, 12).Select(part =>
        $"## Partie {part}\n\n" + string.Join("\n\n", Enumerable.Repeat("Un paragraphe assez long pour occuper une bonne partie de la largeur du texte, et même davantage que cela.", 8))));

    /// <summary>Une longue fiche ordinaire : des parties de texte, et de loin en loin un tableau, une formule ou du code.</summary>
    private static string Words(int count)
    {
        const string part = """
            ## Une partie

            Paragraphe avec du *texte* et du **gras**, un [lien](Autre fiche.md), une adresse https://exemple.org/page, puis une suite assez longue pour faire un vrai paragraphe de fiche de révision, avec quelques mots de plus.

            Un autre paragraphe qui développe l'idée précédente avec des mots simples et une `expression`, sans rien de particulier, pour faire le compte de la partie.

            - premier élément de la liste
            - deuxième élément de la liste
              - imbriqué

            > Une citation qui continue
            > sur deux lignes.


            """;
        const string extras = """
            | A | B |
            |---|---|
            | 1 | 2 |

            Une formule $x_1 + y^2$ dans la ligne.

            ```python
            print("code")
            ```


            """;
        var words = part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return string.Concat(Enumerable.Range(0, (count / words) + 1).Select(index => index % 10 == 0 ? part + extras : part));
    }

    private static byte[] Png(int width)
    {
        var bitmap = BitmapSource.Create(width, 1, 96, 96, PixelFormats.Gray8, null, new byte[width], width);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static OpenedSheet Sheet(string body) => new(
        "f_1", "Mitose", "fiches/Biologie/Mitose.md", "Biologie", ["bio", "partiel"], body, 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []);

    private static SheetReading ReadingOf(OpenedSheet sheet, Func<string, Outcome<byte[]>> read) => SheetReading.Of(sheet, read);

    [STATestMethod]
    public void A_size_is_relative_to_the_text_around_it()
    {
        var elements = LogicalDescendants(Build("<span style=\"font-size:2em\">grand <span style=\"font-size:0.5em\">moyen</span></span>\n\n## <span style=\"font-size:2em\">Titre</span>\n\nfin", showsHeader: false).Document).OfType<Span>().ToList();
        var reading = KyfRev.DesignSystem.Tokens.Typography.BodyLarge.Size;

        Assert.AreEqual(reading * 2, elements[0].FontSize);
        Assert.AreEqual(reading, elements[1].FontSize);
        Assert.IsGreaterThan(reading * 2, elements[2].FontSize, "Dans un titre, c'est le double du titre.");
    }

    [STATestMethod]
    public void A_color_is_readable_on_the_ground_and_follows_the_theme()
    {
        const string written = "#00008b";
        var test = new TestShell();
        test.Design.Theme = ThemeKind.Light;
        var built = Build($"<span style=\"color:{written}\">mot</span>", showsHeader: false).Document;
        built.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var span = LogicalDescendants(built).OfType<ColoredSpan>().Single();
        Color Shown() => ((SolidColorBrush)span.Foreground).Color;

        Assert.AreEqual(TokenColor.FromHex(written).ReadableOn(ThemeColors.For(ThemeKind.Light)[ColorRole.Background]).ToColor(), Shown());

        test.Design.Theme = ThemeKind.Dark;
        TestShell.Flush();
        var dark = ThemeColors.For(ThemeKind.Dark)[ColorRole.Background];
        Assert.AreEqual(TokenColor.FromHex(written).ReadableOn(dark).ToColor(), Shown());
        var shown = Shown();
        Assert.IsGreaterThanOrEqualTo(4.5, new TokenColor(shown.A, shown.R, shown.G, shown.B).ContrastWith(dark));
    }

    [STATestMethod]
    public void In_high_contrast_a_chosen_color_gives_way_to_that_of_the_theme()
    {
        var test = new TestShell();
        test.Design.Theme = ThemeKind.HighContrast;
        var built = Build("<span style=\"color:#ff0000\">mot</span>", showsHeader: false).Document;
        built.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var span = LogicalDescendants(built).OfType<ColoredSpan>().Single();

        Assert.AreNotEqual(BaseValueSource.Local, DependencyPropertyHelper.GetValueSource(span, TextElement.ForegroundProperty).BaseValueSource);
    }

    [STATestMethod]
    [DataRow("center", TextAlignment.Center)]
    [DataRow("right", TextAlignment.Right)]
    [DataRow("justify", TextAlignment.Justify)]
    public void A_passage_is_aligned_with_all_it_holds(string written, TextAlignment expected)
    {
        var built = Build($"<div align=\"{written}\">\n\nUn\n\n- deux\n\n</div>\n\nAprès", showsHeader: false).Document;

        var section = built.Blocks.OfType<Section>().Single();
        Assert.AreEqual(expected, section.TextAlignment);
        Assert.AreEqual(expected, section.Blocks.OfType<Paragraph>().Single().TextAlignment);
        Assert.AreEqual(TextAlignment.Left, built.Blocks.OfType<Paragraph>().Last().TextAlignment);
    }

    [STATestMethod]
    public void A_formula_block_is_still_drawn_as_a_formula()
    {
        var built = Build("$$\nx\n$$", showsHeader: false).Document;

        Assert.IsInstanceOfType<BlockUIContainer>(built.Blocks.FirstBlock);
    }

    [STATestMethod]
    public void A_task_draws_its_box_in_place_of_what_it_writes()
    {
        var built = Build("- [ ] Acheter\n- [x] Fait\n\nFin", showsHeader: false).Document;

        var list = built.Blocks.OfType<List>().Single();
        Assert.AreEqual(TextMarkerStyle.None, list.MarkerStyle, "La case prend la place de la puce.");
        var items = list.ListItems.Select(item => (Paragraph)item.Blocks.FirstBlock).ToList();
        Assert.AreEqual("Acheter", string.Concat(items[0].Inlines.OfType<Run>().Select(run => run.Text)));
        Assert.AreEqual("Fait", string.Concat(items[1].Inlines.OfType<Run>().Select(run => run.Text)));
        var open = (Icon)((InlineUIContainer)items[0].Inlines.FirstInline).Child;
        var done = (Icon)((InlineUIContainer)items[1].Inlines.FirstInline).Child;
        Assert.AreEqual(Icons.TaskOpen, open.Text);
        Assert.AreEqual(Icons.TaskDone, done.Text);
        Assert.AreEqual(ReadingText.TaskOpen, System.Windows.Automation.AutomationProperties.GetName(open));
        Assert.AreEqual(ReadingText.TaskDone, System.Windows.Automation.AutomationProperties.GetName(done));
        Assert.AreEqual(TextDecorations.Strikethrough, items[1].Inlines.OfType<Run>().Single().TextDecorations, "Le texte est barré.");
        Assert.IsFalse(items[1].TextDecorations is { Count: > 0 }, "La case ne l'est pas.");
        Assert.IsFalse(items[0].Inlines.OfType<Run>().Single().TextDecorations is { Count: > 0 });
    }

    [STATestMethod]
    public void A_list_with_tasks_and_points_keeps_its_markers()
    {
        var built = Build("- [ ] Une tâche\n- Un point\n\n1. [x] numérotée\n", showsHeader: false).Document;

        var lists = built.Blocks.OfType<List>().ToList();
        Assert.AreEqual(TextMarkerStyle.Disc, lists[0].MarkerStyle);
        Assert.AreEqual("Un point", string.Concat(((Paragraph)lists[0].ListItems.Last().Blocks.FirstBlock).Inlines.OfType<Run>().Select(run => run.Text)));
    }

    [STATestMethod]
    public void A_box_alone_is_a_task_with_no_text()
    {
        var built = Build("- [ ]\n", showsHeader: false).Document;

        var paragraph = (Paragraph)built.Blocks.OfType<List>().Single().ListItems.Single().Blocks.FirstBlock;
        Assert.AreEqual(string.Empty, string.Concat(paragraph.Inlines.OfType<Run>().Select(run => run.Text)));
        Assert.IsInstanceOfType<InlineUIContainer>(paragraph.Inlines.FirstInline);
    }

    [STATestMethod]
    public void The_text_of_a_task_keeps_its_position_in_the_sheet()
    {
        var reading = SheetReading.Of(Sheet("- [ ] Acheter du pain\n"), _ => Outcome.Refused<byte[]>("non"));
        var builder = new DocumentBuilder(reading, _ => { }, opensLinks: true, showsHeader: false);
        var document = builder.Build();

        var start = reading.Map.Text.IndexOf("Acheter", StringComparison.Ordinal);
        Assert.AreEqual(reading.Map.Text.IndexOf("[ ] ", StringComparison.Ordinal) + 4, start, "Comme la 0.x : la case compte dans le texte.");
        var run = LogicalDescendants(document).OfType<Run>().Single();
        Assert.AreEqual("Acheter du pain", run.Text.Trim());
        Assert.AreSame(run, builder.Layer.ElementAt(start));
        Assert.AreEqual(start, builder.Layer.PositionOf(run.ContentStart, isStart: true));
    }

    [STATestMethod]
    public void A_smartart_is_drawn_instead_of_its_code()
    {
        var built = Build("Avant\n\n```smartart cycle\nG1\nS\n```\n\nAprès", showsHeader: false).Document;

        var view = (DiagramView)built.Blocks.OfType<BlockUIContainer>().Single().Child;
        Assert.AreEqual("Cycle : G1, S", view.Scene!.Description);
        Assert.AreEqual("Cycle : G1, S", System.Windows.Automation.AutomationProperties.GetName(view));
        Assert.IsFalse(LogicalDescendants(built).OfType<Run>().Any(run => run.Text.Contains("G1", StringComparison.Ordinal)));
    }

    [STATestMethod]
    public void A_smartart_with_an_unknown_model_stays_a_code_block()
    {
        var built = Build("```smartart inconnu\nG1\n```", showsHeader: false).Document;

        Assert.IsTrue(LogicalDescendants(built).OfType<Run>().Any(run => run.Text == "G1"));
        Assert.IsEmpty(built.Blocks.OfType<BlockUIContainer>().ToList());
    }

    [STATestMethod]
    public void A_drawn_smartart_leaves_the_text_after_it_at_its_place()
    {
        var reading = SheetReading.Of(Sheet("```smartart cycle\nG1\n```\n\nAprès"), _ => Outcome.Refused<byte[]>("non"));
        var builder = new DocumentBuilder(reading, _ => { }, opensLinks: true, showsHeader: false);
        var document = builder.Build();

        var start = reading.Map.Text.IndexOf("Après", StringComparison.Ordinal);
        var run = LogicalDescendants(document).OfType<Run>().Single(candidate => candidate.Text == "Après");
        Assert.AreSame(run, builder.Layer.ElementAt(start));
    }

    [STATestMethod]
    public void A_drawing_takes_its_size_from_the_width_it_is_given()
    {
        var view = new DiagramView { Scene = KyfRev.Rendering.Diagrams.SmartArt.Draw("cycle", "a\nb") };

        view.Measure(new System.Windows.Size(240, double.PositiveInfinity));
        Assert.AreEqual(new System.Windows.Size(240, 150), view.DesiredSize);
        view.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.AreEqual(new System.Windows.Size(480, 300), view.DesiredSize);
        view.Scene = null;
        view.Measure(new System.Windows.Size(240, 240));
        Assert.AreEqual(default, view.DesiredSize);
    }

    [STATestMethod]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    [DataRow(ThemeKind.HighContrast)]
    public void Every_model_draws_in_every_theme_as_a_drawing_and_as_a_thumbnail(ThemeKind theme)
    {
        var test = new TestShell();
        test.Design.Theme = theme;
        foreach (var layout in KyfRev.Rendering.Diagrams.SmartArt.Layouts)
        {
            foreach (var thumbnail in new[] { false, true })
            {
                var view = new DiagramView { Scene = KyfRev.Rendering.Diagrams.SmartArt.Draw(layout.Id, layout.Sample), IsThumbnail = thumbnail };
                view.Resources.MergedDictionaries.Add(test.Design.Dictionary);
                view.Measure(new System.Windows.Size(240, 150));
                view.Arrange(new Rect(0, 0, 240, 150));
                var bitmap = new RenderTargetBitmap(240, 150, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(view);
                var pixels = new byte[240 * 150 * 4];
                bitmap.CopyPixels(pixels, 240 * 4, 0);
                Assert.IsTrue(pixels.Any(value => value != 0), layout.Id);
            }
        }
    }

    private static (FlowDocument Document, Task Images) Build(string body, bool showsHeader = true, bool opensLinks = true, List<object?>? follow = null, Func<string, Outcome<byte[]>>? read = null)
    {
        var builder = new DocumentBuilder(
            SheetReading.Of(Sheet(body), read ?? (_ => Outcome.Refused<byte[]>("non"))),
            target => follow?.Add(target),
            opensLinks,
            showsHeader);
        var document = builder.Build();
        return (document, builder.Images);
    }

    private static (Window Window, SheetDocumentViewer Viewer) Host(string body, bool flush = true)
    {
        var viewer = new SheetDocumentViewer { Reading = SheetReading.Of(Sheet(body), _ => Outcome.Refused<byte[]>("non")) };
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 860, Height = 700, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        window.UpdateLayout();
        if (flush)
        {
            TestShell.Flush();
        }
        return (window, viewer);
    }

    private static string TextOf(Paragraph paragraph) => new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;

    private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in LogicalDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static ScrollViewer Scroller(SheetDocumentViewer viewer) => Descendants(viewer).OfType<ScrollViewer>().First();

    [STATestMethod]
    public void A_narrow_header_holds_on_one_line_and_puts_the_panels_under_more()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Long(), passage: true);
        var test = new TestShell();
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var window = Show(test, page, 470, 700);
        try
        {
            Assert.IsTrue(page.IsCompactHeader);
            Assert.IsFalse(Named<ToggleButton>(window, "OutlineToggle").IsVisible);
            Assert.IsFalse(Named<ToggleButton>(window, "PassagesToggle").IsVisible);
            var back = Named<Button>(window, "BackButton");
            var line = back.TranslatePoint(new Point(0, back.ActualHeight / 2), window).Y;
            foreach (var name in new[] { "ReadToggle", "ReviseToggle", "WriteToggle", "HeaderMoreButton" })
            {
                var button = Named<ButtonBase>(window, name);
                Assert.AreEqual(line, button.TranslatePoint(new Point(0, button.ActualHeight / 2), window).Y, 6, $"{name} : sur la ligne du retour");
            }

            var more = Named<Button>(window, "HeaderMoreButton");
            Assert.AreEqual($"Plan et passages ({page.PassagesLabel})", System.Windows.Automation.AutomationProperties.GetName(more), "le « ⋯ » dit combien de passages il cache");
            more.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            TestShell.Flush();
            Assert.IsTrue(more.ContextMenu.IsOpen);
            var items = more.ContextMenu.Items.OfType<MenuItem>().ToList();
            Assert.AreEqual(page.PassagesLabel, items[1].Header);
            items[0].IsChecked = true;
            Assert.IsTrue(page.IsOutlineOn, "le plan s'ouvre depuis le menu");
            more.ContextMenu.IsOpen = false;

            // À l'écriture, le « ⋯ » du ruban reçoit les mêmes choix.
            var ribbon = Descendants(window).OfType<SheetPage>().Single().Ribbon;
            var ribbonItems = ribbon.ViewMenu.Items.OfType<MenuItem>().ToList();
            Assert.HasCount(5, ribbonItems);
            Assert.IsTrue(ribbonItems[3].IsChecked, "le plan ouvert se voit aussi dans le menu du ruban");
            ribbonItems[3].IsChecked = false;
            Assert.IsFalse(page.IsOutlineOn);

            // Assez de place : les boutons du plan et des passages reviennent dans l'en-tête.
            window.Width = 1200;
            window.UpdateLayout();
            TestShell.Flush();
            Assert.IsFalse(page.IsCompactHeader);
            Assert.IsTrue(Named<ToggleButton>(window, "PassagesToggle").IsVisible);
            Assert.IsFalse(more.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static ShellWindow Show(TestShell test, object page, double width, double height)
    {
        var shell = test.Shell;
        shell.Navigate(new NavigationItem("revision", "Révision", Icons.Document, int.MaxValue, page));
        var window = new ShellWindow(shell) { ShowActivated = true, Left = -10000, Top = -10000, Width = width, Height = height };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        return window;
    }

    /// <summary>
    /// La page assez large pour ses trois volets, quel que soit l'écran : Windows refuse une fenêtre plus
    /// large que lui, et celui de la CI est bien plus petit qu'un poste de travail. La fenêtre reste donc
    /// étroite, et la page garde sa largeur dans un panneau qui ne la contraint pas.
    /// </summary>
    private static Window ShowWide(TestShell test, object page)
    {
        var sheet = new SheetPage { DataContext = page, Width = 1700, Height = 900 };
        var window = new Window { Content = new Canvas { Children = { sheet } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        return window;
    }

    private static T Named<T>(DependencyObject root, string name)
        where T : FrameworkElement => Descendants(root).OfType<T>().First(element => element.Name == name);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}

/// <summary>Ouvre les adresses pour de faux et les retient.</summary>
internal sealed class RecordingLinks : IExternalLinks
{
    public List<string> Opened { get; } = [];

    public bool Works { get; set; } = true;

    public bool Open(string address)
    {
        Opened.Add(address);
        return Works;
    }
}

/// <summary>Un coffre d'essai avec la fiche « Mitose », et ses passages si on en veut.</summary>
/// <summary>Une page cachée simulée : elle ne dessine que le fichier dont le premier octet vaut 1.</summary>
internal sealed class FakePictures(byte[] png) : IMermaidEngine
{
    public Task<MermaidPicture?> DrawAsync(string code, IReadOnlyDictionary<string, string> theme) => Task.FromResult<MermaidPicture?>(null);

    public Task<MermaidPicture?> DrawSvgAsync(ReadOnlyMemory<byte> svg) =>
        Task.FromResult(svg.Span[0] == 1 ? new MermaidPicture(png, "data:image/png;base64,", 12, 6) : null);

    public Task<MermaidPicture?> DrawNetworkAsync(string schema, IReadOnlyDictionary<string, string> colors) => Task.FromResult<MermaidPicture?>(null);
}

internal sealed class ReadingVault : IDisposable
{
    private readonly TestVaults _vaults;
    private readonly FakeTime _time = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));

    public ReadingVault(string body, bool passage)
    {
        _vaults = new TestVaults(AppMode.Development, _time);
        Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Coffre")).Vault);
        SheetId = App.Sheets.Create("Biologie", "Mitose", null).Value;
        Assert.IsTrue(App.Editing.Save(SheetId, body, App.Sheets.Open(SheetId).Value.Fingerprint, 1).Succeeded);
        if (passage)
        {
            var text = TextMap.Of(SheetRenderer.Render(body, "Mitose")).Text;
            Annotate(text.Contains("autre", StringComparison.Ordinal) ? "autre" : "paragraphe");
            Assert.IsTrue(App.Annotations.CreateDefinition(SheetId, AnchorOn(text.Contains(" mot", StringComparison.Ordinal) ? "mot" : "largeur"), new Definition("mot", "Son sens")).Succeeded);
        }
    }

    public KyfRevApplication App => _vaults.App;

    public string SheetId { get; }

    public RecordingLinks Links { get; } = new();

    public List<string> OpenedSheets { get; } = [];

    public FakeSpelling Spelling { get; } = new();

    public SheetPageViewModel Page(bool markdown = true, bool revising = false, Func<bool>? activeRecall = null, ISpeech? speech = null, INetworkSchemas? schemas = null, bool words = false) => new(App.Sheets, App.Annotations, Bar, SheetId, () => Backs++, OpenedSheets.Add, Links, sheet => new SheetEditorViewModel(sheet, App.Editing, App.Sheets, new EditorEnvironment(Spelling, FakeEnvironment.WebData, markdown, () => schemas)), revising, activeRecall) { ReadAloud = speech is null ? null : new ReadAloudViewModel(speech), MarksDefinedWords = words, OpenPassage = (sheet, passage) => OpenedPassages.Add((sheet, passage)) };

    /// <summary>Les fiches rouvertes sur un passage, avec ce passage.</summary>
    public List<(string Sheet, string Passage)> OpenedPassages { get; } = [];

    /// <summary>Une autre fiche du coffre, « La cellule », où se posent les définitions d'ailleurs ; rien avant la première.</summary>
    public string? OtherSheet { get; private set; }

    /// <summary>Définit un terme dans une autre fiche du coffre ; rend la définition.</summary>
    public string DefineElsewhere(string term, string explanation)
    {
        OtherSheet ??= App.Sheets.Create("Biologie", "La cellule", null).Value;
        var anchor = new Anchor(term, string.Empty, string.Empty, 0, term.Length, [], 1, "sha256:aa");
        return App.Annotations.CreateDefinition(OtherSheet, anchor, new Definition(term, explanation)).Value;
    }

    /// <summary>Définit un terme sur un passage de cette fiche ; rend la définition.</summary>
    public string DefineHere(string exact, string term, string explanation) =>
        App.Annotations.CreateDefinition(SheetId, AnchorOn(exact), new Definition(term, explanation)).Value;

    public SheetReading Reading()
    {
        var opened = App.Sheets.Open(SheetId).Value;
        return SheetReading.Of(opened, _ => Outcome.Refused<byte[]>("non"));
    }

    public int Backs { get; private set; }

    public string Annotate(string exact, bool lost = false)
    {
        var anchor = AnchorOn(exact);
        var id = App.Annotations.Create(SheetId, anchor, "Mon commentaire").Value;
        if (lost)
        {
            Assert.IsTrue(App.Annotations.Reanchor(id, anchor, AnchorState.NotFound).Succeeded);
        }
        return id;
    }

    /// <summary>Passe à un autre coffre : ce qui visait celui-ci est ensuite refusé.</summary>
    public void Leave() => Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Autre")).Vault);

    public void Dispose() => _vaults.Dispose();

    private ReviewBarViewModel Bar(ItemKind kind, string id, ReviewState state) => new(App.Reviews, App.Reminders, App.Sheets, _time, kind, id, state);

    private Anchor AnchorOn(string exact)
    {
        var opened = App.Sheets.Open(SheetId).Value;
        var map = TextMap.Of(SheetRenderer.Render(opened.Body, opened.Title));
        var start = map.Text.IndexOf(exact, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, exact);
        var position = new TextPosition(start, start + exact.Length);
        return Anchoring.AnchorOf(map.Text, position, map.SectionAt(start), map.SourceLineAt(start, opened.FirstBodyLine), opened.Fingerprint);
    }
}
