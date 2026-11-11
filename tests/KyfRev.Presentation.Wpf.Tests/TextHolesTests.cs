using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le texte à trous d'une fiche en révision.</summary>
[TestClass]
public sealed class TextHolesTests
{
    private const string Body = "Un **mot** et un *autre* **mot `code`** ici.";

    [TestMethod]
    public void Passages_and_bold_are_holes_in_the_order_of_the_text()
    {
        var highlights = new List<Highlight> { new("p1", 30, 40, false, false), new("p2", 2, 4, true, false), new("incertain", 50, 60, false, true) };

        var holes = TextHoles.Compute(highlights, [(10, 15), (32, 36), (45, 48)]);

        Assert.AreEqual("passage:p2 gras:0 passage:p1 gras:2", string.Join(' ', holes.Select(hole => hole.Key)));
        Assert.AreEqual("p2", holes[0].PassageId);
        Assert.IsNull(holes[1].PassageId);
    }

    [TestMethod]
    public void A_sheet_without_bold_or_passage_is_recalled_part_by_part()
    {
        // « Intro\nTitre A\nCorps A\nTitre B\nTitre C\nCorps C » : B n'a pas de corps.
        var sections = TextHoles.Sections([(6, 13), (22, 29), (30, 37)], 45);

        Assert.AreEqual("partie:0 partie:2", string.Join(' ', sections.Select(hole => hole.Key)));
        Assert.AreEqual((13, 22), (sections[0].Start, sections[0].End));
        Assert.AreEqual((37, 45), (sections[1].Start, sections[1].End));
        Assert.IsTrue(sections[0].IsSection);
        Assert.IsEmpty(TextHoles.Sections([], 45));

        var state = new TextHoles { IsActive = true };
        state.Set(sections);
        Assert.IsTrue(state.IsBySection);
        Assert.AreEqual("Rappelez-vous chaque partie avant de l'afficher (Entrée) : 0 sur 2 affichées.", state.Summary);
        Assert.AreEqual("partie:0", state.RevealNext()!.Key);
        Assert.AreEqual("Rappelez-vous chaque partie avant de l'afficher (Entrée) : 1 sur 2 affichées.", state.Summary);

        state.Set(TextHoles.Compute([], [(10, 12)]));
        Assert.IsFalse(state.IsBySection);
    }

    [TestMethod]
    public void Revealing_counts_and_what_is_revealed_stays_when_the_text_changes()
    {
        var state = new TextHoles { IsActive = true };
        state.Set(TextHoles.Compute([new("p", 0, 5, false, false)], [(10, 12), (20, 22)]));
        Assert.IsTrue(state.HasHoles);
        Assert.AreEqual("Texte à trous : 0 sur 3 affichés.", state.Summary);
        Assert.IsTrue(state.CanRevealAll);
        Assert.IsFalse(state.CanHideAll);
        Assert.IsTrue(state.Masks("p"));

        state.Reveal("passage:p");
        Assert.AreEqual(1, state.RevealedCount);
        Assert.IsFalse(state.Masks("p"));
        state.Set(TextHoles.Compute([new("p", 0, 5, false, false)], [(10, 12), (20, 22)]));
        Assert.AreEqual(1, state.RevealedCount);

        Assert.AreEqual("gras:0", state.RevealNext()!.Key);
        state.RevealAllCommand.Execute(null);
        Assert.AreEqual(3, state.RevealedCount);
        Assert.IsFalse(state.CanRevealAll);
        Assert.IsNull(state.RevealNext());
        Assert.IsEmpty(state.Hidden);

        state.HideAllCommand.Execute(null);
        Assert.HasCount(3, state.Hidden);

        state.Reveal("passage:p");
        state.IsActive = false;
        Assert.IsEmpty(state.Hidden);
        Assert.IsFalse(state.HasHoles);
        state.IsActive = true;
        Assert.AreEqual(0, state.RevealedCount, "Chaque révision repart avec tous ses trous.");
    }

    [STATestMethod]
    public void The_text_is_hidden_keeps_its_place_and_comes_back_on_a_click()
    {
        var (window, viewer, holes) = Host();
        try
        {
            var bold = LogicalRuns(viewer).Single(run => run.Text == "mot");
            var ordinary = LogicalRuns(viewer).Single(run => run.Text.StartsWith("Un ", StringComparison.Ordinal));
            Assert.HasCount(2, holes.All);
            Assert.AreEqual(bold.Background, bold.Foreground, "Le texte a la couleur du fond : il ne se lit pas.");
            Assert.AreNotEqual(ordinary.Background, ordinary.Foreground);
            Assert.AreEqual("mot", bold.Text, "Le texte garde sa place.");

            Assert.IsFalse(viewer.RevealHoleAt(ordinary));
            Assert.IsFalse(viewer.RevealHoleAt("autre chose"));
            Assert.IsTrue(viewer.RevealHoleAt(bold));
            Assert.AreEqual(1, holes.RevealedCount);
            Assert.AreNotEqual(bold.Background, bold.Foreground);
            Assert.IsFalse(viewer.RevealHoleAt(bold), "Déjà affiché : plus un trou.");

            Assert.IsTrue(viewer.RevealNextHole());
            Assert.IsFalse(viewer.RevealNextHole());
            Assert.AreEqual(2, holes.RevealedCount);

            holes.HideAllCommand.Execute(null);
            Assert.AreEqual(bold.Background, bold.Foreground);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Bold_inside_bold_is_one_hole_and_a_formula_is_hidden_with_it()
    {
        var (window, viewer, holes) = Host("**a *b* c** $x^2$");
        try
        {
            Assert.HasCount(1, holes.All);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_formula_in_a_passage_is_hidden_and_comes_back()
    {
        var (window, viewer, holes) = Host("Une $x^2$ ici.", read =>
        {
            var position = read.Map.Text.IndexOf("x^2", StringComparison.Ordinal);
            return [new Highlight("p", position, position + 3, false, false)];
        });
        try
        {
            TestShell.Flush();
            var slot = LogicalChildrenOfType<System.Windows.Controls.Border>(viewer).First();
            Assert.AreEqual(Visibility.Hidden, slot.Child.Visibility);
            Assert.IsTrue(viewer.RevealHoleAt(slot));
            Assert.AreEqual(Visibility.Visible, slot.Child.Visibility);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Inactive_holes_hide_nothing_and_no_key_reveals_anything()
    {
        var (window, viewer, holes) = Host();
        try
        {
            holes.IsActive = false;
            var bold = LogicalRuns(viewer).Single(run => run.Text == "mot");
            Assert.AreNotEqual(bold.Background, bold.Foreground);
            Assert.IsFalse(viewer.RevealNextHole());
            Assert.IsFalse(viewer.RevealHoleAt(bold));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_page_revises_with_holes_only_if_active_recall_is_chosen()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Un **mot** et un autre ici.", passage: true);
        var page = vault.Page(revising: true, activeRecall: () => true);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        Assert.IsTrue(page.IsRevising);
        Assert.IsTrue(page.IsReviseOn);
        Assert.IsFalse(page.IsReadOn);
        Assert.IsTrue(page.Holes.IsActive);

        page.IsReadOn = true;
        Assert.IsFalse(page.IsRevising);
        Assert.IsFalse(page.Holes.IsActive);
        Assert.IsTrue(page.IsReadOn);

        page.IsReviseOn = true;
        Assert.IsTrue(page.Holes.IsActive);

        using var plain = new ReadingVault("Un **mot** ici.", passage: false);
        var without = plain.Page(revising: true, activeRecall: () => false);
        Assert.IsTrue(without.IsRevising);
        Assert.IsFalse(without.Holes.IsActive);
        without.IsReadOn = false;
        without.IsReviseOn = false;
        Assert.IsTrue(without.IsRevising, "Décocher le mode en cours ne change rien.");
    }

    [STATestMethod]
    public void The_panel_keeps_quiet_about_what_is_still_hidden()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Une autre largeur ici.", passage: true);
        var page = vault.Page(revising: true, activeRecall: () => true);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var entry = page.Passages[0];
        var title = entry.Title;

        page.Holes.Set(TextHoles.Compute([new(entry.Annotation.Id, 0, 2, false, false)], []));
        Assert.IsTrue(entry.IsMasked);
        Assert.AreEqual(ReadingText.PassageToFind, entry.ShownTitle);
        Assert.IsNull(entry.ShownDetail);
        Assert.IsFalse(entry.HasShownDetail);

        page.Holes.RevealAllCommand.Execute(null);
        Assert.IsFalse(entry.IsMasked);
        Assert.AreEqual(title, entry.ShownTitle);
    }

    [STATestMethod]
    public void A_click_beside_the_text_of_a_sheet_with_a_formula_stays_harmless()
    {
        var (window, viewer, _) = Host("Une $x^2$ formule.\n\nUn paragraphe.\n");
        try
        {
            // Un clic à droite d'une ligne vise le paragraphe, un élément du document qui n'est pas visuel.
            var paragraph = viewer.Document.Blocks.OfType<Paragraph>().First();

            Assert.IsEmpty(viewer.AnnotationsAt(paragraph));
            Assert.IsEmpty(viewer.AnnotationsAt(viewer.Document));
            Assert.IsFalse(viewer.RevealHoleAt(paragraph));
            Assert.IsFalse(viewer.RevealHoleAt(viewer.Document));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Going_from_reading_to_revising_makes_the_holes()
    {
        var (window, viewer, holes) = Host();
        try
        {
            holes.IsActive = false;
            viewer.Reading = viewer.Reading;
            TestShell.Flush();
            Assert.IsEmpty(holes.Hidden);

            holes.IsActive = true;
            TestShell.Flush();

            Assert.IsTrue(holes.HasHoles, "le texte devient à trous sans attendre");
            Assert.IsNotEmpty(holes.Hidden);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Without_bold_each_part_is_hidden_under_its_title_and_comes_back_in_order()
    {
        var (window, viewer, holes) = Host("Une entrée en matière.\n\n## Prophase\n\nLes chromosomes se condensent.\n\n- un point\n\n## Métaphase\n\nIls s'alignent.\n");
        try
        {
            static bool IsHidden(Run run) => Equals(run.Background, run.Foreground);
            Run Find(string start) => LogicalRuns(viewer).Single(run => run.Text.StartsWith(start, StringComparison.Ordinal));

            Assert.IsTrue(holes.IsBySection);
            Assert.HasCount(2, holes.All);
            Assert.IsFalse(IsHidden(Find("Une entrée")), "Ce qui précède le premier titre reste lisible.");
            Assert.IsFalse(IsHidden(Find("Prophase")), "Les titres sont les indices.");
            Assert.IsFalse(IsHidden(Find("Métaphase")));
            Assert.IsTrue(IsHidden(Find("Les chromosomes")));
            Assert.IsTrue(IsHidden(Find("un point")));
            Assert.IsTrue(IsHidden(Find("Ils s'alignent")));

            Assert.IsTrue(viewer.RevealNextHole());
            Assert.IsFalse(IsHidden(Find("Les chromosomes")));
            Assert.IsFalse(IsHidden(Find("un point")), "Toute la partie revient d'un coup.");
            Assert.IsTrue(IsHidden(Find("Ils s'alignent")));

            Assert.IsTrue(viewer.RevealHoleAt(Find("Ils s'alignent")));
            Assert.IsFalse(viewer.RevealNextHole());
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void With_bold_the_parts_stay_readable()
    {
        var (window, _, holes) = Host("## Prophase\n\nLes **chromosomes** se condensent.\n");
        try
        {
            Assert.IsFalse(holes.IsBySection);
            Assert.AreEqual("gras:0", string.Join(' ', holes.All.Select(hole => hole.Key)));
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void The_line_under_the_title_says_the_creation_the_cycle_and_the_reading_time()
    {
        static string Details(string folder, string body, DateOnly? created, int? cycle)
        {
            var review = new KyfRev.Application.Model.ReviewState(KyfRev.Application.Model.ReviewStatus.Active, [], "J1", new DateOnly(2026, 10, 2), cycle, null, 0, false, null, false, false, null, false, cycle ?? 1, 0);
            var sheet = new KyfRev.Application.Model.OpenedSheet("f_1", "Mitose", "fiches/Mitose.md", folder, ["bio"], body, 1, "sha256:essai", created, DateTimeOffset.UnixEpoch, review, []);
            return SheetReading.Of(sheet, _ => Outcome.Refused<byte[]>("non")).Details;
        }
        var longText = string.Join(' ', Enumerable.Repeat("mot", 500));

        Assert.AreEqual("#bio", Details(string.Empty, "Court.", null, 1), "ni date, ni premier cycle, ni temps sous une minute");
        Assert.AreEqual("Biologie  ·  #bio  ·  créée le 3 octobre 2026  ·  cycle 2  ·  3 min de lecture", Details("Biologie", longText, new DateOnly(2026, 10, 3), 2));
        Assert.AreEqual("#bio  ·  1 min de lecture", Details(string.Empty, string.Join(' ', Enumerable.Repeat("mot", 200)), null, null));
    }

    [TestMethod]
    public void A_new_passage_does_not_change_which_bold_is_revealed()
    {
        (int, int)[] bold = [(0, 5), (10, 15), (20, 25)];
        var before = TextHoles.Compute([], bold);
        var after = TextHoles.Compute([new Highlight("p", 0, 6, false, false)], bold);

        Assert.AreEqual(before.Single(hole => hole.Start == 10).Key, after.Single(hole => hole.Start == 10).Key);
        Assert.AreEqual(before.Single(hole => hole.Start == 20).Key, after.Single(hole => hole.Start == 20).Key);
    }

    [STATestMethod]
    public void Revising_puts_the_keys_on_the_text_and_writing_hides_the_bars()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("# Mitose\n\nUn **autre** paragraphe avec un mot.\n", passage: true);
        var page = vault.Page(activeRecall: () => true);
        var test = new TestShell();
        var window = new Window { ShowActivated = false, Left = -10000, Top = -10000, Width = 1000, Height = 700, WindowStartupLocation = WindowStartupLocation.Manual, Content = new SheetPage { DataContext = page } };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            page.LoadCommand.Execute(null);
            UiThread.Until(() => page.Reading is not null && !page.IsLoading);
            TestShell.Flush();
            var bars = new List<string>();
            page.PropertyChanged += (_, e) => bars.Add(e.PropertyName ?? string.Empty);

            page.IsReviseOn = true;
            TestShell.Flush();

            var viewer = Named<SheetDocumentViewer>(window, "SheetText");
            Assert.AreSame(viewer, FocusManager.GetFocusedElement(FocusManager.GetFocusScope(viewer)), "Entrée et Espace vont au texte");
            Assert.IsTrue(page.Holes.HasHoles);
            var holesBar = Named<DockPanel>(window, "HolesBar");
            Assert.IsTrue(holesBar.IsVisible);

            page.StartWritingCommand.Execute(null);
            UiThread.Until(() => page.Editor is not null);
            TestShell.Flush();

            Assert.IsFalse(holesBar.IsVisible, "la barre des trous ne passe pas sur le ruban");
            Assert.IsFalse(page.IsReviseOn);
            CollectionAssert.Contains(bars, nameof(SheetPageViewModel.IsReviseOn), "le bouton « Réviser » suit le mode");
            page.StopWritingCommand.Execute(null);
        }
        finally
        {
            window.Close();
        }
    }

    private static T Named<T>(DependencyObject root, string name)
        where T : FrameworkElement =>
        Descendants(root).OfType<T>().First(element => element.Name == name);

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

    private static IEnumerable<Run> LogicalRuns(SheetDocumentViewer viewer) => LogicalChildrenOfType<Run>(viewer);

    private static IEnumerable<T> LogicalChildrenOfType<T>(SheetDocumentViewer viewer)
        where T : DependencyObject
    {
        static IEnumerable<DependencyObject> Walk(DependencyObject root) =>
            LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().SelectMany(child => new[] { child }.Concat(Walk(child)));
        return Walk(viewer.Document!).OfType<T>();
    }

    private static (Window Window, SheetDocumentViewer Viewer, TextHoles Holes) Host(string body = Body, Func<SheetReading, IReadOnlyList<Highlight>>? highlights = null)
    {
        var test = new TestShell();
        var reading = SheetReading.Of(new KyfRev.Application.Model.OpenedSheet("f_1", "Mitose", "fiches/Biologie/Mitose.md", "Biologie", [], body, 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []), _ => Outcome.Refused<byte[]>("non"));
        var holes = new TextHoles { IsActive = true };
        var viewer = new SheetDocumentViewer { Holes = holes };
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 860, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        viewer.Highlights = highlights?.Invoke(reading);
        viewer.Reading = reading;
        TestShell.Flush();
        return (window, viewer, holes);
    }
}
