using System.Collections.Immutable;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Fiches » : ses filtres, la création d'une fiche sur un vrai coffre, et sa page.</summary>
[TestClass]
public sealed class SheetsTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);

    private static readonly string[] NewFolderLabels = ["À la racine", "Histoire", "Nouveau dossier…"];

    private static readonly string[] AllTagged = ["Quatre", "Un", "Deux", "Trois"];

    private static readonly string[] Chips = ["#bio 2", "#maths 1", "#partiel 1"];

    private static readonly string[] BothBio = ["Deux", "Un"];

    private static readonly string[] OnlyOne = ["Un"];

    private static readonly string[] Vanished = ["#vieux 1", "#maths 0"];

    [TestMethod]
    public void A_row_says_where_the_sheet_stands()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show(
        [
            Sheet("En retard", "Sciences/Biologie", step: "J3", due: Day.AddDays(-2), tags: ["bio", "partiel"]),
            Sheet("Du jour", "", step: "J1", due: Day),
            Sheet("À venir", "", step: "J7", due: new DateOnly(2026, 10, 9)),
            Sheet("Archivée", "", archived: true),
            Sheet("Absente", "", step: "J1", due: Day.AddDays(1), present: false),
        ]);

        Assert.AreEqual("5 fiches · 4 en révision · 1 archivée", page.Summary);
        var late = Row(page, "En retard");
        Assert.AreEqual("J3 · 2 j de retard", late.Meta, "le dossier est dans l'en-tête");
        Assert.IsTrue(late.IsLate);
        Assert.AreEqual("Sciences › Biologie · #bio #partiel · J3 · 2 j de retard", late.Details);
        Assert.AreEqual("J1 · aujourd'hui", Row(page, "Du jour").Meta);
        Assert.IsFalse(Row(page, "Du jour").IsLate);
        Assert.AreEqual("J7 · ven. 9 oct.", Row(page, "À venir").Meta);
        Assert.AreEqual("Archivée", Row(page, "Archivée").Meta);
        Assert.IsFalse(Row(page, "Archivée").IsLate);
        Assert.IsTrue(Row(page, "Absente").FileMissing);
        StringAssert.Contains(Row(page, "Absente").ToString(), "fichier introuvable", StringComparison.Ordinal);
        Assert.AreEqual("Fiches", SheetsPageViewModel.Title);
    }

    [TestMethod]
    public void The_summary_agrees_with_the_number_of_sheets()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());

        page.Show([Sheet("Seule", "")]);
        Assert.AreEqual("1 fiche · 1 en révision · 0 archivée", page.Summary);
        page.Show([Sheet("Une", "", archived: true), Sheet("Deux", "", archived: true), Sheet("Trois", "")]);
        Assert.AreEqual("3 fiches · 1 en révision · 2 archivées", page.Summary);
    }

    [TestMethod]
    public void By_folder_the_list_has_a_header_for_each_folder()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show([Sheet("À la racine", ""), Sheet("Atomes", "Sciences/Chimie"), Sheet("Mitose", "Sciences/Chimie")]);

        Assert.HasCount(6, page.Rows);
        Assert.AreEqual("Sans dossier · 1", page.Rows[0].ToString());
        Assert.AreEqual("Sciences · 2", page.Rows[2].ToString());
        Assert.AreEqual("Chimie · 2", page.Rows[3].ToString());

        // Les autres tris sont à plat : chaque ligne dit son dossier.
        var sort = page.Bar.Filters[2];
        sort.Selected = sort.Choices[1];
        Assert.HasCount(3, page.Rows);
        Assert.AreEqual("Sciences › Chimie · J1 · ven. 2 oct.", Row(page, "Atomes").Meta);
    }

    [TestMethod]
    public void The_sorts_go_by_title_or_by_due_date()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show(
        [
            Sheet("Révolution", "", step: "J1", due: Day.AddDays(3)),
            Sheet("Écologie", "", step: "J1", due: Day.AddDays(1)),
            Sheet("Archivée", "", archived: true),
            Sheet("Mitose", "", step: "J1", due: Day.AddDays(3)),
        ]);
        var sort = page.Bar.Filters[2];

        sort.Selected = sort.Choices[1];
        ExpectTitles(page, "Archivée", "Écologie", "Mitose", "Révolution");
        sort.Selected = sort.Choices[2];
        ExpectTitles(page, "Écologie", "Mitose", "Révolution", "Archivée");
        Assert.IsEmpty(page.Bar.ActiveFilters, "un tri ne retire aucune fiche");
    }

    [TestMethod]
    public void The_filters_keep_a_folder_a_state_or_a_word()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        ImmutableArray<VaultSheet> sheets =
        [
            Sheet("Écologie", "Sciences/Biologie", tags: ["bio"], step: "J1", due: Day),
            Sheet("Atomes", "Sciences", step: "J3", due: Day.AddDays(4)),
            Sheet("Révolution", "Histoire", tags: ["partiel"], archived: true),
            Sheet("Sciences po", "Sciences po", step: "J1", due: Day.AddDays(-1)),
        ];
        page.Show(sheets);
        var folder = page.Bar.Filters[0];
        var state = page.Bar.Filters[1];
        Assert.AreEqual("Tous", folder.Choices[0].Label);
        Assert.HasCount(5, folder.Choices);

        folder.Selected = folder.Choices.Single(choice => choice.Label == "Sciences");
        ExpectSheetsInAnyOrder(page, "Écologie", "Atomes");
        page.Show(sheets);
        Assert.AreEqual("Sciences", folder.Selected.Label, "une nouvelle lecture garde le dossier");
        folder.ClearCommand.Execute(null);

        state.Selected = state.Choices[1];
        ExpectSheetsInAnyOrder(page, "Écologie", "Atomes", "Sciences po");
        state.Selected = state.Choices[2];
        ExpectSheetsInAnyOrder(page, "Écologie", "Sciences po");
        state.Selected = state.Choices[3];
        ExpectSheetsInAnyOrder(page, "Révolution");
        state.ClearCommand.Execute(null);

        page.Bar.Search = " ECOLO ";
        ExpectSheetsInAnyOrder(page, "Écologie");
        page.Bar.Search = "partiel";
        ExpectSheetsInAnyOrder(page, "Révolution");
        page.Bar.Search = "introuvable";
        Assert.IsFalse(page.HasRows);
        Assert.IsTrue(page.HasNoMatch);
        Assert.IsFalse(page.IsEmpty);
    }

    [TestMethod]
    public async Task With_room_the_chosen_sheet_is_previewed_and_kept()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        using var vaults = new TestVaults(AppMode.Development, time);
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        var atoms = vaults.App.Sheets.Create("Sciences", "Atomes", null).Value;
        var mitosis = vaults.App.Sheets.Create("Sciences", "Mitose", null).Value;
        var page = Page(vaults, new FakeOpener(), time);

        page.OnNavigatedTo();
        await page.RefreshCommand.ExecutionTask!;
        Assert.IsTrue(page.HasSheets);
        Assert.AreEqual(atoms, ((SheetRow)page.SelectedRow!).Sheet.Id, "la première fiche est choisie");
        await page.LoadPreviewCommand.ExecutionTask!;
        Assert.AreEqual("Atomes", page.Preview!.Title);
        Assert.AreEqual("Ouvrir la fiche", page.Preview.OpenLabel);
        Assert.IsTrue(page.ShowsPreview);

        page.SelectedRow = Row(page, "Mitose");
        await page.LoadPreviewCommand.ExecutionTask!;
        Assert.AreEqual("Mitose", page.Preview!.Title);
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual(mitosis, ((SheetRow)page.SelectedRow!).Sheet.Id, "une nouvelle lecture garde le choix");
        await page.LoadPreviewCommand.ExecutionTask!;

        // Sans la place, rien n'est choisi.
        page.PageWidth = SheetsPageViewModel.PreviewMinimum - 1;
        Assert.IsFalse(page.HasPreview);
        Assert.IsFalse(page.ShowsPreview);
        Assert.IsNull(page.SelectedRow);
        await page.LoadPreviewCommand.ExecutionTask!;
        Assert.IsNull(page.Preview);
        page.PageWidth = SheetsPageViewModel.RoomMinimum - 1;
        Assert.AreEqual("2 fiches", page.Summary);
        Assert.IsFalse(page.Bar.HasRoom);

        // Une fiche disparue : l'aperçu le dit.
        page.PageWidth = double.PositiveInfinity;
        await page.LoadPreviewCommand.ExecutionTask!;
        page.SelectedRow = new SheetRow(Sheet("Disparue", ""), Day, showFolder: false);
        await page.LoadPreviewCommand.ExecutionTask!;
        Assert.IsTrue(page.Preview!.HasError);
        Assert.AreEqual("Disparue", page.Preview.Title);
    }

    [TestMethod]
    public void Only_a_sheet_opens()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var opener = new FakeOpener();
        var page = Page(vaults, opener);
        page.Show([Sheet("Mitose", "Biologie")]);

        page.OpenCommand.Execute(page.Rows[0]);
        Assert.IsEmpty(opener.Opened, "un en-tête n'ouvre rien");
        page.OpenCommand.Execute(page.Rows.OfType<SheetRow>().Single());
        CollectionAssert.AreEqual(new object[] { "f_Mitose" }, opener.Opened);
    }

    [TestMethod]
    public async Task Reading_without_a_vault_shows_the_refusal()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());

        await page.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual(VaultHost.NoVaultOpen, page.Error);
        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.IsLoading);
        Assert.IsFalse(page.IsEmpty, "on ne dit pas qu'il n'y a rien : la lecture a échoué");
        Assert.AreEqual(string.Empty, page.Summary);

        await page.RequestCreateCommand.ExecuteAsync(null);
        Assert.IsFalse(page.IsCreating, "sans coffre, pas de création");
        Assert.AreEqual(VaultHost.NoVaultOpen, page.Error);
    }

    [TestMethod]
    public async Task A_new_sheet_is_created_in_the_chosen_folder_and_opens()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        using var vaults = new TestVaults(AppMode.Development, time);
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        Assert.IsTrue(vaults.App.Folders.Create("Histoire").Succeeded);
        var opener = new FakeOpener();
        var page = Page(vaults, opener, time);
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsTrue(page.IsEmpty);
        Assert.IsFalse(page.HasSheets);

        await page.RequestCreateCommand.ExecuteAsync(null);
        Assert.IsTrue(page.IsCreating);
        Assert.AreEqual("À la racine", page.NewFolder.Selected.Label);
        CollectionAssert.AreEqual(NewFolderLabels, page.NewFolder.Choices.Select(choice => choice.Label).ToArray());
        Assert.AreEqual("Fiche vide", page.NewTemplate.Selected.Label);

        // Sans titre : la fenêtre reste ouverte et dit pourquoi.
        page.NewTitle = "   ";
        await page.AnswerCreateCommand.ExecuteAsync(true);
        Assert.IsTrue(page.IsCreating);
        Assert.AreEqual("Donnez un titre à la fiche.", page.CreateError);
        Assert.IsTrue(page.HasCreateError);
        Assert.IsEmpty(opener.Opened);

        page.NewTitle = " La Révolution ";
        page.NewFolder.Selected = page.NewFolder.Choices[1];
        await page.AnswerCreateCommand.ExecuteAsync(true);
        Assert.IsFalse(page.IsCreating);
        Assert.IsNull(page.CreateError);
        var created = vaults.App.Sheets.List().Value.Single();
        Assert.AreEqual("La Révolution", created.Title);
        Assert.AreEqual("Histoire", created.Folder);
        CollectionAssert.AreEqual(new object[] { created.Id }, opener.Opened);

        // Un nouveau dossier, écrit à la main.
        await page.RefreshCommand.ExecuteAsync(null);
        var folder = page.Bar.Filters[0];
        folder.Selected = folder.Choices[1];
        await page.RequestCreateCommand.ExecuteAsync(null);
        Assert.AreEqual("Histoire", page.NewFolder.Selected.Label, "le dossier filtré est proposé");
        Assert.IsFalse(page.IsNewFolder);
        page.NewFolder.Selected = page.NewFolder.Choices[^1];
        Assert.IsTrue(page.IsNewFolder);
        page.NewFolderName = " Sciences/Chimie ";
        page.NewTitle = "Atomes";
        await page.AnswerCreateCommand.ExecuteAsync(true);
        Assert.AreEqual("Sciences/Chimie", vaults.App.Sheets.List().Value.Single(sheet => sheet.Title == "Atomes").Folder);

        // Renoncer ne crée rien ; un refus du coffre laisse la fenêtre ouverte.
        await page.RequestCreateCommand.ExecuteAsync(null);
        page.NewTitle = "Rien";
        await page.AnswerCreateCommand.ExecuteAsync(false);
        Assert.IsFalse(page.IsCreating);
        Assert.HasCount(2, vaults.App.Sheets.List().Value);
        await page.RequestCreateCommand.ExecuteAsync(null);
        page.NewFolder.Selected = page.NewFolder.Choices[^1];
        page.NewFolderName = "../hors du coffre";
        page.NewTitle = "Refusée";
        await page.AnswerCreateCommand.ExecuteAsync(true);
        Assert.IsTrue(page.IsCreating);
        Assert.IsTrue(page.HasCreateError);
    }

    [TestMethod]
    public async Task A_template_fills_the_new_sheet()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        using var vaults = new TestVaults(AppMode.Development, time);
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        var page = Page(vaults, new FakeOpener(), time);
        await page.RequestCreateCommand.ExecuteAsync(null);
        Assert.IsGreaterThan(1, page.NewTemplate.Choices.Count, "les modèles intégrés sont proposés");

        page.NewTemplate.Selected = page.NewTemplate.Choices[1];
        page.NewTitle = "D'après un modèle";
        await page.AnswerCreateCommand.ExecuteAsync(true);

        var created = vaults.App.Sheets.List().Value.Single();
        Assert.IsFalse(string.IsNullOrWhiteSpace(vaults.App.Sheets.Open(created.Id).Value.Body));
    }

    [TestMethod]
    public void The_arguments_are_required()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var app = vaults.App;
        var time = TimeProvider.System;
        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetsPageViewModel(null!, app.Folders, app.Templates, new FakeOpener(), time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetsPageViewModel(app.Sheets, null!, app.Templates, new FakeOpener(), time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetsPageViewModel(app.Sheets, app.Folders, null!, new FakeOpener(), time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetsPageViewModel(app.Sheets, app.Folders, app.Templates, null!, time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetsPageViewModel(app.Sheets, app.Folders, app.Templates, new FakeOpener(), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Page(vaults, new FakeOpener()).Show(null!));
    }

    [STATestMethod]
    public void The_page_chooses_with_room_and_opens_without()
    {
        using var ui = new UiThread();
        var opener = new FakeOpener();
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, opener);
        page.Show([Sheet("Atomes", "Sciences"), Sheet("Mitose", "Sciences")]);
        var test = new TestShell();
        var window = Show(test, page);
        try
        {
            page.PageWidth = SheetsPageViewModel.PreviewMinimum;
            TestShell.Flush();
            Assert.AreEqual("Fiches", Named<TextBlock>(window, "Heading").Text);
            var list = Named<OpenableList>(window, "Items");
            Assert.IsTrue(Named<Card>(window, "PreviewCard").IsVisible);
            Assert.AreEqual(360, Named<Card>(window, "ListCard").ActualWidth, 1);

            // Un en-tête ne se choisit pas : le choix précédent reste.
            var chosen = page.SelectedRow;
            Assert.IsNotNull(chosen);
            list.SelectedItem = page.Rows[0];
            TestShell.Flush();
            Assert.AreSame(chosen, page.SelectedRow);

            // Avec l'aperçu, un clic choisit et un double clic ouvre.
            var second = Row(page, "Mitose");
            list.SelectedItem = second;
            TestShell.Flush();
            Assert.AreSame(second, page.SelectedRow);
            list.ScrollIntoView(second);
            window.UpdateLayout();
            var container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(second);
            container.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseUpEvent });
            Assert.IsEmpty(opener.Opened);
            list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent });
            CollectionAssert.AreEqual(new object[] { second.Sheet.Id }, opener.Opened);
            opener.Opened.Clear();

            // Entrée ouvre la ligne qui a le focus ; une autre touche ne fait rien.
            Assert.IsTrue(container.Focus());
            list.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(list)!, 0, Key.Space) { RoutedEvent = UIElement.PreviewKeyDownEvent });
            Assert.IsEmpty(opener.Opened);
            list.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(list)!, 0, Key.Enter) { RoutedEvent = UIElement.PreviewKeyDownEvent });
            CollectionAssert.AreEqual(new object[] { second.Sheet.Id }, opener.Opened);
            opener.Opened.Clear();

            // Les lignes s'ouvrent aussi par l'automatisation ; pas un en-tête.
            var peers = UIElementAutomationPeer.CreatePeerForElement(list).GetChildren();
            Assert.IsNull(peers[0].GetPattern(PatternInterface.Invoke));
            ((IInvokeProvider)peers[2].GetPattern(PatternInterface.Invoke)).Invoke();
            UiThread.Until(() => opener.Opened.Count == 1);
            opener.Opened.Clear();

            // Sans l'aperçu, un clic ouvre, et la liste prend la largeur.
            page.PageWidth = SheetsPageViewModel.PreviewMinimum - 1;
            TestShell.Flush();
            window.UpdateLayout();
            Assert.IsFalse(Named<Card>(window, "PreviewCard").IsVisible);
            Assert.IsGreaterThan(360, Named<Card>(window, "ListCard").ActualWidth);
            list.ScrollIntoView(second);
            window.UpdateLayout();
            container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(second);
            container.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.MouseUpEvent });
            Assert.IsEmpty(opener.Opened);
            container.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseUpEvent });
            CollectionAssert.AreEqual(new object[] { second.Sheet.Id }, opener.Opened);
            opener.Opened.Clear();
            list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent });
            Assert.IsEmpty(opener.Opened, "le double clic n'ouvre pas une seconde fois");

            // La page quittée, avec une ligne choisie, perd son contexte sans rien lever.
            list.SelectedItem = second;
            test.Shell.Navigate(test.Shell.Gallery);
            TestShell.Flush();
            Assert.AreSame(test.Shell.Gallery, test.Shell.Current);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_new_sheet_window_answers_through_its_buttons()
    {
        using var ui = new UiThread();
        var time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        var opener = new FakeOpener();
        using var vaults = new TestVaults(AppMode.Development, time);
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        var page = Page(vaults, opener, time);
        var test = new TestShell();
        var window = Show(test, page);
        try
        {
            page.RefreshCommand.Execute(null);
            UiThread.Until(() => page.IsEmpty);
            TestShell.Flush();
            Assert.IsTrue(Named<EmptyState>(window, "Empty").IsVisible);
            Assert.IsFalse(Named<FilterBar>(window, "FilterBar").IsVisible);
            Assert.IsFalse(Named<Card>(window, "ListCard").IsVisible);

            Named<Button>(window, "NewSheetButton").Command.Execute(null);
            UiThread.Until(() => page.IsCreating);
            TestShell.Flush();
            var dialog = Named<Dialog>(window, "NewSheet");
            Assert.IsTrue(dialog.IsOpen);
            Assert.IsFalse(Named<TextBox>(window, "NewFolderBox").IsVisible);

            // Un refus rouvre la fenêtre, avec sa raison.
            dialog.Close(DialogChoice.Primary);
            UiThread.Until(() => page.HasCreateError);
            TestShell.Flush();
            Assert.IsTrue(dialog.IsOpen);
            Assert.IsTrue(Named<Notification>(window, "CreateError").IsVisible);

            dialog.Close(DialogChoice.Secondary);
            UiThread.Until(() => !page.HasCreateError);
            TestShell.Flush();
            Assert.IsFalse(dialog.IsOpen);

            page.RequestCreateCommand.Execute(null);
            UiThread.Until(() => page.IsCreating);
            page.NewTitle = "Mitose";
            dialog.Close(DialogChoice.Primary);
            UiThread.Until(() => opener.Opened.Count == 1);
            TestShell.Flush();
            Assert.IsFalse(dialog.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void No_match_is_said_without_losing_the_filters()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show([Sheet("Mitose", "Biologie")]);
        var window = Show(new TestShell(), page);
        try
        {
            page.Bar.Search = "introuvable";
            TestShell.Flush();
            Assert.IsTrue(Named<TextBlock>(window, "NoMatch").IsVisible);
            Assert.IsTrue(Named<FilterBar>(window, "FilterBar").IsVisible);
            Assert.IsFalse(Named<Card>(window, "ListCard").IsVisible);
            Assert.IsFalse(Named<Card>(window, "PreviewCard").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void The_tag_filter_counts_each_tag_and_keeps_the_sheets_that_carry_them_all()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show(Tagged());
        CollectionAssert.AreEqual(Chips, page.Tags.Select(chip => chip.Text).ToArray(), "la casse ne les distingue pas");
        var bio = page.Tags[0];
        Assert.AreEqual("#bio, 2 fiches", bio.Name);
        Assert.AreEqual(SheetsText.TagFilterAdd, bio.Hint);
        Assert.IsFalse(page.HasChosenTags);

        bio.IsChosen = true;

        CollectionAssert.AreEquivalent(BothBio, Titles(page));
        Assert.AreSame(bio, page.Tags[0], "la pastille pressée garde le focus");
        Assert.AreEqual(SheetsText.TagFilterRemove, bio.Hint);
        Assert.IsTrue(page.HasChosenTags);

        page.Tags[2].IsChosen = true;
        CollectionAssert.AreEquivalent(OnlyOne, Titles(page), "une fiche doit les porter toutes");

        page.Tags[2].IsChosen = false;
        CollectionAssert.AreEquivalent(BothBio, Titles(page));

        page.ClearTagsCommand.Execute(null);

        Assert.IsFalse(page.HasChosenTags);
        Assert.IsFalse(bio.IsChosen);
        CollectionAssert.AreEquivalent(AllTagged, Titles(page));
    }

    [TestMethod]
    public void A_chosen_tag_stays_when_no_sheet_carries_it_any_more()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show([.. Tagged(), Sheet("Archivée", "", tags: ["vieux"], archived: true)]);
        page.Tags.Single(chip => chip.Label == "maths").IsChosen = true;
        var state = page.Bar.Filters[1];

        state.Selected = state.Choices[3];

        CollectionAssert.AreEqual(Vanished, page.Tags.Select(chip => chip.Text).ToArray());
        Assert.IsEmpty(Titles(page));

        page.ClearTagsCommand.Execute(null);

        Assert.HasCount(1, page.Tags, "sans fiche et sans choix, elle s'en va");
    }

    [TestMethod]
    public void On_a_narrow_window_the_tag_filter_folds_away_unless_it_acts()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show(Tagged());
        Assert.IsTrue(page.ShowsTags);

        page.Bar.HasRoom = false;
        Assert.IsFalse(page.ShowsTags);

        page.Bar.AreOpen = true;
        Assert.IsTrue(page.ShowsTags);
        page.Tags[0].IsChosen = true;
        page.Bar.AreOpen = false;

        Assert.IsTrue(page.ShowsTags, "un filtre qui agit reste visible");

        page.Forget();

        Assert.IsEmpty(page.Tags);
        Assert.IsFalse(page.HasChosenTags);
        Assert.IsFalse(page.ShowsTags);
    }

    private static ImmutableArray<VaultSheet> Tagged() =>
    [
        Sheet("Un", "", tags: ["bio", "partiel"]),
        Sheet("Deux", "", tags: ["Bio"]),
        Sheet("Trois", "", tags: ["maths"]),
        Sheet("Quatre", ""),
    ];

    private static SheetsPageViewModel Page(TestVaults vaults, FakeOpener opener, TimeProvider? time = null) =>
        new(vaults.App.Sheets, vaults.App.Folders, vaults.App.Templates, opener, time ?? new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));

    private static VaultSheet Sheet(
        string title,
        string folder,
        ImmutableArray<string> tags = default,
        string? step = "J1",
        DateOnly? due = null,
        bool archived = false,
        bool present = true) =>
        new(
            $"f_{title}",
            title,
            $"fiches/{title}.md",
            folder,
            tags.IsDefault ? [] : tags,
            archived,
            present,
            archived ? null : step,
            archived ? null : due ?? Day.AddDays(1));

    private static SheetRow Row(SheetsPageViewModel page, string title) => page.Rows.OfType<SheetRow>().Single(row => row.Title == title);

    private static string[] Titles(SheetsPageViewModel page) => [.. page.Rows.OfType<SheetRow>().Select(row => row.Title)];

    private static void ExpectTitles(SheetsPageViewModel page, params string[] expected) => CollectionAssert.AreEqual(expected, Titles(page));

    private static void ExpectSheetsInAnyOrder(SheetsPageViewModel page, params string[] expected) => CollectionAssert.AreEquivalent(expected, Titles(page));

    private static ShellWindow Show(TestShell test, object page)
    {
        var shell = test.Shell;
        shell.Navigate(new NavigationItem("fiches", "Fiches", Icons.Document, int.MaxValue, page));
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
