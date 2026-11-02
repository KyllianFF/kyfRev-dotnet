using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using CsCheck;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le surlignage : les passages replacés dans le texte, et ceux qu'on y crée.</summary>
[TestClass]
public sealed class HighlightingTests
{
    private const string Body = "# Mitose\n\n## Partie\n\nUn premier paragraphe avec *un mot* et `du code`.\n\nLe second paragraphe, avec une formule $x^2$ au milieu, puis la fin.\n";

    private static readonly string[] Pieces = ["*", "**", "`", "[", "](b)", "$x$", "\n", "\n\n", "> ", "- ", "# ", "|", "|---|", "<u>", "</u>", "&amp;", "\\", " ", "  ", "a", "mot", "é", "😀", "http://a.b/c", "^[", "[^a]", "[^a]: ", "```"];

    [STATestMethod]
    public void A_highlight_cuts_the_runs_and_leaves_the_text_as_it_was()
    {
        var host = Host(Body);
        try
        {
            var before = TextOf(host.Viewer);
            var position = Position(host.Viewer, "premier paragraphe");

            host.Viewer.Highlights = [new Highlight("a", position.Start, position.End, false, false)];

            Assert.AreEqual(before, TextOf(host.Viewer));
            var marked = Runs(host.Viewer).Single(run => run.Background is not null);
            Assert.AreEqual("premier paragraphe", marked.Text);
            Assert.HasCount(1, marked.TextDecorations);

            host.Viewer.Highlights = [];

            Assert.AreEqual(before, TextOf(host.Viewer));
            Assert.IsFalse(Runs(host.Viewer).Any(run => run.Background is not null));
            Assert.IsFalse(Runs(host.Viewer).Any(run => run.TextDecorations is { Count: > 0 }));
        }
        finally
        {
            host.Window.Close();
        }
    }

    [STATestMethod]
    public void A_highlight_can_cross_emphasis_and_paragraphs_and_overlap_another()
    {
        var host = Host(Body);
        try
        {
            var across = Position(host.Viewer, "avec un mot et du code.\n\nLe second");
            var inside = Position(host.Viewer, "un mot");

            host.Viewer.Highlights = [new Highlight("a", across.Start, across.End, false, false), new Highlight("b", inside.Start, inside.End, true, false)];

            var text = string.Concat(Runs(host.Viewer).Where(run => run.Background is not null).Select(run => run.Text));
            Assert.AreEqual("avec un mot et du code.Le second", text);
            Assert.HasCount(2, Runs(host.Viewer).Single(run => run.Text == "un mot").TextDecorations);
        }
        finally
        {
            host.Window.Close();
        }
    }

    [STATestMethod]
    public void A_definition_is_underlined_with_dots_a_candidate_with_dashes_and_the_active_one_stands_out()
    {
        var host = Host(Body);
        try
        {
            var word = Position(host.Viewer, "premier");
            var other = Position(host.Viewer, "second");

            host.Viewer.Highlights = [new Highlight("d", word.Start, word.End, true, false), new Highlight("c", other.Start, other.End, false, true)];

            var definition = Runs(host.Viewer).Single(run => run.Text == "premier");
            Assert.IsNull(definition.Background, "Une définition n'a pas de fond.");
            Assert.HasCount(2, definition.TextDecorations[0].Pen.DashStyle.Dashes);
            var candidate = Runs(host.Viewer).Single(run => run.Text == "second");
            Assert.IsNull(candidate.Background, "Un candidat n'a pas de fond : ce n'est pas encore un surlignage.");
            Assert.AreEqual(DashStyles.Dash, candidate.TextDecorations[0].Pen.DashStyle);
            var thin = definition.TextDecorations[0].Pen.Thickness;

            host.Viewer.ActiveHighlight = "d";

            Assert.IsGreaterThan(thin, Runs(host.Viewer).Single(run => run.Text == "premier").TextDecorations[0].Pen.Thickness);
        }
        finally
        {
            host.Window.Close();
        }
    }

    [STATestMethod]
    public void A_formula_in_a_passage_is_marked_and_selected_for_its_source()
    {
        var host = Host(Body);
        try
        {
            var slot = LogicalDescendants(host.Viewer.Document).OfType<InlineUIContainer>().Select(container => (Border)container.Child).Single();
            var passage = Position(host.Viewer, "une formule $x^2$ au milieu");

            host.Viewer.Highlights = [new Highlight("a", passage.Start, passage.End, false, false)];
            Assert.IsNotNull(slot.Background);
            Assert.IsGreaterThan(0, slot.BorderThickness.Bottom);
            Assert.AreEqual("a", host.Viewer.AnnotationsAt(slot.Child).Single());
            Assert.AreEqual("a", host.Viewer.AnnotationsAt(slot).Single());

            host.Viewer.Highlights = [];
            Assert.IsNull(slot.Background);
            Assert.AreEqual(0, slot.BorderThickness.Bottom);
        }
        finally
        {
            host.Window.Close();
        }
    }

    [STATestMethod]
    public void A_selection_reads_as_the_exact_displayed_text()
    {
        var host = Host(Body);
        try
        {
            foreach (var exact in new[] { "premier paragraphe", "un mot", "avec un mot et", "du code", "fin", "paragraphe avec un mot et du code.\n\nLe second" })
            {
                var position = Position(host.Viewer, exact);
                Select(host.Viewer, position);

                Assert.AreEqual(position, host.Viewer.CurrentSelection(), exact);
            }
        }
        finally
        {
            host.Window.Close();
        }
    }

    [STATestMethod]
    public void A_selection_that_includes_a_formula_keeps_its_source_and_a_highlight_does_not_shift_positions()
    {
        var host = Host(Body);
        try
        {
            var word = Position(host.Viewer, "second");
            host.Viewer.Highlights = [new Highlight("a", word.Start, word.End, false, false)];
            var position = Position(host.Viewer, "second paragraphe, avec une formule $x^2$ au milieu");

            Select(host.Viewer, position);

            Assert.AreEqual(position, host.Viewer.CurrentSelection());
        }
        finally
        {
            host.Window.Close();
        }
    }

    [STATestMethod]
    public void A_selection_starting_in_the_title_begins_at_the_text_and_one_only_in_the_title_gives_nothing()
    {
        var host = Host(Body);
        try
        {
            var title = (Paragraph)host.Viewer.Document.Blocks.ElementAt(1);
            var end = PointerOf(host.Viewer, Position(host.Viewer, "Partie").End);

            host.Viewer.Selection.Select(title.ContentStart, end);
            Assert.AreEqual("Partie", host.Viewer.Reading!.Map.Text[Range(host.Viewer.CurrentSelection()!.Value)].TrimStart());

            host.Viewer.Selection.Select(title.ContentStart, title.ContentEnd);
            Assert.IsNull(host.Viewer.CurrentSelection());

            host.Viewer.Selection.Select(title.ContentStart, title.ContentStart);
            Assert.IsNull(host.Viewer.CurrentSelection());
        }
        finally
        {
            host.Window.Close();
        }
    }

    [STATestMethod]
    public void Any_selection_of_any_text_stays_within_the_displayed_text_and_highlights_change_nothing()
    {
        var viewer = new SheetDocumentViewer { Width = 600, Height = 400 };
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 640, Height = 440, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            Gen.OneOfConst(Pieces).Array[1, 20].Select(string.Concat).Sample(
                body =>
                {
                    viewer.Reading = SheetReading.Of(Sheet(body), _ => Outcome.Refused<byte[]>("non"));
                    viewer.Highlights = null;
                    var before = TextOf(viewer);
                    var runs = Runs(viewer);
                    if (runs.Count == 0)
                    {
                        return;
                    }
                    var text = viewer.Reading.Map.Text;
                    viewer.Selection.Select(runs[0].ContentStart, runs[^1].ContentEnd);
                    if (viewer.CurrentSelection() is { } position)
                    {
                        Assert.IsTrue(position.Start >= 0 && position.End <= text.Length && position.Start < position.End);
                        viewer.Highlights = [new Highlight("a", position.Start, position.End, false, false), new Highlight("b", position.Start, (position.Start + position.End) / 2, true, false)];
                        Assert.AreEqual(before, TextOf(viewer));
                    }
                },
                iter: 200,
                threads: 1);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_passage_in_a_code_block_is_highlighted_from_the_start()
    {
        var reading = SheetReading.Of(Sheet("```python\nprint(1)\nprint(2)\n```\n"), _ => Outcome.Refused<byte[]>("non"));
        var viewer = new SheetDocumentViewer { Highlights = [new Highlight("a", 9, 17, false, false)], Reading = reading };

        Assert.AreEqual("print(2)", string.Concat(Runs(viewer).Where(run => run.TextDecorations is { Count: > 0 }).Select(run => run.Text)), "le code coloré garde ses morceaux");
    }

    [STATestMethod]
    public void A_highlight_far_down_a_long_sheet_applies_as_the_text_appears()
    {
        using var ui = new UiThread();
        var body = string.Concat(Enumerable.Repeat("Un paragraphe de remplissage assez long pour que la suite vienne plus tard.\n\n", 400)) + "Le tout dernier mot.\n";
        var host = Host(body, flush: false);
        try
        {
            var text = host.Viewer.Reading!.Map.Text;
            var start = text.IndexOf("dernier", StringComparison.Ordinal);
            host.Viewer.Highlights = [new Highlight("a", start, start + "dernier".Length, false, false)];
            Assert.IsTrue(host.Viewer.HasPendingWork, "La fin de cette fiche n'a pas encore paru.");

            UiThread.Until(() => !host.Viewer.HasPendingWork);

            Assert.AreEqual("dernier", Runs(host.Viewer).Single(run => run.Background is not null).Text);
        }
        finally
        {
            host.Window.Close();
        }
    }

    [TestMethod]
    public async Task Highlighting_a_selection_creates_the_passage_and_marks_it()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        Assert.IsEmpty(page.Highlights!);

        await page.HighlightCommand.ExecuteAsync(PositionOf(page, "premier paragraphe"));

        var created = vault.App.Sheets.Open(vault.SheetId).Value.Annotations.Single();
        Assert.AreEqual("premier paragraphe", created.Anchor.Exact);
        Assert.AreEqual(AnchorState.Anchored, created.AnchorState);
        Assert.AreEqual("Partie", created.Anchor.Section.Single());
        Assert.AreEqual(vault.App.Sheets.Open(vault.SheetId).Value.FirstBodyLine + 4L, created.Anchor.SourceLine);
        Assert.AreEqual(created.Id, page.Highlights!.Single().Id);
        Assert.AreEqual(created.Id, page.ActivePassage);
        Assert.IsTrue(page.Passages.Single().IsActive);
        Assert.AreEqual(ReadingText.PassageCreated, page.Notice);
        Assert.IsTrue(page.HasPassages);
    }

    [TestMethod]
    public async Task Annotating_asks_for_a_comment_and_cancelling_creates_nothing()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        var position = PositionOf(page, "premier");

        page.StartAnnotatingCommand.Execute(position);
        Assert.IsTrue(page.IsEditing);
        Assert.IsTrue(page.EditsComment);
        Assert.AreEqual(ReadingText.CommentTitle, page.EditorTitle);
        await page.AnswerEditorCommand.ExecuteAsync(false);
        Assert.IsFalse(page.IsEditing);
        Assert.IsEmpty(vault.App.Sheets.Open(vault.SheetId).Value.Annotations);

        page.StartAnnotatingCommand.Execute(position);
        page.EditorComment = "  Pourquoi ?  ";
        await page.AnswerEditorCommand.ExecuteAsync(true);
        Assert.AreEqual("Pourquoi ?", vault.App.Sheets.Open(vault.SheetId).Value.Annotations.Single().Comment);

        page.StartAnnotatingCommand.Execute(PositionOf(page, "second"));
        page.EditorComment = "   ";
        await page.AnswerEditorCommand.ExecuteAsync(true);
        Assert.IsTrue(vault.App.Sheets.Open(vault.SheetId).Value.Annotations.Any(annotation => annotation.Anchor.Exact == "second" && annotation.Comment is null));
    }

    [TestMethod]
    public async Task Defining_proposes_the_selection_and_needs_an_explanation()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        page.StartDefiningCommand.Execute(PositionOf(page, "du code.\n\nLe second"));

        Assert.IsTrue(page.EditsDefinition);
        Assert.AreEqual(ReadingText.DefinitionTitle, page.EditorTitle);
        Assert.AreEqual("du code. Le second", page.EditorTerm);
        await page.AnswerEditorCommand.ExecuteAsync(true);
        Assert.IsTrue(page.IsEditing, "Sans explication, la saisie reste ouverte.");
        Assert.AreEqual(ReadingText.DefinitionIncomplete, page.EditorProblem);
        Assert.IsTrue(page.HasEditorProblem);

        page.EditorTerm = "Le code";
        page.EditorExplanation = "Ce qui s'exécute.";
        await page.AnswerEditorCommand.ExecuteAsync(true);

        Assert.IsFalse(page.IsEditing);
        var created = vault.App.Sheets.Open(vault.SheetId).Value.Annotations.Single();
        Assert.AreEqual(new Definition("Le code", "Ce qui s'exécute."), created.Definition);
        Assert.AreEqual(ReadingText.DefinitionCreated, page.Notice);
        Assert.IsTrue(page.Highlights!.Single().IsDefinition);
    }

    [TestMethod]
    public async Task A_very_long_selection_proposes_a_term_of_the_allowed_length()
    {
        using var vault = new ReadingVault(string.Concat(Enumerable.Repeat("mot ", 200)), passage: false);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        page.StartDefiningCommand.Execute(new TextPosition(0, 790));

        Assert.AreEqual(SheetPageViewModel.TermMaximum, page.EditorTerm.Length);
    }

    [TestMethod]
    public async Task A_comment_or_a_definition_can_be_changed()
    {
        using var vault = new ReadingVault(Body + "\nUn autre mot.\n", passage: true);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        page.StartEditingCommand.Execute(page.Passages.Single(passage => passage.IsPassage));
        Assert.AreEqual("Mon commentaire", page.EditorComment);
        page.EditorComment = "Un autre";
        await page.AnswerEditorCommand.ExecuteAsync(true);
        Assert.AreEqual("Un autre", page.Passages.Single(passage => passage.IsPassage).Detail);
        Assert.AreEqual(ReadingText.Saved, page.Notice);

        page.StartEditingCommand.Execute(page.Passages.Single(passage => passage.IsDefinition));
        Assert.AreEqual("mot", page.EditorTerm);
        Assert.AreEqual("Son sens", page.EditorExplanation);
        page.EditorExplanation = "Un autre sens";
        await page.AnswerEditorCommand.ExecuteAsync(true);
        Assert.AreEqual("Un autre sens", page.Passages.Single(passage => passage.IsDefinition).Detail);
    }

    [TestMethod]
    public async Task Deleting_asks_first_and_sends_the_passage_to_the_trash()
    {
        using var vault = new ReadingVault(Body + "\nUn autre mot.\n", passage: true);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        var passage = page.Passages.Single(entry => entry.IsPassage);

        page.RequestDeleteCommand.Execute(passage);
        Assert.IsTrue(page.IsDeleting);
        Assert.AreEqual(ReadingText.DeletePassageTitle, page.DeleteTitle);
        await page.AnswerDeleteCommand.ExecuteAsync(false);
        Assert.IsFalse(page.IsDeleting);
        Assert.HasCount(2, page.Passages);

        page.RequestDeleteCommand.Execute(passage);
        await page.AnswerDeleteCommand.ExecuteAsync(true);

        Assert.AreEqual(ReadingText.Deleted, page.Notice);
        Assert.AreEqual("mot", page.Passages.Single().Title);
        Assert.HasCount(1, page.Highlights!);
        Assert.IsTrue(vault.App.Sheets.Open(vault.SheetId).Value.Annotations.Single(annotation => annotation.Id == passage.Annotation.Id).InTrash);
        page.RequestDeleteCommand.Execute(page.Passages.Single());
        Assert.AreEqual(ReadingText.DeleteDefinitionTitle, page.DeleteTitle);
    }

    [TestMethod]
    public async Task Refusals_are_shown_and_keep_the_input_open()
    {
        using var vault = new ReadingVault(Body + "\nUn autre mot.\n", passage: true);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        var passage = page.Passages.Single(entry => entry.IsPassage);
        vault.Leave();

        page.StartEditingCommand.Execute(passage);
        page.EditorComment = "Un autre";
        await page.AnswerEditorCommand.ExecuteAsync(true);
        Assert.IsTrue(page.IsEditing);
        Assert.IsTrue(page.HasEditorProblem);
        Assert.IsTrue(page.HasProblem);

        page.RequestDeleteCommand.Execute(passage);
        await page.AnswerDeleteCommand.ExecuteAsync(true);
        Assert.IsTrue(page.HasProblem);

        await page.HighlightCommand.ExecuteAsync(PositionOf(page, "second"));
        Assert.IsTrue(page.HasProblem);
    }

    [TestMethod]
    public async Task Opening_a_sheet_records_a_passage_that_moved_and_nothing_else()
    {
        using var vault = new ReadingVault(Body, passage: false);
        vault.Annotate("premier paragraphe");
        var before = vault.App.Sheets.Open(vault.SheetId).Value;
        Assert.IsTrue(vault.App.Editing.Save(vault.SheetId, "Un ajout en tête.\n\n" + before.Body, before.Fingerprint, 1).Succeeded);
        var moved = vault.App.Sheets.Open(vault.SheetId).Value.Annotations.Single();
        Assert.AreEqual(before.Annotations.Single().Anchor.Start, moved.Anchor.Start);

        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        var saved = vault.App.Sheets.Open(vault.SheetId).Value;
        var anchor = saved.Annotations.Single().Anchor;
        Assert.AreEqual(page.Reading!.Map.Text.IndexOf("premier paragraphe", StringComparison.Ordinal), anchor.Start);
        Assert.AreEqual(saved.Fingerprint, anchor.SheetFingerprint);
        Assert.AreEqual(anchor.Start, page.Highlights!.Single().Start);
        Assert.IsEmpty(SheetReading.Of(saved, _ => Outcome.Refused<byte[]>("non")).PlacementChanges().ToList(), "Une fiche relue n'a plus rien à enregistrer.");
    }

    [TestMethod]
    public void A_section_label_written_differently_is_not_a_reason_to_write()
    {
        var reading = SheetReading.Of(Sheet(Body), _ => Outcome.Refused<byte[]>("non"));
        var start = reading.Map.Text.IndexOf("premier", StringComparison.Ordinal);
        var anchor = reading.AnchorAt(new TextPosition(start, start + 7));

        reading.Update([Annotation(anchor with { Section = ["Écrit autrement par la 0.x"] }, AnchorState.Anchored)]);

        Assert.IsEmpty(reading.PlacementChanges().ToList());
    }

    [TestMethod]
    public void A_passage_whose_state_changed_is_recorded_even_at_the_same_place()
    {
        var reading = SheetReading.Of(Sheet(Body), _ => Outcome.Refused<byte[]>("non"));
        var start = reading.Map.Text.IndexOf("premier", StringComparison.Ordinal);
        var anchor = reading.AnchorAt(new TextPosition(start, start + 7));

        reading.Update([Annotation(anchor, AnchorState.NeedsReview)]);

        Assert.AreEqual(AnchorState.Anchored, reading.PlacementChanges().Single().State);
    }

    [TestMethod]
    public async Task A_passage_whose_text_changed_is_proposed_and_can_be_confirmed_or_refused()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var id = vault.Annotate("premier paragraphe");
        var before = vault.App.Sheets.Open(vault.SheetId).Value;
        Assert.IsTrue(vault.App.Editing.Save(vault.SheetId, before.Body.Replace("premier paragraphe", "premiers paragraphes", StringComparison.Ordinal), before.Fingerprint, 1).Succeeded);

        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        var candidate = page.Passages.Single();
        Assert.IsTrue(candidate.IsCandidate);
        Assert.IsFalse(candidate.IsLost);
        Assert.IsTrue(page.Highlights!.Single().IsCandidate);
        Assert.AreEqual(AnchorState.NeedsReview, vault.App.Sheets.Open(vault.SheetId).Value.Annotations.Single().AnchorState);

        await page.ConfirmPositionCommand.ExecuteAsync(candidate);

        Assert.AreEqual(ReadingText.PositionConfirmed, page.Notice);
        var confirmed = vault.App.Sheets.Open(vault.SheetId).Value.Annotations.Single();
        Assert.AreEqual(AnchorState.Anchored, confirmed.AnchorState);
        Assert.AreEqual(id, confirmed.Id);
        Assert.IsFalse(page.Passages.Single().IsCandidate);
    }

    [TestMethod]
    public async Task A_position_with_the_same_text_is_confirmed_without_changing_the_passage()
    {
        using var vault = new ReadingVault("aa mot bb mot cc\n", passage: false);
        vault.Annotate("mot");
        var before = vault.App.Sheets.Open(vault.SheetId).Value;
        Assert.IsTrue(vault.App.Editing.Save(vault.SheetId, "xx mot yy mot zz\n", before.Fingerprint, 1).Succeeded);

        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        var candidate = page.Passages.Single();
        Assert.IsTrue(candidate.IsCandidate, "Deux occurrences à égalité : à vérifier.");

        await page.ConfirmPositionCommand.ExecuteAsync(candidate);

        Assert.AreEqual(AnchorState.Anchored, vault.App.Sheets.Open(vault.SheetId).Value.Annotations.Single().AnchorState);
    }

    [TestMethod]
    public async Task A_refused_position_leaves_the_passage_without_a_place()
    {
        using var vault = new ReadingVault(Body, passage: false);
        vault.Annotate("premier paragraphe");
        var before = vault.App.Sheets.Open(vault.SheetId).Value;
        Assert.IsTrue(vault.App.Editing.Save(vault.SheetId, before.Body.Replace("premier paragraphe", "premiers paragraphes", StringComparison.Ordinal), before.Fingerprint, 1).Succeeded);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        await page.KeepWithoutPositionCommand.ExecuteAsync(page.Passages.Single());

        Assert.IsTrue(page.Passages.Single().IsLost);
        Assert.IsEmpty(page.Highlights!);
        Assert.AreEqual(AnchorState.NotFound, vault.App.Sheets.Open(vault.SheetId).Value.Annotations.Single().AnchorState);
    }

    [TestMethod]
    public async Task A_sheet_changed_elsewhere_is_read_again_after_an_action()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        var first = page.Reading;
        var opened = vault.App.Sheets.Open(vault.SheetId).Value;
        Assert.IsTrue(vault.App.Editing.Save(vault.SheetId, opened.Body + "\nUne ligne de plus.\n", opened.Fingerprint, 1).Succeeded);

        await page.HighlightCommand.ExecuteAsync(PositionOf(page, "premier"));

        Assert.AreNotSame(first, page.Reading);
        Assert.Contains("Une ligne de plus.", page.Reading!.Map.Text);
    }

    [STATestMethod]
    public void The_text_menu_offers_what_a_selection_or_a_passage_allows()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body + "\nUn autre mot.\n", passage: true);
        var page = vault.Page();
        var test = new TestShell();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var (window, sheetPage) = ShowWide(test, page);
        try
        {
            var viewer = Named<SheetDocumentViewer>(window, "SheetText");
            Assert.AreEqual(EditingText.EditSheet, Headers(sheetPage.TextMenuItems(null)));

            var run = Runs(viewer).First(candidate => candidate.Background is not null);
            var onPassage = Headers(sheetPage.TextMenuItems(run));
            Assert.AreEqual($"{ReadingText.ShowPassage}|{ReadingText.EditComment}|{ReadingText.DeletePassage}|{EditingText.EditSheet}", onPassage);

            Select(viewer, Position(viewer, "second"));
            Assert.AreEqual($"{ReadingText.Copy}|{ReadingText.Highlight}|{ReadingText.Annotate}|{ReadingText.Define}|{EditingText.EditSheet}", Headers(sheetPage.TextMenuItems(null)));

            ((MenuItem)sheetPage.TextMenuItems(null)[3]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.IsTrue(page.IsEditing);
            Assert.IsTrue(page.EditsComment);
            page.AnswerEditorCommand.Execute(false);
            ((MenuItem)sheetPage.TextMenuItems(null)[4]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.IsTrue(page.EditsDefinition);
            page.AnswerEditorCommand.Execute(false);
            UiThread.Until(() => !page.IsEditing);

            ((MenuItem)sheetPage.TextMenuItems(null)[2]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            UiThread.Until(() => page.Passages.Count == 3);
            Assert.AreEqual(ReadingText.PassageCreated, page.Notice);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_passage_menu_depends_on_its_state()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body + "\nUn autre mot.\n", passage: false);
        vault.Annotate("premier paragraphe");
        var opened = vault.App.Sheets.Open(vault.SheetId).Value;
        Assert.IsTrue(vault.App.Editing.Save(vault.SheetId, opened.Body.Replace("premier paragraphe", "premiers paragraphes", StringComparison.Ordinal), opened.Fingerprint, 1).Succeeded);
        var page = vault.Page();
        var test = new TestShell();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var (window, sheetPage) = ShowWide(test, page);
        try
        {
            var candidate = page.Passages.Single();
            var items = sheetPage.PassageMenuItems(page, candidate);
            Assert.AreEqual($"{ReadingText.ShowPassage}|{ReadingText.EditComment}|{ReadingText.ConfirmPosition}|{ReadingText.KeepWithoutPosition}|{ReadingText.DeletePassage}", Headers(items));

            items[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.AreEqual(candidate.Annotation.Id, page.ActivePassage);
            items[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.IsTrue(page.IsEditing);
            page.AnswerEditorCommand.Execute(false);
            items[^1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.IsTrue(page.IsDeleting);
            page.AnswerDeleteCommand.Execute(false);
            items[3].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            UiThread.Until(() => page.Passages.Single().IsLost);

            Assert.AreEqual($"{ReadingText.EditComment}|{ReadingText.DeletePassage}", Headers(sheetPage.PassageMenuItems(page, page.Passages.Single())));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_candidate_can_be_confirmed_from_its_menu()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        vault.Annotate("premier paragraphe");
        var opened = vault.App.Sheets.Open(vault.SheetId).Value;
        Assert.IsTrue(vault.App.Editing.Save(vault.SheetId, opened.Body.Replace("premier paragraphe", "premiers paragraphes", StringComparison.Ordinal), opened.Fingerprint, 1).Succeeded);
        var page = vault.Page();
        var test = new TestShell();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var (window, sheetPage) = ShowWide(test, page);
        try
        {
            sheetPage.PassageMenuItems(page, page.Passages.Single())[2].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            UiThread.Until(() => !page.Passages.Single().IsCandidate);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_H_key_highlights_the_selection_and_nothing_else_does()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        var page = vault.Page();
        var test = new TestShell();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var (window, _) = ShowWide(test, page);
        try
        {
            var viewer = Named<SheetDocumentViewer>(window, "SheetText");

            Assert.IsFalse(Press(viewer, Key.H), "Sans sélection, la touche ne fait rien.");
            Select(viewer, Position(viewer, "premier"));
            Assert.IsFalse(Press(viewer, Key.J), "Une autre touche ne fait rien.");
            Assert.IsTrue(Press(viewer, Key.H));

            UiThread.Until(() => page.Passages.Count == 1);
            Assert.AreEqual("premier", page.Passages.Single().Title);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Clicking_a_highlight_or_its_entry_makes_it_stand_out_and_right_clicking_opens_a_menu()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body + "\nUn autre mot.\n", passage: true);
        var page = vault.Page();
        var test = new TestShell();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var (window, _) = ShowWide(test, page);
        try
        {
            var viewer = Named<SheetDocumentViewer>(window, "SheetText");
            var marked = Runs(viewer).First(run => run.Background is not null);

            viewer.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Source = marked });
            Assert.AreEqual(page.Passages.Single(passage => passage.Title == "autre").Annotation.Id, page.ActivePassage);

            var entries = Descendants(Named<ContentControl>(window, "PassagesPane")).OfType<Button>().ToList();
            var definition = entries.Single(entry => ((PassageEntry)entry.DataContext).IsDefinition);
            definition.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.AreEqual(((PassageEntry)definition.DataContext).Annotation.Id, page.ActivePassage);
            Assert.IsTrue(((PassageEntry)definition.DataContext).IsActive);

            definition.RaiseEvent(ContextMenuOpening(definition));
            Assert.IsNotEmpty(definition.ContextMenu!.Items);

            var opening = ContextMenuOpening(viewer);
            viewer.RaiseEvent(opening);
            Assert.IsTrue(opening.Handled, "Rien à proposer : le menu ne s'ouvre pas.");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_dialogs_of_the_page_answer_the_view_model()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body + "\nUn autre mot.\n", passage: true);
        var page = vault.Page();
        var test = new TestShell();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var (window, _) = ShowWide(test, page);
        try
        {
            page.StartAnnotatingCommand.Execute(new TextPosition(0, 3));
            TestShell.Flush();
            Named<Dialog>(window, "PassageEditor").Close(DialogChoice.None);
            Assert.IsFalse(page.IsEditing);

            page.RequestDeleteCommand.Execute(page.Passages[0]);
            TestShell.Flush();
            Named<Dialog>(window, "PassageDelete").Close(DialogChoice.None);
            UiThread.Until(() => !page.IsDeleting);
            Assert.HasCount(2, page.Passages);
        }
        finally
        {
            window.Close();
        }
    }

    private static ContextMenuEventArgs ContextMenuOpening(object source)
    {
        var arguments = (ContextMenuEventArgs)Activator.CreateInstance(typeof(ContextMenuEventArgs), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, [source, true], null)!;
        arguments.RoutedEvent = FrameworkElement.ContextMenuOpeningEvent;
        return arguments;
    }

    private static bool Press(UIElement target, Key key)
    {
        var arguments = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = UIElement.PreviewKeyDownEvent };
        target.RaiseEvent(arguments);
        return arguments.Handled;
    }

    private static string Headers(IReadOnlyList<Control> items) =>
        string.Join('|', items.OfType<MenuItem>().Select(item => item.Header));

    private static Range Range(TextPosition position) => position.Start..position.End;

    private static TextPosition PositionOf(SheetPageViewModel page, string exact)
    {
        var start = page.Reading!.Map.Text.IndexOf(exact, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, exact);
        return new TextPosition(start, start + exact.Length);
    }

    private static TextPosition Position(SheetDocumentViewer viewer, string exact)
    {
        var start = viewer.Reading!.Map.Text.IndexOf(exact, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, exact);
        return new TextPosition(start, start + exact.Length);
    }

    private static void Select(SheetDocumentViewer viewer, TextPosition position) =>
        viewer.Selection.Select(PointerOf(viewer, position.Start), PointerOf(viewer, position.End));

    /// <summary>Un endroit du document pour une position du texte affiché : le premier morceau qui la contient.</summary>
    private static TextPointer PointerOf(SheetDocumentViewer viewer, int position)
    {
        viewer.Show(position);
        var text = viewer.Reading!.Map.Text;
        var offset = 0;
        foreach (var run in Runs(viewer))
        {
            var found = text.IndexOf(run.Text.Length > 0 && text.AsSpan(offset).StartsWith(run.Text) ? run.Text : run.Text, offset, StringComparison.Ordinal);
            if (found >= 0)
            {
                if (position >= found && position <= found + run.Text.Length)
                {
                    return run.ContentStart.GetPositionAtOffset(position - found)!;
                }
                offset = found + run.Text.Length;
            }
        }
        throw new AssertFailedException($"Aucun morceau à la position {position}.");
    }

    private static List<Run> Runs(SheetDocumentViewer viewer) => [.. LogicalDescendants(viewer.Document).OfType<Run>().Where(run => run.Text.Length > 0)];

    private static string TextOf(SheetDocumentViewer viewer) => new TextRange(viewer.Document.ContentStart, viewer.Document.ContentEnd).Text;

    private static OpenedSheet Sheet(string body) => new(
        "f_1", "Mitose", "fiches/Biologie/Mitose.md", "Biologie", [], body, 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []);

    private static AnnotationView Annotation(Anchor anchor, AnchorState state) => new("p_1", null, null, anchor, state, false, null!);

    private static (Window Window, SheetDocumentViewer Viewer) Host(string body, bool flush = true)
    {
        var viewer = new SheetDocumentViewer { Reading = SheetReading.Of(Sheet(body), _ => Outcome.Refused<byte[]>("non")) };
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 860, Height = 700, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(new TestShell().Design.Dictionary);
        window.Show();
        window.UpdateLayout();
        if (flush)
        {
            TestShell.Flush();
        }
        return (window, viewer);
    }

    /// <summary>La page assez large pour ses volets, quel que soit l'écran (voir ReadingTests).</summary>
    private static (Window Window, SheetPage Page) ShowWide(TestShell test, object page)
    {
        var sheet = new SheetPage { DataContext = page, Width = 1700, Height = 900 };
        var window = new Window { Content = new Canvas { Children = { sheet } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        return (window, sheet);
    }

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
