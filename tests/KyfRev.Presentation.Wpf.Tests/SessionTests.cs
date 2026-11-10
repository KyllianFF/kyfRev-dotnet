using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La séance d'étude, sur un vrai coffre et une horloge simulée : progression, « Passer » ou
/// « Suivant », bilan (temps, faits, reportés, restants), séance gardée à chaque pas et reprise le
/// même jour, plein écran ; et « Commencer » ou « Reprendre » depuis le tableau de bord.
/// </summary>
[TestClass]
public sealed class SessionTests
{
    [TestMethod]
    public async Task A_session_follows_the_items_and_ends_with_a_summary()
    {
        using var vault = new SessionVault();
        var session = await vault.StartAsync();

        Assert.AreEqual("Séance : 1 / 3", session.ProgressLabel);
        Assert.AreEqual("Progression de la séance : élément 1 sur 3", session.ProgressName);
        Assert.AreEqual(0, session.Progress);
        Assert.IsTrue(session.IsRunning);
        Assert.IsTrue(session.IsWaiting);
        Assert.AreEqual("Passer", session.NextLabel);
        Assert.AreEqual(1, vault.Sessions.Saves, "gardée dès le début");

        // 1 : validée.
        var first = await SessionVault.LoadedAsync(session);
        await first.Review!.ValidateCommand.ExecuteAsync(null);
        Assert.IsTrue(session.HasActed);
        Assert.AreEqual("Suivant", session.NextLabel);
        vault.Time.Now += TimeSpan.FromSeconds(45);
        session.NextCommand.Execute(null);
        Assert.AreEqual("Séance : 2 / 3", session.ProgressLabel);
        Assert.AreEqual(1.0 / 3, session.Progress, 1e-9);
        Assert.IsFalse(session.HasActed);
        Assert.AreEqual(1, session.Session.Done);

        // 2 : reportée.
        var second = await SessionVault.LoadedAsync(session);
        await second.Review!.PostponeTomorrowCommand.ExecuteAsync(null);
        vault.Time.Now += TimeSpan.FromSeconds(30);
        session.NextCommand.Execute(null);

        // 3 : passée sans rien faire ; c'était la dernière.
        await SessionVault.LoadedAsync(session);
        vault.Time.Now += TimeSpan.FromSeconds(5);
        session.NextCommand.Execute(null);

        Assert.IsTrue(session.IsFinished);
        Assert.IsNull(session.Current);
        Assert.AreEqual(1, session.Progress);
        Assert.AreEqual("3 éléments passés en revue.", session.Summary);
        Assert.AreEqual("1 min 20 s", session.Duration);
        Assert.AreEqual(1, session.DoneCount);
        Assert.AreEqual(1, session.PostponedCount);
        Assert.AreEqual(1, session.RemainingCount);
        Assert.IsTrue(vault.Sessions.Forgotten, "une séance terminée est oubliée");
        Assert.IsFalse(session.NextCommand.CanExecute(null));
        Assert.IsFalse(session.EndCommand.CanExecute(null));

        session.LeaveCommand.Execute(null);
        Assert.AreSame(vault.Shell.Home, vault.Shell.Current);
        Assert.IsTrue(vault.Sessions.Forgotten, "quitter le bilan ne garde rien");
    }

    [TestMethod]
    public async Task An_interrupted_session_is_kept_and_resumed_the_same_day()
    {
        using var vault = new SessionVault();
        var session = await vault.StartAsync();
        var first = await SessionVault.LoadedAsync(session);
        await first.Review!.ValidateCommand.ExecuteAsync(null);
        session.NextCommand.Execute(null);
        vault.Time.Now += TimeSpan.FromSeconds(20);

        // Retour depuis la fiche en cours : la séance est quittée, gardée avec le temps passé.
        var second = await SessionVault.LoadedAsync(session);
        ((SheetPageViewModel)second).BackCommand.Execute(null);

        Assert.AreSame(vault.Shell.Home, vault.Shell.Current);
        var kept = vault.Sessions.Kept!;
        Assert.AreEqual(1, kept.Position);
        Assert.AreEqual(1, kept.Done);
        Assert.AreEqual(2, kept.Remaining);
        Assert.AreEqual(TimeSpan.FromSeconds(20), kept.Elapsed);
        Assert.AreEqual(2, vault.Navigation.ResumableItems());

        vault.Navigation.ResumeSession();
        var resumed = (StudySessionViewModel)vault.Shell.Current.Page;
        Assert.AreEqual("Séance : 2 / 3", resumed.ProgressLabel);

        // Terminée tout de suite : l'élément en cours et le suivant restent à faire.
        resumed.EndCommand.Execute(null);
        Assert.AreEqual(1, resumed.DoneCount);
        Assert.AreEqual(2, resumed.RemainingCount);
        Assert.AreEqual("1 élément passé en revue.", resumed.Summary);
        Assert.AreEqual("20 s", resumed.Duration);
        Assert.IsNull(vault.Navigation.ResumableItems());
        vault.Navigation.ResumeSession();
        Assert.AreSame(resumed, vault.Shell.Current.Page, "rien à reprendre");
    }

    [TestMethod]
    public async Task Ending_after_acting_counts_the_current_item()
    {
        using var vault = new SessionVault();
        var session = await vault.StartAsync();
        var first = await SessionVault.LoadedAsync(session);
        await first.Review!.PostponeThreeDaysCommand.ExecuteAsync(null);

        session.EndCommand.Execute(null);

        Assert.AreEqual(1, session.PostponedCount);
        Assert.AreEqual(2, session.RemainingCount);
    }

    [TestMethod]
    public async Task A_session_from_another_day_or_vault_is_not_resumed()
    {
        using var vault = new SessionVault();
        var session = await vault.StartAsync();
        session.LeaveCommand.Execute(null);
        Assert.AreEqual(3, vault.Navigation.ResumableItems());

        vault.Sessions.Kept = vault.Sessions.Kept! with { Vault = "Un autre coffre" };
        Assert.IsNull(vault.Navigation.ResumableItems());

        vault.Sessions.Kept = vault.Sessions.Kept! with { Vault = vault.Root };
        vault.Time.Now += TimeSpan.FromDays(1);
        Assert.IsNull(vault.Navigation.ResumableItems());

        vault.Navigation.StartSession([], activeRecall: true);
        Assert.AreSame(vault.Shell.Home, vault.Shell.Current, "une liste vide ne lance rien");
        Assert.ThrowsExactly<ArgumentNullException>(() => vault.Navigation.StartSession(null!, true));
    }

    [TestMethod]
    public async Task Passages_of_a_session_open_as_memory_cards_and_see_in_sheet_comes_back()
    {
        using var vault = new SessionVault(withPassage: true);
        var session = await vault.StartAsync();
        var sessionItem = vault.Shell.Current;
        while (session.Current is not PassagePageViewModel)
        {
            session.NextCommand.Execute(null);
        }
        var passage = (PassagePageViewModel)session.Current;
        await Waiting.UntilAsync(() => !passage.IsLoading);
        Assert.IsTrue(passage.IsRecall);

        passage.SeeInSheetCommand.Execute(null);
        ((SheetPageViewModel)vault.Shell.Current.Page).BackCommand.Execute(null);
        Assert.AreSame(sessionItem, vault.Shell.Current, "Retour revient dans la séance");
    }

    [TestMethod]
    public void The_session_needs_its_services()
    {
        var store = new MemorySessions();
        var session = new StudySession("C", new DateOnly(2026, 10, 3), [new SessionItem(ItemKind.Sheet, "a", "a")], 0, 0, 0, 0, TimeSpan.Zero, true);
        Assert.ThrowsExactly<ArgumentNullException>(() => new StudySessionViewModel(null!, store, TimeProvider.System, (_, _) => new object(), () => { }, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new StudySessionViewModel(session, null!, TimeProvider.System, (_, _) => new object(), () => { }, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new StudySessionViewModel(session, store, null!, (_, _) => new object(), () => { }, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new StudySessionViewModel(session, store, TimeProvider.System, null!, () => { }, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new StudySessionViewModel(session, store, TimeProvider.System, (_, _) => new object(), null!, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new StudySessionViewModel(session, store, TimeProvider.System, (_, _) => new object(), () => { }, null!));

        // Une page sans barre de révision : l'élément ne peut être que passé.
        var fullScreen = 0;
        var plain = new StudySessionViewModel(session, store, TimeProvider.System, (_, _) => new object(), () => { }, () => fullScreen++);
        plain.ToggleFullScreenCommand.Execute(null);
        Assert.AreEqual(1, fullScreen);
        plain.NextCommand.Execute(null);
        Assert.AreEqual(1, plain.RemainingCount);
        plain.Interrupt();
        Assert.IsTrue(store.Forgotten, "une séance terminée n'est plus gardée");
    }

    [TestMethod]
    public void The_dashboard_starts_or_resumes_a_session()
    {
        var opener = new FakeOpener();
        using var vault = new TestVaults(AppMode.Development);
        var dashboard = new DashboardPageViewModel(vault.App.Reminders, vault.App.Sheets, opener);
        var overdue = Item("Mitose", ItemKind.Sheet);
        var today = Item("Méiose", ItemKind.Passage);
        var day = new Dashboard(new DateOnly(2026, 10, 3), [overdue], [today], [], [], [], [], 2, true, [], null, 2, 0, 0, 0, false, false);

        dashboard.Show(day);
        Assert.IsTrue(dashboard.CanStart);
        Assert.AreEqual("Commencer (2)", dashboard.StartLabel);
        Assert.IsFalse(dashboard.CanResume);
        Assert.AreEqual(string.Empty, dashboard.ResumeLabel);
        dashboard.SelectedFilter = dashboard.Filters[2];
        Assert.AreEqual("Commencer (1)", dashboard.StartLabel);
        dashboard.StartCommand.Execute(null);
        var (kind, items, activeRecall) = ((string, IReadOnlyList<DueItem>, bool))opener.Opened.Single();
        Assert.AreEqual("séance", kind);
        Assert.AreSame(today, items.Single());
        Assert.IsTrue(activeRecall);

        opener.Resumable = 3;
        dashboard.Show(day);
        Assert.IsTrue(dashboard.CanResume);
        Assert.AreEqual("Reprendre la séance (3 restants)", dashboard.ResumeLabel);
        opener.Resumable = 1;
        dashboard.Show(day);
        Assert.AreEqual("Reprendre la séance (1 restant)", dashboard.ResumeLabel);
        dashboard.ResumeCommand.Execute(null);
        Assert.AreEqual("reprise", opener.Opened[^1]);

        dashboard.Show(day with { Overdue = [], Today = [] });
        Assert.IsFalse(dashboard.CanStart);
        Assert.IsFalse(dashboard.StartCommand.CanExecute(null));
    }

    [TestMethod]
    public void Full_screen_hides_the_menu_and_escape_leaves_it()
    {
        var shell = new TestShell().Shell;
        shell.ShowWorkspace();

        shell.ToggleFullScreen();
        Assert.IsTrue(shell.IsFullScreen);
        Assert.IsFalse(shell.IsMenuVisible);
        shell.ExitFullScreen();
        Assert.IsFalse(shell.IsFullScreen);
        Assert.IsTrue(shell.IsMenuVisible);
        shell.OpenPalette();
        shell.PaletteQuery = "plein";
        Assert.AreEqual("F11", shell.PaletteResults.Single().Detail);
    }

    [STATestMethod]
    public void The_window_covers_the_screen_and_comes_back()
    {
        var test = new TestShell();
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 900, Height = 600, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            test.Shell.ToggleFullScreen();
            Assert.AreEqual(WindowStyle.None, window.WindowStyle);
            Assert.AreEqual(WindowState.Maximized, window.WindowState);
            test.Shell.ToggleFullScreen();
            Assert.AreEqual(WindowStyle.SingleBorderWindow, window.WindowStyle);
            Assert.AreEqual(WindowState.Normal, window.WindowState);
            Assert.IsTrue(window.InputBindings.OfType<KeyBinding>().Any(binding => binding.Key == Key.F11));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_session_page_swaps_skip_for_next_and_shows_the_summary()
    {
        using var ui = new UiThread();
        var test = new TestShell();
        var store = new MemorySessions();
        var bar = new FakeReviewPage();
        var session = new StudySessionViewModel(
            new StudySession("C", new DateOnly(2026, 10, 3), [new SessionItem(ItemKind.Sheet, "a", "a")], 0, 0, 0, 0, TimeSpan.Zero, true),
            store,
            TimeProvider.System,
            (_, _) => bar,
            () => { },
            () => { });
        test.Shell.Navigate(new NavigationItem("seance", "Séance", "", int.MaxValue, session));
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            Assert.IsTrue(Named<Button>(window, "SkipButton").IsVisible);
            Assert.IsFalse(Named<Button>(window, "NextButton").IsVisible);
            Assert.AreEqual("Ctrl+→", AutomationProperties.GetAcceleratorKey(Named<Button>(window, "SkipButton")));
            Assert.AreEqual("Progression de la séance : élément 1 sur 1", AutomationProperties.GetName(Named<TextBlock>(window, "ProgressLabel").Parent));
            // Les boutons passent sous la progression quand la place manque.
            var actions = (WrapPanel)VisualTreeHelper.GetParent(Named<Button>(window, "EndButton"));
            Assert.AreEqual(Dock.Right, DockPanel.GetDock(actions));
            window.Width = 420;
            window.UpdateLayout();
            TestShell.Flush();
            Assert.AreEqual(Dock.Bottom, DockPanel.GetDock(actions));

            session.NextCommand.Execute(null);
            TestShell.Flush();
            Assert.IsTrue(Named<StackPanel>(window, "SummaryPanel").IsVisible);
            Assert.IsFalse(Named<Border>(window, "SessionBar").IsVisible);
            Assert.AreEqual("1", Named<TextBlock>(window, "Remaining").Text);
            var shortcut = Descendants(window).OfType<StudySessionPage>().Single().InputBindings.OfType<KeyBinding>().Single();
            Assert.AreEqual((Key.Right, ModifierKeys.Control), (shortcut.Key, shortcut.Modifiers));
        }
        finally
        {
            window.Close();
        }
    }

    private static DueItem Item(string title, ItemKind kind) =>
        new(kind, $"id-{title}", $"fiche-{title}", title, null, null, [], null, "Biologie", [], "J1", new DateOnly(2026, 10, 2), 1, new DateOnly(2026, 10, 1), 1, true);

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

    /// <summary>Une page de révision dont la barre n'arrive jamais.</summary>
    private sealed class FakeReviewPage : IReviewPage
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public ReviewBarViewModel? Review => null;
    }

    /// <summary>
    /// Un coffre de trois fiches (Atome, Méiose, Mitose) créées le jeudi 1er octobre 2026, en retard
    /// le samedi 3 ; avec, au besoin, un passage de Mitose. La coquille est ouverte sur le coffre.
    /// </summary>
    private sealed class SessionVault : IDisposable
    {
        private readonly TestVaults _vaults;

        public SessionVault(bool withPassage = false)
        {
            Time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
            _vaults = new TestVaults(AppMode.Development, Time);
            Root = _vaults.PathOf("Coffre");
            Assert.IsNotNull(App.Vaults.Create(Root).Vault);
            foreach (var title in new[] { "Atome", "Méiose", "Mitose" })
            {
                var id = App.Sheets.Create("Sciences", title, null).Value;
                if (withPassage && title == "Mitose")
                {
                    App.Annotations.Create(id, new Anchor("La prophase", string.Empty, " précède.", 0, 11, [], 1, "sha256:aa"), "Indice").Value.ToString();
                }
            }
            Time.Now += TimeSpan.FromDays(2);
            var test = new TestShell();
            Navigation = new ReviewNavigation(App.Sheets, App.Annotations, App.Reviews, App.Reminders, App.Vaults, Sessions, Time, new RecordingLinks(), App.Editing, FakeEnvironment.Markdown());
            Dashboard = new DashboardPageViewModel(App.Reminders, App.Sheets, Navigation);
            Shell = new ShellViewModel(CorePages.All(new Dictionary<string, object> { [CorePages.Today] = Dashboard }), test.Appearance, test.Gallery);
            Navigation.Attach(Shell);
        }

        public FakeTime Time { get; }

        public string Root { get; }

        public KyfRevApplication App => _vaults.App;

        public MemorySessions Sessions { get; } = new();

        public ReviewNavigation Navigation { get; }

        public DashboardPageViewModel Dashboard { get; }

        public ShellViewModel Shell { get; }

        /// <summary>La séance lancée depuis le tableau de bord, sur toute la liste du jour.</summary>
        public async Task<StudySessionViewModel> StartAsync()
        {
            Shell.ShowWorkspace();
            await Waiting.UntilAsync(() => Dashboard.Dashboard is not null);
            Dashboard.StartCommand.Execute(null);
            return (StudySessionViewModel)Shell.Current.Page;
        }

        /// <summary>La page de l'élément en cours, une fois lue.</summary>
        public static async Task<IReviewPage> LoadedAsync(StudySessionViewModel session)
        {
            var page = (IReviewPage)session.Current!;
            await Waiting.UntilAsync(() => page.Review is not null);
            return page;
        }

        public void Dispose() => _vaults.Dispose();
    }
}
