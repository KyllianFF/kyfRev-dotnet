using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La charge des 30 prochains jours : les colonnes (échéances sous les étapes projetées), le résumé, le jour choisi, et le
/// déplacement d'une échéance vers un jour moins chargé ; à la plus petite largeur, et au clavier.
/// </summary>
[TestClass]
public sealed class WorkloadTests
{
    private static readonly Anchor Passage = new("La prophase", "", " précède la métaphase.", 0, 11, ["Mitose", "Étapes"], 5, "sha256:aa");

    private static readonly DateOnly October1 = new(2026, 10, 1);

    [TestMethod]
    public async Task There_is_a_column_for_today_and_the_thirty_days_after()
    {
        using var vault = new LoadVault();
        var page = await vault.PageAsync();

        Assert.HasCount(31, page.Days);
        Assert.AreEqual(October1, page.Days[0].Day);
        Assert.AreEqual(new DateOnly(2026, 10, 31), page.Days[^1].Day);
        Assert.AreEqual("auj.", page.Days[0].Label);
        Assert.AreEqual("2", page.Days[1].Label);

        // J1 attendu le 2 ; si elle est faite à temps : J3 le 5, J7 le 12, J14 le 26.
        var due = page.Days[1];
        Assert.AreEqual(1, due.DueCount);
        Assert.AreEqual(0, due.ProjectedCount);
        Assert.IsFalse(due.HasBoth);
        var projected = page.Days[4];
        Assert.AreEqual(0, projected.DueCount);
        Assert.AreEqual(1, projected.ProjectedCount);
        Assert.AreEqual(4, page.Total);
        Assert.IsTrue(page.HasLoad);
        Assert.IsFalse(page.ShowsEmpty);
        Assert.AreEqual("En moyenne 0,1 par jour · le plus chargé : ven. 2 oct. (1)", page.Summary);
        Assert.AreEqual("vendredi 2 octobre : 1 révision", due.Name);
        Assert.AreEqual(due.Name, due.ToString());
        Assert.AreEqual("lundi 5 octobre : 1 révision, dont 1 projetée", projected.Name);
        Assert.AreEqual("jeudi 1 octobre : aucune révision", page.Days[0].Name);
        Assert.IsTrue(due.IsPeak);
        Assert.IsTrue(due.ShowsValue, "le jour le plus chargé écrit son nombre");
        Assert.IsFalse(projected.ShowsValue);
        Assert.IsFalse(page.HasNote);
        Assert.IsFalse(page.HasSelection);
        Assert.IsTrue(page.Days.Single(day => day.Day == new DateOnly(2026, 10, 3)).IsWeekend);
        Assert.IsFalse(due.IsWeekend);
    }

    [TestMethod]
    public async Task Known_due_dates_stack_under_projected_steps_on_the_same_day()
    {
        using var vault = new LoadVault();
        // La seconde fiche, due le 5 : ce jour-là, son échéance connue et J3 projeté de la première.
        var other = vault.App.Sheets.Create("Chimie", "Atome", null).Value;
        Assert.IsTrue(vault.App.Reviews.Postpone(ItemKind.Sheet, other, new DateOnly(2026, 10, 5)).Succeeded);
        var page = await vault.PageAsync();

        var both = page.Days[4];
        Assert.IsTrue(both.HasBoth);
        Assert.AreEqual(1, both.DueCount);
        Assert.AreEqual(1, both.ProjectedCount);
        Assert.AreEqual("lundi 5 octobre : 2 révisions, dont 1 projetée", both.Name);
        Assert.AreEqual(both.DueHeight, both.ProjectedHeight);
        Assert.AreEqual(WorkloadPageViewModel.ShortBar / 2, both.DueHeight, "la colonne la plus chargée prend toute la hauteur");
        Assert.IsTrue(both.IsPeak);
    }

    [TestMethod]
    public void A_nearly_empty_day_stays_visible_and_an_empty_one_has_no_height()
    {
        var day = new WorkloadDay(October1.AddDays(1), [Item("A")], []);
        static double Height(double count) => count == 0 ? 0 : Math.Max(3, count / 100 * 96);

        var column = new LoadDay(day, isToday: true, isPeak: false, Height, 0.5, 114);

        Assert.AreEqual(3, column.DueHeight);
        Assert.AreEqual(0, column.ProjectedHeight);
        Assert.AreEqual(3, column.AverageHeight);
        Assert.AreEqual(114, column.PlotHeight);
        Assert.AreEqual("auj.", column.Label);
        Assert.AreEqual(string.Empty, column.Month);
        Assert.AreEqual("oct.", new LoadDay(day with { Day = October1 }, isToday: false, isPeak: false, Height, 0, 114).Month);
    }

    [TestMethod]
    public async Task An_empty_vault_says_so()
    {
        using var vault = new LoadVault(sheet: false);
        var page = await vault.PageAsync();

        Assert.IsTrue(page.ShowsEmpty);
        Assert.IsFalse(page.HasLoad);
        Assert.AreEqual(string.Empty, page.Summary);
        Assert.IsTrue(page.Days.All(day => !day.ShowsValue));
    }

    [TestMethod]
    public async Task The_filter_of_the_list_applies()
    {
        using var vault = new LoadVault();
        Assert.IsNotNull(vault.App.Annotations.Create(vault.SheetId, Passage, "Avant la métaphase").Value);
        var page = await vault.PageAsync();
        Assert.AreEqual(8, page.Total);

        page.SelectedFilter = page.Filters[1];
        Assert.AreEqual(4, page.Total);
        Assert.IsTrue(page.Days[1].Source.Due.All(item => item.Kind == ItemKind.Sheet));

        page.SelectedFilter = page.Filters[2];
        Assert.AreEqual(4, page.Total);
        Assert.IsTrue(page.Days[1].Source.Due.All(item => item.Kind == ItemKind.Passage));

        // Ouverte depuis « Aujourd'hui », la vue reprend son filtre.
        page.Prepare(DashboardFilter.Sheets, () => { });
        Assert.AreEqual(DashboardFilter.Sheets, page.SelectedFilter.Value);
    }

    [TestMethod]
    public async Task The_reminders_are_mentioned_when_sheets_are_archived_except_on_passages_alone()
    {
        using var vault = new LoadVault();
        var other = vault.App.Sheets.Create(null, "Archivée", null).Value;
        Assert.IsTrue(vault.App.Reviews.Archive(other).Succeeded);
        var page = await vault.PageAsync();

        Assert.IsTrue(page.HasNote);
        Assert.AreEqual("S'y ajoutent les rappels de consolidation : 2 fiches archivées par jour au plus, tirées au hasard.", page.Note);

        page.SelectedFilter = page.Filters[2];
        Assert.IsFalse(page.HasNote);

        var settings = vault.App.Settings.Get().Value;
        Assert.IsTrue(vault.App.Settings.Change(settings with { MaxPerDay = 1 }).Succeeded);
        page.SelectedFilter = page.Filters[0];
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual("S'y ajoutent les rappels de consolidation : 1 fiche archivée par jour au plus, tirées au hasard.", page.Note);
    }

    [TestMethod]
    public async Task A_chosen_day_shows_its_reviews_and_hides_what_active_recall_hides()
    {
        using var vault = new LoadVault();
        Assert.IsNotNull(vault.App.Annotations.Create(vault.SheetId, Passage, "Avant la métaphase").Value);
        Assert.IsNotNull(vault.App.Annotations.CreateDefinition(vault.SheetId, Passage with { Exact = "prophase", Start = 3 }, new Definition("Prophase", "Première phase.")).Value);
        var page = await vault.PageAsync();

        page.SelectedDay = page.Days[1];

        Assert.IsTrue(page.HasSelection);
        Assert.IsTrue(page.Days[1].IsSelected);
        Assert.IsTrue(page.Days[1].ShowsValue);
        Assert.AreEqual("Vendredi 2 octobre · 3 révisions", page.DayTitle);
        Assert.IsTrue(page.HasDue);
        Assert.IsFalse(page.HasProjected);
        Assert.HasCount(3, page.DueRows);

        page.SelectedDay = page.Days[4];

        Assert.IsFalse(page.Days[1].IsSelected);
        Assert.IsFalse(page.HasDue);
        Assert.IsTrue(page.HasProjected);
        var rows = page.ProjectedRows;
        Assert.AreEqual("J3 Mitose", rows[0].ToString());
        Assert.AreEqual(rows[0].ToString(), rows[0].Spoken);
        Assert.AreEqual("J3", rows[1].Step);
        Assert.AreEqual("Prophase", rows.First(row => row.Source.Length > 0 && !row.IsHidden).Heading);
        var hidden = rows.Single(row => row.IsHidden);
        Assert.AreEqual(DashboardText.PassageToRecall, hidden.Heading);
        Assert.AreEqual("· Mitose › Étapes", hidden.Source);

        // Le passage caché n'est nommé que par sa fiche, même pour le déplacer.
        page.SelectedDay = page.Days[1];
        page.PrepareMoveCommand.Execute(page.DueRows.Single(row => row.Heading == DueItemHeading.Hidden));
        Assert.AreEqual("Déplacer J1 (passage de « Mitose ») au", page.MoveLabel);
        page.SelectedDay = page.Days[4];

        page.SelectedDay = null;
        Assert.IsFalse(page.HasSelection);
        Assert.AreEqual(string.Empty, page.DayTitle);
        Assert.IsEmpty(page.DueRows);
    }

    [TestMethod]
    public async Task Without_active_recall_a_passage_is_written_out()
    {
        using var vault = new LoadVault();
        var settings = vault.App.Settings.Get().Value;
        Assert.IsTrue(vault.App.Settings.Change(settings with { ActiveRecall = false }).Succeeded);
        Assert.IsNotNull(vault.App.Annotations.Create(vault.SheetId, Passage, null).Value);
        var page = await vault.PageAsync();

        page.SelectedDay = page.Days[4];

        Assert.IsTrue(page.ProjectedRows.Any(row => row.Heading == "« La prophase »" && row.Source == "· Mitose"));

        page.SelectedDay = page.Days[1];
        var passage = page.DueRows.Single(row => row.Item.Kind == ItemKind.Passage);
        page.PrepareMoveCommand.Execute(passage);
        Assert.AreEqual("Déplacer J1 (« La prophase ») au", page.MoveLabel);
    }

    [TestMethod]
    public async Task The_day_proposed_is_the_lightest_nearby()
    {
        using var vault = new LoadVault(sheet: false);
        await vault.PageAsync();

        static LoadDay Column(int offset, int count) => new(new WorkloadDay(October1.AddDays(offset), [.. Enumerable.Repeat(Item("A"), count)], []), offset == 0, false, n => n, 0, 100);
        LoadDay[] days = [Column(0, 0), Column(1, 2), Column(2, 2), Column(3, 5), Column(4, 2), Column(5, 2), Column(6, 3), Column(7, 0)];

        // Autour du 4 (le quatrième jour) : à égalité, le plus proche puis le plus tardif ; pas aujourd'hui.
        Assert.AreEqual(October1.AddDays(4), WorkloadPageViewModel.ProposedDay(days, October1.AddDays(3)));
        // Autour du deuxième jour : aujourd'hui n'est jamais proposé.
        Assert.AreEqual(October1.AddDays(2), WorkloadPageViewModel.ProposedDay(days, October1.AddDays(1)));
        // Le jour le moins chargé l'emporte sur le plus proche.
        Assert.AreEqual(October1.AddDays(7), WorkloadPageViewModel.ProposedDay(days, October1.AddDays(5)));
        // Hors des jours affichés : rien.
        Assert.IsNull(WorkloadPageViewModel.ProposedDay(days, October1.AddDays(40)));
        // Aucune voisine possible.
        Assert.IsNull(WorkloadPageViewModel.ProposedDay([Column(0, 1)], October1));
    }

    [TestMethod]
    public async Task A_due_date_moves_to_a_lighter_day_and_the_load_is_read_again()
    {
        using var vault = new LoadVault();
        var page = await vault.PageAsync();
        page.SelectedDay = page.Days[1];
        var row = page.DueRows.Single();
        Assert.AreEqual("Déplacer J1 : Mitose", row.MoveName);
        Assert.AreEqual(row.ToString(), row.Spoken);

        page.PrepareMoveCommand.Execute(row);

        Assert.IsTrue(page.IsMoving);
        Assert.AreEqual("Déplacer J1 (Mitose) au", page.MoveLabel);
        Assert.AreEqual(new DateOnly(2026, 10, 3), page.MoveTo, "le jour le moins chargé alentour");
        Assert.AreEqual(new DateOnly(2026, 10, 2), page.EarliestMove, "un report ne vise jamais aujourd'hui");
        Assert.AreEqual(WorkloadText.HintEmpty, page.MoveHint);
        Assert.IsTrue(page.MoveCommand.CanExecute(null));

        page.MoveTo = new DateOnly(2026, 10, 12);
        Assert.AreEqual("Ce jour-là : 1 révision.", page.MoveHint);
        page.MoveTo = new DateOnly(2026, 11, 30);
        Assert.AreEqual(WorkloadText.BeyondChart, page.MoveHint);
        page.MoveTo = null;
        Assert.AreEqual(string.Empty, page.MoveHint);
        Assert.IsFalse(page.MoveCommand.CanExecute(null));
        page.MoveTo = new DateOnly(2026, 10, 3);

        await page.MoveCommand.ExecuteAsync(null);

        Assert.AreEqual("J1 (Mitose) déplacée au samedi 3 octobre.", page.Message);
        Assert.IsTrue(page.HasMessage);
        Assert.IsFalse(page.HasError);
        Assert.IsFalse(page.IsMoving);
        Assert.IsFalse(page.IsBusy);
        Assert.AreEqual(page.Days[1], page.SelectedDay, "le jour choisi reste, avec le message");
        Assert.AreEqual(0, page.Days[1].DueCount);
        Assert.AreEqual(1, page.Days[2].DueCount);

        page.SelectedDay = page.Days[2];
        Assert.IsFalse(page.HasMessage, "un autre jour efface le message");
    }

    [TestMethod]
    public async Task A_move_the_vault_refuses_is_said_and_the_field_stays_open()
    {
        using var vault = new LoadVault();
        var page = await vault.PageAsync();
        page.SelectedDay = page.Days[1];
        page.PrepareMoveCommand.Execute(page.DueRows.Single());

        // Le même jour : le coffre le refuse.
        page.MoveTo = new DateOnly(2026, 10, 2);
        await page.MoveCommand.ExecuteAsync(null);

        Assert.AreEqual("Cette révision est déjà prévue le 2026-10-02.", page.Error);
        Assert.IsTrue(page.HasError);
        Assert.IsTrue(page.IsMoving);

        page.CancelMoveCommand.Execute(null);
        Assert.IsFalse(page.IsMoving);
        Assert.IsNull(page.MoveTo);
        Assert.AreEqual(string.Empty, page.MoveLabel);
    }

    [TestMethod]
    public async Task A_closed_vault_is_said_when_the_load_is_read()
    {
        using var vault = new LoadVault();
        var page = await vault.PageAsync();

        vault.App.Vaults.Close();
        await page.RefreshCommand.ExecuteAsync(null);

        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.IsLoading);
    }

    [TestMethod]
    public async Task A_row_opens_for_review_and_back_goes_back()
    {
        using var vault = new LoadVault();
        var page = await vault.PageAsync();
        page.SelectedDay = page.Days[1];
        var row = page.DueRows.Single();

        page.OpenCommand.Execute(row);
        Assert.HasCount(1, vault.Opener.Opened);

        var backs = 0;
        page.BackCommand.Execute(null);
        Assert.AreEqual(0, backs, "pas encore de quoi revenir");
        page.Prepare(DashboardFilter.All, () => backs++);
        page.BackCommand.Execute(null);
        Assert.AreEqual(1, backs);
    }

    [TestMethod]
    public async Task The_chart_uses_one_row_only_when_the_columns_can_be_aimed_at()
    {
        using var vault = new LoadVault();
        var page = await vault.PageAsync();
        var column = page.Days[1];
        Assert.AreEqual(WorkloadPageViewModel.NarrowColumns, page.ChartColumns);

        page.ChartWidth = WorkloadPageViewModel.OneRowMinimum - 1;
        Assert.AreSame(column, page.Days[1], "la disposition n'a pas changé : rien n'est refait");

        page.SelectedDay = column;
        page.ChartWidth = WorkloadPageViewModel.OneRowMinimum;

        Assert.AreEqual(WorkloadPageViewModel.WideColumns, page.ChartColumns);
        Assert.AreNotSame(column, page.Days[1]);
        Assert.AreEqual(WorkloadPageViewModel.TallBar, page.Days[1].DueHeight);
        Assert.AreEqual(WorkloadPageViewModel.TallBar + WorkloadPageViewModel.ValueRoom, page.Days[1].PlotHeight);
        Assert.AreEqual(column.Day, page.SelectedDay!.Day, "le jour choisi reste");
    }

    [TestMethod]
    public async Task The_view_opens_again_on_a_clean_slate_and_reads_the_vault_each_time()
    {
        using var vault = new LoadVault();
        var page = await vault.PageAsync();
        page.SelectedDay = page.Days[1];
        page.PrepareMoveCommand.Execute(page.DueRows.Single());

        page.Prepare(DashboardFilter.All, () => { });
        Assert.IsFalse(page.IsMoving);
        Assert.IsNull(page.SelectedDay);

        Assert.IsTrue(vault.App.Reviews.Postpone(ItemKind.Sheet, vault.SheetId, new DateOnly(2026, 10, 8)).Succeeded);
        page.OnNavigatedTo();
        await page.RefreshCommand.ExecutionTask!;

        Assert.AreEqual(1, page.Days[7].DueCount);
    }

    [STATestMethod]
    public void Only_the_last_reading_shows()
    {
        using var ui = new UiThread();
        using var vault = new LoadVault();
        var page = vault.Page();

        var first = page.RefreshCommand.ExecuteAsync(null);
        Assert.IsTrue(page.IsLoading);
        Assert.IsTrue(vault.App.Reviews.Postpone(ItemKind.Sheet, vault.SheetId, new DateOnly(2026, 10, 8)).Succeeded);
        var second = page.RefreshCommand.ExecuteAsync(null);
        UiThread.Until(() => first.IsCompleted && second.IsCompleted);

        Assert.AreEqual(1, page.Days[7].DueCount, "la dernière lecture demandée");
        Assert.IsFalse(page.IsLoading);
    }

    [STATestMethod]
    public void Forgetting_drops_the_load_and_what_arrives_after_puts_nothing_back()
    {
        using var ui = new UiThread();
        using var vault = new LoadVault();
        var page = vault.Page();
        var read = page.RefreshCommand.ExecuteAsync(null);
        UiThread.Until(() => read.IsCompleted);
        page.ChartWidth = WorkloadPageViewModel.OneRowMinimum;
        page.SelectedFilter = page.Filters[1];
        page.SelectedDay = page.Days[1];
        page.PrepareMoveCommand.Execute(page.DueRows.Single());

        page.Forget();

        Assert.IsEmpty(page.Days);
        Assert.AreEqual(0, page.Total);
        Assert.IsFalse(page.ShowsEmpty);
        Assert.IsNull(page.SelectedDay);
        Assert.IsFalse(page.HasSelection);
        Assert.AreEqual(string.Empty, page.Summary);
        Assert.AreEqual(string.Empty, page.DayTitle);
        Assert.IsEmpty(page.DueRows);
        Assert.IsEmpty(page.ProjectedRows);
        Assert.IsFalse(page.IsMoving);
        Assert.IsNull(page.MoveTo);
        Assert.IsNull(page.EarliestMove);
        Assert.AreEqual(page.Filters[1], page.SelectedFilter, "le filtre reste");
        Assert.AreEqual(WorkloadPageViewModel.WideColumns, page.ChartColumns, "la largeur aussi");

        page.SelectedFilter = page.Filters[0];
        Assert.IsEmpty(page.Days, "rien à refaire sans lecture");

        // Une lecture partie avant l'oubli n'y remet rien.
        var late = page.RefreshCommand.ExecuteAsync(null);
        page.Forget();
        UiThread.Until(() => late.IsCompleted);
        Assert.IsEmpty(page.Days);
        Assert.IsFalse(page.IsLoading);

        // Un déplacement parti avant l'oubli non plus.
        var again = page.RefreshCommand.ExecuteAsync(null);
        UiThread.Until(() => again.IsCompleted);
        page.SelectedDay = page.Days[1];
        page.PrepareMoveCommand.Execute(page.DueRows.Single());
        page.MoveTo = new DateOnly(2026, 10, 9);
        var move = page.MoveCommand.ExecuteAsync(null);
        page.Forget();
        UiThread.Until(() => move.IsCompleted);
        Assert.IsFalse(page.HasMessage);
        Assert.IsEmpty(page.Days);
        Assert.IsFalse(page.IsBusy);
    }

    [STATestMethod]
    public void The_navigation_opens_the_view_and_comes_back()
    {
        using var ui = new UiThread();
        using var vault = new LoadVault();
        var test = new TestShell();
        var navigation = new ReviewNavigation(vault.App.Sheets, vault.App.Annotations, vault.App.Reviews, vault.App.Reminders, vault.App.Vaults, new MemorySessions(), vault.Time, new RecordingLinks(), vault.App.Editing, FakeEnvironment.Markdown());
        navigation.Attach(test.Shell);

        navigation.OpenWorkload(DashboardFilter.All);
        Assert.IsNotInstanceOfType<WorkloadPageViewModel>(test.Shell.Current.Page, "sans la vue tenue par la composition, rien ne s'ouvre");

        var page = vault.Page();
        navigation.Workload = page;
        var from = test.Shell.Current;
        navigation.OpenWorkload(DashboardFilter.Passages);

        Assert.AreSame(page, test.Shell.Current.Page);
        Assert.AreEqual(WorkloadText.Title, test.Shell.Current.Label);
        Assert.AreEqual(DashboardFilter.Passages, page.SelectedFilter.Value);

        page.BackCommand.Execute(null);
        Assert.AreSame(from, test.Shell.Current);
    }

    [STATestMethod]
    public void The_chart_fits_the_smallest_window_and_answers_mouse_and_keyboard()
    {
        using var ui = new UiThread();
        using var vault = new LoadVault();
        var page = vault.Page();
        var test = new TestShell();
        var window = Show(test, page, 470, 540);
        try
        {
            page.RefreshCommand.Execute(null);
            UiThread.Until(() => page.Days.Count > 0);
            TestShell.Flush();
            var chart = Named<ListBox>(window, "Chart");
            Assert.AreEqual(WorkloadText.Days, AutomationProperties.GetName(chart));
            Assert.AreEqual(WorkloadPageViewModel.NarrowColumns, page.ChartColumns, "à l'étroit, deux rangées");
            Assert.HasCount(31, Enumerable.Range(0, 31).Select(chart.ItemContainerGenerator.ContainerFromIndex).OfType<ListBoxItem>().ToList());

            var first = (ListBoxItem)chart.ItemContainerGenerator.ContainerFromIndex(1);
            Assert.IsGreaterThanOrEqualTo(24.0, first.ActualWidth, "assez large pour être visée");
            Assert.IsLessThanOrEqualTo(window.ActualWidth, chart.TranslatePoint(new Point(chart.ActualWidth, 0), window).X, "dans la fenêtre");
            Assert.AreEqual(0, Named<ScrollViewer>(window, "Scroller").ScrollableWidth, "jamais de défilement horizontal");
            var due = Descendants(first).OfType<Border>().First(border => border.Name == "Due");
            Assert.AreEqual(Visibility.Visible, due.Visibility);
            Assert.AreEqual(Visibility.Collapsed, Descendants(first).OfType<Border>().First(border => border.Name == "Projected").Visibility);
            var projectedColumn = (ListBoxItem)chart.ItemContainerGenerator.ContainerFromIndex(4);
            Assert.AreEqual(Visibility.Collapsed, Descendants(projectedColumn).OfType<Border>().First(border => border.Name == "Due").Visibility);
            Assert.AreEqual(test.Design.Dictionary["Kyf.Brush.Surface"], Descendants(projectedColumn).OfType<Border>().First(border => border.Name == "Projected").Background, "une étape projetée n'est que dessinée");

            // Un clic choisit le jour, un autre clic sur lui le rend.
            var inside = Descendants(first).OfType<Grid>().First();
            MouseOn(inside, Mouse.PreviewMouseDownEvent);
            first.IsSelected = true;
            TestShell.Flush();
            Assert.AreEqual(page.Days[1], page.SelectedDay);
            Assert.AreEqual(Visibility.Visible, Named<Card>(window, "DetailCard").Visibility);
            Assert.AreEqual(Visibility.Visible, Descendants(first).OfType<Border>().First(border => border.Name == "Mark").Visibility);
            MouseOn(inside, Mouse.PreviewMouseDownEvent);
            Assert.IsNull(page.SelectedDay);
            TestShell.Flush();
            Assert.AreEqual(Visibility.Collapsed, Named<Card>(window, "DetailCard").Visibility);

            // Au clavier : les flèches parcourent les jours, Entrée amène le détail.
            first.IsSelected = true;
            Assert.IsTrue(first.Focus());
            KeyOn(first, Key.Right);
            UiThread.Until(() => page.SelectedDay == page.Days[2]);
            KeyOn((UIElement)Keyboard.FocusedElement, Key.Enter);
            TestShell.Flush();

            page.SelectedDay = page.Days[1];
            TestShell.Flush();
            MouseOn(inside, Mouse.MouseUpEvent);

            // L'échéance du jour se déplace : la date prend le focus, le formulaire tient dans la largeur.
            var rows = Named<ItemsControl>(window, "DueList");
            var move = Descendants(rows).OfType<Button>().First(button => button.Name == "MoveRow");
            Assert.AreEqual("Déplacer J1 : Mitose", AutomationProperties.GetName(move));
            move.Command.Execute(move.CommandParameter);
            TestShell.Flush();
            var field = Named<DateField>(window, "MoveDay");
            Assert.AreEqual(Visibility.Visible, Named<Border>(window, "MoveForm").Visibility);
            UiThread.Until(() => field.IsKeyboardFocusWithin);
            Assert.AreEqual(page.MoveLabel, AutomationProperties.GetName(field));
            Assert.AreEqual(page.MoveTo, field.Day);
            var confirm = Named<Button>(window, "ConfirmMove");
            Assert.IsLessThanOrEqualTo(window.ActualWidth, confirm.TranslatePoint(new Point(confirm.ActualWidth, 0), window).X);

            var open = Descendants(rows).OfType<Button>().First(button => button.Name == "OpenRow");
            Assert.AreEqual(page.DueRows[0].Spoken, AutomationProperties.GetName(open));
            open.Command.Execute(open.CommandParameter);
            Assert.HasCount(1, vault.Opener.Opened);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_wide_window_keeps_the_thirty_one_columns_on_one_row_in_the_dark_theme_too()
    {
        using var ui = new UiThread();
        using var vault = new LoadVault();
        var page = vault.Page();
        var test = new TestShell(dark: true);
        test.Design.Theme = KyfRev.DesignSystem.Tokens.ThemeKind.Dark;
        var window = Show(test, page, 1200, 800);
        try
        {
            page.RefreshCommand.Execute(null);
            UiThread.Until(() => page.Days.Count > 0);
            TestShell.Flush();

            Assert.AreEqual(WorkloadPageViewModel.WideColumns, page.ChartColumns);
            var chart = Named<ListBox>(window, "Chart");
            var label = Descendants(chart).OfType<TextBlock>().First(text => text.Name == "Label");
            Assert.AreEqual(test.Design.Dictionary["Kyf.Brush.TextSecondary"], label.Foreground);
            Assert.AreEqual(Heading(window), WorkloadText.Title);
        }
        finally
        {
            window.Close();
        }
    }

    private static string Heading(DependencyObject root) => Named<TextBlock>(root, "Heading").Text;

    private static DueItem Item(string title) =>
        new(ItemKind.Sheet, title, title, title, null, null, [], null, string.Empty, [], "J1", October1, 0, October1, 1, true);

    private static ShellWindow Show(TestShell test, object page, double width, double height)
    {
        test.Shell.Navigate(new NavigationItem("charge", WorkloadText.Title, KyfRev.DesignSystem.Controls.Icons.Chart, int.MaxValue, page));
        var window = new ShellWindow(test.Shell) { ShowActivated = true, Left = -10000, Top = -10000, Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        window.Activate();
        TestShell.Flush();
        return window;
    }

    private static void MouseOn(UIElement element, RoutedEvent routed) =>
        element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = routed });

    private static void KeyOn(UIElement element, Key key) =>
        element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element), 0, key) { RoutedEvent = Keyboard.KeyDownEvent });

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

    /// <summary>Un coffre d'essai, le jeudi 1er octobre 2026 : la fiche « Mitose » créée ce jour-là, J1 dû le 2.</summary>
    private sealed class LoadVault : IDisposable
    {
        private readonly TestVaults _vaults;

        public LoadVault(bool sheet = true)
        {
            Time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
            _vaults = new TestVaults(AppMode.Development, Time);
            Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Coffre")).Vault);
            SheetId = sheet ? App.Sheets.Create("Biologie", "Mitose", null).Value : string.Empty;
        }

        public FakeTime Time { get; }

        public FakeOpener Opener { get; } = new();

        public KyfRev.Application.UseCases.KyfRevApplication App => _vaults.App;

        public string SheetId { get; }

        public WorkloadPageViewModel Page() => new(App.Reminders, App.Reviews, Opener);

        public async Task<WorkloadPageViewModel> PageAsync()
        {
            var page = Page();
            await page.RefreshCommand.ExecuteAsync(null);
            return page;
        }

        public void Dispose() => _vaults.Dispose();
    }
}
