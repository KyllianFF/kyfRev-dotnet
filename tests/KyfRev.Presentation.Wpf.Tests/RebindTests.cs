using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Rattacher un passage à un autre texte, et la corbeille du volet, sur un vrai coffre.</summary>
[TestClass]
public sealed class RebindTests
{
    private const string Body = "# Mitose\n\nUn autre paragraphe avec un mot à définir, et une nouvelle phrase.\n";

    [TestMethod]
    public async Task A_passage_changes_place_and_keeps_its_progress()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var id = vault.Annotate("autre paragraphe");
        var page = await LoadedAsync(vault);
        var entry = page.Passages.Single();
        var before = entry.Annotation.Review;

        page.StartRebindingCommand.Execute(entry);
        Assert.IsTrue(page.IsRebinding);
        StringAssert.Contains(page.RebindingPrompt, "autre paragraphe", StringComparison.Ordinal);
        Assert.IsFalse(page.IsConfirmingRebinding);

        page.RebindAtCommand.Execute(PositionOf(page, "nouvelle phrase"));
        Assert.IsTrue(page.IsConfirmingRebinding);
        StringAssert.Contains(page.RebindingMessage, "nouvelle phrase", StringComparison.Ordinal);

        await page.AnswerRebindingCommand.ExecuteAsync(false);

        Assert.IsFalse(page.IsRebinding);
        Assert.IsFalse(page.IsConfirmingRebinding);
        Assert.AreEqual(ReadingText.RebindKept, page.Notice);
        var moved = page.Passages.Single(passage => passage.Annotation.Id == id);
        Assert.AreEqual("nouvelle phrase", moved.Annotation.Anchor.Exact);
        Assert.AreEqual(before.Steps.Length, moved.Annotation.Review.Steps.Length, "la progression reste");
        Assert.AreEqual(AnchorState.Anchored, moved.State);
    }

    [TestMethod]
    public async Task A_passage_can_start_again_from_J0()
    {
        using var vault = new ReadingVault(Body, passage: false);
        vault.Annotate("autre paragraphe");
        var page = await LoadedAsync(vault);

        page.StartRebindingCommand.Execute(page.Passages.Single());
        page.RebindAtCommand.Execute(PositionOf(page, "nouvelle phrase"));
        await page.AnswerRebindingCommand.ExecuteAsync(true);

        Assert.AreEqual(ReadingText.RebindRestarted, page.Notice);
        Assert.AreEqual("nouvelle phrase", page.Passages.Single().Annotation.Anchor.Exact);
    }

    [TestMethod]
    public async Task A_lost_passage_finds_a_place_again()
    {
        using var vault = new ReadingVault(Body, passage: false);
        vault.Annotate("autre paragraphe");
        Lose(vault);
        var page = await LoadedAsync(vault);
        var entry = page.Passages.Single();
        Assert.IsTrue(entry.IsLost);

        page.StartRebindingCommand.Execute(entry);
        page.RebindAtCommand.Execute(PositionOf(page, "nouvelle phrase"));
        await page.AnswerRebindingCommand.ExecuteAsync(false);

        Assert.IsFalse(page.Passages.Single().IsLost);
    }

    [TestMethod]
    public async Task Leaving_the_confirmation_lets_one_choose_again()
    {
        using var vault = new ReadingVault(Body, passage: false);
        vault.Annotate("autre paragraphe");
        var page = await LoadedAsync(vault);
        page.StartRebindingCommand.Execute(page.Passages.Single());
        page.RebindAtCommand.Execute(PositionOf(page, "nouvelle phrase"));

        await page.AnswerRebindingCommand.ExecuteAsync(null);

        Assert.IsTrue(page.IsRebinding, "on peut choisir autre chose");
        Assert.IsFalse(page.IsConfirmingRebinding);
        Assert.AreEqual("autre paragraphe", page.Passages.Single().Annotation.Anchor.Exact);

        page.CancelRebindingCommand.Execute(null);
        Assert.IsFalse(page.IsRebinding);
        Assert.AreEqual(string.Empty, page.RebindingPrompt);
        Assert.AreEqual(string.Empty, page.RebindingMessage);
    }

    [TestMethod]
    public async Task A_position_means_nothing_when_no_passage_waits()
    {
        using var vault = new ReadingVault(Body, passage: false);
        vault.Annotate("autre paragraphe");
        var page = await LoadedAsync(vault);

        page.RebindAtCommand.Execute(PositionOf(page, "nouvelle phrase"));

        Assert.IsFalse(page.IsConfirmingRebinding);
    }

    [TestMethod]
    public async Task Escape_gives_up_waiting()
    {
        using var vault = new ReadingVault(Body, passage: false);
        vault.Annotate("autre paragraphe");
        var page = await LoadedAsync(vault);
        page.StartRebindingCommand.Execute(page.Passages.Single());

        page.CloseOverlayCommand.Execute(null);

        Assert.IsFalse(page.IsRebinding);
    }

    [TestMethod]
    public async Task What_goes_to_the_trash_comes_back_from_the_panel()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var passage = vault.Annotate("autre paragraphe");
        var page = await LoadedAsync(vault);
        Assert.IsFalse(page.HasTrashedPassages);

        page.RequestDeleteCommand.Execute(page.Passages.Single());
        await page.AnswerDeleteCommand.ExecuteAsync(true);

        Assert.IsEmpty(page.Passages);
        Assert.IsTrue(page.HasTrashedPassages);
        Assert.IsTrue(page.HasPassages, "le volet reste : sa corbeille est là");
        Assert.AreEqual("Corbeille · 1", page.TrashLabel);
        var trashed = page.TrashedPassages.Single();
        Assert.AreEqual(passage, trashed.Id);
        Assert.AreEqual("« autre paragraphe »", trashed.Label);

        await page.RestorePassageCommand.ExecuteAsync(trashed);

        Assert.AreEqual(ReadingText.Restored, page.Notice);
        Assert.AreEqual(passage, page.Passages.Single().Annotation.Id);
        Assert.IsFalse(page.HasTrashedPassages);
    }

    [TestMethod]
    public void The_trash_names_what_it_holds_in_short()
    {
        Assert.AreEqual("Définition : Noyau", ReadingText.TrashedDefinition("Noyau"));
        var long_text = new string('a', 200);
        var label = ReadingText.TrashedPassage(long_text);
        Assert.IsLessThan(70, label.Length);
        StringAssert.EndsWith(label, "… »", StringComparison.Ordinal);
    }

    [STATestMethod]
    public void The_page_offers_it_from_the_menu_the_bar_and_the_dialog()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        vault.Annotate("autre paragraphe");
        var page = vault.Page();
        var test = new TestShell();
        test.Shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, page));
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 1000, Height = 760, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            page.LoadCommand.Execute(null);
            UiThread.Until(() => page.Reading is not null && !page.IsLoading);
            TestShell.Flush();
            var view = Descendants(window).OfType<SheetPage>().Single();
            var entry = page.Passages.Single();

            var menu = view.PassageMenuItems(page, entry).OfType<MenuItem>().ToList();
            var modify = menu.Single(item => (string)item.Header == ReadingText.RebindOther);
            modify.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            TestShell.Flush();
            Assert.IsTrue(page.IsRebinding);
            var bar = Named<DockPanel>(window, "RebindBar");
            Assert.AreEqual(Visibility.Visible, bar.Visibility);

            var here = Named<Button>(window, "RebindHereButton");
            here.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.AreEqual(ReadingText.RebindNoSelection, page.Notice, "sans sélection, on le rappelle");
            Assert.IsFalse(page.IsConfirmingRebinding);

            var viewer = Named<SheetDocumentViewer>(window, "SheetText");
            var range = RangeOf(viewer, "nouvelle phrase");
            viewer.Selection!.Select(range.Start, range.End);
            var textMenu = view.TextMenuItems(null).OfType<MenuItem>().ToList();
            Assert.AreEqual(ReadingText.RebindHere, textMenu[0].Header);
            Assert.AreEqual(ReadingText.RebindCancel, textMenu[1].Header);

            here.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            TestShell.Flush();
            Assert.IsTrue(page.IsConfirmingRebinding);
            var dialog = Named<Dialog>(window, "RebindConfirm");
            Assert.IsTrue(dialog.IsOpen);

            Named<Button>(window, "RebindConfirmCancel").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            TestShell.Flush();
            Assert.IsFalse(page.IsConfirmingRebinding);
            Assert.IsTrue(page.IsRebinding, "« Annuler » revient au choix");

            page.RebindAtCommand.Execute(PositionOf(page, "nouvelle phrase"));
            TestShell.Flush();
            dialog.Close(DialogChoice.Secondary);
            UiThread.Until(() => page.Notice == ReadingText.RebindRestarted);

            textMenu = [.. view.TextMenuItems(null).OfType<MenuItem>()];
            Assert.AreNotEqual(ReadingText.RebindHere, textMenu[0].Header, "hors rattachement, le menu redevient ordinaire");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void On_a_narrow_page_no_button_slips_under_another()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        vault.Annotate("autre paragraphe");
        var page = vault.Page();
        var test = new TestShell();
        var host = new Border { Width = 374, Height = 760, Child = new SheetPage { DataContext = page } };
        var window = new Window { ShowActivated = false, Left = -10000, Top = -10000, Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.Manual, Content = host };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            page.LoadCommand.Execute(null);
            UiThread.Until(() => page.Reading is not null && !page.IsLoading);
            page.StartRebindingCommand.Execute(page.Passages.Single());
            TestShell.Flush();

            var back = Named<Button>(window, "BackButton");
            var limit = back.TransformToAncestor(host).Transform(default).X + back.ActualWidth;
            foreach (var name in new[] { "ReadToggle", "ReviseToggle", "WriteToggle" })
            {
                Assert.IsGreaterThanOrEqualTo(limit, Named<ToggleButton>(window, name).TransformToAncestor(host).Transform(default).X, name);
            }
            var prompt = Named<TextBlock>(window, "RebindPrompt");
            Assert.IsGreaterThan(300, prompt.ActualWidth, "la consigne garde la largeur");
            Assert.IsLessThan(64, Named<Button>(window, "RebindHereButton").ActualHeight, "le bouton ne s'étire pas");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_lost_passage_is_offered_another_place_in_its_menu()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        vault.Annotate("autre paragraphe");
        Lose(vault);
        var page = vault.Page();
        var view = new SheetPage { DataContext = page };
        page.LoadCommand.Execute(null);
        UiThread.Until(() => page.Reading is not null && !page.IsLoading);

        var headers = view.PassageMenuItems(page, page.Passages.Single()).OfType<MenuItem>().Select(item => (string)item.Header).ToList();

        CollectionAssert.Contains(headers, ReadingText.RebindLost);
        CollectionAssert.DoesNotContain(headers, ReadingText.RebindOther);
    }

    /// <summary>Le texte du passage quitte la fiche : il n'a plus de place.</summary>
    private static void Lose(ReadingVault vault) =>
        Assert.IsTrue(vault.App.Editing.Save(vault.SheetId, "# Mitose\n\nUne tout autre rédaction, avec une nouvelle phrase.\n", vault.App.Sheets.Open(vault.SheetId).Value.Fingerprint, 2).Succeeded);

    private static async Task<SheetPageViewModel> LoadedAsync(ReadingVault vault)
    {
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        return page;
    }

    private static KyfRev.Rendering.TextPosition PositionOf(SheetPageViewModel page, string exact)
    {
        var start = page.Reading!.Map.Text.IndexOf(exact, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, exact);
        return new KyfRev.Rendering.TextPosition(start, start + exact.Length);
    }

    private static System.Windows.Documents.TextRange RangeOf(SheetDocumentViewer viewer, string exact)
    {
        var start = viewer.Document.ContentStart;
        for (var position = start; position is not null; position = position.GetNextContextPosition(System.Windows.Documents.LogicalDirection.Forward))
        {
            if (position.GetPointerContext(System.Windows.Documents.LogicalDirection.Forward) == System.Windows.Documents.TextPointerContext.Text)
            {
                var run = position.GetTextInRun(System.Windows.Documents.LogicalDirection.Forward);
                var index = run.IndexOf(exact, StringComparison.Ordinal);
                if (index >= 0)
                {
                    var from = position.GetPositionAtOffset(index)!;
                    return new System.Windows.Documents.TextRange(from, from.GetPositionAtOffset(exact.Length)!);
                }
            }
        }
        throw new AssertFailedException(exact);
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
}
