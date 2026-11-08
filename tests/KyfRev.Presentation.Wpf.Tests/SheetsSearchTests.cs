using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La recherche de la vue « Fiches » : dans le texte, avec un extrait ; et ce qui sert à la montrer.</summary>
[TestClass]
public sealed class SheetsSearchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [STATestMethod]
    public void A_word_of_the_text_finds_the_sheet_and_shows_the_excerpt()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Sheet("Biologie", "Mitose", "La prophase précède la métaphase.");
        scene.Sheet("Histoire", "Révolution", "Le clergé et la noblesse.");
        scene.Load();

        scene.Page.Bar.Search = "PROPHASE";
        UiThread.Until(() => scene.Rows.Count == 1 && scene.Rows[0].HasExcerpt);

        var row = scene.Rows.Single();
        Assert.AreEqual("Mitose", row.Title);
        Assert.AreEqual("La |prophase| précède la métaphase.", string.Join('|', row.Excerpt.Select(piece => piece.Text)));
        StringAssert.Contains(row.ToString(), "La prophase précède la métaphase.", StringComparison.Ordinal);
    }

    [STATestMethod]
    public void Until_the_answer_the_title_and_the_tags_are_enough()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Sheet("Biologie", "Mitose", "La prophase.");
        scene.Sheet("Histoire", "Révolution", "Le clergé.");
        scene.Load();

        scene.Page.Bar.Search = "mitose";

        Assert.AreEqual("Mitose", scene.Rows.Single().Title, "tout de suite, par le titre");
        Assert.IsFalse(scene.Rows.Single().HasExcerpt);
    }

    [STATestMethod]
    public void A_title_that_holds_the_words_has_no_excerpt()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Sheet("Biologie", "Mitose", "La mitose est une division.");
        scene.Load();

        scene.Page.Bar.Search = "mitose";
        var searching = scene.Page.SearchTextCommand.ExecutionTask!;
        UiThread.Until(() => searching.IsCompleted);

        Assert.IsFalse(scene.Rows.Single().HasExcerpt);
    }

    [STATestMethod]
    public void Emptying_the_search_forgets_its_answer()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Sheet("Biologie", "Mitose", "La prophase.");
        scene.Sheet("Histoire", "Révolution", "Le clergé.");
        scene.Load();
        scene.Page.Bar.Search = "prophase";
        UiThread.Until(() => scene.Rows.Count == 1 && scene.Rows[0].HasExcerpt);

        scene.Page.Bar.Search = "   ";

        Assert.HasCount(2, scene.Rows);
        Assert.IsTrue(scene.Rows.All(row => !row.HasExcerpt));
    }

    [STATestMethod]
    public void Only_the_last_search_is_shown()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Sheet("Biologie", "Mitose", "La prophase.");
        scene.Sheet("Histoire", "Révolution", "Le clergé.");
        scene.Load();

        scene.Page.Bar.Search = "prophase";
        var older = scene.Page.SearchTextCommand.ExecutionTask!;
        scene.Page.Bar.Search = "clergé";
        var newer = scene.Page.SearchTextCommand.ExecutionTask!;
        UiThread.Until(() => older.IsCompleted && newer.IsCompleted);

        Assert.AreEqual("Révolution", scene.Rows.Single().Title);
    }

    [STATestMethod]
    public void A_search_opens_the_folders_and_leaves_them_as_they_were_afterwards()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Sheet("Biologie", "Mitose", "La prophase.");
        scene.Load();
        scene.Page.ToggleFolderCommand.Execute(scene.Page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Biologie"));
        Assert.IsEmpty(scene.Rows, "le dossier est plié");

        scene.Page.Bar.Search = "prophase";
        UiThread.Until(() => scene.Rows.Count == 1 && scene.Rows[0].HasExcerpt);

        scene.Page.Bar.Search = string.Empty;
        Assert.IsEmpty(scene.Rows, "il se replie");
    }

    [STATestMethod]
    public void The_search_is_done_again_when_the_list_is_read_again()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var id = scene.Sheet("Biologie", "Mitose", "La prophase.");
        scene.Load();
        scene.Page.Bar.Search = "prophase";
        UiThread.Until(() => scene.Rows.Count == 1 && scene.Rows[0].HasExcerpt);

        var app = scene.Vaults.App;
        Assert.IsTrue(app.Editing.Save(id, "Plus rien.", app.Sheets.Open(id).Value.Fingerprint, 2).Succeeded);
        scene.Page.RefreshCommand.Execute(null);
        UiThread.Until(() => scene.Rows.Count == 0);

        Assert.IsTrue(scene.Page.HasNoMatch);
    }

    [STATestMethod]
    public void A_refused_search_is_said()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Sheet("Biologie", "Mitose", "La prophase.");
        scene.Load();
        scene.Vaults.App.Host.Dispose();

        scene.Page.Bar.Search = "prophase";
        UiThread.Until(() => scene.Page.Error is not null);

        Assert.AreEqual(KyfRev.Application.UseCases.VaultHost.NoVaultOpen, scene.Page.Error);
    }

    [STATestMethod]
    public void An_answer_clears_the_error_of_the_previous_search()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Sheet("Biologie", "Mitose", "La prophase.");
        scene.Load();
        scene.Page.Bar.Search = "prophase";
        var searching = scene.Page.SearchTextCommand.ExecutionTask!;
        UiThread.Until(() => searching.IsCompleted);

        Assert.IsNull(scene.Page.Error);
    }

    [STATestMethod]
    public void Locking_stops_the_search_and_its_answer_puts_nothing_back()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Sheet("Biologie", "Mitose", "La prophase.");
        scene.Load();

        scene.Page.Bar.Search = "prophase";
        var searching = scene.Page.SearchTextCommand.ExecutionTask!;
        scene.Page.Forget();
        UiThread.Until(() => searching.IsCompleted);

        Assert.IsEmpty(scene.Page.Rows);
        Assert.AreEqual(string.Empty, scene.Page.Bar.Search);
    }

    [STATestMethod]
    public void The_field_of_the_view_says_it_searches_the_text()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var other = new FilterBarViewModel(() => { });
        scene.Load();

        Assert.AreEqual("Rechercher dans les fiches", scene.Page.Bar.SearchPlaceholder);
        Assert.AreEqual("Rechercher dans les fiches (titre, étiquettes et texte)", scene.Page.Bar.SearchName);
        Assert.AreEqual(ListText.SearchPlaceholder, other.SearchPlaceholder, "les autres listes gardent le leur");
        Assert.AreEqual(ListText.SearchName, other.SearchName);
    }

    [STATestMethod]
    public void The_excerpt_is_under_the_title_at_the_smallest_width()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var body = "La prophase précède la métaphase. " + string.Concat(Enumerable.Repeat("Une phrase pour remplir plusieurs lignes. ", 10));
        scene.Sheet("Biologie", "Mitose", body);
        var test = new TestShell();
        test.Shell.Navigate(new NavigationItem("fiches", "Fiches", Icons.Document, int.MaxValue, scene.Page));
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 470, Height = 540, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            scene.Load();
            scene.Page.Bar.Search = "prophase";
            UiThread.Until(() => scene.Rows.Count == 1 && scene.Rows[0].HasExcerpt);
            TestShell.Flush();

            var excerpt = Descendants(window).OfType<ExcerptText>().Single(text => text.IsVisible);
            var view = Descendants(window).OfType<SheetsPage>().Single();
            Assert.IsLessThanOrEqualTo(view.ActualWidth, excerpt.ActualWidth, "l'extrait va à la ligne plutôt que de déborder");
            Assert.AreEqual(SheetsText.SearchName, AutomationProperties.GetName(Descendants(window).OfType<TextBox>().First(box => box.Name == "SearchBox")));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void An_excerpt_highlights_the_words_found_and_stays_on_two_lines()
    {
        var design = new TestShell().Design;
        var text = new ExcerptText { Pieces = [new ExcerptPiece("Le ", false), new ExcerptPiece("clergé", true)] };
        var host = new Border { Width = 200, Child = text };
        host.Resources.MergedDictionaries.Add(design.Dictionary);
        var window = new Window { Content = host, Left = -10000, Top = -10000, Width = 300, Height = 200, ShowActivated = false };
        window.Show();
        try
        {
            TestShell.Flush();
            var runs = text.Inlines.OfType<Run>().ToList();
            Assert.HasCount(2, runs);
            Assert.IsNull(runs[0].Background);
            Assert.AreEqual(((SolidColorBrush)host.FindResource("Kyf.Brush.Highlight")).Color, ((SolidColorBrush)runs[1].Background).Color);
            Assert.AreEqual(((SolidColorBrush)host.FindResource("Kyf.Brush.OnHighlight")).Color, ((SolidColorBrush)runs[1].Foreground).Color);
            Assert.AreEqual(2 * text.LineHeight, text.MaxHeight, 1e-6);

            var before = text.MaxHeight;
            text.LineHeight = double.NaN;
            Assert.AreEqual(before, text.MaxHeight, "sans hauteur de ligne, la borne reste");

            text.Pieces = null;
            Assert.IsEmpty(text.Inlines);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Only_the_latest_search_is_answered()
    {
        using var ui = new UiThread();
        var latest = new LatestSearch();
        var shown = new List<string>();
        CancellationToken older = default;

        var first = latest.RunAsync(
            TimeSpan.Zero,
            token =>
            {
                older = token;
                return "ancienne";
            },
            shown.Add);
        var second = latest.RunAsync(TimeSpan.Zero, _ => "nouvelle", shown.Add);
        UiThread.Until(() => first.IsCompleted && second.IsCompleted);

        Assert.AreEqual("nouvelle", shown.Single());
        Assert.IsTrue(older.IsCancellationRequested);
    }

    [STATestMethod]
    public void A_search_cancelled_during_its_pause_never_starts()
    {
        using var ui = new UiThread();
        var latest = new LatestSearch();
        var started = false;

        var running = latest.RunAsync(
            TimeSpan.FromMilliseconds(30),
            _ =>
            {
                started = true;
                return 1;
            },
            _ => { });
        latest.Cancel();
        UiThread.Until(() => running.IsCompleted);

        Assert.IsFalse(started);
    }

    [TestMethod]
    public void Cancelling_when_nothing_runs_does_nothing() => new LatestSearch().Cancel();

    [STATestMethod]
    public void A_search_after_its_pause_gives_its_answer()
    {
        using var ui = new UiThread();
        var shown = new List<int>();

        var running = new LatestSearch().RunAsync(TimeSpan.FromMilliseconds(1), _ => 7, shown.Add);
        UiThread.Until(() => running.IsCompleted);

        Assert.AreEqual(7, shown.Single());
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }

    /// <summary>La vue « Fiches » sur un vrai coffre.</summary>
    private sealed class Scene : IDisposable
    {
        public Scene()
        {
            var time = new FakeTime(Now);
            Vaults = new TestVaults(AppMode.Development, time);
            Assert.IsNotNull(Vaults.App.Vaults.Create(Vaults.PathOf("Coffre")).Vault);
            Page = new SheetsPageViewModel(Vaults.App.Sheets, Vaults.App.Folders, Vaults.App.Templates, new FakeOpener(), time, Vaults.App.Reviews, Vaults.App.Trash);
        }

        public TestVaults Vaults { get; }

        public SheetsPageViewModel Page { get; }

        public List<SheetRow> Rows => [.. Page.Rows.OfType<SheetRow>()];

        /// <summary>Une fiche écrite ; rend son identifiant.</summary>
        public string Sheet(string folder, string title, string body)
        {
            var id = Vaults.App.Sheets.Create(folder, title, null).Value;
            Assert.IsTrue(Vaults.App.Editing.Save(id, body, Vaults.App.Sheets.Open(id).Value.Fingerprint, 1).Succeeded);
            return id;
        }

        /// <summary>La vue s'ouvre et lit le coffre.</summary>
        public void Load()
        {
            Page.OnNavigatedTo();
            UiThread.Until(() => Page.RefreshCommand.ExecutionTask is { IsCompleted: true });
        }

        public void Dispose() => Vaults.Dispose();
    }
}
