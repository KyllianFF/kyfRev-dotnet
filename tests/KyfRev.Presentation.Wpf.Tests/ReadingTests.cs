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
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
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
        Assert.AreEqual(4, table.Columns.Count);
        var alignments = table.RowGroups[1].Rows[0].Cells.Select(cell => ((Paragraph)cell.Blocks.FirstBlock).TextAlignment).ToList();
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

        Assert.AreEqual("print(1)\nprint(2)", ((Run)blocks[0].Inlines.FirstInline).Text);
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

    public SheetPageViewModel Page() => new(App.Sheets, App.Annotations, Bar, SheetId, () => Backs++, OpenedSheets.Add, Links, sheet => new SheetEditorViewModel(sheet, App.Editing, App.Sheets, Spelling));

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
