using System.Collections.Immutable;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La vue « Aujourd'hui », comme dans la 0.x : la liste à réviser d'abord, les rappels, puis les
/// sept jours à venir (replié) ; filtre, tri, états vides, avertissements ; relue à chaque
/// ouverture ; une liste virtualisée, rapide sur 2 000 fiches.
/// </summary>
[TestClass]
public sealed class DashboardTests
{
    private static readonly DateOnly Day = new(2026, 10, 9);

    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose", "Étapes"], 5, "sha256:aa");

    [TestMethod]
    public void Dates_are_written_as_in_the_0x()
    {
        Assert.AreEqual("vendredi 9 octobre", Dates.Long(Day));
        Assert.AreEqual("ven. 9 oct.", Dates.Short(Day));
        Assert.AreEqual("Vendredi 9 octobre", Dates.Capitalized(Dates.Long(Day)));
        Assert.AreEqual(string.Empty, Dates.Capitalized(string.Empty));
        Assert.AreEqual(14, DashboardPageViewModel.StepValue("J14"));
        Assert.AreEqual(0, DashboardPageViewModel.StepValue("J"));
    }

    [TestMethod]
    public async Task The_list_to_review_comes_first_and_upcoming_is_folded()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        using var test = new TestVaults(AppMode.Development, time);
        var app = test.App;
        Assert.IsNotNull(app.Vaults.Create(test.PathOf("Coffre")).Vault);
        var mitosis = app.Sheets.Create("Biologie", "Mitose", null).Value;
        app.Annotations.Create(mitosis, Passage, "Avant la métaphase").Value.ToString();
        time.AdvanceDays(1);
        app.Sheets.Create("Chimie", "Atome", null).Value.ToString();
        time.AdvanceDays(1);
        var meiosis = app.Sheets.Create("Biologie", "Méiose", null).Value;
        app.Annotations.CreateDefinition(meiosis, Passage with { Exact = "prophase", Start = 3 }, new Definition("Prophase", "Première phase.")).Value.ToString();
        time.AdvanceDays(1);

        var page = new DashboardPageViewModel(app.Reminders, new FakeOpener());
        await page.RefreshCommand.ExecuteAsync(null);

        // Le 4 : Mitose et son passage en retard de 2 jours, Atome d'1 jour ; Méiose et sa définition aujourd'hui.
        Assert.IsNull(page.Error);
        Assert.AreEqual("Dimanche 4 octobre · 5 éléments à réviser", page.Subtitle);
        Assert.AreEqual("3 fiches actives · 0 fiche archivée", page.Footer, "aucune échéance après aujourd'hui");
        var rows = page.Rows;
        Assert.AreEqual("En retard, 3", rows[0].ToString());
        Assert.AreEqual("Fiche, Mitose, J1, Retard 2 j", rows[1].ToString());
        // Rappel actif (réglage par défaut) : le passage est caché, son commentaire sert d'indice.
        Assert.AreEqual("Passage, Passage à retrouver · Mitose › Étapes, J1, Retard 2 j", rows[2].ToString());
        Assert.AreEqual("Fiche, Atome, J1, Retard 1 j", rows[3].ToString());
        Assert.AreEqual("Aujourd'hui, 2", rows[4].ToString());
        Assert.AreEqual("Fiche, Méiose, J1", rows[5].ToString());
        Assert.AreEqual("Définition, Prophase · Méiose, J1", rows[6].ToString());
        Assert.HasCount(7, rows);
        Assert.IsFalse(page.HasHealthWarning);
        Assert.IsFalse(page.HasPriorityWarning);
        Assert.IsFalse(page.IsLoading);

        var mitosisRow = (DueItemRow)rows[1];
        Assert.AreEqual(PillKind.Sheet, mitosisRow.PillKind);
        Assert.AreEqual(DueItemHeading.Title, mitosisRow.Heading);
        Same("Biologie | cycle 1 | dernière révision jeu. 1 oct.", mitosisRow.Details);
        Assert.IsFalse(mitosisRow.FileMissing);
        Assert.IsNull(mitosisRow.DueDate);
        Assert.AreEqual(DueItemHeading.Hidden, ((DueItemRow)rows[2]).Heading);
        Assert.AreEqual("Commentaire : Avant la métaphase", ((DueItemRow)rows[2]).Details[0]);
        Assert.AreEqual(DueItemHeading.Term, ((DueItemRow)rows[6]).Heading);

        // Les révisions faites, il ne reste que « À venir », replié ; il se déplie à la demande.
        foreach (var item in page.Dashboard!.Overdue.Concat(page.Dashboard.Today))
        {
            Assert.IsTrue(app.Reviews.Validate(item.Kind, item.Id).Succeeded);
        }
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual("Dimanche 4 octobre · rien à réviser", page.Subtitle);
        Assert.AreEqual("3 fiches actives · 0 fiche archivée", page.Footer);
        Assert.AreEqual("Rien à réviser aujourd'hui. Prochaine révision : mercredi 7 octobre.", page.Rows[0].ToString());
        var upcoming = (DashboardGroup)page.Rows[1];
        Assert.AreEqual("À venir — 7 jours, 5", upcoming.ToString());
        Assert.IsFalse(upcoming.IsExpanded);
        Assert.HasCount(2, page.Rows);
        upcoming.ToggleCommand.Execute(null);
        Assert.HasCount(7, page.Rows);
        Assert.AreEqual("mer. 7 oct.", ((DueItemRow)page.Rows[2]).DueDate);
        upcoming.ToggleCommand.Execute(null);
        Assert.HasCount(2, page.Rows);
    }

    [TestMethod]
    public void Filters_hide_sheets_or_passages_and_the_reminders_with_the_sheets()
    {
        var page = Page();
        page.Show(Sample(reminders: [Reminder("Cellule", previous: 0)]));
        Assert.AreEqual("Vendredi 9 octobre · 5 éléments à réviser", page.Subtitle);

        page.SelectedFilter = page.Filters[1];
        Assert.AreEqual(DashboardFilter.Sheets, page.SelectedFilter.Value);
        Assert.IsTrue(page.Rows.OfType<DueItemRow>().All(row => row.Item.Kind == ItemKind.Sheet));
        Assert.HasCount(1, page.Rows.OfType<ReminderRow>());

        page.SelectedFilter = page.Filters[2];
        Assert.IsTrue(page.Rows.OfType<DueItemRow>().All(row => row.Item.Kind == ItemKind.Passage));
        Assert.IsEmpty(page.Rows.OfType<ReminderRow>());
        Assert.AreEqual("Passages", page.SelectedFilter.ToString());
    }

    [TestMethod]
    public void Sorts_are_stable_and_follow_the_0x()
    {
        var page = Page();
        page.Show(Sample());
        string[] Titles() => [.. page.Rows.OfType<DueItemRow>().Select(row => row.Item.Title)];
        Same("Zèbre | Éléphant | Douane | Fourmi", Titles(), "échéance : l'ordre du coffre");

        page.SelectedSort = page.Sorts[4];
        Same("Éléphant | Zèbre | Douane | Fourmi", Titles(), "titre, dans l'ordre du français (É avant Z)");

        page.SelectedSort = page.Sorts[1];
        Same("Éléphant | Zèbre | Douane | Fourmi", Titles(), "étape J croissante, en nombre (J3 avant J14)");

        page.SelectedSort = page.Sorts[2];
        Same("Zèbre | Éléphant | Fourmi | Douane", Titles(), "cycles décroissants");

        page.SelectedSort = page.Sorts[3];
        Same("Zèbre | Éléphant | Fourmi | Douane", Titles(), "dernière révision, la plus ancienne d'abord");
        Assert.AreEqual(DashboardSort.LastReview, page.SelectedSort.Value);
    }

    [TestMethod]
    public void Empty_states_warnings_and_reminders_read_as_in_the_0x()
    {
        var page = Page();
        page.Show(Empty() with { NextDueDate = null, ActiveSheets = 0, ArchivedSheets = 0 });
        Assert.AreEqual("Créez votre première fiche : sa révision J1 sera due demain.", ((EmptyDashboardRow)page.Rows.Single()).Description);
        Assert.AreEqual("Rien à réviser aujourd'hui", EmptyDashboardRow.Title);
        Assert.AreEqual("0 fiche active · 0 fiche archivée", page.Footer);

        page.Show(Empty() with { NextDueDate = null, ActiveSheets = 0, ArchivedSheets = 4 });
        Assert.AreEqual("Toutes vos fiches sont archivées ; les rappels de consolidation apparaîtront ici.", ((EmptyDashboardRow)page.Rows.Single()).Description);

        page.Show(Empty() with { HealthProblems = 1, UnservedPriorities = 1 });
        Assert.AreEqual("1 point à vérifier dans le coffre (fichier manquant, lien à revoir…).", page.HealthWarning);
        Assert.AreEqual("1 fiche attend un rappel depuis plus de 30 jours : augmentez le nombre de rappels quotidiens.", page.PriorityWarning);
        page.Show(Empty() with { HealthProblems = 2, UnservedPriorities = 3 });
        Assert.AreEqual("2 points à vérifier dans le coffre (fichier manquant, lien à revoir…).", page.HealthWarning);
        Assert.AreEqual("3 fiches attendent un rappel depuis plus de 30 jours : augmentez le nombre de rappels quotidiens.", page.PriorityWarning);
        Assert.IsTrue(page.HasHealthWarning);
        Assert.IsTrue(page.HasPriorityWarning);

        page.Show(Empty() with { Reminders = [Reminder("Cellule", previous: 0), Reminder("Noyau", previous: 2) with { Folder = string.Empty }] });
        var reminders = page.Rows.OfType<ReminderRow>().ToList();
        Assert.AreEqual("Rappels de consolidation, 2", page.Rows[0].ToString());
        Assert.AreEqual("Rappel, Cellule, 1er rappel, Archivée le mar. 1 sept.", reminders[0].ToString());
        Assert.AreEqual("3e rappel", reminders[1].Rank);
        Assert.AreEqual("Biologie", reminders[0].Folder);
        Assert.AreEqual(string.Empty, reminders[1].Folder);
    }

    [TestMethod]
    public void Active_recall_hides_the_passages_but_not_the_terms()
    {
        var hidden = new DueItemRow(Item("Mitose", ItemKind.Passage, "J3", excerpt: "La prophase", comment: "Avant la métaphase"), activeRecall: true, showDueDate: false);
        Assert.AreEqual(DueItemHeading.Hidden, hidden.Heading);
        Assert.AreEqual("Passage à retrouver", hidden.HeadingText);
        Assert.AreEqual("· Mitose › Étapes", hidden.Source);
        Assert.AreEqual("Commentaire : Avant la métaphase", hidden.Details[0]);

        var term = new DueItemRow(Item("Mitose", ItemKind.Passage, "J3", excerpt: "prophase", term: "Prophase"), activeRecall: true, showDueDate: false);
        Assert.AreEqual(DueItemHeading.Term, term.Heading);
        Assert.AreEqual("Prophase", term.HeadingText);

        var shown = new DueItemRow(Item("Mitose", ItemKind.Passage, "J3", excerpt: "La prophase", comment: "Avant la métaphase"), activeRecall: false, showDueDate: true);
        Assert.AreEqual("« La prophase »", shown.HeadingText);
        Assert.IsFalse(shown.Details.Any(detail => detail.StartsWith("Commentaire", StringComparison.Ordinal)));
        Assert.AreEqual("ven. 9 oct.", shown.DueDate);

        var missing = new DueItemRow(Item("Disparue", ItemKind.Sheet, "J7", daysLate: 4) with { FilePresent = false, Tags = ["bio", "examen"], Folder = string.Empty }, activeRecall: false, showDueDate: true);
        Assert.IsTrue(missing.FileMissing);
        Assert.AreEqual("Retard 4 j", missing.Late);
        Assert.IsNull(missing.DueDate);
        Same("#bio | #examen | cycle 1 | dernière révision jeu. 1 oct.", missing.Details);
    }

    [TestMethod]
    public async Task Without_a_vault_the_refusal_is_shown()
    {
        using var test = new TestVaults(AppMode.Development);
        var page = new DashboardPageViewModel(test.App.Reminders, new FakeOpener());

        await page.RefreshCommand.ExecuteAsync(null);

        Assert.AreEqual("Aucun coffre n'est ouvert.", page.Error);
        Assert.IsTrue(page.HasError);
        Assert.IsEmpty(page.Rows);
        Assert.ThrowsExactly<ArgumentNullException>(() => new DashboardPageViewModel(null!, new FakeOpener()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DashboardPageViewModel(test.App.Reminders, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => page.Show(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => CorePages.All(null!));
    }

    [TestMethod]
    public async Task The_dashboard_is_read_again_each_time_the_view_opens()
    {
        using var test = new TestVaults(AppMode.Development);
        var page = new DashboardPageViewModel(test.App.Reminders, new FakeOpener());
        var shell = new ShellViewModel(CorePages.All(new Dictionary<string, object> { [CorePages.Today] = page }), new TestShell().Appearance, new object());
        Assert.AreSame(page, shell.Home.Page);
        var reads = 0;
        page.PropertyChanged += (_, e) => reads += e.PropertyName == nameof(DashboardPageViewModel.Dashboard) ? 1 : 0;

        // Pas de coffre, pas de lecture : la coquille n'a pas encore été ouverte sur un coffre.
        shell.Navigate(shell.MainNavigation[1]);
        shell.Navigate(shell.Home);
        Assert.IsNull(page.Dashboard);

        Assert.IsNotNull(test.App.Vaults.Create(test.PathOf("Coffre")).Vault);
        shell.ShowWorkspace();
        await WaitForAsync(() => reads == 1);
        shell.Navigate(shell.MainNavigation[1]);
        shell.Navigate(shell.Home);
        await WaitForAsync(() => reads == 2);
        Assert.AreEqual("0 fiche active · 0 fiche archivée", page.Footer);
    }

    [STATestMethod]
    public void The_view_shows_the_groups_and_rows_and_folds_with_the_keyboard()
    {
        var test = new TestShell();
        var page = Page();
        page.Show(Sample(reminders: [Reminder("Cellule", previous: 1)]) with { HealthProblems = 2 });
        var shell = new ShellViewModel(CorePages.All(new Dictionary<string, object> { [CorePages.Today] = page }), test.Appearance, test.Gallery);
        var window = Show(test, shell);
        try
        {
            var list = Named<ListBox>(window, "Items");
            Assert.AreEqual("Éléments à réviser", AutomationProperties.GetName(list));
            Assert.AreEqual("Vendredi 9 octobre · 5 éléments à réviser", Named<TextBlock>(window, "Subtitle").Text);
            Assert.AreEqual(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(Named<TextBlock>(window, "Heading")));
            Assert.IsTrue(Named<Notification>(window, "HealthWarning").IsOpen);
            Assert.IsFalse(Named<Notification>(window, "PriorityWarning").IsOpen);
            Assert.IsFalse(Named<Notification>(window, "ErrorMessage").IsOpen);
            Assert.AreEqual(Visibility.Collapsed, Named<StackPanel>(window, "Loading").Visibility);
            var headers = Descendants(list).OfType<ToggleButton>().Where(button => button.Name == "Header").ToList();
            Same("En retard | Aujourd'hui | Rappels de consolidation", headers.Select(AutomationProperties.GetName));
            Assert.AreEqual(AutomationHeadingLevel.Level2, AutomationProperties.GetHeadingLevel(headers[0]));
            Assert.IsGreaterThanOrEqualTo(4, Descendants(list).OfType<Pill>().Count(pill => pill.Kind is PillKind.Sheet or PillKind.Passage));

            // Entrée sur l'en-tête replie le groupe, Espace le déplie, le focus restant sur l'en-tête ;
            // une autre touche ne fait rien.
            var overdue = (DashboardGroup)page.Rows[0];
            var first = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
            Assert.IsTrue(first.Focus());
            var count = page.Rows.Count;
            Press(first, Key.Enter);
            Assert.HasCount(count - 2, page.Rows);
            Assert.AreSame(overdue, ((FrameworkElement)Keyboard.FocusedElement).DataContext);
            Press((UIElement)Keyboard.FocusedElement, Key.Space);
            Assert.HasCount(count, page.Rows);
            Assert.AreSame(overdue, ((FrameworkElement)Keyboard.FocusedElement).DataContext);
            Press((UIElement)Keyboard.FocusedElement, Key.Down);
            Assert.HasCount(count, page.Rows);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Two_thousand_overdue_sheets_are_displayed_quickly()
    {
        var test = new TestShell();
        var page = Page();
        var shell = new ShellViewModel(CorePages.All(new Dictionary<string, object> { [CorePages.Today] = page }), test.Appearance, test.Gallery);
        var window = Show(test, shell);
        try
        {
            var many = Empty() with
            {
                Overdue = [.. Enumerable.Range(0, 2000).Select(n => Item($"Fiche {n}", ItemKind.Sheet, "J3", daysLate: 1 + (n % 30)))],
                ActiveSheets = 2000,
            };
            var list = Named<ListBox>(window, "Items");

            // La meilleure de trois présentations : sur la machine partagée de la CI, une mesure isolée
            // peut être ralentie par à-coups.
            var best = TimeSpan.MaxValue;
            for (var display = 0; display < 3; display++)
            {
                var clock = Stopwatch.StartNew();
                page.Show(many);
                list.UpdateLayout();
                clock.Stop();
                best = clock.Elapsed < best ? clock.Elapsed : best;
            }

            Assert.HasCount(2001, page.Rows);
            var realized = Enumerable.Range(0, page.Rows.Count).Count(index => list.ItemContainerGenerator.ContainerFromIndex(index) is not null);
            Assert.IsLessThan(100, realized, "la liste est virtualisée");
            // Critère de J6 : moins de 300 ms en tout ; le calcul en prend moins de 150 (DashboardTests du coffre).
            Assert.IsLessThan(TimeSpan.FromMilliseconds(150), best, $"Affichage : {best.TotalMilliseconds:0} ms");
        }
        finally
        {
            window.Close();
        }
    }

    private static void Same(string expected, IEnumerable<string> actual, string message = "") =>
        Assert.AreEqual(expected, string.Join(" | ", actual), message);

    private static DashboardPageViewModel Page()
    {
        var test = new TestVaults(AppMode.Development);
        test.Dispose();
        return new DashboardPageViewModel(test.App.Reminders, new FakeOpener());
    }

    private static ShellWindow Show(TestShell test, ShellViewModel shell)
    {
        var window = new ShellWindow(shell) { ShowActivated = true, Left = -10000, Top = -10000, Width = 1200, Height = 800, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        window.Activate();
        TestShell.Flush();
        return window;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.IsLessThan(deadline, DateTime.UtcNow, "délai dépassé");
            await Task.Yield();
        }
    }

    private static void Press(UIElement element, Key key) =>
        element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

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

    private static DueItem Item(string title, ItemKind kind, string step, int daysLate = 0, int cycle = 1, string? excerpt = null, string? term = null, string? comment = null, int lastReviewDay = 1) =>
        new(kind, $"id-{title}", $"fiche-{title}", title, excerpt, term, ["Mitose", "Étapes"], comment, "Biologie", [], step, Day, daysLate, new DateOnly(2026, 10, lastReviewDay), cycle, true);

    private static PendingReminder Reminder(string title, int previous) =>
        new($"fiche-{title}", title, "Biologie", [], new DateOnly(2026, 9, 1), Day, previous);

    private static Dashboard Empty() =>
        new(Day, [], [], [], [], [], 2, false, [], new DateOnly(2026, 10, 12), 3, 0, 0, 0, false, false);

    /// <summary>
    /// Deux retards et deux éléments du jour, dans l'ordre du coffre ; chaque tri change l'ordre
    /// d'au moins un groupe.
    /// </summary>
    private static Dashboard Sample(ImmutableArray<PendingReminder> reminders = default) => Empty() with
    {
        Overdue = [Item("Zèbre", ItemKind.Sheet, "J7", daysLate: 3, cycle: 2, lastReviewDay: 3), Item("Éléphant", ItemKind.Passage, "J1", daysLate: 1, excerpt: "trompe", lastReviewDay: 5)],
        Today = [Item("Douane", ItemKind.Sheet, "J3", cycle: 3, lastReviewDay: 2), Item("Fourmi", ItemKind.Passage, "J14", cycle: 5, excerpt: "colonie", lastReviewDay: 1)],
        Reminders = reminders.IsDefault ? [] : reminders,
    };
}
