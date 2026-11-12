using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La recherche (Ctrl+K) dans le coffre : les commandes, puis ce qui est trouvé, groupé ; un résultat s'ouvre.</summary>
[TestClass]
public sealed class GlobalSearchTests
{
    private const string Query = "prophase";

    private static readonly string[] Headings = ["Fiches · 1", "Passages et définitions · 2", "Citations · 1"];

    private static readonly Anchor Passage = new("La prophase", string.Empty, " précède", 0, 11, ["Mitose"], 5, "sha256:aa");

    [STATestMethod]
    public void The_results_come_by_group_with_their_place_their_nature_and_their_excerpt()
    {
        using var ui = new UiThread();
        var quote = new SearchHit(SearchKinds.Quote, "citations/x.md", "Blaise Pascal", []) { Text = "Le cœur a ses raisons." };
        using var scene = new Scene(new AnswersWith(_ => Outcome.Of<IReadOnlyList<SearchHit>>([quote])));
        var sheet = scene.Sheet("Sciences/Biologie", "Mitose", "La prophase précède la métaphase.");
        var passage = scene.Vaults.App.Annotations.Create(sheet, Passage, "prophase à revoir").Value;
        var definition = scene.Vaults.App.Annotations.CreateDefinition(sheet, Passage, new Definition("Prophase", "La première phase.")).Value;

        scene.Search(Query);

        var hits = scene.Shell.PaletteResults;
        CollectionAssert.AreEqual(Headings, hits.Select(hit => hit.Group.Heading).Distinct().ToList());
        Assert.HasCount(4, hits);

        var inSheet = hits[0];
        Assert.AreEqual("Mitose", inSheet.Label);
        Assert.AreEqual("Sciences › Biologie", inSheet.Place);
        Assert.AreEqual(" · Sciences › Biologie", inSheet.PlaceText);
        Assert.IsNull(inSheet.Detail);
        Assert.AreEqual(Icons.Document, inSheet.Glyph);
        Assert.AreEqual(PaletteAccent.None, inSheet.Accent);
        Assert.IsTrue(inSheet.HasExcerpt);
        Assert.AreEqual("La |prophase| précède la métaphase.", string.Join('|', inSheet.Excerpt.Select(piece => piece.Text)));
        Assert.AreEqual("Mitose, Sciences › Biologie, La prophase précède la métaphase.", inSheet.ToString());

        var inPassage = hits[1];
        Assert.AreEqual("« La prophase »", inPassage.Label);
        Assert.AreEqual("passage", inPassage.Detail);
        Assert.AreEqual(Icons.Highlighter, inPassage.Glyph);
        Assert.AreEqual(PaletteAccent.Passage, inPassage.Accent);
        Assert.AreEqual("Mitose", inPassage.Place);

        var inDefinition = hits[2];
        Assert.AreEqual("Prophase", inDefinition.Label);
        Assert.AreEqual("définition", inDefinition.Detail);
        Assert.AreEqual(Icons.Dictionary, inDefinition.Glyph);
        Assert.AreEqual(PaletteAccent.Term, inDefinition.Accent);
        Assert.IsFalse(inDefinition.HasExcerpt, "le terme a suffi");
        Assert.AreEqual("Prophase, Mitose, définition", inDefinition.ToString(), "sa nature se lit aussi sans extrait");

        var inQuote = hits[3];
        Assert.AreEqual("Blaise Pascal", inQuote.Label);
        Assert.AreEqual(Icons.Quote, inQuote.Glyph);
        Assert.AreEqual("Le cœur a ses raisons.", inQuote.Excerpt.Single().Text, "le texte se lit à la place de l'extrait");
        Assert.IsFalse(inQuote.Excerpt.Single().Found);
        Assert.AreEqual(string.Empty, inQuote.PlaceText);

        Assert.IsTrue(scene.Shell.ShowsKeyHelp);
        Assert.IsFalse(scene.Shell.HasNoResult);

        foreach (var entry in hits)
        {
            entry.Execute();
        }
        CollectionAssert.AreEqual(new[] { sheet, passage, definition, quote.Id }, scene.Opened.Select(hit => hit.Id).ToList());
    }

    [STATestMethod]
    public void The_commands_come_first_without_a_heading_and_keep_their_name()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Vaults.App.Sheets.Create(string.Empty, "Carnet de lecture", string.Empty);

        scene.Search("carnet");

        var command = scene.Shell.PaletteResults[0];
        Assert.AreEqual("Aller à : Carnet", command.Label);
        Assert.AreSame(PaletteGroup.Commands, command.Group);
        Assert.IsFalse(command.Group.HasHeading);
        Assert.AreEqual(command.Label, command.ToString(), "un raccourci ne se lit pas dans le nom");
        Assert.AreEqual("Carnet de lecture", scene.Shell.PaletteResults[^1].Label);
        Assert.AreSame(command, scene.Shell.SelectedResult);
    }

    [STATestMethod]
    public void An_archived_sheet_says_so()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        Assert.IsTrue(scene.Vaults.App.Reviews.Archive(scene.Vaults.App.Sheets.Create(string.Empty, "Mitose", string.Empty).Value).Succeeded);

        scene.Search("mitose");

        var entry = scene.Shell.PaletteResults.Single();
        Assert.AreEqual("archivée", entry.Detail);
        Assert.AreEqual(Icons.Archive, entry.Glyph);
        Assert.IsNull(entry.Place, "à la racine");
    }

    [STATestMethod]
    public void A_single_letter_searches_only_the_commands_and_says_how_to_search()
    {
        using var ui = new UiThread();
        var module = new AnswersWith(_ => Outcome.Of<IReadOnlyList<SearchHit>>([]));
        using var scene = new Scene(module);

        scene.Shell.OpenPalette();
        Assert.IsTrue(scene.Shell.ShowsSearchHint);

        scene.Shell.PaletteQuery = "z";
        TestShell.Flush();

        Assert.IsTrue(scene.Shell.ShowsSearchHint);
        Assert.IsFalse(scene.Shell.IsSearching);
        Assert.IsEmpty(module.Asked);
        Assert.AreEqual(ShellText.NoCommand, scene.Shell.NoResultText);
    }

    [STATestMethod]
    public void When_nothing_matches_the_search_says_so()
    {
        using var ui = new UiThread();
        using var scene = new Scene();

        scene.Shell.OpenPalette();
        scene.Shell.PaletteQuery = "  xyzzy  ";
        Assert.IsTrue(scene.Shell.IsSearching);
        Assert.IsFalse(scene.Shell.HasNoResult, "on ne le dit pas avant la réponse");

        UiThread.Until(() => !scene.Shell.IsSearching);

        Assert.IsTrue(scene.Shell.HasNoResult);
        Assert.AreEqual("Rien ne correspond à « xyzzy ».", scene.Shell.NoResultText);
        Assert.IsFalse(scene.Shell.ShowsSearchHint);
        Assert.IsFalse(scene.Shell.ShowsKeyHelp);
    }

    [STATestMethod]
    public void A_result_opens_and_the_search_closes_without_keeping_it()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var sheet = scene.Vaults.App.Sheets.Create(string.Empty, "Mitose", string.Empty).Value;

        scene.Search("mitose");
        scene.Shell.RunSelectedCommand.Execute(null);

        Assert.AreEqual(sheet, scene.Opened.Single().Id);
        Assert.IsFalse(scene.Shell.IsPaletteOpen);
        Assert.IsFalse(scene.Shell.ShowsKeyHelp);
        Assert.IsTrue(scene.Shell.PaletteResults.All(entry => !entry.Group.HasHeading), "ce qui a été trouvé n'est pas gardé");
    }

    [STATestMethod]
    public void A_newer_search_stops_the_older_and_its_answer_is_never_shown()
    {
        using var ui = new UiThread();
        using var module = new HeldProvider();
        using var scene = new Scene(module);

        scene.Shell.OpenPalette();
        scene.Shell.PaletteQuery = "mito";
        UiThread.Until(() => module.Asked.Count == 1);
        var (older, olderToken) = module.Asked.Single();
        Assert.AreEqual("mito", older);

        scene.Shell.PaletteQuery = "mitose";
        Assert.IsTrue(olderToken.IsCancellationRequested, "elle s'arrête tout de suite");

        module.Release.Set();
        UiThread.Until(() => !scene.Shell.IsSearching);

        Assert.AreEqual("mitose", scene.Shell.PaletteResults.Single().Label);
    }

    [STATestMethod]
    public void A_refusal_is_shown_and_the_next_search_clears_it()
    {
        using var ui = new UiThread();
        var refuses = true;
        using var scene = new Scene(new AnswersWith(_ => refuses
            ? Outcome.Refused<IReadOnlyList<SearchHit>>("Illisible.")
            : Outcome.Of<IReadOnlyList<SearchHit>>([])));

        scene.Search("mitose");
        Assert.AreEqual("Illisible.", scene.Shell.PaletteError);
        Assert.IsTrue(scene.Shell.HasPaletteError);
        Assert.IsFalse(scene.Shell.HasNoResult);

        refuses = false;
        scene.Shell.PaletteQuery = "mitoses";
        UiThread.Until(() => !scene.Shell.IsSearching);

        Assert.IsNull(scene.Shell.PaletteError);
        Assert.IsFalse(scene.Shell.HasPaletteError);
        Assert.IsTrue(scene.Shell.HasNoResult);
    }

    [STATestMethod]
    public void Twenty_results_are_shown_by_group_and_the_rest_is_counted()
    {
        var hits = Enumerable.Range(0, 25).Select(n => new SearchHit(SearchKinds.Sheet, $"f_{n}", $"Fiche {n}", [])).ToList();

        var entries = SearchEntries.Of(hits, _ => { });

        Assert.HasCount(SearchEntries.MaximumPerGroup, entries);
        var group = entries[0].Group;
        Assert.AreEqual(25, group.Total);
        Assert.AreEqual(20, group.Shown);
        Assert.IsTrue(group.HasMoreHits);
        Assert.AreEqual("5 autres résultats : précisez la recherche.", group.MoreHits);

        Assert.AreEqual("1 autre résultat : précisez la recherche.", SearchEntries.Of([.. hits.Take(21)], _ => { })[0].Group.MoreHits);
        Assert.IsFalse(SearchEntries.Of([.. hits.Take(20)], _ => { })[0].Group.HasMoreHits);
    }

    [STATestMethod]
    public void A_quote_without_author_is_said_so()
    {
        var entry = SearchEntries.Of([new SearchHit(SearchKinds.Quote, "citations/x.md", string.Empty, [])], _ => { }).Single();

        Assert.AreEqual(QuotesText.UnknownAuthor, entry.Label);
    }

    [STATestMethod]
    public void An_answer_keeps_the_line_already_chosen()
    {
        using var ui = new UiThread();
        using var scene = new Scene(new AnswersWith(query => Outcome.Of<IReadOnlyList<SearchHit>>([new SearchHit(SearchKinds.Sheet, "f_1", query, [])])));

        scene.Shell.OpenPalette();
        scene.Shell.PaletteQuery = "aller";
        scene.Shell.MoveSelectionCommand.Execute("1");
        var chosen = scene.Shell.SelectedResult;
        Assert.AreSame(scene.Shell.PaletteResults[1], chosen);

        UiThread.Until(() => !scene.Shell.IsSearching);

        Assert.AreSame(chosen, scene.Shell.SelectedResult);
        Assert.AreEqual("aller", scene.Shell.PaletteResults[^1].Label);
    }

    [STATestMethod]
    public void A_line_the_answer_replaced_gives_way_to_the_first()
    {
        using var ui = new UiThread();
        using var scene = new Scene(new AnswersWith(query => Outcome.Of<IReadOnlyList<SearchHit>>([new SearchHit(SearchKinds.Sheet, "f_1", query, [])])));

        scene.Search("mitose");
        Assert.AreEqual("mitose", scene.Shell.SelectedResult!.Label);

        scene.Shell.PaletteQuery = "mitoses";
        UiThread.Until(() => !scene.Shell.IsSearching);

        Assert.AreEqual("mitoses", scene.Shell.SelectedResult!.Label);
    }

    [STATestMethod]
    public void Shortening_the_search_or_closing_it_forgets_what_was_found()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Vaults.App.Sheets.Create(string.Empty, "Mitose", string.Empty);

        scene.Search("mitose");
        Assert.HasCount(1, scene.Shell.PaletteResults);

        scene.Shell.PaletteQuery = "m";
        Assert.IsFalse(scene.Shell.ShowsKeyHelp);

        scene.Search("mitose");
        scene.Shell.ClosePalette();
        Assert.IsFalse(scene.Shell.ShowsKeyHelp);
    }

    [STATestMethod]
    public void Locking_forgets_the_search_and_an_answer_that_comes_after_shows_nothing()
    {
        using var ui = new UiThread();
        using var module = new HeldProvider();
        using var scene = new Scene(module);

        scene.Shell.OpenPalette();
        scene.Shell.PaletteQuery = "mitose";
        UiThread.Until(() => module.Asked.Count == 1);

        scene.Shell.Forget();

        Assert.IsTrue(module.Asked.Single().Token.IsCancellationRequested);
        Assert.IsFalse(scene.Shell.IsPaletteOpen);
        Assert.AreEqual(string.Empty, scene.Shell.PaletteQuery);
        Assert.IsFalse(scene.Shell.IsSearching);

        var searching = scene.Shell.SearchVaultCommand.ExecutionTask!;
        module.Release.Set();
        UiThread.Until(() => searching.IsCompleted);
        TestShell.Flush();

        Assert.IsFalse(scene.Shell.ShowsKeyHelp);
        Assert.IsTrue(scene.Shell.PaletteResults.All(entry => !entry.Group.HasHeading));
    }

    [STATestMethod]
    public void A_stopped_search_says_nothing_and_starts_again_if_the_vault_stays_open()
    {
        using var ui = new UiThread();
        using var module = new HeldProvider();
        using var scene = new Scene(module);

        scene.Shell.OpenPalette();
        scene.Shell.PaletteQuery = "mitose";
        UiThread.Until(() => module.Asked.Count == 1);
        var searching = scene.Shell.SearchVaultCommand.ExecutionTask!;

        scene.Shell.StopSearch();

        Assert.IsTrue(module.Asked.Single().Token.IsCancellationRequested);
        Assert.IsTrue(scene.Shell.IsPaletteOpen, "rien n'est oublié : le coffre reste peut-être ouvert");
        Assert.IsTrue(scene.Shell.IsSearching);
        module.Release.Set();
        UiThread.Until(() => searching.IsCompleted);
        Assert.IsFalse(scene.Shell.ShowsKeyHelp, "sa réponse ne se montre pas");

        scene.Shell.ResumeSearch();
        UiThread.Until(() => !scene.Shell.IsSearching);

        Assert.HasCount(2, module.Asked);
        Assert.AreEqual("mitose", scene.Shell.PaletteResults.Single().Label);
    }

    [STATestMethod]
    public void A_search_that_answered_or_was_abandoned_does_not_start_again()
    {
        using var ui = new UiThread();
        var module = new AnswersWith(_ => Outcome.Of<IReadOnlyList<SearchHit>>([]));
        using var scene = new Scene(module);
        scene.Search("mitose");

        scene.Shell.StopSearch();
        scene.Shell.ResumeSearch();
        TestShell.Flush();
        Assert.HasCount(1, module.Asked, "elle avait répondu");

        scene.Shell.PaletteQuery = "mitoses";
        scene.Shell.StopSearch();
        scene.Shell.ClosePalette();
        scene.Shell.ResumeSearch();
        TestShell.Flush();
        Assert.HasCount(1, module.Asked, "fermée, elle est abandonnée");
    }

    [STATestMethod]
    public void A_click_on_the_words_of_a_line_does_not_close_the_search()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Sheet("Sciences", "Mitose", "La prophase précède la métaphase.");
        var window = new ShellWindow(scene.Test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(scene.Test.Design.Dictionary);
        window.Show();
        try
        {
            scene.Search(Query);
            window.UpdateLayout();
            TestShell.Flush();
            var list = (ListBox)window.FindName("PaletteResults");
            var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);

            // WPF donne le morceau de texte (Run) comme source du clic : le titre, le dossier, l'extrait.
            var label = Descendants(item).OfType<TextBlock>().SelectMany(text => text.Inlines.OfType<Run>()).First(run => run.Text == "Mitose");
            var words = Descendants(item).OfType<ExcerptText>().Single().Inlines.OfType<Run>().First();
            foreach (var clicked in new[] { label, words, new Run("sans bloc") })
            {
                Press(window, clicked);
                Assert.IsTrue(scene.Shell.IsPaletteOpen, clicked.Text);
            }

            var elsewhere = new Run("ailleurs");
            _ = new TextBlock(elsewhere);
            Press(window, elsewhere);
            Assert.IsFalse(scene.Shell.IsPaletteOpen, "du texte hors de la recherche : un clic ailleurs, qui la ferme");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void An_empty_answer_is_a_status_for_the_screen_reader()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var window = new ShellWindow(scene.Test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(scene.Test.Design.Dictionary);
        window.Show();
        try
        {
            scene.Shell.OpenPalette();
            TestShell.Flush();
            var message = Assert.IsInstanceOfType<StatusText>(window.FindName("NoResult"));
            Assert.IsFalse(message.IsVisible);

            scene.Search("xyzzy");
            TestShell.Flush();

            Assert.IsTrue(message.IsVisible);
            Assert.AreEqual("Rien ne correspond à « xyzzy ».", message.Text);
            Assert.AreEqual(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(message));
            Assert.AreEqual(((SolidColorBrush)window.FindResource("Kyf.Brush.TextSecondary")).Color, ((SolidColorBrush)message.Foreground).Color, "le style du texte s'y applique");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_field_says_what_is_searched()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var window = new ShellWindow(scene.Test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(scene.Test.Design.Dictionary);
        window.Show();
        try
        {
            var field = (TextBox)window.FindName("PaletteQuery");
            var list = (ListBox)window.FindName("PaletteResults");
            TestShell.Flush();
            Assert.AreEqual("Rechercher partout", AutomationProperties.GetName(field));
            Assert.AreEqual("Rechercher partout", Field.GetPlaceholder(field));

            scene.Shell.ShowWelcome(new object());
            TestShell.Flush();

            Assert.AreEqual("Rechercher une commande", AutomationProperties.GetName(field), "sans coffre, seules les commandes se cherchent");
            Assert.AreEqual("Rechercher une commande", Field.GetPlaceholder(field));
            Assert.AreEqual("Rechercher une commande", AutomationProperties.GetName(list));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Without_a_vault_only_the_commands_are_searched()
    {
        using var ui = new UiThread();
        var module = new AnswersWith(_ => Outcome.Of<IReadOnlyList<SearchHit>>([]));
        using var scene = new Scene(module);
        scene.Shell.ShowWelcome(new object());

        scene.Shell.OpenPalette();
        scene.Shell.PaletteQuery = "mitose";
        TestShell.Flush();

        Assert.IsFalse(scene.Shell.ShowsSearchHint);
        Assert.IsFalse(scene.Shell.IsSearching);
        Assert.IsEmpty(module.Asked);
        Assert.AreEqual(ShellText.NoCommand, scene.Shell.NoResultText);
        Assert.AreEqual(ShellText.SearchCommandsPlaceholder, scene.Shell.PaletteLabel);
    }

    [STATestMethod]
    public void Without_a_search_given_only_the_commands_are_searched()
    {
        var shell = new TestShell().Shell;

        shell.OpenPalette();
        shell.PaletteQuery = "mitose";

        Assert.IsFalse(shell.IsSearching);
        Assert.IsFalse(shell.ShowsSearchHint);
        Assert.AreEqual(ShellText.NoCommand, shell.NoResultText);
    }

    [STATestMethod]
    public void A_sheet_a_passage_and_a_definition_open_in_their_sheet()
    {
        var opener = new FakeOpener();
        var hits = new SearchHitOpener(opener, new TestShell().Shell);

        hits.Open(new SearchHit(SearchKinds.Sheet, "f_1", "Mitose", []));
        hits.Open(new SearchHit(SearchKinds.Passage, "a_1", "La prophase", []) { SheetId = "f_2" });
        hits.Open(new SearchHit(SearchKinds.Definition, "a_2", "Prophase", []) { SheetId = "f_3" });

        CollectionAssert.AreEqual(new object[] { "f_1", ("f_2", "a_1"), ("f_3", "a_2") }, opener.Opened);
        Assert.ThrowsExactly<ArgumentNullException>(() => hits.Open(null!));
    }

    [STATestMethod]
    public void What_cannot_be_opened_is_left_alone()
    {
        var opener = new FakeOpener();
        var shell = new TestShell().Shell;
        var hits = new SearchHitOpener(opener, shell);
        var current = shell.Current;

        hits.Open(new SearchHit("inconnu", "x", "x", []));
        hits.Open(new SearchHit(SearchKinds.Passage, "a_1", "Sans fiche", []));
        hits.Open(new SearchHit(SearchKinds.Quote, "citations/x.md", "Module inactif", []));

        Assert.IsEmpty(opener.Opened);
        Assert.AreSame(current, shell.Current);
    }

    [STATestMethod]
    public void The_list_fits_the_smallest_window_and_every_line_has_its_name()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var sheet = scene.Sheet("Sciences", "Mitose", "La prophase précède la métaphase. " + string.Concat(Enumerable.Repeat("Une phrase longue pour remplir plusieurs lignes. ", 10)));
        scene.Vaults.App.Annotations.CreateDefinition(sheet, Passage, new Definition("Prophase", "La première phase."));
        var window = new ShellWindow(scene.Test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 470, Height = 540, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(scene.Test.Design.Dictionary);
        window.Show();
        try
        {
            scene.Search(Query);
            window.UpdateLayout();
            TestShell.Flush();

            var list = (ListBox)window.FindName("PaletteResults");
            var card = (FrameworkElement)window.FindName("PaletteCard");
            var bottom = card.TranslatePoint(new Point(0, card.ActualHeight), (FrameworkElement)window.Content).Y;
            Assert.IsLessThanOrEqualTo(((FrameworkElement)window.Content).ActualHeight, bottom, "la carte tient dans la fenêtre");
            Assert.IsLessThanOrEqualTo(card.ActualWidth, list.ActualWidth, "la liste tient dans la carte");
            Assert.AreEqual(ScrollBarVisibility.Disabled, ScrollViewer.GetHorizontalScrollBarVisibility(list));
            Assert.AreEqual(Visibility.Visible, ((TextBlock)window.FindName("SearchKeys")).Visibility);

            var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
            var excerpt = Descendants(item).OfType<ExcerptText>().Single();
            Assert.IsLessThanOrEqualTo(item.ActualWidth, excerpt.ActualWidth, "l'extrait va à la ligne plutôt que de déborder");
            Assert.IsLessThanOrEqualTo(excerpt.MaxHeight + 0.5, excerpt.ActualHeight, "deux lignes au plus");
            var found = excerpt.Inlines.OfType<Run>().Single(run => run.Text == Query);
            Assert.AreEqual(((SolidColorBrush)window.FindResource("Kyf.Brush.Highlight")).Color, ((SolidColorBrush)found.Background).Color);
            Assert.AreEqual(((SolidColorBrush)window.FindResource("Kyf.Brush.OnHighlight")).Color, ((SolidColorBrush)found.Foreground).Color);

            var headings = Descendants(list).OfType<TextBlock>().Where(text => text.Text == "Fiches · 1").ToList();
            Assert.AreEqual(AutomationHeadingLevel.Level2, AutomationProperties.GetHeadingLevel(headings.Single()));
            scene.Shell.MoveSelectionCommand.Execute("1");
            Assert.AreSame(scene.Shell.PaletteResults[1], list.SelectedItem);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Un clic sur <paramref name="source"/>, tel que la fenêtre le reçoit d'abord.</summary>
    private static void Press(Window window, DependencyObject source) =>
        window.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent, Source = source });

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

    /// <summary>Une coquille sur un vrai coffre ; ce qui s'ouvre est noté.</summary>
    private sealed class Scene : IDisposable
    {
        public Scene(params ISearchProvider[] modules)
        {
            Vaults = new TestVaults(AppMode.Development);
            Assert.IsNotNull(Vaults.App.Vaults.Create(Vaults.PathOf("Coffre")).Vault);
            Shell.Search = new GlobalSearch(Vaults.App.Search, () => modules, Opened.Add);
        }

        public TestVaults Vaults { get; }

        public TestShell Test { get; } = new();

        public ShellViewModel Shell => Test.Shell;

        public List<SearchHit> Opened { get; } = [];

        /// <summary>Une fiche écrite ; rend son identifiant.</summary>
        public string Sheet(string folder, string title, string body)
        {
            var id = Vaults.App.Sheets.Create(folder, title, null).Value;
            Assert.IsTrue(Vaults.App.Editing.Save(id, body, Vaults.App.Sheets.Open(id).Value.Fingerprint, 1).Succeeded);
            return id;
        }

        /// <summary>Ouvre la recherche, écrit <paramref name="text"/> et attend la réponse.</summary>
        public void Search(string text)
        {
            Shell.OpenPalette();
            Shell.PaletteQuery = text;
            UiThread.Until(() => !Shell.IsSearching);
        }

        public void Dispose() => Vaults.Dispose();
    }

    /// <summary>Un module d'essai : il répond ce qu'on lui dit, et note ce qu'on lui demande.</summary>
    private sealed class AnswersWith(Func<string, Outcome<IReadOnlyList<SearchHit>>> answer) : ISearchProvider
    {
        public ConcurrentQueue<string> Asked { get; } = new();

        public Outcome<IReadOnlyList<SearchHit>> Search(string query, CancellationToken cancellationToken)
        {
            Asked.Enqueue(query);
            return answer(query);
        }
    }

    /// <summary>Un module d'essai qui ne répond que lorsqu'on le libère : son nom est le texte cherché.</summary>
    private sealed class HeldProvider : ISearchProvider, IDisposable
    {
        public ManualResetEventSlim Release { get; } = new();

        public ConcurrentQueue<(string Query, CancellationToken Token)> Asked { get; } = new();

        public Outcome<IReadOnlyList<SearchHit>> Search(string query, CancellationToken cancellationToken)
        {
            Asked.Enqueue((query, cancellationToken));
            Release.Wait(TimeSpan.FromSeconds(10), CancellationToken.None);
            return Outcome.Of<IReadOnlyList<SearchHit>>([new SearchHit(SearchKinds.Sheet, query, query, [])]);
        }

        public void Dispose() => Release.Dispose();
    }
}
