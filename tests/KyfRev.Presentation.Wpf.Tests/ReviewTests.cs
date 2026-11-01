using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Une fiche ou un passage en révision, sur un vrai coffre et une horloge simulée : la barre de
/// révision de la 0.x (valider, en avance, reporter, recommencer, archiver, rappels), la carte
/// mémoire d'un passage (définition, rappel actif, texte à trous), et l'ouverture depuis le
/// tableau de bord.
/// </summary>
[TestClass]
public sealed class ReviewTests
{
    private static readonly Anchor Passage = new("La prophase", "", " précède la métaphase.", 0, 11, ["Mitose", "Étapes"], 5, "sha256:aa");

    [TestMethod]
    public void The_cloze_cuts_the_context_as_in_the_0x()
    {
        var both = Cloze.Of(Passage with { Exact = "trois ordres", Before = "ciété d'Ancien Régime est divisée en ", After = " : le clergé, la noblesse et le tie" });
        Assert.AreEqual(new Cloze("d'Ancien Régime est divisée en ", " : le clergé, la noblesse et le", true, true, 2), both);
        Assert.AreEqual("… d'Ancien Régime est divisée en ", both.Leading);
        Assert.AreEqual(" : le clergé, la noblesse et le …", both.Trailing);

        Assert.AreEqual(new Cloze(string.Empty, " deux cellules.", false, false, 3), Cloze.Of(Passage with { Exact = "La mitose\nproduit", Before = string.Empty, After = " deux cellules." }));
        Assert.AreEqual(6, Cloze.Of(Passage with { Exact = @"Dette : $\tfrac{1}{2}$ de toutes les dépenses", Before = string.Empty, After = string.Empty }).Words);
        Assert.AreEqual(2, Cloze.Of(Passage with { Exact = "a — b", Before = string.Empty, After = string.Empty }).Words);
        Assert.AreEqual("Titre Un ", Cloze.Of(Passage with { Exact = "x", Before = "Titre\n\nUn ", After = string.Empty }).Before);
        // Un contexte coupé qui finit par un saut de ligne ne perd que ce saut, comme en JavaScript.
        Assert.AreEqual(" la noblesse et le clergé de France", Cloze.Of(Passage with { Exact = "x", Before = string.Empty, After = " la noblesse et le clergé de France\n" }).After);
        Assert.ThrowsExactly<ArgumentNullException>(() => Cloze.Of(null!));
    }

    [TestMethod]
    public async Task Validating_moves_to_the_next_step_and_says_so()
    {
        using var vault = new ReviewVault();
        var bar = await vault.SheetBarAsync();
        var changes = 0;
        bar.Changed += (_, _) => changes++;

        Assert.IsTrue(bar.IsActive);
        Assert.IsFalse(bar.IsArchived);
        Assert.IsTrue(bar.IsSheet);
        Assert.AreEqual("Étape attendue", bar.StepLabel);
        Assert.AreEqual("en retard de 1 j (prévue ven. 2 oct.)", bar.DueText);
        Assert.IsTrue(bar.IsLate);
        Assert.AreEqual("Valider la révision J1", bar.ValidateLabel);
        Assert.AreEqual("J0 : validée le jeu. 1 oct. | J1 : étape attendue, prévue ven. 2 oct. | J3 : à venir", string.Join(" | ", bar.Steps.Take(3)));
        Assert.IsTrue(bar.Steps[1].IsCurrent);

        await bar.ValidateCommand.ExecuteAsync(null);

        Assert.AreEqual("J1 validée. Prochaine étape : J3, mardi 6 octobre.", bar.Message);
        Assert.IsTrue(bar.HasMessage);
        Assert.IsFalse(bar.HasError);
        Assert.AreEqual(1, changes);
        Assert.IsTrue(bar.ShowsReviewedToday);
        Assert.IsFalse(bar.CanValidate);
        Assert.IsFalse(bar.ValidateCommand.CanExecute(null));
        Assert.AreEqual("J1 : validée le sam. 3 oct. (1 j de retard)", bar.Steps[1].Description);
        Assert.IsTrue(bar.Steps[1].WasLate);
    }

    [TestMethod]
    public async Task An_early_review_is_confirmed_first()
    {
        using var vault = new ReviewVault();
        var bar = await vault.SheetBarAsync();
        await bar.ValidateCommand.ExecuteAsync(null);
        vault.Time.AdvanceDays(1);
        bar = await vault.SheetBarAsync();

        Assert.IsTrue(bar.IsEarly);
        Assert.AreEqual("Prochaine étape", bar.StepLabel);
        Assert.AreEqual("prévue mardi 6 octobre", bar.DueText);
        await bar.ValidateCommand.ExecuteAsync(null);
        Assert.AreEqual(ReviewConfirmation.Early, bar.Confirmation);
        Assert.IsTrue(bar.IsConfirming);
        Assert.AreEqual("Réviser en avance ?", bar.ConfirmTitle);
        Assert.AreEqual("Cette révision est prévue mardi 6 octobre. La valider maintenant fera repartir le calendrier d'aujourd'hui.", bar.ConfirmMessage);
        Assert.AreEqual("Valider maintenant", bar.ConfirmLabel);

        await bar.AnswerCommand.ExecuteAsync(false);
        Assert.IsFalse(bar.IsConfirming);
        Assert.IsNull(bar.Message);

        await bar.ValidateCommand.ExecuteAsync(null);
        await bar.AnswerCommand.ExecuteAsync(true);
        Assert.AreEqual("J3 validée. Prochaine étape : J7, dimanche 11 octobre.", bar.Message);
        Assert.AreEqual("J3 : validée le dim. 4 oct. (2 j en avance)", bar.Steps[2].Description);
        Assert.AreEqual(string.Empty, bar.ConfirmTitle);
        Assert.AreEqual(string.Empty, bar.ConfirmMessage);
        Assert.AreEqual(string.Empty, bar.ConfirmLabel);
    }

    [TestMethod]
    public async Task Postponing_restarting_archiving_and_reminders()
    {
        using var vault = new ReviewVault();
        var bar = await vault.SheetBarAsync();

        await bar.PostponeTomorrowCommand.ExecuteAsync(null);
        Assert.AreEqual("J1 reportée au dimanche 4 octobre.", bar.Message);
        Assert.AreEqual("prévue dimanche 4 octobre", bar.DueText);
        await bar.PostponeThreeDaysCommand.ExecuteAsync(null);
        Assert.AreEqual("J1 reportée au mardi 6 octobre.", bar.Message);

        bar.RequestRestartCommand.Execute(null);
        Assert.AreEqual("Recommencer à J0 ?", bar.ConfirmTitle);
        Assert.AreEqual("Le cycle en cours de la fiche sera interrompu et un nouveau cycle commencera aujourd'hui. L'historique est conservé.", bar.ConfirmMessage);
        Assert.AreEqual("Recommencer", bar.ConfirmLabel);
        await bar.AnswerCommand.ExecuteAsync(true);
        Assert.AreEqual("Nouveau cycle : J0 validé aujourd'hui, J1 dimanche 4 octobre.", bar.Message);

        bar.RequestArchiveCommand.Execute(null);
        Assert.AreEqual("Archiver la fiche ?", bar.ConfirmTitle);
        Assert.AreEqual("Archiver", bar.ConfirmLabel);
        StringAssert.StartsWith(bar.ConfirmMessage, "Elle quitte les révisions actives", StringComparison.Ordinal);
        await bar.AnswerCommand.ExecuteAsync(true);
        Assert.AreEqual("Fiche archivée. Elle reste consultable dans « Archives » et n'est jamais supprimée.", bar.Message);
        Assert.IsTrue(bar.IsArchived);
        StringAssert.StartsWith(bar.ArchivedStatus, "Fiche archivée le samedi 3 octobre · ", StringComparison.Ordinal);
        StringAssert.Contains(bar.ArchivedStatus, "cycle(s) terminé(s)", StringComparison.Ordinal);
        Assert.AreEqual("Exclure des rappels", bar.ReminderToggleLabel);

        await bar.ToggleRemindersCommand.ExecuteAsync(null);
        Assert.AreEqual("Fiche exclue des rappels de consolidation.", bar.Message);
        StringAssert.EndsWith(bar.ArchivedStatus, " · exclue des rappels", StringComparison.Ordinal);
        Assert.AreEqual("Réinclure dans les rappels", bar.ReminderToggleLabel);
        await bar.ToggleRemindersCommand.ExecuteAsync(null);
        Assert.AreEqual("Fiche réincluse dans les rappels de consolidation.", bar.Message);

        await bar.RestartArchivedCommand.ExecuteAsync(null);
        Assert.IsTrue(bar.IsActive);
        StringAssert.StartsWith(bar.Message, "Nouveau cycle : J0 validé aujourd'hui", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task A_passage_has_its_own_messages_and_no_archiving()
    {
        using var vault = new ReviewVault();
        var page = await vault.PassageAsync(activeRecall: false);
        var bar = page.Review!;

        Assert.IsFalse(bar.IsSheet);
        bar.RequestRestartCommand.Execute(null);
        Assert.AreEqual("Le cycle en cours du passage sera interrompu et un nouveau cycle commencera aujourd'hui. L'historique est conservé.", bar.ConfirmMessage);
        await bar.AnswerCommand.ExecuteAsync(false);
        Assert.AreEqual("Passage archivé · 0 cycle(s) terminé(s)", bar.ArchivedStatus);
        await bar.ValidateCommand.ExecuteAsync(null);
        Assert.AreEqual("J1 validée. Prochaine étape : J3, mardi 6 octobre.", bar.Message);
    }

    [TestMethod]
    public async Task A_refused_action_is_shown_and_changes_nothing()
    {
        using var vault = new ReviewVault();
        var state = (await vault.SheetBarAsync()).State;
        var bar = vault.Bar(ItemKind.Sheet, "introuvable", state);

        await bar.PostponeTomorrowCommand.ExecuteAsync(null);

        Assert.IsTrue(bar.HasError);
        Assert.IsNull(bar.Message);
        Assert.AreSame(state, bar.State);
        Assert.IsFalse(bar.IsBusy);
        Assert.ThrowsExactly<ArgumentNullException>(() => vault.Bar(ItemKind.Sheet, null!, state));
        Assert.ThrowsExactly<ArgumentNullException>(() => vault.Bar(ItemKind.Sheet, "id", null!));
    }

    [TestMethod]
    public async Task A_definition_is_a_memory_card()
    {
        using var vault = new ReviewVault();
        var page = await vault.DefinitionAsync();

        Assert.IsTrue(page.IsLoaded);
        Assert.IsTrue(page.IsDefinition);
        Assert.IsFalse(page.IsRecall);
        Assert.IsFalse(page.IsPlain);
        Assert.AreEqual("Définition · J1", page.TypeLabel);
        Assert.AreEqual("Mitose › Mitose › Étapes", page.Source);
        Assert.AreEqual("Prophase", page.Term);
        Assert.IsTrue(page.IsHidden);

        page.RevealCommand.Execute(null);

        Assert.IsTrue(page.IsRevealed);
        Assert.AreEqual("Première phase.", page.Explanation);
        Assert.AreEqual("« prophase »", page.Quote);
        Assert.IsNull(page.Cloze);
        Assert.AreEqual(string.Empty, page.ClozeMiddle);
        Assert.AreEqual(string.Empty, page.ClozeSpoken);
        Assert.AreEqual(string.Empty, page.HiddenPassage);
    }

    [TestMethod]
    public async Task With_active_recall_the_passage_is_a_hole_in_its_context()
    {
        using var vault = new ReviewVault();
        var page = await vault.PassageAsync(activeRecall: true);

        Assert.IsTrue(page.IsRecall);
        Assert.AreEqual("Passage · J1", page.TypeLabel);
        Assert.AreEqual("Avant la métaphase", page.Comment);
        Assert.IsTrue(page.HasComment);
        Assert.IsFalse(page.ShowsCommentBelow, "le commentaire est l'indice, avant le trou");
        Assert.AreEqual("2 mots", page.ClozeMiddle);
        Assert.AreEqual("passage caché : 2 mots", page.HiddenPassage);
        Assert.AreEqual("passage caché : 2 mots précède la métaphase.", page.ClozeSpoken);

        page.RevealCommand.Execute(null);

        Assert.AreEqual("La prophase", page.ClozeMiddle);
        Assert.AreEqual("La prophase précède la métaphase.", page.ClozeSpoken);
    }

    [TestMethod]
    public async Task Without_active_recall_the_passage_is_shown_with_its_comment()
    {
        using var vault = new ReviewVault();
        var page = await vault.PassageAsync(activeRecall: false);

        Assert.IsTrue(page.IsPlain);
        Assert.AreEqual("« La prophase »", page.Quote);
        Assert.IsTrue(page.ShowsCommentBelow);
        Assert.IsFalse(page.HasAnchorWarning);

        Assert.IsTrue(vault.App.Annotations.Reanchor(vault.PassageId, Passage, AnchorState.NeedsReview).Succeeded);
        await page.LoadCommand.ExecuteAsync(null);
        Assert.AreEqual("La position de ce passage dans la fiche est à vérifier : le texte ci-dessus reste celui que vous aviez surligné.", page.AnchorWarning);
        Assert.IsTrue(vault.App.Annotations.Reanchor(vault.PassageId, Passage, AnchorState.NotFound).Succeeded);
        await page.LoadCommand.ExecuteAsync(null);
        StringAssert.Contains(page.AnchorWarning, "est introuvable", StringComparison.Ordinal);

        page.SeeInSheetCommand.Execute(null);
        page.BackCommand.Execute(null);
        Assert.AreEqual($"fiche {vault.SheetId} | retour", string.Join(" | ", vault.Calls));
    }

    [TestMethod]
    public async Task A_missing_passage_or_sheet_is_reported()
    {
        using var vault = new ReviewVault();
        var missing = vault.PassagePage("introuvable", vault.SheetId, activeRecall: true);
        await missing.LoadCommand.ExecuteAsync(null);
        Assert.AreEqual("Ce passage est introuvable.", missing.Error);
        Assert.IsTrue(missing.HasError);
        Assert.IsFalse(missing.IsLoading);
        Assert.AreEqual(string.Empty, missing.TypeLabel);
        Assert.AreEqual(string.Empty, missing.Source);
        Assert.AreEqual(string.Empty, missing.Quote);

        var noSheet = vault.PassagePage(vault.PassageId, "introuvable", activeRecall: true);
        await noSheet.LoadCommand.ExecuteAsync(null);
        Assert.IsTrue(noSheet.HasError);

        var sheet = vault.SheetPage("introuvable");
        await sheet.LoadCommand.ExecuteAsync(null);
        Assert.IsTrue(sheet.HasError);
        Assert.IsNull(sheet.Review);
    }

    [TestMethod]
    public async Task A_sheet_shows_its_text_and_its_review_bar()
    {
        using var vault = new ReviewVault();
        var page = vault.SheetPage(vault.SheetId);
        Assert.IsTrue(page.IsLoading);

        await page.LoadCommand.ExecuteAsync(null);

        Assert.AreEqual("Mitose", page.Title);
        Assert.AreEqual("Biologie | #bio | #partiel", string.Join(" | ", page.Details));
        Assert.AreEqual(vault.App.Sheets.Open(vault.SheetId).Value.Body, page.Body);
        Assert.AreEqual(ItemKind.Sheet, page.Review!.Kind);
        Assert.AreEqual(vault.SheetId, page.Review.Id);
        Assert.IsFalse(page.HasError);
        page.BackCommand.Execute(null);
        Assert.AreEqual("retour", vault.Calls.Single());
    }

    [TestMethod]
    public async Task Rows_of_the_dashboard_open_their_item_and_back_returns_to_it()
    {
        using var vault = new ReviewVault();
        var test = new TestShell();
        var navigation = new ReviewNavigation(vault.App.Sheets, vault.App.Reviews, vault.App.Reminders, vault.App.Vaults, new MemorySessions(), vault.Time);
        var dashboard = new DashboardPageViewModel(vault.App.Reminders, navigation);
        var shell = new ShellViewModel(CorePages.All(new Dictionary<string, object> { [CorePages.Today] = dashboard }), test.Appearance, test.Gallery);
        Assert.ThrowsExactly<InvalidOperationException>(() => navigation.OpenSheet(vault.SheetId));
        navigation.Attach(shell);
        shell.ShowWorkspace();
        await Waiting.UntilAsync(() => dashboard.Dashboard is not null);

        // Une fiche : sa page, puis Retour au tableau de bord, relu.
        var sheetRow = dashboard.Rows.OfType<DueItemRow>().First(row => row.Item.Kind == ItemKind.Sheet);
        dashboard.OpenCommand.Execute(sheetRow);
        var sheet = (SheetPageViewModel)shell.Current.Page;
        await Waiting.UntilAsync(() => !sheet.IsLoading);
        Assert.AreEqual("Fiche", shell.Current.Label);
        Assert.IsNull(shell.SelectedMain);
        await sheet.Review!.ValidateCommand.ExecuteAsync(null);
        var before = dashboard.Dashboard;
        sheet.BackCommand.Execute(null);
        Assert.AreSame(shell.Home, shell.Current);
        await Waiting.UntilAsync(() => !ReferenceEquals(before, dashboard.Dashboard));
        Assert.IsFalse(dashboard.Dashboard!.Overdue.Concat(dashboard.Dashboard.Today).Any(item => item.Id == vault.SheetId), "la fiche révisée a quitté la liste du jour");

        // Un passage, « Voir dans la fiche », puis deux fois Retour.
        var passageRow = dashboard.Rows.OfType<DueItemRow>().First(row => row.Item.Id == vault.PassageId);
        dashboard.OpenCommand.Execute(passageRow);
        var passage = (PassagePageViewModel)shell.Current.Page;
        await Waiting.UntilAsync(() => !passage.IsLoading);
        Assert.IsTrue(passage.IsRecall, "le rappel actif du coffre est suivi");
        var passageItem = shell.Current;
        passage.SeeInSheetCommand.Execute(null);
        var fromPassage = (SheetPageViewModel)shell.Current.Page;
        fromPassage.BackCommand.Execute(null);
        Assert.AreSame(passageItem, shell.Current);
        passage.BackCommand.Execute(null);
        Assert.AreSame(shell.Home, shell.Current);

        // Un rappel ouvre sa fiche ; un en-tête n'ouvre rien.
        navigation.OpenReminder(new PendingReminder(vault.SheetId, "Mitose", "Biologie", [], new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 3), 0));
        Assert.IsInstanceOfType<SheetPageViewModel>(shell.Current.Page);
        shell.Navigate(shell.Home);
        var current = shell.Current;
        dashboard.OpenCommand.Execute(dashboard.Rows.OfType<DashboardGroup>().First());
        dashboard.OpenCommand.Execute(null);
        Assert.AreSame(current, shell.Current);
        Assert.ThrowsExactly<ArgumentNullException>(() => navigation.Attach(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => navigation.Open(null!, true));
        Assert.ThrowsExactly<ArgumentNullException>(() => navigation.OpenReminder(null!));
    }

    [TestMethod]
    public void A_reminder_row_goes_to_the_opener()
    {
        var opener = new FakeOpener();
        using var vault = new ReviewVault();
        var dashboard = new DashboardPageViewModel(vault.App.Reminders, opener);
        var reminder = new PendingReminder("fiche", "Cellule", string.Empty, [], new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 3), 0);

        dashboard.OpenCommand.Execute(new ReminderRow(reminder));

        Assert.AreSame(reminder, opener.Opened.Single());
    }

    [STATestMethod]
    public void The_sheet_page_validates_with_ctrl_enter_and_confirms_in_a_dialog()
    {
        using var ui = new UiThread();
        using var vault = new ReviewVault();
        var test = new TestShell();
        var page = vault.SheetPage(vault.SheetId);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var window = Show(test, page);
        try
        {
            Assert.AreEqual(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(Named<TextBlock>(window, "Heading")));
            var validate = Named<Button>(window, "ValidateButton");
            Assert.AreEqual("Valider la révision J1", AutomationProperties.GetName(validate));
            Assert.AreEqual("Ctrl+Entrée", AutomationProperties.GetAcceleratorKey(validate));
            Assert.AreEqual(Visibility.Collapsed, Named<StackPanel>(window, "Done").Visibility);
            Assert.AreEqual("J0 : validée le jeu. 1 oct.", AutomationProperties.GetName(Descendants(Named<ItemsControl>(window, "Steps")).OfType<StackPanel>().First()));

            // « Reporter » et « ⋯ » ouvrent leurs actions, au-dessus du bouton.
            foreach (var name in new[] { "PostponeButton", "MoreButton" })
            {
                var button = Named<Button>(window, name);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                var menu = button.ContextMenu;
                Assert.IsTrue(menu.IsOpen);
                Assert.AreSame(button, menu.PlacementTarget);
                Assert.AreEqual(PlacementMode.Top, menu.Placement);
                Assert.AreSame(page.Review, menu.DataContext);
                menu.IsOpen = false;
            }
            Assert.AreEqual(Visibility.Collapsed, Descendants(Named<ItemsControl>(window, "Steps")).OfType<Border>().First(border => border.Name == "Link").Visibility, "la première étape n'a rien avant elle");

            // Recommencer à J0 : le dialogue s'ouvre, Annuler ne fait rien, le bouton principal confirme.
            page.Review!.RequestRestartCommand.Execute(null);
            TestShell.Flush();
            var dialog = Named<Dialog>(window, "Confirmation");
            Assert.IsTrue(dialog.IsOpen);
            Assert.AreEqual("Recommencer à J0 ?", dialog.Title);
            dialog.Close(DialogChoice.Secondary);
            UiThread.Until(() => !page.Review.IsConfirming);
            Assert.IsNull(page.Review.Message);
            page.Review.RequestRestartCommand.Execute(null);
            TestShell.Flush();
            Assert.IsTrue(dialog.IsOpen, "la liaison du dialogue tient après une fermeture");
            dialog.Close(DialogChoice.Primary);
            UiThread.Until(() => page.Review.HasMessage);
            StringAssert.StartsWith(page.Review.Message, "Nouveau cycle", StringComparison.Ordinal);

            // Sur une ligne quand il y a la place, l'un sous l'autre sinon ; jamais de défilement horizontal.
            var steps = Named<ItemsControl>(window, "Steps");
            Assert.AreEqual(0, Grid.GetRow(steps));
            window.Width = 420;
            window.UpdateLayout();
            TestShell.Flush();
            Assert.AreEqual(1, Grid.GetRow(steps));
            Assert.IsLessThanOrEqualTo(window.ActualWidth, Named<Button>(window, "ValidateButton").TranslatePoint(new Point(Named<Button>(window, "ValidateButton").ActualWidth, 0), window).X);

            // Ctrl+Entrée valide la révision.
            var shortcut = Descendants(window).OfType<SheetPage>().Single().InputBindings.OfType<KeyBinding>().Single(binding => binding.Key == Key.Enter && binding.Modifiers == ModifierKeys.Control);
            vault.Time.AdvanceDays(1);
            shortcut.Command.Execute(null);
            UiThread.Until(() => page.Review.ShowsReviewedToday);
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, Named<StackPanel>(window, "Done").Visibility);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_passage_page_reveals_the_hole()
    {
        using var ui = new UiThread();
        using var vault = new ReviewVault();
        var test = new TestShell();
        var page = vault.PassagePage(vault.PassageId, vault.SheetId, activeRecall: true);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var window = Show(test, page);
        try
        {
            var cloze = Named<TextBlock>(window, "Cloze");
            Assert.AreEqual("2 mots précède la métaphase.", new TextRange(cloze.ContentStart, cloze.ContentEnd).Text);
            Assert.AreEqual("passage caché : 2 mots précède la métaphase.", AutomationProperties.GetName(cloze));
            Assert.AreEqual(Visibility.Visible, Named<Button>(window, "ShowPassage").Visibility);
            Named<Button>(window, "ShowPassage").Command.Execute(null);
            TestShell.Flush();
            Assert.AreEqual("La prophase précède la métaphase.", new TextRange(cloze.ContentStart, cloze.ContentEnd).Text);
            Assert.IsFalse(Named<Button>(window, "ShowPassage").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_click_or_enter_on_a_dashboard_row_opens_it()
    {
        var test = new TestShell();
        var opener = new FakeOpener();
        using var vault = new ReviewVault();
        var dashboard = new DashboardPageViewModel(vault.App.Reminders, opener);
        var item = new DueItem(ItemKind.Sheet, "id", "id", "Mitose", null, null, [], null, "Biologie", [], "J1", new DateOnly(2026, 10, 2), 1, new DateOnly(2026, 10, 1), 1, true);
        dashboard.Show(new Dashboard(new DateOnly(2026, 10, 3), [item], [], [], [], [], [], 2, true, [], null, 1, 0, 0, 0, false, false));
        var shell = new ShellViewModel(CorePages.All(new Dictionary<string, object> { [CorePages.Today] = dashboard }), test.Appearance, test.Gallery);
        var window = new ShellWindow(shell) { ShowActivated = true, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        window.Activate();
        TestShell.Flush();
        try
        {
            var list = Named<ListBox>(window, "Items");
            var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1);
            var text = Descendants(row).OfType<TextBlock>().First();
            text.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
            text.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = Mouse.MouseUpEvent });
            Assert.IsTrue(row.Focus());
            row.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(row), 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            row.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(row), 0, Key.Space) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

            Assert.HasCount(2, opener.Opened);
            Assert.AreEqual((item, true), opener.Opened[0]);
        }
        finally
        {
            window.Close();
        }
    }

    private static ShellWindow Show(TestShell test, object page)
    {
        var shell = test.Shell;
        shell.Navigate(new NavigationItem("revision", "Révision", Icons.Document, int.MaxValue, page));
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

    /// <summary>
    /// Un coffre d'essai : la fiche « Mitose » (Biologie, #bio, #partiel) créée le jeudi 1er octobre
    /// 2026 avec un passage commenté et une définition ; l'horloge est au samedi 3, J1 en retard d'un jour.
    /// </summary>
    private sealed class ReviewVault : IDisposable
    {
        private readonly TestVaults _vaults;

        public ReviewVault()
        {
            Time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
            _vaults = new TestVaults(AppMode.Development, Time);
            Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Coffre")).Vault);
            SheetId = App.Sheets.Create("Biologie", "Mitose", null).Value;
            Assert.IsTrue(App.Sheets.SetTags(SheetId, ["bio", "partiel"]).Succeeded);
            PassageId = App.Annotations.Create(SheetId, Passage, "Avant la métaphase").Value;
            DefinitionId = App.Annotations.CreateDefinition(SheetId, Passage with { Exact = "prophase", Start = 3 }, new Definition("Prophase", "Première phase.")).Value;
            Time.AdvanceDays(2);
        }

        public FakeTime Time { get; }

        public KyfRev.Application.UseCases.KyfRevApplication App => _vaults.App;

        public string SheetId { get; }

        public string PassageId { get; }

        public string DefinitionId { get; }

        public List<string> Calls { get; } = [];

        public ReviewBarViewModel Bar(ItemKind kind, string id, ReviewState state) => new(App.Reviews, App.Reminders, Time, kind, id, state);

        public async Task<ReviewBarViewModel> SheetBarAsync()
        {
            var page = SheetPage(SheetId);
            await page.LoadCommand.ExecuteAsync(null);
            return page.Review!;
        }

        public SheetPageViewModel SheetPage(string id) => new(App.Sheets, Bar, id, () => Calls.Add("retour"));

        public PassagePageViewModel PassagePage(string passageId, string sheetId, bool activeRecall) =>
            new(App.Sheets, Bar, passageId, sheetId, activeRecall, () => Calls.Add("retour"), sheet => Calls.Add($"fiche {sheet}"));

        public async Task<PassagePageViewModel> PassageAsync(bool activeRecall)
        {
            var page = PassagePage(PassageId, SheetId, activeRecall);
            await page.LoadCommand.ExecuteAsync(null);
            return page;
        }

        public async Task<PassagePageViewModel> DefinitionAsync()
        {
            var page = PassagePage(DefinitionId, SheetId, activeRecall: true);
            await page.LoadCommand.ExecuteAsync(null);
            return page;
        }

        public void Dispose() => _vaults.Dispose();
    }
}
