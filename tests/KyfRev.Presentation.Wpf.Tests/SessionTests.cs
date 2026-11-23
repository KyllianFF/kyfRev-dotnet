using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
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

    /// <summary>
    /// Rangée 27A : validée, la séance passe seule à l'élément suivant après un instant ; « Rester » (Échap, un clic dans la
    /// fiche) l'arrête, et le bouton qui a pris la place de « Valider » y mène quand on veut.
    /// </summary>
    [TestMethod]
    public async Task After_a_validation_the_session_moves_on_by_itself_unless_one_stays()
    {
        using var vault = new SessionVault();
        var session = await vault.StartAsync();
        var waits = new List<(TimeSpan Delay, TaskCompletionSource Done)>();
        session.Wait = (delay, token) =>
        {
            var done = new TaskCompletionSource();
            token.Register(() => done.TrySetCanceled(token));
            waits.Add((delay, done));
            return done.Task;
        };

        // 1 : la barre sait où mène la suite ; validée, la séance attend un instant puis y va.
        var first = await SessionVault.LoadedAsync(session);
        var bar = first.Review!;
        Assert.AreSame(session.NextCommand, bar.GoNext);
        Assert.AreEqual(SessionText.NextSheet, bar.NextName);
        Assert.IsFalse(bar.ShowsNextButton, "avant la validation, « Valider »");
        await bar.ValidateCommand.ExecuteAsync(null);
        Assert.IsTrue(session.IsAdvancing);
        Assert.IsTrue(bar.IsAdvancing);
        Assert.IsTrue(bar.ShowsNextButton);
        Assert.IsFalse(bar.ShowsDoneText);
        Assert.AreEqual(SessionText.NextSheet + "…", bar.DoneLabel);
        Assert.AreEqual(StudySessionViewModel.AdvanceDelay, waits.Single().Delay);
        Assert.AreEqual(TimeSpan.FromSeconds(1.5), StudySessionViewModel.AdvanceDelay);
        waits[0].Done.SetResult();
        await Waiting.UntilAsync(() => !ReferenceEquals(session.Current, first));
        Assert.AreEqual("Séance : 2 / 3", session.ProgressLabel);
        Assert.IsFalse(session.IsAdvancing);
        Assert.AreEqual(1, session.DoneCount);

        // 2 : validée puis « Rester » : la séance ne bouge plus, même l'attente finie ; « Fiche suivante » y mène.
        var second = await SessionVault.LoadedAsync(session);
        await second.Review!.ValidateCommand.ExecuteAsync(null);
        Assert.IsTrue(session.IsAdvancing);
        session.StayCommand.Execute(null);
        Assert.IsFalse(session.IsAdvancing);
        Assert.IsFalse(second.Review.IsAdvancing);
        Assert.AreEqual(SessionText.NextSheet, second.Review.DoneLabel, "sans les points : elle n'avance plus seule");
        Assert.IsTrue(waits[1].Done.Task.IsCanceled, "l'attente est abandonnée");
        Assert.AreSame(second, session.Current);
        second.Review.GoNext!.Execute(null);
        Assert.AreEqual("Séance : 3 / 3", session.ProgressLabel);

        // 3 : le dernier mène au bilan ; une autre action que la validation n'avance pas seule.
        var third = await SessionVault.LoadedAsync(session);
        Assert.AreEqual(SessionText.ToSummary, third.Review!.NextName);
        await third.Review.PostponeTomorrowCommand.ExecuteAsync(null);
        Assert.IsFalse(session.IsAdvancing, "reporter laisse choisir");
        Assert.HasCount(2, waits);
        Assert.IsTrue(session.HasActed);
        session.NextCommand.Execute(null);
        Assert.IsTrue(session.IsFinished);
    }

    [TestMethod]
    public async Task Leaving_or_ending_while_the_session_moves_on_stops_it()
    {
        using var vault = new SessionVault();
        var session = await vault.StartAsync();
        var waits = new List<(CancellationToken Token, TaskCompletionSource Done)>();
        session.Wait = (_, token) =>
        {
            var done = new TaskCompletionSource();
            waits.Add((token, done));
            return done.Task;
        };
        var first = await SessionVault.LoadedAsync(session);
        await first.Review!.ValidateCommand.ExecuteAsync(null);
        Assert.IsTrue(session.IsAdvancing);

        // Quitter la séance (Retour) l'interrompt : elle n'avancera pas dans le dos de la vue d'où l'on vient.
        session.Interrupt();
        Assert.IsFalse(session.IsAdvancing);
        Assert.IsTrue(waits[0].Token.IsCancellationRequested);
        waits[0].Done.SetResult();
        await Task.Yield();
        Assert.AreSame(first, session.Current, "l'attente finie après coup n'y change rien");

        // « Terminer » pendant l'attente : le bilan, sans avancer d'abord.
        session.NextCommand.Execute(null);
        var second = await SessionVault.LoadedAsync(session);
        await second.Review!.ValidateCommand.ExecuteAsync(null);
        Assert.IsTrue(session.IsAdvancing);
        session.EndCommand.Execute(null);
        Assert.IsTrue(session.IsFinished);
        Assert.IsFalse(session.IsAdvancing);
        Assert.IsTrue(waits[1].Token.IsCancellationRequested);
        Assert.AreEqual(2, session.DoneCount);
    }

    /// <summary>
    /// Valider en séance ne bouscule rien : ni la barre de séance (« Passer » et « Suivant » ont la même hauteur), ni la
    /// bande de révision ; Échap ou un clic dans la fiche font rester, le bouton de la bande mène à la suite.
    /// </summary>
    [STATestMethod]
    [DataRow(false, 470)]
    [DataRow(true, 470)]
    [DataRow(false, 900)]
    [DataRow(true, 1440)]
    [DataRow(false, 1440)]
    [DataRow(true, 2560)]
    public void Validating_in_a_session_moves_nothing_and_escape_or_a_click_stays(bool dark, int width)
    {
        using var ui = new UiThread();
        using var vault = new SessionVault();
        // Des titres : le plan s'ouvre à côté de la fiche, et s'arrête avant la bande.
        var atom = vault.App.Sheets.List().Value.Single(sheet => sheet.Title == "Atome").Id;
        Assert.IsTrue(vault.App.Editing.Save(atom, "# Atome\n\n## Noyau\n\nProtons et neutrons.\n\n## Électrons\n\nLeur nuage.\n", vault.App.Sheets.Open(atom).Value.Fingerprint, 1).Succeeded);
        var test = new TestShell(dark);
        var window = new ShellWindow(vault.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = width, Height = 800, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            if (window.Content is Panel { Background: null } root)
            {
                root.SetResourceReference(Panel.BackgroundProperty, "Kyf.Brush.Background");
            }
            vault.Shell.ShowWorkspace();
            UiThread.Until(() => vault.Dashboard.Dashboard is not null);
            vault.Dashboard.StartCommand.Execute(null);
            var session = (StudySessionViewModel)vault.Shell.Current.Page;
            var never = new TaskCompletionSource();
            session.Wait = (_, _) => never.Task;
            var page = (IReviewPage)session.Current!;
            UiThread.Until(() => page.Review is not null);
            for (var pass = 0; pass < 6; pass++)
            {
                TestShell.Flush();
                window.UpdateLayout();
            }
            var theme = dark ? "sombre" : "clair";
            ((FrameworkElement)window.Content).Capture(FormattableString.Invariant($"seance-bande-{width}-{theme}-avant"));
            var sessionBar = Named<Border>(window, "SessionBar");
            var band = Named<Border>(Descendants(window).OfType<ReviewBar>().Single(view => view.IsVisible), "Bar");
            var before = (Session: sessionBar.ActualHeight, Band: band.ActualHeight, Top: band.TranslatePoint(default, window).Y);

            page.Review!.ValidateCommand.Execute(null);
            UiThread.Until(() => session.IsAdvancing);
            for (var pass = 0; pass < 6; pass++)
            {
                TestShell.Flush();
                window.UpdateLayout();
            }
            ((FrameworkElement)window.Content).Capture(FormattableString.Invariant($"seance-bande-{width}-{theme}-apres"));
            Assert.AreEqual(before.Session, sessionBar.ActualHeight, 0.5, "« Suivant » a la hauteur de « Passer »");
            Assert.AreEqual(before.Band, band.ActualHeight, 0.5, "la bande garde sa hauteur");
            Assert.AreEqual(before.Top, band.TranslatePoint(default, window).Y, 0.5, "et sa place");
            var next = Named<Button>(window, "NextButton");
            Assert.IsTrue(next.IsVisible, "« Fiche suivante… » à la place de « Valider »");
            Assert.AreSame(session.NextCommand, next.Command);
            Assert.IsTrue(Named<Border>(window, "Countdown").IsVisible, "le trait dit que la suite vient");
            Assert.IsLessThanOrEqualTo(window.ActualWidth, next.TranslatePoint(new Point(next.ActualWidth, 0), window).X, "rien ne dépasse");
            // Le plan, quand il a la place, s'arrête avant la bande ; le bas de la page a la couleur du menu.
            if (Named<ContentControl>(window, "OutlinePane") is { IsVisible: true } outline)
            {
                // L'emplacement donné au plan, et non la hauteur que son contenu réclame (rognée à l'écran).
                var slot = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(outline);
                var gap = -((UIElement)VisualTreeHelper.GetParent(outline)).TranslatePoint(new Point(0, slot.Bottom - outline.Margin.Bottom), band).Y;
                Assert.IsGreaterThanOrEqualTo(12, gap, "un écart entre le plan et la bande");
            }
            Assert.AreSame(test.Design.Dictionary["Kyf.Brush.Surface"], Named<Border>(window, "SheetFooter").Background);

            // Échap : on reste.
            var view = Descendants(window).OfType<StudySessionPage>().Single();
            var escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            view.RaiseEvent(escape);
            Assert.IsTrue(escape.Handled);
            Assert.IsFalse(session.IsAdvancing);
            Assert.IsFalse(Named<Border>(window, "Countdown").IsVisible);

            // Un clic dans la fiche aussi ; mais pas sur le bouton de la suite.
            session.NextCommand.Execute(null);
            page = (IReviewPage)session.Current!;
            UiThread.Until(() => page.Review is not null);
            TestShell.Flush();
            page.Review!.ValidateCommand.Execute(null);
            UiThread.Until(() => session.IsAdvancing);
            TestShell.Flush();
            next = Named<Button>(window, "NextButton");
            var onNext = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent, Source = next };
            next.RaiseEvent(onNext);
            Assert.IsTrue(session.IsAdvancing, "le bouton de la suite ne fait pas rester");
            var onSheet = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent };
            Named<TextBlock>(window, "Status").RaiseEvent(onSheet);
            Assert.IsFalse(session.IsAdvancing, "un clic dans la fiche fait rester");
            Assert.AreSame(page, session.Current);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public async Task A_sheet_of_a_session_keeps_its_page_its_zoom_and_its_column_as_anywhere_else()
    {
        // Ce que l'ordinateur retient : une feuille A4 pour la première fiche de la séance, un zoom, une colonne étroite.
        var machine = new MachineSettings(new MemoryMachinePreferences { Stored = new MachinePreferences { SheetZoom = 1.9, Column = ReadingColumn.Narrow, RibbonReduced = true } });
        using var vault = new SessionVault(machine: machine);
        var first = vault.App.Sheets.List().Value.Single(sheet => sheet.Title == "Atome").Id;
        var a4 = new PageLayout(PageFormat.A4);
        machine.Update(preferences => preferences with { SheetPages = preferences.SheetPages.With(first, a4) });

        var session = await vault.StartAsync();
        var page = (SheetPageViewModel)await SessionVault.LoadedAsync(session);
        Assert.AreEqual(first, page.SheetId);

        // La même fiche ouverte depuis le Carnet, pour comparer.
        vault.Navigation.OpenSheet(first);
        var elsewhere = (SheetPageViewModel)vault.Shell.Current.Page;

        Assert.AreEqual(a4, page.PageSetup!.Layout, "sa feuille, la même qu'ailleurs : le texte se mesure sur elle");
        Assert.AreEqual(elsewhere.TextWidth, page.TextWidth);
        Assert.AreEqual(1.9, page.SheetZoom);
        Assert.IsTrue(page.IsRibbonReduced);
        Assert.IsNotNull(page.Masks);
        Assert.IsTrue(page.CanPrint || page.Print is not null);

        // Le zoom changé en séance est retenu, comme ailleurs.
        page.SheetZoom = 1.5;
        Assert.AreEqual(1.5, machine.Current.SheetZoom);

        // Rien de ce qui ferait sortir de la séance.
        Assert.IsFalse(page.CanFocus, "la séance a son plein écran");
        Assert.IsNull(page.SheetActions);
        Assert.IsNull(page.OpenPassage);
    }

    /// <summary>
    /// Sur un papier ligné, le texte d'une fiche de séance reste posé sur les lignes d'un mode à l'autre, comme ailleurs :
    /// il glissait un peu plus à chaque passage de Lire à Réviser quand la séance ignorait la mise en page de la fiche.
    /// </summary>
    [STATestMethod]
    public void On_a_ruled_paper_the_text_of_a_session_stays_on_the_lines_from_one_mode_to_the_other()
    {
        using var ui = new UiThread();
        var lined = new PaperChoices(IsOn: true, Endless: PaperStyle.Lines, A4: PaperStyle.Lines, A5: PaperStyle.Lines, A6: PaperStyle.Lines);
        var machine = new MachineSettings(new MemoryMachinePreferences { Stored = new MachinePreferences { Paper = lined, SheetZoom = 1.9, Column = ReadingColumn.Narrow } });
        using var vault = new SessionVault(machine: machine);
        var atom = vault.App.Sheets.List().Value.Single(sheet => sheet.Title == "Atome").Id;
        var body = "# ATOME\n\n" + string.Join("\n\n", Enumerable.Range(1, 12).Select(index => $"HIHI PARAGRAPHE {index} AVEC ASSEZ DE MOTS POUR PASSER À LA LIGNE DANS LA FEUILLE ET MONTRER DEUX LIGNES.")) + "\n";
        Assert.IsTrue(vault.App.Editing.Save(atom, body, vault.App.Sheets.Open(atom).Value.Fingerprint, 1).Succeeded);
        machine.Update(preferences => preferences with { SheetPages = preferences.SheetPages.With(atom, new PageLayout(PageFormat.A4)) });
        var test = new TestShell();
        var window = new ShellWindow(vault.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 1440, Height = 900, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Resources[SheetPaper.ResourceKey] = SheetPaper.Of(machine.Current);
        window.Show();
        try
        {
            vault.Shell.ShowWorkspace();
            UiThread.Until(() => vault.Dashboard.Dashboard is not null);
            vault.Dashboard.StartCommand.Execute(null);
            var session = (StudySessionViewModel)vault.Shell.Current.Page;
            var page = (SheetPageViewModel)session.Current!;
            UiThread.Until(() => page.Review is not null && page.Reading is not null);

            var worst = new List<string>();
            for (var turn = 0; turn < 10; turn++)
            {
                switch (turn % 3)
                {
                    case 0:
                        page.IsReadOn = true;
                        break;
                    case 1:
                        page.IsReviseOn = true;
                        break;
                    default:
                        page.IsWriteOn = true;
                        UiThread.Until(() => page.IsWriting);
                        break;
                }
                for (var pass = 0; pass < 12; pass++)
                {
                    TestShell.Flush();
                    window.UpdateLayout();
                }
                if (page.IsWriting)
                {
                    continue;
                }
                var viewer = Descendants(window).OfType<SheetDocumentViewer>().First(candidate => candidate.IsVisible && candidate.Document is not null);
                var step = viewer.WritingStep;
                Assert.IsGreaterThan(0, step, "le papier est réglé");
                var sheets = viewer.PagePaper.Sheets;
                foreach (var paragraph in viewer.Document!.Blocks.OfType<Paragraph>().Where(candidate => candidate.Tag is null && candidate.LineStackingStrategy == LineStackingStrategy.BlockLineHeight))
                {
                    var glyphs = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward).GetCharacterRect(LogicalDirection.Forward);
                    var baseline = glyphs.Top + (paragraph.FontFamily.Baseline * paragraph.FontSize);
                    if (sheets.FirstOrDefault(sheet => baseline > sheet.Y && baseline < sheet.Bottom) is not { Width: > 0 } sheet)
                    {
                        continue;
                    }
                    var offset = (baseline - (sheet.Y + viewer.PagePaper.WritingLine)) / step;
                    if (Math.Abs(offset - Math.Round(offset)) > 0.06)
                    {
                        worst.Add(FormattableString.Invariant($"bascule {turn} : {offset:0.00} pas"));
                    }
                    // La fin de sa première ligne reste sur la feuille : le texte se mesure sur elle, pas sur la vue.
                    var lineEnd = (paragraph.ContentStart.GetLineStartPosition(1) ?? paragraph.ContentEnd).GetInsertionPosition(LogicalDirection.Backward).GetCharacterRect(LogicalDirection.Backward);
                    if (glyphs.Left < sheet.X || lineEnd.Right > sheet.Right)
                    {
                        worst.Add(FormattableString.Invariant($"bascule {turn} : texte de {glyphs.Left:0} à {lineEnd.Right:0}, feuille de {sheet.X:0} à {sheet.Right:0}"));
                    }
                }
            }
            Assert.IsEmpty(worst, string.Join(" ; ", worst));
        }
        finally
        {
            window.Close();
        }
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
    public void Full_screen_keeps_the_menu_and_escape_leaves_it()
    {
        var shell = new TestShell().Shell;
        shell.ShowWorkspace();

        shell.ToggleFullScreen();
        Assert.IsTrue(shell.IsFullScreen);
        Assert.IsTrue(shell.IsMenuVisible, "le menu reste : c'est par lui qu'on change de vue");
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

        public SessionVault(bool withPassage = false, MachineSettings? machine = null)
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
            Navigation = new ReviewNavigation(App.Sheets, App.Annotations, App.Reviews, App.Reminders, App.Vaults, Sessions, Time, new RecordingLinks(), App.Editing, FakeEnvironment.Markdown(), machine: machine);
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
