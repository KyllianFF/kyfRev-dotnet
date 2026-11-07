using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Un mot défini dans le coffre se marque dans toutes les fiches qui le contiennent, hors révision.</summary>
[TestClass]
public sealed class DefinedWordsTests
{
    private const string Body = "La mitose partage le noyau de la cellule.\n\n## Suite\n\nChaque noyau fils reçoit les chromosomes.\n";

    [TestMethod]
    public async Task A_word_defined_in_another_sheet_is_marked_while_reading_never_while_revising()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var definition = vault.DefineElsewhere("Noyau", "Il renferme l'ADN.");
        var page = vault.Page(words: true);
        await page.LoadCommand.ExecuteAsync(null);

        var marks = page.Words!;
        Assert.HasCount(2, marks, "une fois par partie");
        Assert.AreEqual("noyau", page.Reading!.Map.Text[marks[0].Start..marks[0].End]);
        Assert.AreEqual((definition, "Noyau", "Il renferme l'ADN.", "La cellule"), (marks[0].DefinitionId, marks[0].Term, marks[0].Explanation, marks[0].SheetTitle));

        page.IsReviseOn = true;
        Assert.IsEmpty(page.Words!, "en révision, le mot donnerait la réponse");
        page.IsReadOn = true;
        Assert.HasCount(2, page.Words!);
    }

    [TestMethod]
    public async Task Without_the_setting_or_for_a_definition_kept_to_its_sheet_nothing_is_marked()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var definition = vault.DefineElsewhere("Noyau", "Il renferme l'ADN.");
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        Assert.IsEmpty(page.Words!, "le réglage de l'ordinateur l'a coupé");

        Assert.IsTrue(vault.App.Annotations.MarkEverywhere(definition, everywhere: false).Succeeded);
        var kept = vault.Page(words: true);
        await kept.LoadCommand.ExecuteAsync(null);

        Assert.IsEmpty(kept.Words!);
    }

    [TestMethod]
    public async Task A_marked_word_opens_its_sheet_on_the_definition_or_stops_being_marked()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var definition = vault.DefineElsewhere("Noyau", "Il renferme l'ADN.");
        var page = vault.Page(words: true);
        await page.LoadCommand.ExecuteAsync(null);
        var word = page.Words![0];

        page.OpenWordDefinitionCommand.Execute(word);
        Assert.AreEqual((vault.OtherSheet, definition), vault.OpenedPassages.Single());

        await page.StopMarkingWordCommand.ExecuteAsync(word);
        Assert.IsEmpty(page.Words!);
        Assert.AreEqual(ReadingText.WordNoLongerMarked("Noyau"), page.Notice);
        Assert.IsFalse(vault.App.Annotations.Definitions().Value.Single().Everywhere);
    }

    [TestMethod]
    public async Task A_word_defined_in_this_sheet_is_not_marked_on_its_own_place_and_points_to_it()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var definition = vault.DefineHere("noyau", "Noyau", "Il renferme l'ADN.");
        var page = vault.Page(words: true);
        await page.LoadCommand.ExecuteAsync(null);

        var word = page.Words!.Single();
        Assert.IsGreaterThan(page.Reading!.Map.Text.IndexOf("Suite", StringComparison.Ordinal), word.Start, "le passage défini garde sa propre marque");

        page.OpenWordDefinitionCommand.Execute(word);
        Assert.AreEqual(definition, page.ActivePassage);
        Assert.IsEmpty(vault.OpenedPassages);
    }

    [STATestMethod]
    public void A_marked_word_is_dotted_and_shows_its_definition_on_hover()
    {
        using var vault = new ReadingVault(Body, passage: false);
        vault.DefineElsewhere("Noyau", "Il renferme l'ADN.");
        var reading = vault.Reading();
        var marks = reading.WordsOf(vault.App.Annotations.Definitions().Value, vault.SheetId);
        var viewer = new SheetDocumentViewer { Reading = reading, Words = marks };
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 860, Height = 700, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            TestShell.Flush();
            var runs = Runs(viewer.Document).Where(run => run.Text == "noyau").ToList();

            Assert.HasCount(2, runs, "le mot a son propre morceau de texte");
            Assert.IsInstanceOfType<ToolTip>(runs[0].ToolTip);
            Assert.HasCount(1, runs[0].TextDecorations);
            Assert.AreEqual(marks[0], viewer.WordAt(runs[0]));
            Assert.IsNull(viewer.WordAt(Runs(viewer.Document).First(run => run.Text.Contains("mitose", StringComparison.Ordinal))));

            viewer.Words = [];
            Assert.IsNull(runs[0].ToolTip);
            Assert.IsNull(viewer.WordAt(runs[0]));
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<Run> Runs(FlowDocument document) => LogicalDescendants(document).OfType<Run>();

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
}
