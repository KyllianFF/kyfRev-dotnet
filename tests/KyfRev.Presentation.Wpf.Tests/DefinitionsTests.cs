using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Définitions » : le glossaire d'un vrai coffre, sa recherche, ses dossiers et ses cartes.</summary>
[TestClass]
public sealed class DefinitionsTests
{
    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose", "Étapes"], 5, "sha256:aa");

    private static readonly string[] AllTerms = ["Bastille", "Écologie", "Mitose", "Noyau"];

    private static readonly string[] BiologyTerms = ["Écologie", "Mitose", "Noyau"];

    private static readonly string[] CellTerms = ["Mitose", "Noyau"];

    private static readonly string[] FolderLabels = ["Tout le coffre", "Histoire", "Sciences", "   Cellule"];

    [TestMethod]
    public async Task The_glossary_lists_the_definitions_by_folder_then_term()
    {
        using var glossary = new Glossary();
        var (_, kept) = glossary.Filled();
        glossary.App.Annotations.Create(kept, Passage, "Un simple commentaire.");
        glossary.App.Annotations.Trash(glossary.App.Annotations.CreateDefinition(kept, Passage, new Definition("Jetée", "À la corbeille.")).Value);

        var page = await glossary.LoadedAsync();

        CollectionAssert.AreEqual(AllTerms, Terms(page), "un commentaire ou une définition à la corbeille n'y sont pas, et É se range avec E");
        Assert.AreEqual("4 définitions", page.Summary);
        Assert.IsFalse(page.IsEmpty);
        Assert.IsFalse(page.HasNoMatch);
        Assert.IsFalse(page.HasError);
        CollectionAssert.AreEqual(FolderLabels, page.Folders.Select(folder => folder.Label).ToArray());
    }

    [TestMethod]
    public async Task A_row_says_where_the_definition_comes_from()
    {
        using var glossary = new Glossary();
        glossary.Filled();
        var page = await glossary.LoadedAsync();

        var row = page.Rows.Single(candidate => candidate.Term == "Mitose");

        Assert.AreEqual("Division de la cellule.", row.Explanation);
        Assert.AreEqual("La cellule › Mitose › Étapes · J1 le 2 oct.", row.Source);
        Assert.IsFalse(row.HasWarning);
        Assert.IsNull(row.Warning);
    }

    [TestMethod]
    public async Task A_definition_without_a_next_step_says_only_its_place()
    {
        using var glossary = new Glossary();
        var page = await glossary.LoadedAsync();

        var row = new DefinitionRow(new VaultDefinition("id", "fiche", "La cellule", "Sciences", "Noyau", "Il renferme l'ADN.", ["Structure"], AnchorState.Anchored, null, null), page);

        Assert.AreEqual("La cellule › Structure", row.Source);
    }

    [TestMethod]
    public async Task Searching_ignores_accents_and_case_on_the_term_and_its_explanation()
    {
        using var glossary = new Glossary();
        glossary.Filled();
        var page = await glossary.LoadedAsync();

        page.Search = "ecologie";
        Assert.AreEqual("Écologie", page.Rows.Single().Term, "sans accent ni majuscule");

        page.Search = "  DIVISION  ";
        Assert.AreEqual("Mitose", page.Rows.Single().Term, "dans l'explication aussi");
        Assert.AreEqual("1 définition", page.Summary);

        page.Search = "absent";
        Assert.IsEmpty(page.Rows);
        Assert.IsTrue(page.HasNoMatch);
        Assert.AreEqual("0 définition", page.Summary);

        page.Search = string.Empty;
        CollectionAssert.AreEqual(AllTerms, Terms(page));
    }

    [TestMethod]
    public async Task Choosing_a_folder_shows_its_subfolders_too()
    {
        using var glossary = new Glossary();
        glossary.Filled();
        var page = await glossary.LoadedAsync();

        page.SelectedFolder = page.Folders.Single(folder => folder.Path == "Sciences");
        CollectionAssert.AreEqual(BiologyTerms, Terms(page));

        page.SelectedFolder = page.Folders.Single(folder => folder.Path == "Sciences/Cellule");
        CollectionAssert.AreEqual(CellTerms, Terms(page));

        page.Search = "noyau";
        Assert.AreEqual("Noyau", page.Rows.Single().Term, "la recherche se combine au dossier");

        page.Search = "bastille";
        Assert.IsTrue(page.HasNoMatch, "elle ne sort pas du dossier");
    }

    [TestMethod]
    public async Task A_definition_changes_in_place_and_the_folder_stays_chosen()
    {
        using var glossary = new Glossary();
        glossary.Filled();
        var page = await glossary.LoadedAsync();
        page.SelectedFolder = page.Folders.Single(folder => folder.Path == "Histoire");
        var row = page.Rows.Single();
        Assert.IsTrue(row.IsShown);

        row.EditCommand.Execute(null);
        Assert.IsTrue(row.IsEditing);
        Assert.IsFalse(row.IsShown);

        row.EditedTerm = "  ";
        Assert.IsFalse(row.SaveCommand.CanExecute(null), "un terme vide ne s'enregistre pas");
        row.EditedTerm = "Prise de la Bastille";
        row.EditedExplanation = string.Empty;
        Assert.IsFalse(row.SaveCommand.CanExecute(null), "une explication vide non plus");
        row.EditedExplanation = "  Le 14 juillet 1789. ";

        await row.SaveCommand.ExecuteAsync(null);

        var saved = page.Rows.Single();
        Assert.AreEqual("Prise de la Bastille", saved.Term);
        Assert.AreEqual("Le 14 juillet 1789.", saved.Explanation, "les blancs autour sont retirés");
        Assert.IsTrue(saved.IsShown);
        Assert.AreEqual("Histoire", page.SelectedFolder!.Path);
        var stored = glossary.App.Annotations.Definitions().Value.Single(definition => definition.Id == saved.Definition.Id);
        Assert.AreEqual("Prise de la Bastille", stored.Term);
    }

    [TestMethod]
    public async Task A_draft_survives_a_search_a_folder_another_save_and_a_return()
    {
        using var glossary = new Glossary();
        glossary.Filled();
        var page = await glossary.LoadedAsync();
        var draft = page.Rows.Single(row => row.Term == "Mitose");
        draft.EditCommand.Execute(null);
        draft.EditedExplanation = "Mon brouillon.";

        page.Search = "noyau";
        Assert.IsFalse(page.Rows.Contains(draft));
        page.Search = string.Empty;
        Assert.AreSame(draft, page.Rows.Single(row => row.Term == "Mitose"), "la recherche ne perd rien");

        page.SelectedFolder = page.Folders.Single(folder => folder.Path == "Histoire");
        page.SelectedFolder = page.Folders[0];
        Assert.AreSame(draft, page.Rows.Single(row => row.Term == "Mitose"), "le dossier non plus");

        var other = page.Rows.Single(row => row.Term == "Noyau");
        other.EditCommand.Execute(null);
        other.EditedExplanation = "Il renferme tout l'ADN.";
        await other.SaveCommand.ExecuteAsync(null);
        var after = page.Rows.Single(row => row.Term == "Mitose");
        Assert.AreEqual("Mon brouillon.", after.EditedExplanation, "enregistrer une autre carte non plus");
        Assert.IsTrue(after.IsEditing);
        Assert.IsTrue(page.Rows.Single(row => row.Term == "Noyau").IsShown, "celle qu'on enregistre se lit de nouveau");

        await page.LoadCommand.ExecuteAsync(null);
        Assert.AreEqual("Mon brouillon.", page.Rows.Single(row => row.Term == "Mitose").EditedExplanation, "revenir sur la vue non plus");
    }

    [TestMethod]
    public async Task A_view_that_comes_apart_loses_neither_the_folder_nor_the_page()
    {
        using var glossary = new Glossary();
        glossary.Filled();
        var page = await glossary.LoadedAsync();
        page.SelectedFolder = page.Folders.Single(folder => folder.Path == "Histoire");

        // Une liste qui se défait écrit « rien » dans ce qui lui est lié.
        page.SelectedFolder = null;
        Assert.HasCount(1, page.Rows);

        await page.LoadCommand.ExecuteAsync(null);

        Assert.AreEqual("Histoire", page.SelectedFolder!.Path);
        Assert.HasCount(1, page.Rows);
    }

    [TestMethod]
    public async Task Cancelling_gives_back_what_was_there()
    {
        using var glossary = new Glossary();
        glossary.Filled();
        var page = await glossary.LoadedAsync();
        var row = page.Rows[0];
        row.EditCommand.Execute(null);
        row.EditedTerm = "Autre chose";
        row.EditedExplanation = "Autre explication";

        row.CancelCommand.Execute(null);

        Assert.IsTrue(row.IsShown);
        Assert.AreEqual(row.Term, row.EditedTerm);
        Assert.AreEqual(row.Explanation, row.EditedExplanation);
    }

    [TestMethod]
    public async Task A_definition_that_lost_its_place_says_so()
    {
        using var glossary = new Glossary();
        var (_, kept) = glossary.Filled();
        var toCheck = glossary.App.Annotations.CreateDefinition(kept, Passage, new Definition("À vérifier", "Position incertaine.")).Value;
        var lost = glossary.App.Annotations.CreateDefinition(kept, Passage, new Definition("Perdue", "Passage disparu.")).Value;
        Assert.IsTrue(glossary.App.Annotations.Reanchor(toCheck, Passage, AnchorState.NeedsReview).Succeeded);
        Assert.IsTrue(glossary.App.Annotations.Reanchor(lost, Passage, AnchorState.NotFound).Succeeded);

        var page = await glossary.LoadedAsync();

        Assert.AreEqual(DefinitionsText.ToCheck, page.Rows.Single(row => row.Term == "À vérifier").Warning);
        var missing = page.Rows.Single(row => row.Term == "Perdue");
        Assert.AreEqual(DefinitionsText.Lost, missing.Warning);
        Assert.IsTrue(missing.HasWarning);
    }

    [TestMethod]
    public async Task Seeing_in_the_sheet_asks_for_the_sheet_and_the_passage()
    {
        using var glossary = new Glossary();
        var (_, kept) = glossary.Filled();
        var page = await glossary.LoadedAsync();
        var row = page.Rows.Single(candidate => candidate.Term == "Mitose");

        row.OpenCommand.Execute(null);

        Assert.AreEqual((kept, row.Definition.Id), glossary.Opener.Opened.Single());
    }

    [TestMethod]
    public async Task A_vault_without_definitions_says_how_to_make_one()
    {
        using var glossary = new Glossary();
        var page = await glossary.LoadedAsync();

        Assert.IsTrue(page.IsEmpty);
        Assert.IsFalse(page.HasNoMatch);
        Assert.AreEqual(string.Empty, page.Summary, "rien à compter");
        Assert.HasCount(1, page.Folders);
    }

    [TestMethod]
    public async Task A_failure_is_said_rather_than_hidden()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = new DefinitionsPageViewModel(vaults.App.Annotations, new FakeOpener());

        await page.LoadCommand.ExecuteAsync(null);

        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.IsEmpty, "un coffre illisible n'est pas un coffre vide");
        Assert.IsFalse(page.IsLoading);
    }

    [TestMethod]
    public async Task A_refused_change_says_why_and_keeps_what_was_typed()
    {
        using var glossary = new Glossary();
        glossary.Filled();
        var page = await glossary.LoadedAsync();
        var row = page.Rows[0];
        row.EditCommand.Execute(null);
        row.EditedTerm = "Modifié";
        glossary.Leave();

        await row.SaveCommand.ExecuteAsync(null);

        Assert.IsTrue(page.HasError);
        Assert.IsTrue(row.IsEditing);
        Assert.AreEqual("Modifié", row.EditedTerm);
    }

    [TestMethod]
    public void The_arguments_are_required()
    {
        using var vaults = new TestVaults(AppMode.Development);
        Assert.ThrowsExactly<ArgumentNullException>(() => new DefinitionsPageViewModel(null!, new FakeOpener()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DefinitionsPageViewModel(vaults.App.Annotations, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => DefinitionsText.WithoutAccents(null!));
    }

    [STATestMethod]
    public void The_page_uses_the_width_and_stacks_when_it_lacks()
    {
        using var ui = new UiThread();
        using var glossary = new Glossary();
        glossary.Filled();
        var page = new DefinitionsPageViewModel(glossary.App.Annotations, glossary.Opener);
        var test = new TestShell();

        // La largeur est celle de la page et non de la fenêtre, que Windows borne à la taille de l'écran.
        var host = new Border { Width = 1500, Height = 900, Child = new DefinitionsPage { DataContext = page } };
        var window = new Window { ShowActivated = false, Left = -10000, Top = -10000, Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.Manual, Content = host };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            UiThread.Until(() => page.Rows.Count == AllTerms.Length);
            TestShell.Flush();
            var scroller = Named<ScrollViewer>(window, "Scroller");
            var wide = Cards(scroller);
            Assert.HasCount(AllTerms.Length, wide);
            Assert.IsLessThan(Left(wide[1], scroller), Left(wide[0], scroller), "plusieurs colonnes quand la place le permet");
            Assert.AreEqual(Top(wide[0], scroller), Top(wide[1], scroller));
            AssertInside(wide, scroller);
            Assert.AreEqual(0, Grid.GetRow(Named<FrameworkElement>(window, "ListPanel")));

            host.Width = 560;
            host.Height = 420;
            TestShell.Flush();
            Assert.IsGreaterThan(0, scroller.ScrollableHeight, "en largeur étroite aussi, les cartes défilent");

            var narrow = Cards(scroller);
            Assert.AreEqual(1, Grid.GetRow(Named<FrameworkElement>(window, "ListPanel")), "les dossiers passent au-dessus");
            Assert.AreEqual(Left(narrow[0], scroller), Left(narrow[1], scroller), 0.5, "une seule colonne");
            AssertInside(narrow, scroller);
            Assert.AreEqual(Visibility.Collapsed, Named<Notification>(window, "ErrorMessage").Visibility);

            page.Search = "absent";
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, Named<TextBlock>(window, "NoMatch").Visibility);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void An_opened_sheet_goes_to_the_defined_passage_once()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("# Mitose\n\nUn paragraphe avec un mot à définir.\n", passage: true);
        var id = vault.App.Annotations.Definitions().Value.Single().Id;
        var test = new TestShell();
        var navigation = new ReviewNavigation(vault.App.Sheets, vault.App.Annotations, vault.App.Reviews, vault.App.Reminders, vault.App.Vaults, new MemorySessions(), new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)), new RecordingLinks(), vault.App.Editing, FakeEnvironment.Markdown());
        navigation.Attach(test.Shell);
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 1300, Height = 800, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            navigation.OpenPassageInSheet(vault.SheetId, id);
            var sheet = (SheetPageViewModel)test.Shell.Current.Page;
            UiThread.Until(() => sheet.ActivePassage == id);

            Assert.IsNull(sheet.PassageToShow, "une seule fois : relire la fiche ne ramène pas au passage");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_sheet_read_before_its_view_appears_still_goes_to_the_passage()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("# Mitose\n\nUn paragraphe avec un mot à définir.\n", passage: true);
        var id = vault.App.Annotations.Definitions().Value.Single().Id;
        var page = vault.Page();
        page.PassageToShow = id;
        page.LoadCommand.Execute(null);
        UiThread.Until(() => page.Reading is not null && !page.IsLoading);
        var test = new TestShell();
        var window = new Window { ShowActivated = false, Left = -10000, Top = -10000, Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.Manual, Content = new SheetPage { DataContext = page } };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            UiThread.Until(() => page.ActivePassage == id);

            Assert.IsNull(page.PassageToShow);
        }
        finally
        {
            window.Close();
        }
    }

    private static string[] Terms(DefinitionsPageViewModel page) => [.. page.Rows.Select(row => row.Term)];

    private static List<Card> Cards(DependencyObject root) => [.. Descendants(root).OfType<Card>()];

    private static double Left(FrameworkElement element, Visual root) => element.TransformToAncestor(root).Transform(default).X;

    private static double Top(FrameworkElement element, Visual root) => element.TransformToAncestor(root).Transform(default).Y;

    private static void AssertInside(List<Card> cards, ScrollViewer scroller)
    {
        foreach (var card in cards)
        {
            Assert.IsLessThanOrEqualTo(scroller.ActualWidth + 0.5, Left(card, scroller) + card.ActualWidth, "pas de défilement horizontal");
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

    /// <summary>Un coffre jetable, et ce qu'il faut pour y poser des définitions.</summary>
    private sealed class Glossary : IDisposable
    {
        private readonly TestVaults _vaults;

        public Glossary()
        {
            _vaults = new TestVaults(AppMode.Development, new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));
            Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Coffre")).Vault);
        }

        public KyfRevApplication App => _vaults.App;

        public FakeOpener Opener { get; } = new();

        /// <summary>Quatre définitions, sur trois dossiers ; rend les fiches d'histoire et de la cellule.</summary>
        public (string History, string Cell) Filled()
        {
            var history = App.Sheets.Create("Histoire", "La Révolution", null).Value;
            var science = App.Sheets.Create("Sciences", "Le vivant", null).Value;
            var cell = App.Sheets.Create("Sciences/Cellule", "La cellule", null).Value;
            Assert.IsTrue(App.Annotations.CreateDefinition(history, Passage with { Section = [] }, new Definition("Bastille", "Une prison royale.")).Succeeded);
            Assert.IsTrue(App.Annotations.CreateDefinition(science, Passage, new Definition("Écologie", "L'étude des milieux.")).Succeeded);
            Assert.IsTrue(App.Annotations.CreateDefinition(cell, Passage, new Definition("Noyau", "Il renferme l'ADN.")).Succeeded);
            Assert.IsTrue(App.Annotations.CreateDefinition(cell, Passage, new Definition("Mitose", "Division de la cellule.")).Succeeded);
            return (history, cell);
        }

        public async Task<DefinitionsPageViewModel> LoadedAsync()
        {
            var page = new DefinitionsPageViewModel(App.Annotations, Opener);
            await page.LoadCommand.ExecuteAsync(null);
            return page;
        }

        public void Leave() => Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Autre")).Vault);

        public void Dispose() => _vaults.Dispose();
    }
}
