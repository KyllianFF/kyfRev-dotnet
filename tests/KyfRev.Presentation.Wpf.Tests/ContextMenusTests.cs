using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le clic droit : les actions rapides en tête, puis la liste, selon ce qui est visé.</summary>
[TestClass]
public sealed class ContextMenusTests
{
    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose", "Étapes"], 5, "sha256:aa");

    private static readonly string[] SheetRowMenu = [DashboardText.ReviewAction, DashboardText.OpenSheet, ReviewText.PostponeTomorrow, ReviewText.ArchiveAction];

    private static readonly string[] SheetRowQuick = [DashboardText.ReviewAction, DashboardText.OpenSheet];

    private static readonly string[] Runs = ["ouvrir", "supprimer", "copier"];

    private static readonly string[] CopiedLine = ["Mitose : Fiche manquante"];

    [STATestMethod]
    public void A_quick_action_and_an_entry_run_what_they_say()
    {
        using var ui = new UiThread();
        var runs = new List<string>();

        var quick = ActionMenus.Quick("Ouvrir", Icons.Document, () => runs.Add("ouvrir"));
        var off = ActionMenus.Quick("Réviser", Icons.Review, () => runs.Add("réviser"), isEnabled: false);
        var entry = ActionMenus.Item("Supprimer", () => runs.Add("supprimer"), "Suppr");
        var command = ActionMenus.Quick("Copier", Icons.Copy, new RelayCommand(() => runs.Add("copier")), new TextBox());

        Click(quick);
        Click(entry);
        command.Command.Execute(null);

        CollectionAssert.AreEqual(Runs, runs);
        Assert.AreEqual("Ouvrir", quick.Header);
        Assert.AreEqual(Icons.Document, ((Icon)quick.Icon).Text);
        Assert.IsFalse(off.IsEnabled);
        Assert.AreEqual("Suppr", entry.InputGestureText);
        Assert.IsNotNull(command.CommandTarget);
    }

    [STATestMethod]
    public void A_menu_without_entries_stays_closed()
    {
        using var ui = new UiThread();
        var menu = new ContextMenu();
        var refused = Opening(new Border());
        ActionMenus.Fill(menu, [], refused);
        Assert.IsTrue(refused.Handled);

        var opened = Opening(new Border());
        ActionMenus.Fill(menu, [ActionMenus.Item("Un", () => { })], opened);
        Assert.IsFalse(opened.Handled);
        Assert.HasCount(1, menu.Items);

        ActionMenus.Fill(menu, [ActionMenus.Item("Deux", () => { }), new Separator()], opened);
        Assert.HasCount(2, menu.Items, "le menu repart de rien à chaque ouverture");
        Assert.ThrowsExactly<ArgumentNullException>(() => ActionMenus.Fill(null!, [], opened));
        Assert.ThrowsExactly<ArgumentNullException>(() => ActionMenus.Fill(menu, null!, opened));
        Assert.ThrowsExactly<ArgumentNullException>(() => ActionMenus.Fill(menu, [], null!));
    }

    [STATestMethod]
    public void A_line_of_today_offers_to_review_open_postpone_and_archive()
    {
        using var ui = new UiThread();
        using var today = new TodayVault();
        var test = new TestShell();
        var page = today.Page();
        page.RefreshCommand.Execute(null);
        UiThread.Until(() => page.Rows.Count > 0);
        var view = new DashboardPage { DataContext = page };
        var row = page.Rows.OfType<DueItemRow>().First();

        var items = view.RowMenuItems(row);

        CollectionAssert.AreEqual(SheetRowMenu, Headers(items));
        Assert.IsTrue(ActionMenuPanel.GetIsQuick(Show(test, items[0])), "le style du design system la met en tête");
        Click(items[0]);
        Assert.HasCount(1, today.Opener.Opened);
        Click(items[1]);
        Assert.AreEqual(row.Item.SheetId, today.Opener.Opened[1]);
        Assert.IsEmpty(view.RowMenuItems(page.Rows[0]), "un en-tête de groupe n'a pas de menu");
        Assert.IsEmpty(view.RowMenuItems(null));

        var reminder = new ReminderRow(new PendingReminder("f_1", "Mitose", "Biologie", [], new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), 0));
        CollectionAssert.AreEqual(new[] { DashboardText.OpenSheet }, Headers(view.RowMenuItems(reminder)));
        Click(view.RowMenuItems(reminder)[0]);
        Assert.AreEqual(reminder.Reminder, today.Opener.Opened[2]);

        var withoutActions = new DashboardPage { DataContext = new DashboardPageViewModel(today.App.Reminders, today.App.Sheets, today.Opener) };
        CollectionAssert.AreEqual(SheetRowQuick, Headers(withoutActions.RowMenuItems(row)), "sans les cas d'usage, seulement ce qui ouvre");
    }

    [TestMethod]
    public async Task Postponing_a_line_moves_it_to_tomorrow()
    {
        using var today = new TodayVault();
        var page = today.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var row = page.Rows.OfType<DueItemRow>().Single();
        Assert.IsTrue(page.CanAct);

        await page.PostponeTomorrowAsync(row);

        Assert.IsNull(page.Error);
        Assert.IsEmpty(page.Rows.OfType<DueItemRow>().Where(line => line.Item.Id == row.Item.Id && line.Item.DaysLate > 0));
        Assert.AreEqual(page.Dashboard!.Day.AddDays(1), today.App.Reminders.Dashboard().Value.Upcoming.Single().DueDate);
    }

    [TestMethod]
    public async Task Without_its_use_cases_a_line_cannot_be_postponed()
    {
        using var today = new TodayVault();
        var page = new DashboardPageViewModel(today.App.Reminders, today.App.Sheets, today.Opener);
        await page.RefreshCommand.ExecuteAsync(null);
        var row = page.Rows.OfType<DueItemRow>().Single();
        Assert.IsFalse(page.CanAct);

        await page.PostponeTomorrowAsync(row);
        page.RequestArchive(row);
        await page.AnswerArchiveCommand.ExecuteAsync(true);

        Assert.IsNull(page.Error);
        Assert.IsEmpty(today.App.Reminders.Archives().Value);
    }

    [TestMethod]
    public async Task Archiving_a_line_waits_for_its_confirmation()
    {
        using var today = new TodayVault();
        var page = today.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var row = page.Rows.OfType<DueItemRow>().Single();

        await page.AnswerArchiveCommand.ExecuteAsync(true);
        Assert.IsEmpty(today.App.Reminders.Archives().Value, "rien n'a été demandé");

        page.RequestArchive(row);
        Assert.IsTrue(page.IsConfirmingArchive);
        await page.AnswerArchiveCommand.ExecuteAsync(false);
        Assert.IsFalse(page.IsConfirmingArchive);
        Assert.IsEmpty(today.App.Reminders.Archives().Value);

        page.RequestArchive(row);
        await page.AnswerArchiveCommand.ExecuteAsync(true);

        Assert.HasCount(1, today.App.Reminders.Archives().Value);
        Assert.IsEmpty(page.Rows.OfType<DueItemRow>());
    }

    [TestMethod]
    public async Task A_refused_action_on_a_line_says_why()
    {
        using var today = new TodayVault();
        var page = today.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var row = page.Rows.OfType<DueItemRow>().Single();
        today.App.Trash.TrashSheet(row.Item.SheetId).Value.ToString();

        await page.PostponeTomorrowAsync(row);

        Assert.IsNotNull(page.Error);
    }

    [STATestMethod]
    public void The_menu_of_today_opens_on_the_line_under_the_pointer_and_not_on_a_title()
    {
        using var ui = new UiThread();
        using var today = new TodayVault();
        var test = new TestShell();
        var model = today.Page();
        model.RefreshCommand.Execute(null);
        UiThread.Until(() => model.Rows.Count > 0);
        var page = new DashboardPage { DataContext = model };
        var window = Window(test, page, 1000, 700);
        try
        {
            var line = (ListBoxItem)page.Items.ItemContainerGenerator.ContainerFromItem(model.Rows.OfType<DueItemRow>().First());
            var opened = Opening(line);
            line.RaiseEvent(opened);
            Assert.IsFalse(opened.Handled);
            Assert.HasCount(5, page.Items.ContextMenu!.Items);

            var title = (ListBoxItem)page.Items.ItemContainerGenerator.ContainerFromItem(model.Rows[0]);
            var refused = Opening(title);
            title.RaiseEvent(refused);
            Assert.IsTrue(refused.Handled);

            model.RequestArchive(model.Rows.OfType<DueItemRow>().First());
            TestShell.Flush();
            Assert.IsTrue(page.ArchiveConfirmation.IsOpen);
            page.ArchiveConfirmation.Close(DialogChoice.Secondary);
            TestShell.Flush();
            Assert.IsFalse(model.IsConfirmingArchive);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void An_archived_sheet_offers_to_open_restart_and_change_its_reminders()
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
        var window = Window(test, page, 900, 600);
        try
        {
            var row = model.Rows.Single();
            var items = page.RowMenuItems(row);
            CollectionAssert.AreEqual(new[] { ArchivesText.Open, ArchivesText.Restart, row.ToggleLabel }, Headers(items));

            Click(items[0]);
            CollectionAssert.AreEqual(new object[] { sheet }, opener.Opened);
            Click(items[1]);
            Assert.IsTrue(model.IsConfirming);
            model.AnswerRestartCommand.Execute(false);
            var label = row.ToggleLabel;
            Click(items[3]);
            UiThread.Until(() => model.Rows.Single().ToggleLabel != label);

            var border = Descendants<Border>(window).First(element => element.Name == "RowBorder");
            var opened = Opening(border);
            border.RaiseEvent(opened);
            Assert.IsFalse(opened.Handled);
            Assert.HasCount(4, border.ContextMenu!.Items);

            page.DataContext = null;
            Assert.IsEmpty(page.RowMenuItems(row));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_definition_offers_to_open_edit_and_delete()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development, new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        var sheet = vaults.App.Sheets.Create("Sciences", "La cellule", null).Value;
        var id = vaults.App.Annotations.CreateDefinition(sheet, Passage, new Definition("Noyau", "Il renferme l'ADN.")).Value;
        var opener = new FakeOpener();
        var model = new DefinitionsPageViewModel(vaults.App.Annotations, opener);
        var test = new TestShell();
        var page = new DefinitionsPage { DataContext = model };
        var window = Window(test, page, 1000, 600);
        try
        {
            UiThread.Until(() => model.Rows.Count == 1);
            TestShell.Flush();
            var row = model.Rows.Single();
            var items = page.RowMenuItems(row);
            CollectionAssert.AreEqual(new[] { DefinitionsText.SeeInSheet, DefinitionsText.Edit, ReadingText.DeleteDefinition }, Headers(items));

            Click(items[0]);
            Assert.AreEqual((sheet, id), opener.Opened.Single());
            Click(items[1]);
            Assert.IsTrue(row.IsEditing);
            Assert.IsEmpty(page.RowMenuItems(row), "pendant sa modification, les boutons de la carte suffisent");
            row.CancelCommand.Execute(null);

            var card = Descendants<Card>(window).First(element => element.DataContext == row);
            var opened = Opening(card);
            card.RaiseEvent(opened);
            Assert.IsFalse(opened.Handled);

            Click(page.RowMenuItems(row)[3]);
            Assert.IsTrue(model.IsConfirmingDelete);
            TestShell.Flush();
            page.DeleteConfirmation.Close(DialogChoice.Secondary);
            TestShell.Flush();
            Assert.HasCount(1, model.Rows, "le refus ne change rien");

            Click(page.RowMenuItems(row)[3]);
            TestShell.Flush();
            page.DeleteConfirmation.Close(DialogChoice.Primary);
            UiThread.Until(() => model.Rows.Count == 0);
            Assert.HasCount(1, vaults.App.Trash.TrashedAnnotations().Value, "elle va à la corbeille");

            page.DataContext = null;
            Assert.IsEmpty(page.RowMenuItems(row));
            page.DeleteConfirmation.Close(DialogChoice.None);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public async Task A_definition_that_cannot_be_deleted_says_why()
    {
        using var vaults = new TestVaults(AppMode.Development, new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        var sheet = vaults.App.Sheets.Create("Sciences", "La cellule", null).Value;
        vaults.App.Annotations.CreateDefinition(sheet, Passage, new Definition("Noyau", "Il renferme l'ADN.")).Value.ToString();
        var model = new DefinitionsPageViewModel(vaults.App.Annotations, new FakeOpener());
        await model.LoadCommand.ExecuteAsync(null);

        await model.AnswerDeleteCommand.ExecuteAsync(true);
        Assert.HasCount(1, model.Rows, "rien n'a été demandé");

        model.RequestDelete(model.Rows.Single());
        vaults.App.Host.Dispose();
        await model.AnswerDeleteCommand.ExecuteAsync(true);

        Assert.IsTrue(model.HasError);
    }

    [STATestMethod]
    public void A_line_of_the_vault_health_offers_its_action_and_a_copy()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        var copied = new List<string>();
        var model = new HealthPageViewModel(vaults.App.Maintenance, vaults.App.Backups, vaults.App.Sheets, new FakeOpener(), () => string.Empty, copied.Add);
        var page = new HealthPage { DataContext = model };
        var runs = 0;
        var action = new HealthRow("Mitose", "Fiche manquante", "Réparer", new RelayCommand(() => runs++));
        var opening = new HealthRow("Mitose", "À relire", "Ouvrir", new RelayCommand(() => runs++), subtle: true);
        var plain = new HealthRow("Mitose", "Pour information");

        var items = page.RowMenuItems(action);
        CollectionAssert.AreEqual(new[] { "Réparer", DesignText.Copy }, Headers(items));
        Assert.AreEqual(Icons.Sync, ((Icon)((MenuItem)items[0]).Icon).Text);
        Assert.AreEqual(Icons.Document, ((Icon)((MenuItem)page.RowMenuItems(opening)[0]).Icon).Text);
        CollectionAssert.AreEqual(new[] { DesignText.Copy }, Headers(page.RowMenuItems(plain)));

        Click(items[0]);
        Click(items[1]);
        Assert.AreEqual(1, runs);
        CollectionAssert.AreEqual(CopiedLine, copied);

        var test = new TestShell();
        var row = new Border { DataContext = plain, ContextMenu = new ContextMenu() };
        row.ContextMenuOpening += (sender, e) => ActionMenus.Fill(row.ContextMenu!, page.RowMenuItems(plain), e);
        var opened = Opening(row);
        row.RaiseEvent(opened);
        Assert.HasCount(1, row.ContextMenu!.Items);
        Assert.IsNotNull(test);

        page.DataContext = null;
        Assert.IsEmpty(page.RowMenuItems(plain));
    }

    [STATestMethod]
    public void Listening_from_the_text_starts_on_its_block()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Le premier bloc.\n\nLe second bloc.\n\nLe troisième bloc.\n", passage: false);
        var test = new TestShell();
        var model = vault.Page(speech: new FakeVoices("Hortense"));
        model.LoadCommand.Execute(null);
        UiThread.Until(() => model.Reading is not null && !model.IsLoading);
        var page = new SheetPage { DataContext = model, Width = 1700, Height = 900 };
        var window = Window(test, page, 800, 600);
        try
        {
            var document = page.SheetText.Document;
            UiThread.Until(() => document.Blocks.Count >= 3);
            var second = document.Blocks.OfType<Paragraph>().First(paragraph => new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Contains("second", StringComparison.Ordinal));
            var run = second.Inlines.First();

            var items = page.TextMenuItems(run);
            CollectionAssert.AreEqual(new[] { ReadingText.ListenFromHere, EditingText.EditSheet }, Headers(items));

            Click(items[0]);

            Assert.IsTrue(model.ReadAloud!.IsOpen);
            Assert.IsTrue(model.ReadAloud.IsPlaying);
            StringAssert.Contains(page.SheetText.SpokenBlocks()[model.ReadAloud.Current!.Value].Text, "second", StringComparison.Ordinal);

            model.ReadAloud.CloseCommand.Execute(null);
            CollectionAssert.AreEqual(new[] { EditingText.EditSheet }, Headers(page.TextMenuItems(new Border())), "hors du texte, rien à écouter");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_tool_is_an_icon_with_its_name_in_a_tooltip()
    {
        using var ui = new UiThread();
        var runs = 0;

        var tool = ActionMenus.Tool("Gras", Icons.Bold, () => runs++);
        Click(tool);

        Assert.AreEqual(1, runs);
        Assert.AreEqual("Gras", tool.Header, "le lecteur d'écran dit son nom");
        Assert.AreEqual("Gras", tool.ToolTip);
        Assert.AreEqual(Icons.Bold, ((Icon)tool.Icon).Text);
    }

    [STATestMethod]
    public void Showing_a_hidden_passage_uncovers_it_and_makes_it_stand_out()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("La prophase précède la métaphase.\n", passage: false);
        var id = vault.Annotate("La prophase");
        var test = new TestShell();
        var model = vault.Page(revising: true, activeRecall: () => true);
        model.LoadCommand.Execute(null);
        UiThread.Until(() => model.Reading is not null && !model.IsLoading);
        var page = new SheetPage { DataContext = model, Width = 1700, Height = 900 };
        var window = Window(test, page, 1700, 900);
        try
        {
            UiThread.Until(() => model.Holes.HasHoles);
            TestShell.Flush();
            Assert.IsTrue(model.Holes.Masks(id));
            var hole = TextRuns(page.SheetText.Document).First(run => run.Text.Contains("prophase", StringComparison.Ordinal));
            Assert.AreEqual(System.Windows.Input.Cursors.Hand, hole.Cursor, "un trou se clique, et le curseur le dit");

            Click(page.PassageMenuItems(model, model.Passages.Single())[0]);

            Assert.IsFalse(model.Holes.Masks(id));
            Assert.AreEqual(id, model.ActivePassage);
            TestShell.Flush();
            var shown = TextRuns(page.SheetText.Document).First(run => run.Text.Contains("prophase", StringComparison.Ordinal));
            Assert.AreEqual(test.Design.Dictionary["Kyf.Brush.HighlightActive"], shown.Background, "le passage visé ressort du texte");
            Assert.AreEqual(test.Design.Dictionary["Kyf.Brush.OnHighlight"], shown.Foreground);

            model.ActivateCommand.Execute(id);
            TestShell.Flush();
            var other = TextRuns(page.SheetText.Document).First(run => run.Text.Contains("métaphase", StringComparison.Ordinal));
            Assert.IsNull(other.ReadLocalValue(System.Windows.FrameworkContentElement.CursorProperty) as System.Windows.Input.Cursor, "le reste du texte garde son curseur");
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<Run> TextRuns(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is Run run)
            {
                yield return run;
            }
            foreach (var nested in TextRuns(child))
            {
                yield return nested;
            }
        }
    }

    private static List<string> Headers(IReadOnlyList<Control> items) =>
        [.. items.OfType<MenuItem>().Select(item => (string)item.Header)];

    private static void Click(Control item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    /// <summary>L'ouverture d'un menu sur cet élément, celle que WPF lui enverrait.</summary>
    private static ContextMenuEventArgs Opening(object source)
    {
        var opening = (ContextMenuEventArgs)Activator.CreateInstance(typeof(ContextMenuEventArgs), BindingFlags.Instance | BindingFlags.NonPublic, null, [source, true], null)!;
        opening.RoutedEvent = ContextMenuService.ContextMenuOpeningEvent;
        return opening;
    }

    /// <summary>Applique les styles du design system à un élément de menu.</summary>
    private static Control Show(TestShell test, Control item)
    {
        var menu = new ContextMenu { Resources = test.Design.Dictionary };
        menu.Items.Add(item);
        item.ApplyTemplate();
        return item;
    }

    private static Window Window(TestShell test, FrameworkElement page, double width, double height)
    {
        var host = new Border { Width = width, Height = height, Child = page };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        TestShell.Flush();
        return window;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T element)
            {
                yield return element;
            }
            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>Une fiche en retard d'un jour, sur un vrai coffre.</summary>
    private sealed class TodayVault : IDisposable
    {
        private readonly TestVaults _vaults;

        public TodayVault()
        {
            var time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
            _vaults = new TestVaults(AppMode.Development, time);
            App.Vaults.Create(_vaults.PathOf("Coffre"));
            App.Sheets.Create("Biologie", "Mitose", null).Value.ToString();
            time.AdvanceDays(2);
        }

        public KyfRev.Application.UseCases.KyfRevApplication App => _vaults.App;

        public FakeOpener Opener { get; } = new();

        public DashboardPageViewModel Page() => new(App.Reminders, App.Sheets, Opener, null, App.Reviews);

        public void Dispose() => _vaults.Dispose();
    }
}
