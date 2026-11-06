using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Domain.Reviews;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Archives » : ses filtres comme dans la 0.x, ses actions sur un vrai coffre, et sa page.</summary>
[TestClass]
public sealed class ArchivesTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);

    private static readonly string[] AllTitles = ["Écologie", "Mitose", "Révolution"];

    private static readonly string[] FolderLabels = ["Tous", "Histoire", "Sciences", "Sciences po", "Sciences › Biologie"];

    [TestMethod]
    public void A_row_describes_the_sheet_and_its_reminders()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());

        page.Show(
        [
            Entry("Mitose", folder: "Sciences/Biologie", archivedOn: new DateOnly(2026, 9, 18), cycles: 2, eligibleOn: new DateOnly(2026, 11, 18)),
            Entry("Révolution", folder: "", manual: true, cycles: 1, status: ReminderStatus.Pending, lastOn: new DateOnly(2026, 9, 1), last: ReminderOutcome.Known),
            Entry("Écologie", status: ReminderStatus.Eligible, lastOn: new DateOnly(2026, 9, 2), last: ReminderOutcome.SomePointsToReview, cycles: 0),
            Entry("Absente", status: ReminderStatus.Excluded, lastOn: new DateOnly(2026, 9, 3), last: ReminderOutcome.ReviewEntirely, present: false),
        ]);

        StringAssert.StartsWith(page.Summary, "4 fiches archivées · elles restent ici", StringComparison.Ordinal);
        var mitosis = Row(page, "Mitose");
        Assert.AreEqual("Sciences › Biologie · archivée le ven. 18 sept. · 2 cycles terminés", mitosis.Meta);
        Assert.AreEqual("Au repos jusqu'au mer. 18 nov.", mitosis.Status);
        Assert.AreEqual(PillKind.Archived, mitosis.StatusKind);
        Assert.AreEqual("Exclure des rappels", mitosis.ToggleLabel);
        Assert.IsFalse(mitosis.FileMissing);
        var revolution = Row(page, "Révolution");
        Assert.AreEqual("archivée le jeu. 1 oct. (à la main) · 1 cycle terminé · dernier rappel le mar. 1 sept. : connue", revolution.Meta);
        Assert.AreEqual("Rappel en attente", revolution.Status);
        Assert.AreEqual(PillKind.Reminder, revolution.StatusKind);
        var ecology = Row(page, "Écologie");
        StringAssert.Contains(ecology.Meta, "0 cycle terminé", StringComparison.Ordinal);
        StringAssert.Contains(ecology.Meta, "certains points à revoir", StringComparison.Ordinal);
        Assert.AreEqual("Éligible à un rappel", ecology.Status);
        var missing = Row(page, "Absente");
        StringAssert.Contains(missing.Meta, "à revoir entièrement", StringComparison.Ordinal);
        Assert.AreEqual("Exclue des rappels", missing.Status);
        Assert.AreEqual(PillKind.Archived, missing.StatusKind);
        Assert.AreEqual("Réinclure", missing.ToggleLabel);
        Assert.AreEqual("Réinclure : Absente", missing.ToggleName);
        Assert.AreEqual("Remettre en révision : Absente", missing.RestartName);
        Assert.IsTrue(missing.FileMissing);
        StringAssert.Contains(missing.Spoken, "fichier introuvable", StringComparison.Ordinal);
        Assert.AreEqual(missing.Spoken, missing.ToString());
        Assert.AreEqual("Archives", ArchivesPageViewModel.Title);
    }

    [TestMethod]
    public void The_search_ignores_case_and_accents_and_reads_the_tags()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show([Entry("Écologie", tags: ["bio"]), Entry("Révolution", tags: ["Histoire"])]);

        page.Bar.Search = "  ECOLO ";
        ExpectTitles(page, "Écologie");
        page.Bar.Search = "histoire";
        ExpectTitles(page, "Révolution");

        page.Bar.Search = "introuvable";
        Assert.IsFalse(page.HasRows);
        Assert.IsTrue(page.HasNoMatch);
        Assert.IsFalse(page.IsEmpty);
        page.Bar.Search = string.Empty;
        Assert.HasCount(2, page.Rows);
        Assert.IsFalse(page.HasNoMatch);
    }

    [TestMethod]
    public void The_folder_filter_includes_its_subfolders_and_survives_a_new_reading()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        ImmutableArray<ArchivedSheetEntry> entries =
        [
            Entry("Mitose", folder: "Sciences/Biologie"),
            Entry("Atomes", folder: "Sciences"),
            Entry("Révolution", folder: "Histoire"),
            Entry("Sciences po", folder: "Sciences po"),
            Entry("Sans dossier", folder: ""),
        ];
        page.Show(entries);
        var folder = page.Bar.Filters[0];
        Assert.AreEqual("Tous", folder.Choices[0].Label);
        CollectionAssert.AreEquivalent(FolderLabels, folder.Choices.Select(choice => choice.Label).ToArray());

        folder.Selected = folder.Choices.Single(choice => choice.Label == "Sciences");
        Assert.AreEqual("Dossier : Sciences", folder.Name);
        ExpectTitlesInAnyOrder(page, "Mitose", "Atomes");

        // Le choix reste, tant que le dossier existe ; sinon le filtre revient à « Tous ».
        page.Show(entries);
        Assert.AreEqual("Sciences", folder.Selected.Label);
        page.Show([Entry("Révolution", folder: "Histoire")]);
        Assert.AreEqual("Tous", folder.Selected.Label);
        Assert.HasCount(1, page.Rows);
    }

    [TestMethod]
    [DataRow(1, "Mitose")]
    [DataRow(2, "Révolution")]
    [DataRow(3, "Écologie")]
    [DataRow(4, "Absente")]
    public void The_reminder_filter_keeps_one_state(int choice, string kept)
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show(
        [
            Entry("Mitose", status: ReminderStatus.Resting),
            Entry("Révolution", status: ReminderStatus.Eligible),
            Entry("Écologie", status: ReminderStatus.Pending),
            Entry("Absente", status: ReminderStatus.Excluded),
        ]);

        page.Bar.Filters[1].Selected = page.Bar.Filters[1].Choices[choice];

        ExpectTitles(page, kept);
    }

    [TestMethod]
    public void The_period_filter_counts_back_from_today()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener(), new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));
        page.Show(
        [
            Entry("Il y a 31 jours", archivedOn: Day.AddDays(-31)),
            Entry("Il y a 32 jours", archivedOn: Day.AddDays(-32)),
            Entry("Il y a 183 jours", archivedOn: Day.AddDays(-183)),
            Entry("Il y a 184 jours", archivedOn: Day.AddDays(-184)),
        ]);
        var period = page.Bar.Filters[2];

        Assert.HasCount(4, page.Rows);
        period.Selected = period.Choices[1];
        ExpectTitles(page, "Il y a 31 jours");
        period.Selected = period.Choices[2];
        ExpectTitles(page, "Il y a 31 jours", "Il y a 32 jours", "Il y a 183 jours");
        period.Selected = period.Choices[3];
        ExpectTitles(page, "Il y a 184 jours");
    }

    [TestMethod]
    public void The_cycles_filter_counts_the_completed_cycles()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show([Entry("Zéro", cycles: 0), Entry("Un", cycles: 1), Entry("Deux", cycles: 2), Entry("Trois", cycles: 3), Entry("Quatre", cycles: 4)]);
        var cycles = page.Bar.Filters[3];

        cycles.Selected = cycles.Choices[1];
        ExpectTitles(page, "Un");
        cycles.Selected = cycles.Choices[2];
        ExpectTitles(page, "Deux");
        cycles.Selected = cycles.Choices[3];
        ExpectTitlesInAnyOrder(page, "Trois", "Quatre");
        cycles.Selected = cycles.Choices[0];
        Assert.HasCount(5, page.Rows);
    }

    [TestMethod]
    public void The_list_sorts_by_archiving_title_or_cycles()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show(
        [
            Entry("Révolution", archivedOn: Day.AddDays(-3), cycles: 1),
            Entry("Écologie", archivedOn: Day.AddDays(-1), cycles: 2),
            Entry("Mitose", archivedOn: Day.AddDays(-2), cycles: 2),
        ]);
        var sort = page.Bar.Filters[4];

        ExpectTitles(page, "Écologie", "Mitose", "Révolution");
        sort.Selected = sort.Choices[1];
        CollectionAssert.AreEqual(AllTitles, Titles(page), "L'accent ne renvoie pas le titre à la fin de la liste.");
        sort.Selected = sort.Choices[2];
        ExpectTitles(page, "Écologie", "Mitose", "Révolution");
        sort.Selected = sort.Choices[0];
        ExpectTitles(page, "Écologie", "Mitose", "Révolution");
    }

    [TestMethod]
    public async Task Reading_without_a_vault_shows_the_refusal()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());

        await page.RefreshCommand.ExecuteAsync(null);

        Assert.IsTrue(page.HasError);
        Assert.AreEqual(VaultHost.NoVaultOpen, page.Error);
        Assert.IsFalse(page.IsLoading);
        Assert.IsFalse(page.IsEmpty, "On ne dit pas qu'il n'y a rien : la lecture a échoué.");
        Assert.IsFalse(page.HasArchives);
    }

    [TestMethod]
    public async Task An_archived_sheet_can_be_excluded_included_and_restarted()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        using var vaults = new TestVaults(AppMode.Development, time);
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        var sheet = vaults.App.Sheets.Create("Biologie", "Mitose", null).Value;
        Assert.IsTrue(vaults.App.Reviews.Archive(sheet).Succeeded);
        var opener = new FakeOpener();
        var page = Page(vaults, opener, time);

        page.OnNavigatedTo();
        await page.RefreshCommand.ExecutionTask!;
        Assert.IsTrue(page.HasArchives);
        Assert.IsFalse(page.IsEmpty);
        var row = page.Rows.Single();
        Assert.AreEqual(sheet, row.Entry.Id);
        Assert.IsTrue(row.Entry.ManualArchiving);

        page.OpenCommand.Execute(row);
        CollectionAssert.AreEqual(new object[] { sheet }, opener.Opened);

        await page.ToggleRemindersCommand.ExecuteAsync(row);
        Assert.AreEqual("« Mitose » est exclue des rappels de consolidation.", page.Message);
        Assert.IsTrue(page.HasMessage);
        Assert.IsTrue(page.Rows.Single().IsExcluded);

        await page.ToggleRemindersCommand.ExecuteAsync(page.Rows.Single());
        Assert.AreEqual("« Mitose » est réincluse dans les rappels de consolidation.", page.Message);
        Assert.IsFalse(page.Rows.Single().IsExcluded);

        // Renoncer ne change rien ; la confirmation annonce ce qui va se passer.
        page.RequestRestartCommand.Execute(page.Rows.Single());
        Assert.IsTrue(page.IsConfirming);
        StringAssert.StartsWith(page.ConfirmMessage, "« Mitose » redevient active", StringComparison.Ordinal);
        await page.AnswerRestartCommand.ExecuteAsync(false);
        Assert.IsFalse(page.IsConfirming);
        Assert.HasCount(1, page.Rows);

        page.RequestRestartCommand.Execute(page.Rows.Single());
        await page.AnswerRestartCommand.ExecuteAsync(true);
        Assert.AreEqual("« Mitose » est de nouveau en révision : J1 demain.", page.Message);
        Assert.IsFalse(page.HasRows);
        Assert.IsTrue(page.IsEmpty);
        Assert.IsFalse(page.HasArchives);
        Assert.AreEqual(ReviewStatus.Active, vaults.App.Sheets.Open(sheet).Value.Review.Status);

        // Revenir sur la vue efface le message de l'action précédente.
        page.OnNavigatedTo();
        Assert.IsNull(page.Message);
        await page.RefreshCommand.ExecutionTask!;
    }

    [TestMethod]
    public async Task A_refused_action_shows_why_and_keeps_the_list()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show([Entry("Mitose")]);

        await page.ToggleRemindersCommand.ExecuteAsync(page.Rows[0]);

        Assert.AreEqual(VaultHost.NoVaultOpen, page.Error);
        Assert.IsNull(page.Message);
        Assert.HasCount(1, page.Rows);
    }

    [TestMethod]
    public void The_arguments_are_required()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var app = vaults.App;
        var time = TimeProvider.System;
        Assert.ThrowsExactly<ArgumentNullException>(() => new ArchivesPageViewModel(null!, app.Reviews, new FakeOpener(), time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ArchivesPageViewModel(app.Reminders, null!, new FakeOpener(), time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ArchivesPageViewModel(app.Reminders, app.Reviews, null!, time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ArchivesPageViewModel(app.Reminders, app.Reviews, new FakeOpener(), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Page(vaults, new FakeOpener()).Show(null!));
    }

    [STATestMethod]
    public void The_page_shows_its_filters_rows_and_confirmation()
    {
        using var ui = new UiThread();
        var opener = new FakeOpener();
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, opener);
        page.Show([Entry("Mitose", folder: "Biologie"), Entry("Révolution", folder: "Histoire")]);
        var test = new TestShell();
        var window = Show(test, page);
        try
        {
            Assert.AreEqual("Archives", Named<TextBlock>(window, "Heading").Text);
            Assert.IsTrue(Named<Card>(window, "ListCard").IsVisible);
            Assert.IsFalse(Named<EmptyState>(window, "Empty").IsVisible);
            Assert.IsFalse(Named<TextBlock>(window, "NoMatch").IsVisible);
            var rows = Descendants(window).OfType<Button>().Where(button => button.Name == "OpenRow").ToList();
            Assert.HasCount(2, rows);
            Assert.AreEqual(page.Rows[0].Spoken, System.Windows.Automation.AutomationProperties.GetName(rows[0]));

            // Un filtre : son menu s'ouvre sous le bouton, et le choix est retenu.
            var filter = Descendants(window).OfType<Button>().First(button => button.Name == "FilterButton");
            filter.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            TestShell.Flush();
            var menu = filter.ContextMenu;
            Assert.IsTrue(menu.IsOpen);
            Assert.AreSame(filter, menu.PlacementTarget);
            Assert.AreEqual(PlacementMode.Bottom, menu.Placement);
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.AreEqual("Tous", page.Bar.Filters[0].Selected.Label);
            var choice = page.Bar.Filters[0].Choices[2];
            ((MenuItem)menu.ItemContainerGenerator.ContainerFromItem(choice)).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.IsOpen = false;
            Assert.AreSame(choice, page.Bar.Filters[0].Selected);
            Assert.HasCount(1, page.Rows);
            TestShell.Flush();

            // Rien ne correspond : on le dit, sans perdre les filtres.
            page.Bar.Search = "introuvable";
            TestShell.Flush();
            Assert.IsTrue(Named<TextBlock>(window, "NoMatch").IsVisible);
            Assert.IsFalse(Named<Card>(window, "ListCard").IsVisible);
            Assert.IsTrue(Named<FilterBar>(window, "FilterBar").IsVisible);
            page.Bar.Search = string.Empty;
            TestShell.Flush();

            // La confirmation passe par le dialogue de la page.
            page.RequestRestartCommand.Execute(page.Rows[0]);
            TestShell.Flush();
            var dialog = Named<Dialog>(window, "Confirmation");
            Assert.IsTrue(dialog.IsOpen);
            Assert.AreEqual("Remettre en révision ?", dialog.Title);
            dialog.Close(DialogChoice.Secondary);
            TestShell.Flush();
            Assert.IsFalse(page.IsConfirming);

            // Le titre garde sa place : quand l'état et les actions la lui prendraient (vu à 760 pixels, une lettre par ligne),
            // ils passent dessous, alignés sur lui.
            var layout = Descendants(window).OfType<AdaptiveRow>().First(row => row.Name == "RowLayout");
            var side = Descendants(window).OfType<StackPanel>().First(panel => panel.Name == "Side");
            var open = Descendants(window).OfType<Button>().First(button => button.Name == "OpenRow");
            var title = Descendants(open).OfType<TextBlock>().First();
            foreach (var width in new[] { 1400.0, 1000.0, 870.0, 760.0, 640.0 })
            {
                window.Width = width;
                window.UpdateLayout();
                TestShell.Flush();
                Assert.IsTrue(layout.IsStacked || open.ActualWidth >= layout.MainMinWidth, $"à {width} pixels, le titre garde sa place");
            }
            Assert.IsTrue(layout.IsStacked);
            Assert.IsGreaterThan(title.TranslatePoint(default, window).Y, side.TranslatePoint(default, window).Y, "l'état et les actions sous le titre");
            Assert.AreEqual(HorizontalAlignment.Left, side.HorizontalAlignment);

            // Très étroite : l'état au-dessus des actions.
            window.Width = 1100;
            window.UpdateLayout();
            TestShell.Flush();
            Assert.IsTrue(page.HasRoom);
            page.PageWidth = ArchivesPageViewModel.RoomMinimum - 1;
            TestShell.Flush();
            Assert.IsFalse(page.HasRoom);
            Assert.AreEqual(Orientation.Vertical, side.Orientation);

            // La page quittée perd son contexte sans rien lever.
            test.Shell.Navigate(test.Shell.Gallery);
            TestShell.Flush();
            Assert.AreSame(test.Shell.Gallery, test.Shell.Current);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void On_a_small_screen_the_filters_fold_and_the_chosen_ones_stay_visible()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show([Entry("Mitose", folder: "Biologie", cycles: 2), Entry("Révolution", folder: "Histoire", cycles: 1)]);

        // Avec de la place : tout est là, sans bouton ni étiquette.
        Assert.IsTrue(page.Bar.ShowsFilters);
        Assert.IsFalse(page.Bar.ShowsToggle);
        Assert.IsFalse(page.Bar.ShowsActive);
        StringAssert.Contains(page.Summary, "elles restent ici", StringComparison.Ordinal);

        page.PageWidth = ArchivesPageViewModel.RoomMinimum - 1;
        Assert.IsFalse(page.Bar.ShowsFilters);
        Assert.IsTrue(page.Bar.ShowsToggle);
        Assert.AreEqual("2 fiches archivées", page.Summary);
        Assert.AreEqual("Filtres", page.Bar.ToggleLabel);
        Assert.IsFalse(page.Bar.ShowsActive);

        page.Bar.AreOpen = true;
        Assert.IsTrue(page.Bar.ShowsFilters);
        var folder = page.Bar.Filters[0];
        var cycles = page.Bar.Filters[3];
        folder.Selected = folder.Choices.Single(choice => choice.Label == "Biologie");
        cycles.Selected = cycles.Choices[2];
        Assert.AreEqual("Filtres · 2", page.Bar.ToggleLabel);
        Assert.IsFalse(page.Bar.ShowsActive, "panneau ouvert, les filtres se lisent déjà");
        CollectionAssert.AreEqual(new[] { folder, cycles }, page.Bar.ActiveFilters.ToArray());
        Assert.AreEqual("Retirer le filtre Dossier : Biologie", folder.RemoveName);

        // Le tri ne retire aucune fiche : ce n'est pas un filtre à signaler.
        var sort = page.Bar.Filters[4];
        sort.Selected = sort.Choices[1];
        Assert.HasCount(2, page.Bar.ActiveFilters);

        // Replier le panneau garde les filtres, et les étiquettes les retirent.
        page.Bar.AreOpen = false;
        Assert.IsFalse(page.Bar.ShowsFilters);
        Assert.IsTrue(page.Bar.ShowsActive);
        ExpectTitles(page, "Mitose");
        folder.ClearCommand.Execute(null);
        Assert.AreEqual("Filtres · 1", page.Bar.ToggleLabel);
        Assert.AreEqual("Tous", folder.Selected.Label);
        cycles.ClearCommand.Execute(null);
        Assert.AreEqual("Filtres", page.Bar.ToggleLabel);
        Assert.IsFalse(page.Bar.ShowsActive);
        Assert.HasCount(2, page.Rows);
    }

    [STATestMethod]
    public void On_a_small_screen_the_search_fills_the_line_next_to_the_filters_button()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show([Entry("Mitose", folder: "Biologie"), Entry("Révolution", folder: "Histoire")]);
        var window = Show(new TestShell(), page);
        try
        {
            var search = Named<TextBox>(window, "SearchBox");
            var toggle = Named<ToggleButton>(window, "FiltersToggle");
            Assert.AreEqual(260, search.ActualWidth, 1);
            Assert.IsFalse(toggle.IsVisible);
            Assert.IsTrue(Named<ItemsControl>(window, "Filters").IsVisible);

            page.PageWidth = 358;
            TestShell.Flush();
            window.UpdateLayout();
            Assert.IsTrue(toggle.IsVisible);
            Assert.IsGreaterThan(260, search.ActualWidth, "la recherche prend la ligne");
            Assert.AreEqual(search.TranslatePoint(default, window).Y, toggle.TranslatePoint(default, window).Y, 0.5, "sur la même ligne");
            Assert.IsFalse(Named<ItemsControl>(window, "Filters").IsVisible);
            Assert.IsFalse(Named<ItemsControl>(window, "ActiveFilters").IsVisible);

            toggle.IsChecked = true;
            TestShell.Flush();
            Assert.IsTrue(page.Bar.AreOpen);
            Assert.IsTrue(Named<ItemsControl>(window, "Filters").IsVisible);

            page.Bar.Filters[0].Selected = page.Bar.Filters[0].Choices[1];
            toggle.IsChecked = false;
            TestShell.Flush();
            Assert.IsFalse(Named<ItemsControl>(window, "Filters").IsVisible);
            Assert.IsTrue(Named<ItemsControl>(window, "ActiveFilters").IsVisible);
            var chip = Descendants(window).OfType<Button>().Single(button => button.Name == "RemoveFilter");
            Assert.AreEqual(page.Bar.Filters[0].RemoveName, System.Windows.Automation.AutomationProperties.GetName(chip));
            chip.Command.Execute(null);
            TestShell.Flush();
            Assert.IsFalse(page.Bar.HasActiveFilters);
            Assert.IsFalse(Named<ItemsControl>(window, "ActiveFilters").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void An_empty_vault_says_so_without_filters()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show([]);
        var window = Show(new TestShell(), page);
        try
        {
            Assert.IsTrue(Named<EmptyState>(window, "Empty").IsVisible);
            Assert.IsFalse(Named<FilterBar>(window, "FilterBar").IsVisible);
            Assert.IsFalse(Named<Card>(window, "ListCard").IsVisible);
            StringAssert.StartsWith(page.Summary, "0 fiche archivée", StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    private static ArchivesPageViewModel Page(TestVaults vaults, FakeOpener opener, TimeProvider? time = null) =>
        new(vaults.App.Reminders, vaults.App.Reviews, opener, time ?? new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));

    private static ArchivedSheetEntry Entry(
        string title,
        string folder = "Histoire",
        DateOnly? archivedOn = null,
        bool manual = false,
        ReminderStatus status = ReminderStatus.Resting,
        DateOnly? eligibleOn = null,
        int cycles = 1,
        DateOnly? lastOn = null,
        ReminderOutcome? last = null,
        bool present = true,
        ImmutableArray<string> tags = default) =>
        new(
            $"f_{title}",
            title,
            folder,
            tags.IsDefault ? [] : tags,
            archivedOn ?? Day,
            manual,
            status,
            eligibleOn ?? Day.AddDays(61),
            null,
            cycles,
            last is null ? 0 : 1,
            lastOn,
            last,
            present);

    private static ArchiveRow Row(ArchivesPageViewModel page, string title) => page.Rows.Single(row => row.Title == title);

    private static string[] Titles(ArchivesPageViewModel page) => [.. page.Rows.Select(row => row.Title)];

    private static void ExpectTitles(ArchivesPageViewModel page, params string[] expected) => CollectionAssert.AreEqual(expected, Titles(page));

    private static void ExpectTitlesInAnyOrder(ArchivesPageViewModel page, params string[] expected) => CollectionAssert.AreEquivalent(expected, Titles(page));

    private static ShellWindow Show(TestShell test, object page)
    {
        var shell = test.Shell;
        shell.Navigate(new NavigationItem("archives", "Archives", Icons.Archive, int.MaxValue, page));
        var window = new ShellWindow(shell) { ShowActivated = true, Left = -10000, Top = -10000, Width = 1100, Height = 800, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        window.Activate();
        TestShell.Flush();
        return window;
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
