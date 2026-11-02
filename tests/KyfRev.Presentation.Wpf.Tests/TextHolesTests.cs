using System.Windows;
using System.Windows.Documents;
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

        Assert.AreEqual("passage:p2 gras:0 passage:p1 gras:1", string.Join(' ', holes.Select(hole => hole.Key)));
        Assert.AreEqual("p2", holes[0].PassageId);
        Assert.IsNull(holes[1].PassageId);
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
