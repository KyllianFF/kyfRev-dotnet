using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Les fiches qui mènent à la fiche ouverte, dans leur panneau, et l'aperçu d'une fiche liée au survol ou au focus.</summary>
[TestClass]
public sealed class BacklinksTests
{
    private const string Linking = "Voir [cette fiche](Mitose.md) pour le détail.\n";

    private static readonly string[] ExcerptRuns = ["Voir ", "cette fiche", " pour le détail."];

    [TestMethod]
    public async Task The_sheets_that_lead_here_are_listed_with_the_text_around_their_link()
    {
        using var vault = new ReadingVault("Le texte de la cible.\n", passage: false);
        var source = Source(vault, "Histoire", "Révolution", "Voir [la mitose](../Biologie/Mitose.md), et [encore](../Biologie/Mitose.md).\n");
        var page = vault.Page();

        await page.LoadCommand.ExecuteAsync(null);

        var entry = page.Backlinks.Single();
        Assert.AreEqual(("Révolution", "Histoire", false, "2 liens"), (entry.Title, entry.Folder, entry.IsArchived, entry.SeveralLinks));
        Assert.IsTrue(entry.HasFolder);
        Assert.IsTrue(entry.HasSeveralLinks);
        Assert.AreEqual("Voir la mitose, et encore.", string.Concat(entry.Excerpt.Select(part => part.Text)));
        Assert.AreEqual("Révolution, Histoire, 2 liens, Voir la mitose, et encore.", entry.Spoken);
        Assert.AreEqual("Rétroliens · 1", page.BacklinksLabel);
        Assert.AreEqual("Fiches qui mènent ici · 1", page.BacklinksHeading);
        Assert.AreEqual(source, entry.SheetId);

        page.OpenBacklinkCommand.Execute(entry);
        Assert.AreEqual(source, vault.OpenedSheets.Single());
    }

    [TestMethod]
    public async Task An_archived_source_at_the_root_says_so_and_a_single_link_is_not_counted()
    {
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        var source = vault.App.Sheets.Create(null, "Racine", null).Value;
        Assert.IsTrue(vault.App.Editing.Save(source, "[c](Biologie/Mitose.md)\n", vault.App.Sheets.Open(source).Value.Fingerprint, 1).Succeeded);
        Assert.IsTrue(vault.App.Reviews.Archive(source).Succeeded);
        var page = vault.Page();

        await page.LoadCommand.ExecuteAsync(null);

        var entry = page.Backlinks.Single();
        Assert.IsTrue(entry.IsArchived);
        Assert.IsFalse(entry.HasFolder);
        Assert.IsFalse(entry.HasSeveralLinks);
        Assert.AreEqual("Racine, archivée, c", entry.Spoken);
    }

    [TestMethod]
    public async Task A_sheet_that_nothing_leads_to_keeps_all_the_room_for_its_text()
    {
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        page.PageWidth = 2000;

        Assert.IsFalse(page.HasBacklinks);
        Assert.IsFalse(page.ShowsBacklinks);
        Assert.IsFalse(page.HasPanels);
        Assert.AreEqual(ReadingText.PanelsMenu, page.PanelsMenuName);
    }

    [TestMethod]
    public async Task The_panel_only_shows_while_reading()
    {
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        Source(vault, "Biologie", "Source", Linking);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        page.PageWidth = 2000;
        Assert.IsTrue(page.ShowsBacklinks);

        page.IsReviseOn = true;
        Assert.IsFalse(page.HasBacklinks, "en révision, un extrait donnerait la réponse");
        Assert.IsFalse(page.ShowsBacklinks);

        page.IsReadOn = true;
        UiThread.Until(() => page.ShowsBacklinks);

        await page.StartWritingCommand.ExecuteAsync(null);
        Assert.IsFalse(page.HasBacklinks);
        Assert.IsFalse(page.ShowsBacklinks);
    }

    [TestMethod]
    public async Task While_revising_from_the_start_nothing_is_searched()
    {
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        Source(vault, "Biologie", "Source", Linking);
        var page = vault.Page(revising: true);

        await page.LoadCommand.ExecuteAsync(null);

        Assert.IsEmpty(page.Backlinks);

        page.IsReadOn = true;
        UiThread.Until(() => page.Backlinks.Count == 1);
    }

    [TestMethod]
    public async Task The_same_button_hides_the_panel_or_opens_it_over_the_text()
    {
        using var vault = new ReadingVault("# Partie\n\nUn autre paragraphe et un mot.\n", passage: true);
        Source(vault, "Biologie", "Source", Linking);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        page.PageWidth = SheetPageViewModel.PassagesMinimum;
        Assert.IsTrue(page.ShowsBacklinks);
        Assert.IsTrue(page.IsBacklinksOn);
        page.IsBacklinksOn = false;
        Assert.IsFalse(page.ShowsBacklinks);
        page.IsBacklinksOn = true;

        page.PageWidth = SheetPageViewModel.PassagesMinimum - 1;
        Assert.IsFalse(page.ShowsBacklinks);
        Assert.IsFalse(page.IsBacklinksOn);
        page.IsBacklinksOn = true;
        Assert.IsTrue(page.ShowsBacklinksOverlay);
        Assert.IsTrue(page.IsBacklinksOn);

        page.IsPassagesOn = true;
        Assert.IsFalse(page.ShowsBacklinksOverlay, "un seul volet à la fois par-dessus le texte");
        page.IsBacklinksOn = true;
        Assert.IsFalse(page.ShowsPassagesOverlay);
        page.IsOutlineOn = true;
        Assert.IsFalse(page.ShowsBacklinksOverlay);
        page.IsBacklinksOn = true;
        Assert.IsFalse(page.ShowsOutlineOverlay);

        page.CloseOverlayCommand.Execute(null);
        Assert.IsFalse(page.IsBacklinksOn);
        Assert.AreEqual("Volets : Plan, Passages · 2, Rétroliens · 1", page.PanelsMenuName);

        page.IsBacklinksOn = true;
        page.OpenBacklinkCommand.Execute(page.Backlinks[0]);
        Assert.IsFalse(page.ShowsBacklinksOverlay, "ouvrir une fiche referme le volet");
    }

    [TestMethod]
    public async Task Alone_the_backlinks_name_the_more_menu()
    {
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        Source(vault, "Biologie", "Source", Linking);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        Assert.IsTrue(page.HasPanels);
        Assert.AreEqual("Volets : Rétroliens · 1", page.PanelsMenuName, "le menu n'annonce ni plan ni passages qu'il n'a pas");
    }

    [TestMethod]
    public async Task A_search_that_fails_says_so_in_the_panel()
    {
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        Source(vault, "Biologie", "Source", Linking);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        Assert.HasCount(1, page.Backlinks);

        vault.Leave();
        page.IsReviseOn = true;
        page.IsReadOn = true;

        UiThread.Until(() => page.HasBacklinksError && page.Backlinks.Count == 0);
        Assert.IsTrue(page.HasBacklinks, "le panneau dit pourquoi il est vide");
    }

    [STATestMethod]
    public void A_search_that_fails_is_announced_by_a_notification()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        Source(vault, "Biologie", "Source", Linking);
        var test = new TestShell();
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => page.Backlinks.Count == 1);
        var window = Show(test, page, 1200, 700);
        try
        {
            var message = Descendants(Named<ContentControl>(window, "BacklinksPane")).OfType<Notification>().Single();
            Assert.IsFalse(message.IsVisible, "rien à dire tant que la recherche réussit");

            vault.Leave();
            page.IsReviseOn = true;
            page.IsReadOn = true;
            UiThread.Until(() => page.HasBacklinksError);
            TestShell.Flush();

            Assert.IsTrue(message.IsVisible, "une notification s'annonce d'elle-même au lecteur d'écran");
            Assert.AreEqual(page.BacklinksError, message.Message);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public async Task A_result_that_did_not_change_keeps_the_list_in_place()
    {
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        var source = Source(vault, "Biologie", "Source", Linking);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        var before = page.Backlinks;

        await page.LoadCommand.ExecuteAsync(null);
        Assert.AreSame(before, page.Backlinks, "rien n'a changé : rien à reconstruire");

        Assert.IsTrue(vault.App.Editing.Save(source, "Voir [cette fiche](Mitose.md) autrement.\n", vault.App.Sheets.Open(source).Value.Fingerprint, 2).Succeeded);
        await page.LoadCommand.ExecuteAsync(null);
        Assert.AreNotSame(before, page.Backlinks, "un extrait qui change se relit");
        Assert.AreEqual("Voir cette fiche autrement.", string.Concat(page.Backlinks.Single().Excerpt.Select(part => part.Text)));
    }

    [STATestMethod]
    public void A_sheet_linked_by_many_others_only_builds_the_entries_on_screen()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        const int many = 150;
        for (var n = 0; n < many; n++)
        {
            Source(vault, "Biologie", $"Source {n}", Linking);
        }
        var test = new TestShell();
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => page.Backlinks.Count == many);
        var window = Show(test, page, 1200, 700);
        try
        {
            var pane = Named<ContentControl>(window, "BacklinksPane");
            var list = Descendants(pane).OfType<ItemsControl>().Single();

            var realized = Enumerable.Range(0, many).Count(index => list.ItemContainerGenerator.ContainerFromIndex(index) is not null);

            Assert.IsGreaterThan(0, realized);
            Assert.IsLessThan(many / 2, realized, "la liste est virtualisée");
            Assert.AreEqual(ScrollBarVisibility.Disabled, Descendants(pane).OfType<ScrollViewer>().Single().HorizontalScrollBarVisibility, "jamais de défilement horizontal");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_newer_search_stops_an_older_one()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        var source = Source(vault, "Biologie", "Source", Linking);
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => page.Backlinks.Count == 1);
        var before = page.Backlinks;
        Assert.IsTrue(vault.App.Editing.Save(source, "Voir [cette fiche](Mitose.md) autrement.\n", vault.App.Sheets.Open(source).Value.Fingerprint, 2).Succeeded);
        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = Task.Run(() => vault.App.Host.Use(_ =>
        {
            held.Set();
            release.Wait();
        }));
        held.Wait();

        // Le coffre est occupé : la première recherche attend son tour, et la seconde l'annule.
        page.IsReviseOn = true;
        page.IsReadOn = true;
        page.IsReviseOn = true;
        page.IsReadOn = true;
        release.Set();
        holder.GetAwaiter().GetResult();

        UiThread.Until(() => !ReferenceEquals(before, page.Backlinks));
        Assert.HasCount(1, page.Backlinks);
        Assert.IsFalse(page.HasBacklinksError, "une recherche annulée n'est pas un échec");
    }

    [TestMethod]
    public async Task Coming_back_to_the_sheet_reads_again_what_leads_to_it()
    {
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        var source = Source(vault, "Biologie", "Source", Linking);
        var page = vault.Page();
        page.OnNavigatedTo();
        await page.LoadCommand.ExecuteAsync(null);
        Assert.HasCount(1, page.Backlinks);

        var opened = vault.App.Sheets.Open(source).Value;
        Assert.IsTrue(vault.App.Editing.Save(source, "Plus de lien.\n", opened.Fingerprint, 2).Succeeded);
        page.OnNavigatedTo();

        UiThread.Until(() => page.Backlinks.Count == 0);
    }

    [TestMethod]
    public async Task A_rename_of_the_linked_sheet_reloads_the_text_whose_link_was_rewritten()
    {
        using var vault = new ReadingVault("Voir [la cible](Cible.md).\n", passage: false);
        var target = vault.App.Sheets.Create("Biologie", "Cible", null).Value;
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        var before = page.Reading;
        Assert.IsTrue(vault.App.Sheets.ChangeTitle(target, "Destination").Succeeded);

        page.OnNavigatedTo();
        UiThread.Until(() => !ReferenceEquals(before, page.Reading));

        Assert.AreEqual("Destination", vault.App.Sheets.Preview("fiches/Biologie/Destination.md").Value!.Title);
        Assert.Contains("Destination.md", vault.App.Sheets.Open(vault.SheetId).Value.Body);
    }

    [STATestMethod]
    public void The_panel_beside_the_text_opens_the_source_from_the_keyboard_and_narrows_over_the_text()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        var source = Source(vault, "Biologie", "Source", Linking);
        var test = new TestShell();
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => page.Backlinks.Count == 1);
        var window = Show(test, page, 1200, 700);
        try
        {
            var pane = Named<ContentControl>(window, "BacklinksPane");
            Assert.AreEqual(Visibility.Visible, pane.Visibility);
            var button = Descendants(Descendants(pane).OfType<ItemsControl>().Single()).OfType<Button>().Single();
            Assert.AreEqual(page.Backlinks[0].Spoken, AutomationProperties.GetName(button));
            var excerpt = Descendants(pane).OfType<ExcerptText>().Single();
            CollectionAssert.AreEqual(
                ExcerptRuns,
                excerpt.Inlines.OfType<Run>().Select(run => run.Text).ToArray());
            Assert.IsNotNull(excerpt.Inlines.OfType<Run>().ElementAt(1).Background, "le texte du lien ressort");
            Assert.IsTrue(Named<ToggleButton>(window, "BacklinksToggle").IsVisible);

            button.Command.Execute(button.CommandParameter);
            Assert.AreEqual(source, vault.OpenedSheets.Single());

            window.Width = 470;
            window.UpdateLayout();
            TestShell.Flush();
            Assert.IsTrue(page.IsCompactHeader);
            Assert.AreEqual(Visibility.Collapsed, pane.Visibility);
            Assert.IsFalse(Named<ToggleButton>(window, "BacklinksToggle").IsVisible);
            var more = Named<Button>(window, "HeaderMoreButton");
            Assert.IsTrue(more.IsVisible, "à la largeur minimale, ils sont sous « ⋯ »");
            more.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            TestShell.Flush();
            var item = more.ContextMenu.Items.OfType<MenuItem>().Single(entry => entry.Name == "BacklinksItem");
            Assert.AreEqual(page.BacklinksLabel, item.Header);
            item.IsChecked = true;
            more.ContextMenu.IsOpen = false;
            TestShell.Flush();

            var overlay = Named<ContentControl>(window, "BacklinksOverlay");
            Assert.AreEqual(Visibility.Visible, overlay.Visibility);
            var scroller = Descendants(overlay).OfType<ScrollViewer>().Single();
            Assert.AreEqual(ScrollBarVisibility.Disabled, scroller.HorizontalScrollBarVisibility, "jamais de défilement horizontal");
            Assert.IsLessThanOrEqualTo(window.ActualWidth, overlay.ActualWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Just_wide_enough_the_three_panel_buttons_hold_on_the_line_of_back()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("# Partie\n\nUn autre paragraphe et un mot.\n", passage: true);
        Source(vault, "Biologie", "Source avec un long titre", Linking);
        var test = new TestShell();
        var page = vault.Page(speech: new FakeVoices("Hortense"));
        page.LoadCommand.Execute(null);
        UiThread.Until(() => page.Backlinks.Count == 1);
        var sheet = new SheetPage { DataContext = page, Width = SheetPageViewModel.CompactHeaderWidth + SheetPageViewModel.BacklinksToggleRoom, Height = 700 };
        var window = new Window { Content = new Canvas { Children = { sheet } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        try
        {
            Assert.IsFalse(page.IsCompactHeader);
            var back = Named<Button>(window, "BackButton");
            var line = back.TranslatePoint(new Point(0, back.ActualHeight / 2), window).Y;
            foreach (var name in new[] { "OutlineToggle", "PassagesToggle", "BacklinksToggle", "ReadToggle", "ListenToggle" })
            {
                var button = Named<ButtonBase>(window, name);
                Assert.IsTrue(button.IsVisible, name);
                Assert.AreEqual(line, button.TranslatePoint(new Point(0, button.ActualHeight / 2), window).Y, 6, $"{name} : sur la ligne du retour");
            }

            sheet.Width -= 1;
            TestShell.Flush();
            Assert.IsTrue(page.IsCompactHeader, "un cran plus étroit, ils passent sous « ⋯ »");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_preview_stays_within_the_width_of_the_text()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Un assez long début de ligne avant le lien : [la cible](Cible.md).\n", passage: false);
        Target(vault, "Le texte.\n");
        var (window, viewer, _) = Open(vault);
        try
        {
            window.Width = 470;
            window.UpdateLayout();
            TestShell.Flush();
            viewer.FocusReached(Link(viewer));
            UiThread.Until(() => viewer.LinkPopup?.IsOpen == true);

            var popup = viewer.LinkPopup!;
            var place = popup.PlacementRectangle.Left + popup.HorizontalOffset;
            Assert.IsGreaterThanOrEqualTo(0, place);
            Assert.IsLessThanOrEqualTo(Descendants(viewer).OfType<ScrollViewer>().First().ActualWidth, place + LinkPreviewCard.CardWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Pointing_at_a_link_shows_its_preview_after_a_moment_and_leaving_hides_it()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Voir [la cible](Cible.md) et du texte.\n", passage: false);
        var target = Target(vault, "- La **prophase** commence.\n");
        vault.App.Sheets.SetTags(target, ["bio", "cellule"]);
        var (window, viewer, page) = Open(vault);
        try
        {
            var link = Link(viewer);
            var run = link.Inlines.OfType<Run>().Single();
            viewer.PointAt(run);
            Assert.AreSame(page.LinkPreview, viewer.LinkPreview);
            Assert.IsNull(viewer.LinkPopup, "rien n'est lu tout de suite : le pointeur ne fait peut-être que passer");

            UiThread.Until(() => viewer.LinkPopup?.IsOpen == true);

            Assert.IsTrue(page.LinkPreview.IsOpen);
            Assert.AreEqual(("Cible", "Biologie", "#bio  #cellule"), (page.LinkPreview.Title, page.LinkPreview.Folder, page.LinkPreview.Tags));
            Assert.IsTrue(page.LinkPreview.HasFolder && page.LinkPreview.HasTags && page.LinkPreview.HasStep);
            Assert.AreEqual("La prophase commence.", page.LinkPreview.Excerpt);
            Assert.StartsWith("J1 · prévue ", page.LinkPreview.Step);
            Assert.AreEqual(ReadingText.PreviewOpen("Cible"), page.LinkPreview.OpenName);
            Assert.AreEqual(page.LinkPreview.Description, AutomationProperties.GetHelpText(link), "le lien décrit son aperçu au lecteur d'écran");

            viewer.PointAt(viewer);
            Assert.IsTrue(viewer.LinkPopup!.IsOpen, "il reste un instant, de quoi l'atteindre");
            UiThread.Until(() => !viewer.LinkPopup.IsOpen);

            Assert.IsFalse(page.LinkPreview.IsOpen);
            Assert.AreEqual(string.Empty, AutomationProperties.GetHelpText(link));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_mouse_move_on_the_text_is_what_starts_the_preview()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Voir [la cible](Cible.md) et du texte.\n", passage: false);
        Target(vault, "Le texte.\n");
        var (window, viewer, _) = Open(vault);
        try
        {
            var run = Link(viewer).Inlines.OfType<Run>().Single();

            run.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent });

            Assert.AreSame(Link(viewer), viewer.PointedLink);
            viewer.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
            Assert.IsNull(viewer.PointedLink);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_keyboard_focus_shows_the_preview_at_once_and_escape_closes_it()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Voir [la cible](Cible.md) et du texte.\n", passage: false);
        Target(vault, "Le texte.\n");
        var (window, viewer, page) = Open(vault);
        try
        {
            var link = Link(viewer);
            viewer.FocusReached(link);
            Assert.AreSame(link, viewer.PreviewedLink, "sans attendre");
            UiThread.Until(() => viewer.LinkPopup?.IsOpen == true);

            viewer.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(viewer)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

            Assert.IsFalse(viewer.LinkPopup!.IsOpen);
            Assert.IsFalse(page.LinkPreview.IsOpen);

            viewer.FocusReached(link);
            UiThread.Until(() => viewer.LinkPopup.IsOpen);
            viewer.FocusLeft(viewer);
            Assert.IsTrue(viewer.LinkPopup.IsOpen, "un autre élément perd le focus : rien ne change pour le lien");
            viewer.FocusLeft(link);
            UiThread.Until(() => !viewer.LinkPopup.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_pointer_that_only_passes_reads_nothing()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Voir [la cible](Cible.md) et du texte.\n", passage: false);
        Target(vault, "Le texte.\n");
        var (window, viewer, _) = Open(vault);
        try
        {
            var reads = 0;
            var counted = new LinkPreviewViewModel(_ => { reads++; return Outcome.Of<SheetPreview?>(null); }, _ => { });
            viewer.LinkPreview = counted;
            viewer.PointAt(Link(viewer));
            viewer.PointAt(viewer);

            WaitFor(LinkHover.ShowDelay + LinkHover.ShowDelay);

            Assert.AreEqual(0, reads, "la lecture ne part qu'après le délai, si le pointeur est resté");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Leaving_during_the_read_cancels_it_and_a_late_answer_shows_nothing()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Voir [la cible](Cible.md) et du texte.\n", passage: false);
        Target(vault, "Le texte.\n");
        var (window, viewer, _) = Open(vault);
        var answer = new ManualResetEventSlim();
        var started = new ManualResetEventSlim();
        try
        {
            var slow = new LinkPreviewViewModel(
                _ =>
                {
                    started.Set();
                    answer.Wait();
                    return Outcome.Of<SheetPreview?>(new SheetPreview("f_1", "Cible", string.Empty, [], false, "Texte.", null, null));
                },
                _ => { });
            viewer.LinkPreview = slow;
            var link = Link(viewer);
            viewer.FocusReached(link);
            UiThread.Until(() => started.IsSet);

            viewer.PointAt(link);
            viewer.PointAt(viewer);
            Assert.IsNull(viewer.PreviewedLink, "le pointeur parti, la lecture est annulée");
            answer.Set();
            WaitFor(LinkHover.HideDelay);

            Assert.IsFalse(slow.IsOpen);
            Assert.IsNull(viewer.LinkPopup, "la réponse arrive trop tard pour être montrée");
        }
        finally
        {
            answer.Set();
            window.Close();
            answer.Dispose();
            started.Dispose();
        }
    }

    [STATestMethod]
    public void Moving_to_another_link_replaces_the_preview_and_coming_back_keeps_it()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("[Une](Cible.md) et [Deux](Autre.md).\n", passage: false);
        Target(vault, "Le texte.\n");
        var other = vault.App.Sheets.Create("Biologie", "Autre", null).Value;
        Assert.IsTrue(vault.App.Reviews.Archive(other).Succeeded);
        var (window, viewer, page) = Open(vault);
        try
        {
            var links = LogicalDescendants(viewer.Document).OfType<Hyperlink>().ToList();
            viewer.FocusReached(links[0]);
            UiThread.Until(() => viewer.LinkPopup?.IsOpen == true && page.LinkPreview.Title == "Cible");

            viewer.FocusReached(links[0]);
            Assert.AreSame(links[0], viewer.PreviewedLink, "le même lien : rien ne se relit");

            viewer.FocusReached(links[1]);
            UiThread.Until(() => page.LinkPreview.Title == "Autre");

            Assert.IsTrue(viewer.LinkPopup!.IsOpen);
            Assert.IsTrue(page.LinkPreview.IsArchived);
            Assert.IsFalse(page.LinkPreview.HasStep, "une fiche archivée n'attend pas d'étape");
            Assert.AreEqual(string.Empty, page.LinkPreview.Step);
            Assert.IsTrue(page.LinkPreview.IsEmpty, "elle n'a pas encore de texte");
            Assert.AreEqual(ReadingText.PreviewEmpty, page.LinkPreview.Excerpt);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_pointer_can_reach_the_preview_and_its_click_opens_the_sheet()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Voir [la cible](Cible.md) et du texte.\n", passage: false);
        var target = Target(vault, "Le texte.\n");
        var (window, viewer, page) = Open(vault);
        try
        {
            var link = Link(viewer);
            viewer.FocusReached(link);
            UiThread.Until(() => viewer.LinkPopup?.IsOpen == true);
            var card = (LinkPreviewCard)viewer.LinkPopup!.Child;

            viewer.PointAt(viewer);
            viewer.PointAt(link);
            viewer.PointAt(viewer);
            Assert.IsTrue(viewer.IsHidingPreview, "le pointeur a quitté le lien : l'aperçu va partir");
            card.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            Assert.IsFalse(viewer.IsHidingPreview, "le pointeur est dans l'aperçu : il reste");

            card.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
            Assert.IsTrue(viewer.IsHidingPreview, "le pointeur a quitté l'aperçu");
            UiThread.Until(() => !viewer.LinkPopup.IsOpen);

            viewer.FocusReached(link);
            UiThread.Until(() => viewer.LinkPopup.IsOpen);
            card.OpenButton.Command.Execute(null);

            Assert.AreEqual(target, vault.OpenedSheets.Single());
            Assert.IsFalse(viewer.LinkPopup.IsOpen);
            Assert.IsFalse(page.LinkPreview.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Nothing_shows_for_a_missing_sheet_a_web_link_or_a_page_without_preview()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("[absente](Absente.md) et [web](https://exemple.org) et [ancre](#fn).\n", passage: false);
        var (window, viewer, page) = Open(vault);
        try
        {
            var links = LogicalDescendants(viewer.Document).OfType<Hyperlink>().ToList();
            viewer.FocusReached(links[1]);
            Assert.IsNull(viewer.PreviewedLink, "une adresse web n'a pas d'aperçu");
            viewer.FocusReached(links[2]);
            Assert.IsNull(viewer.PreviewedLink);

            viewer.FocusReached(links[0]);
            UiThread.Until(() => viewer.PreviewedLink is null);
            Assert.IsNull(viewer.LinkPopup);
            Assert.IsFalse(page.LinkPreview.IsOpen);

            viewer.LinkPreview = null;
            viewer.PointAt(links[0]);
            viewer.FocusReached(links[0]);
            viewer.FocusLeft(links[0]);
            Assert.IsNull(viewer.PreviewedLink, "sans aperçu branché (rendu, mini-aperçu), un lien reste un lien");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Scrolling_clicking_or_changing_sheet_closes_the_preview()
    {
        using var ui = new UiThread();
        var body = "Voir [la cible](Cible.md).\n\n" + string.Concat(Enumerable.Repeat("Un paragraphe assez long pour remplir la page.\n\n", 200));
        using var vault = new ReadingVault(body, passage: false);
        Target(vault, "Le texte.\n");
        var (window, viewer, page) = Open(vault);
        try
        {
            // La suite du texte s'ajoute peu à peu : elle ne doit pas bouger l'aperçu pendant l'essai.
            UiThread.Until(() => !viewer.HasPendingWork);
            TestShell.Flush();
            var link = Link(viewer);
            viewer.FocusReached(link);
            UiThread.Until(() => viewer.LinkPopup?.IsOpen == true);
            Descendants(viewer).OfType<ScrollViewer>().First().ScrollToVerticalOffset(200);
            UiThread.Until(() => !viewer.LinkPopup!.IsOpen);

            viewer.FocusReached(link);
            UiThread.Until(() => viewer.LinkPopup!.IsOpen);
            viewer.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            Assert.IsFalse(viewer.LinkPopup!.IsOpen);

            viewer.FocusReached(link);
            UiThread.Until(() => viewer.LinkPopup.IsOpen);
            page.StartWritingCommand.Execute(null);
            UiThread.Until(() => !viewer.LinkPopup.IsOpen);
            Assert.IsFalse(page.LinkPreview.IsOpen, "la vue passe à l'écriture : l'aperçu n'a plus de lien où se poser");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Reaching_a_link_below_the_fold_by_the_keyboard_shows_its_preview_under_it()
    {
        using var ui = new UiThread();
        var filler = string.Concat(Enumerable.Repeat("Un paragraphe assez long pour remplir la page.\n\n", 100));
        using var vault = new ReadingVault($"{filler}Voir [la cible](Cible.md).\n\n{filler}", passage: false);
        Target(vault, "Le texte.\n");
        var (window, viewer, _) = Open(vault);
        try
        {
            UiThread.Until(() => !viewer.HasPendingWork);
            TestShell.Flush();
            var link = Link(viewer);
            var scroller = Descendants(viewer).OfType<ScrollViewer>().First();
            Assert.AreEqual(0, scroller.VerticalOffset);

            Assert.IsTrue(link.Focus());
            UiThread.Until(() => scroller.VerticalOffset > 0 && viewer.LinkPopup?.IsOpen == true);
            TestShell.Flush();

            var popup = viewer.LinkPopup!;
            Assert.IsTrue(popup.IsOpen, "le défilement dû au focus ne referme pas l'aperçu");
            Assert.AreSame(link, viewer.PreviewedLink);
            Assert.AreEqual(link.ContentStart.GetCharacterRect(LogicalDirection.Forward), popup.PlacementRectangle, "posé sous le lien, à sa nouvelle place");

            // Le focus a amené le lien en haut de la vue : en remontant un peu, il se voit encore.
            var before = popup.PlacementRectangle;
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset - 40);
            UiThread.Until(() => popup.PlacementRectangle != before);
            Assert.IsTrue(popup.IsOpen, "le lien se voit encore : l'aperçu le suit");
            Assert.AreEqual(link.ContentStart.GetCharacterRect(LogicalDirection.Forward), popup.PlacementRectangle);

            scroller.ScrollToVerticalOffset(0);
            UiThread.Until(() => !popup.IsOpen);
            Assert.IsTrue(link.IsKeyboardFocused, "le focus reste ; le lien n'est plus à l'écran, l'aperçu si");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_link_under_a_hidden_hole_shows_no_preview_until_it_is_revealed()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Le **[Cycle de Krebs](Cible.md)** et [une autre](Cible.md).\n", passage: false);
        Target(vault, "Le texte.\n");
        var test = new TestShell();
        var page = vault.Page(revising: true, activeRecall: () => true);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var window = Show(test, page, 900, 700);
        try
        {
            var viewer = Named<SheetDocumentViewer>(window, "SheetText");
            UiThread.Until(() => viewer.Document is { } document && LogicalDescendants(document).OfType<Hyperlink>().Count() == 2 && page.Holes.HasHoles);
            var links = LogicalDescendants(viewer.Document).OfType<Hyperlink>().ToList();
            var run = LogicalDescendants(links[0]).OfType<Run>().Single();

            viewer.PointAt(run);
            WaitFor(LinkHover.ShowDelay + LinkHover.ShowDelay);
            Assert.IsNull(viewer.PreviewedLink, "l'aperçu donnerait la réponse");
            viewer.PointAt(viewer);
            viewer.FocusReached(links[0]);
            Assert.IsNull(viewer.PreviewedLink, "ni au pointeur, ni au clavier");
            Assert.IsNull(viewer.LinkPopup);
            Assert.AreEqual(string.Empty, AutomationProperties.GetHelpText(links[0]));

            viewer.FocusReached(links[1]);
            UiThread.Until(() => viewer.LinkPopup?.IsOpen == true);
            viewer.FocusLeft(links[1]);
            UiThread.Until(() => !viewer.LinkPopup!.IsOpen);

            Assert.IsTrue(viewer.RevealHoleAt(run));
            viewer.FocusReached(links[0]);
            UiThread.Until(() => viewer.LinkPopup!.IsOpen);
            Assert.AreSame(links[0], viewer.PreviewedLink, "le trou découvert, le lien a son aperçu");

            page.Holes.HideAllCommand.Execute(null);
            Assert.IsNull(viewer.PreviewedLink, "remasqué, l'aperçu part avec lui");
            Assert.IsFalse(viewer.LinkPopup!.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Closing_the_page_leaves_nothing_of_the_preview_on_screen()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Voir [la cible](Cible.md) et du texte.\n", passage: false);
        Target(vault, "Le texte.\n");
        var (window, viewer, page) = Open(vault);
        var link = Link(viewer);
        viewer.FocusReached(link);
        UiThread.Until(() => viewer.LinkPopup?.IsOpen == true);

        window.Close();
        TestShell.Flush();

        Assert.IsFalse(viewer.LinkPopup!.IsOpen);
        Assert.IsFalse(page.LinkPreview.IsOpen);
        Assert.IsNull(page.LinkPreview.Shown, "rien de la fiche liée ne reste en mémoire dans l'aperçu");
    }

    [STATestMethod]
    public void The_window_moving_away_closes_the_preview()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Voir [la cible](Cible.md) et du texte.\n", passage: false);
        Target(vault, "Le texte.\n");
        var (window, viewer, _) = Open(vault);
        try
        {
            viewer.FocusReached(Link(viewer));
            UiThread.Until(() => viewer.LinkPopup?.IsOpen == true);

            window.Left -= 40;
            TestShell.Flush();

            Assert.IsFalse(viewer.LinkPopup!.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public async Task Escape_closes_the_preview_while_the_focus_is_elsewhere_and_a_newer_request_replaces_an_older_one()
    {
        using var vault = new ReadingVault("Le texte.\n", passage: false);
        var page = vault.Page();
        var seen = new List<string>();
        var preview = new LinkPreviewViewModel(
            path =>
            {
                seen.Add(path);
                return Outcome.Of<SheetPreview?>(new SheetPreview("f_1", path, "A/B", [], false, "Texte.", "J3", null));
            },
            _ => { });

        Assert.IsTrue(await preview.ShowAsync("a"));
        Assert.AreEqual("A › B", preview.Folder);
        Assert.AreEqual("J3", preview.Step, "sans échéance connue, l'étape seule");
        Assert.IsTrue(await preview.ShowAsync("b"));
        Assert.AreEqual("b", preview.Title);
        Assert.AreEqual("a|b", string.Join('|', seen));

        var first = preview.ShowAsync("c");
        var second = preview.ShowAsync("d");
        Assert.IsFalse(await first, "la plus récente l'emporte");
        Assert.IsTrue(await second);

        preview.Hide();
        Assert.IsFalse(preview.IsOpen);
        Assert.AreEqual(string.Empty, preview.Title);
        Assert.AreEqual(string.Empty, preview.Tags);

        await page.LoadCommand.ExecuteAsync(null);
        Assert.IsTrue(await page.LinkPreview.ShowAsync("fiches/Biologie/Mitose.md"));
        page.EscapeCommand.Execute(null);
        Assert.IsFalse(page.LinkPreview.IsOpen);
    }

    [TestMethod]
    public async Task A_refused_read_shows_no_preview()
    {
        var preview = new LinkPreviewViewModel(_ => Outcome.Refused<SheetPreview?>("non"), _ => { });

        Assert.IsTrue(await preview.ShowAsync("a"));

        Assert.IsFalse(preview.IsOpen);
        preview.OpenCommand.Execute(null);
    }

    [TestMethod]
    public void An_excerpt_without_parts_stays_empty()
    {
        var thread = new Thread(() =>
        {
            var excerpt = new ExcerptText { Parts = [new ExcerptPart("a", false)] };
            Assert.HasCount(1, excerpt.Inlines);
            excerpt.Parts = null;
            Assert.IsEmpty(excerpt.Inlines);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private static string Source(ReadingVault vault, string folder, string title, string body)
    {
        var id = vault.App.Sheets.Create(folder, title, null).Value;
        Assert.IsTrue(vault.App.Editing.Save(id, body, vault.App.Sheets.Open(id).Value.Fingerprint, 1).Succeeded);
        return id;
    }

    private static string Target(ReadingVault vault, string body) => Source(vault, "Biologie", "Cible", body);

    private static (ShellWindow Window, SheetDocumentViewer Viewer, SheetPageViewModel Page) Open(ReadingVault vault)
    {
        var test = new TestShell();
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var window = Show(test, page, 900, 700);
        var viewer = Named<SheetDocumentViewer>(window, "SheetText");
        UiThread.Until(() => viewer.Document is { } document && LogicalDescendants(document).OfType<Hyperlink>().Any());
        return (window, viewer, page);
    }

    private static Hyperlink Link(SheetDocumentViewer viewer) => LogicalDescendants(viewer.Document).OfType<Hyperlink>().First();

    private static void WaitFor(TimeSpan duration)
    {
        var deadline = DateTime.UtcNow + duration;
        UiThread.Until(() => DateTime.UtcNow >= deadline);
    }

    private static ShellWindow Show(TestShell test, object page, double width, double height)
    {
        var shell = test.Shell;
        shell.Navigate(new NavigationItem("revision", "Révision", KyfRev.DesignSystem.Controls.Icons.Document, int.MaxValue, page));
        var window = new ShellWindow(shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = width, Height = height };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        return window;
    }

    private static T Named<T>(DependencyObject root, string name)
        where T : FrameworkElement => Descendants(root).OfType<T>().First(element => element.Name == name);

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

    private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in LogicalDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
