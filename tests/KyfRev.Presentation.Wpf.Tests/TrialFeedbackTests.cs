using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Ce qu'un essai à la vraie souris a montré, et que les tests ne voyaient pas.</summary>
[TestClass]
public sealed class TrialFeedbackTests
{
    private const string Body = "# Mitose\n\nLa mitose donne **deux cellules filles** identiques et un paragraphe de plus.\n\nLa méiose produit **quatre cellules**.\n";

    [STATestMethod]
    public void A_click_on_a_hole_counts_where_it_was_pressed_the_text_keeps_the_mouse_afterwards()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        var (window, page, sheet) = Shown(vault, revising: true);
        try
        {
            Assert.AreEqual(0, page.Holes.RevealedCount);

            // Une vraie souris : l'appui sur le texte, puis le relâchement capté par le lecteur.
            sheet.TextPressed(Run(sheet, "deux cellules"));
            sheet.TextReleased(sheet.SheetText);
            Assert.AreEqual(0, page.Holes.RevealedCount, "Le texte ne change qu'après le relâchement : il doit d'abord rendre la souris.");
            TestShell.Flush();

            Assert.AreEqual(1, page.Holes.RevealedCount);

            // Un relâchement sans appui connu vise ce qu'il touche.
            sheet.TextReleased(Run(sheet, "quatre cellules"));
            TestShell.Flush();
            Assert.AreEqual(2, page.Holes.RevealedCount);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_click_on_a_masked_passage_shows_it()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        vault.Annotate("paragraphe");
        var (window, page, sheet) = Shown(vault, revising: true);
        try
        {
            Assert.AreEqual(3, page.Holes.All.Count);

            sheet.TextPressed(Run(sheet, "paragraphe"));
            sheet.TextReleased(sheet.SheetText);
            TestShell.Flush();

            Assert.AreEqual(1, page.Holes.RevealedCount);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Pulling_to_select_over_a_hole_is_not_a_click()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        var (window, page, sheet) = Shown(vault, revising: true);
        try
        {
            var run = Run(sheet, "deux cellules");
            sheet.TextPressed(run);
            sheet.SheetText.Selection.Select(run.ContentStart, run.ContentEnd);
            sheet.TextReleased(sheet.SheetText);
            TestShell.Flush();

            Assert.AreEqual(0, page.Holes.RevealedCount);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_click_on_a_highlighted_passage_makes_it_stand_out_in_the_panel()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        var passage = vault.Annotate("paragraphe");
        var (window, page, sheet) = Shown(vault, revising: false);
        try
        {
            sheet.TextPressed(Run(sheet, "paragraphe"));
            sheet.TextReleased(sheet.SheetText);
            TestShell.Flush();

            Assert.AreEqual(passage, page.ActivePassage);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_history_of_a_preview_stays_folded_until_asked()
    {
        using var ui = new UiThread();
        var time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        using var vaults = new TestVaults(AppMode.Development, time);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        vaults.App.Sheets.Create("Biologie", "Mitose", null).Value.ToString();
        var model = new SheetsPageViewModel(vaults.App.Sheets, vaults.App.Folders, vaults.App.Templates, new FakeOpener(), time, vaults.App.Reviews, vaults.App.Trash);
        model.RefreshCommand.Execute(null);
        UiThread.Until(() => model.Rows.Count > 0);
        var test = new TestShell();
        var page = new SheetsPage { DataContext = model };
        var window = Host(test, page, 1400, 800);
        try
        {
            model.PageWidth = 1400;
            model.SelectedRow = model.Rows.OfType<SheetRow>().Single();
            UiThread.Until(() => Descendants(window).OfType<ToggleButton>().Any(button => button.Name == "HistoryToggle"));
            TestShell.Flush();
            var toggle = Descendants(window).OfType<ToggleButton>().Single(button => button.Name == "HistoryToggle");
            var history = Descendants(window).OfType<ScrollViewer>().Single(viewer => viewer.Name == "PreviewHistory");

            Assert.AreEqual(Visibility.Visible, toggle.Visibility);
            Assert.AreEqual(Visibility.Collapsed, history.Visibility, "Le texte passe avant.");
            Assert.AreEqual(DashboardText.HistoryCountOf(1), ((TodayPreview)model.Preview!).HistoryLabel);

            toggle.IsChecked = true;
            TestShell.Flush();

            Assert.AreEqual(Visibility.Visible, history.Visibility);
            Assert.AreEqual(96, history.MaxHeight, "Déplié, il reste court.");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void An_archive_reacts_on_its_whole_line_and_a_click_outside_its_buttons_opens_it()
    {
        using var ui = new UiThread();
        var time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        using var vaults = new TestVaults(AppMode.Development, time);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        var sheet = vaults.App.Sheets.Create("Biologie", "Mitose", null).Value;
        vaults.App.Reviews.Archive(sheet).Value.ToString();
        var opener = new FakeOpener();
        var model = new ArchivesPageViewModel(vaults.App.Reminders, vaults.App.Reviews, opener, time);
        model.OnNavigatedTo();
        UiThread.Until(() => model.Rows.Count > 0);
        var test = new TestShell();
        var page = new ArchivesPage { DataContext = model };
        var window = Host(test, page, 1400, 600);
        try
        {
            var line = Descendants(window).OfType<Border>().Single(border => border.Name == "RowHover");
            var side = Descendants(window).OfType<StackPanel>().Single(panel => panel.Name == "Side");
            var open = Descendants(window).OfType<Button>().Single(button => button.Name == "OpenRow");

            Assert.AreEqual(Orientation.Horizontal, side.Orientation, "L'état et les actions, sur la ligne du titre.");
            Assert.IsGreaterThan(open.ActualWidth, line.ActualWidth, "La ligne couvre aussi les actions.");
            Assert.AreEqual(Brushes.Transparent, open.Background, "Le bouton ne dessine pas son propre survol.");
            var hover = line.Style.Triggers.OfType<Trigger>().Single();
            Assert.AreEqual(UIElement.IsMouseOverProperty, hover.Property);

            line.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });

            CollectionAssert.AreEqual(new object[] { sheet }, opener.Opened);

            page.DataContext = null;
            line.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            Assert.HasCount(1, opener.Opened);
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, SheetPageViewModel Page, SheetPage Sheet) Shown(ReadingVault vault, bool revising)
    {
        var page = vault.Page(markdown: true, revising: revising, activeRecall: () => true);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var sheet = new SheetPage { DataContext = page };
        var window = Host(new TestShell(), sheet, 1200, 800);
        UiThread.Until(() => Runs(sheet.SheetText.Document).Any());
        TestShell.Flush();
        return (window, page, sheet);
    }

    private static Window Host(TestShell test, FrameworkElement page, double width, double height)
    {
        page.Width = width;
        page.Height = height;
        var window = new Window { Content = new Canvas { Children = { page } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        return window;
    }

    private static Run Run(SheetPage sheet, string text) =>
        Runs(sheet.SheetText.Document).First(run => run.Text.Contains(text, StringComparison.Ordinal));

    private static IEnumerable<Run> Runs(FlowDocument? document)
    {
        if (document is null)
        {
            yield break;
        }
        var pending = new Stack<object>();
        pending.Push(document);
        while (pending.TryPop(out var item))
        {
            if (item is Run run)
            {
                yield return run;
            }
            if (item is DependencyObject parent)
            {
                foreach (var child in LogicalTreeHelper.GetChildren(parent))
                {
                    pending.Push(child);
                }
            }
        }
    }

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
