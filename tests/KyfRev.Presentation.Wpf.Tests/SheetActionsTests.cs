using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Fiches » : l'arborescence, le clic droit, et ce qu'il fait au coffre.</summary>
[TestClass]
public sealed class SheetActionsTests
{
    private static readonly string[] SheetMenu =
    [
        SheetActionsText.Open, SheetActionsText.Review, SheetActionsText.Write,
        SheetActionsText.Rename, SheetActionsText.Tags, SheetActionsText.Move,
        SheetActionsText.Archive, SheetActionsText.Delete,
    ];

    private static readonly string[] FolderMenu =
    [
        SheetActionsText.NewSheet, SheetActionsText.NewFolder, SheetActionsText.Rename, SheetActionsText.Move, SheetActionsText.Delete,
    ];

    private static readonly string[] FreeSpaceMenu = [SheetActionsText.NewSheet, SheetActionsText.NewFolder];

    private static readonly string[] OpeningMenu = [SheetActionsText.Open, SheetActionsText.Review, SheetActionsText.Write];

    private static readonly string[] Tree =
    [
        "Sans dossier · 1", "À la racine",
        "Sciences · 3", "Biologie · 1", "Cellule · 1", "Noyau · 1", "Membrane", "Chimie · 1", "Atomes", "Lois",
        "Vide · 0",
    ];

    private static readonly string[] NewTags = ["bio", "partiel"];

    [TestMethod]
    public async Task The_folders_nest_and_empty_ones_are_shown()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();

        CollectionAssert.AreEqual(Tree, Labels(page));
        var deep = page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences/Biologie/Cellule/Noyau");
        Assert.AreEqual(3, deep.Depth);
        Assert.AreEqual(48, deep.Indent.Left, "chaque niveau décale");
        Assert.IsFalse(deep.IsTooDeep, "un dossier et trois sous-dossiers : c'est la limite tranquille");
        Assert.AreEqual(64, page.Rows.OfType<SheetRow>().Single(row => row.Title == "Membrane").Indent.Left);
    }

    [TestMethod]
    public async Task A_folder_folds_and_unfolds()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();
        var science = page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences");
        Assert.AreEqual(Icons.ChevronDown, science.Chevron);

        page.ToggleFolderCommand.Execute(science);

        Assert.IsFalse(Labels(page).Contains("Atomes"));
        var folded = page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences");
        Assert.AreEqual(Icons.ChevronRight, folded.Chevron);
        Assert.AreEqual("Sciences · 3", folded.ToString(), "il dit encore ce qu'il contient");

        page.ToggleFolderCommand.Execute(folded);
        CollectionAssert.AreEqual(Tree, Labels(page));

        page.ToggleFolderCommand.Execute(page.Rows[0]);
        CollectionAssert.AreEqual(Tree, Labels(page), "« Sans dossier » ne se plie pas");
    }

    [TestMethod]
    public async Task A_filter_hides_the_empty_folders()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();

        page.Bar.Search = "atomes";

        Assert.IsFalse(Labels(page).Contains("Vide · 0"));
        Assert.IsFalse(Labels(page).Contains("Biologie · 1"));
        Assert.IsTrue(Labels(page).Contains("Chimie · 1"));
        Assert.IsTrue(Labels(page).Contains("Sciences · 1"), "les parents restent, pour dire où elle est");
    }

    [STATestMethod]
    public void A_sheet_offers_everything_and_its_review_only_if_it_is_active()
    {
        using var ui = new UiThread();
        using var vault = new SheetsVault();
        var page = vault.Loaded();
        var view = new SheetsPage { DataContext = page };
        var row = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes");

        var items = view.RowMenuItems(row);

        CollectionAssert.AreEqual(SheetMenu, Headers(items));
        Assert.IsTrue(((MenuItem)items[1]).IsEnabled);
        Click(items[0]);
        Click(items[1]);
        Click(items[2]);
        Assert.HasCount(3, vault.Opener.Opened);
        Assert.AreEqual(("réviser", row.Sheet.Id), vault.Opener.Opened[1]);
        Assert.AreEqual(("écrire", row.Sheet.Id), vault.Opener.Opened[2]);

        vault.App.Reviews.Archive(row.Sheet.Id).Value.ToString();
        page = vault.Loaded();
        view.DataContext = page;
        var archived = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes");
        var menu = view.RowMenuItems(archived);
        Assert.IsFalse(((MenuItem)menu[1]).IsEnabled, "une fiche archivée ne se révise pas");
        CollectionAssert.Contains(Headers(menu), SheetActionsText.Restart);

        view.DataContext = null;
        Assert.IsEmpty(view.RowMenuItems(row));
    }

    [STATestMethod]
    public void Extensions_add_their_entries_after_those_of_kyfrev()
    {
        using var ui = new UiThread();
        using var vault = new SheetsVault();
        var page = vault.Loaded();
        var asked = new List<string>();
        var ran = 0;
        page.ExtensionMenus = (zone, context) =>
        {
            asked.Add($"{zone} {string.Join(",", context.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => $"{entry.Key}={entry.Value}"))}");
            return zone == ExtensionMenuZone.Sheet ? [new MenuContribution("Créer un quiz", () => ran++)] : [];
        };
        var view = new SheetsPage { DataContext = page };
        var row = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes");

        var items = view.RowMenuItems(row);
        CollectionAssert.AreEqual(SheetMenu.Append("Créer un quiz").ToList(), Headers(items));
        Assert.IsInstanceOfType<Separator>(items[^2]);
        Click(items[^1]);
        Assert.AreEqual(1, ran);
        Assert.AreEqual($"Sheet dossier={row.Sheet.Folder},id={row.Sheet.Id},titre=Atomes", asked[0]);

        var folder = page.Rows.OfType<SheetFolderRow>().Single(line => line.Path == "Sciences");
        CollectionAssert.AreEqual(FolderMenu, Headers(view.RowMenuItems(folder)));
        Assert.AreEqual("Folder dossier=Sciences", asked[^1]);
        var withExtensions = view.RowMenuItems(folder).Count;
        page.ExtensionMenus = null;
        Assert.AreEqual(view.RowMenuItems(folder).Count, withExtensions, "Rien à ajouter, aucun séparateur de plus.");
    }

    [STATestMethod]
    public void A_folder_and_the_free_space_have_their_menus()
    {
        using var ui = new UiThread();
        using var vault = new SheetsVault();
        var page = vault.Loaded();
        var view = new SheetsPage { DataContext = page };

        var folder = page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences");
        CollectionAssert.AreEqual(FolderMenu, Headers(view.RowMenuItems(folder)));
        CollectionAssert.AreEqual(FreeSpaceMenu, Headers(view.RowMenuItems(null)));
        CollectionAssert.AreEqual(FreeSpaceMenu, Headers(view.RowMenuItems(page.Rows[0])), "« Sans dossier » n'est pas un vrai dossier");

        Click(view.RowMenuItems(folder)[1]);
        Assert.IsTrue(page.IsActing);
        Assert.AreEqual("Nouveau dossier dans « Sciences »", page.ActionTitle);
        page.AnswerActionCommand.Execute(false);

        Click(view.RowMenuItems(null)[1]);
        Assert.AreEqual(SheetActionsText.NewFolderTitle, page.ActionTitle);
        page.AnswerActionCommand.Execute(false);
    }

    [STATestMethod]
    public void Without_its_use_cases_the_view_only_offers_what_opens_and_creates()
    {
        using var ui = new UiThread();
        using var vault = new SheetsVault();
        var page = new SheetsPageViewModel(vault.App.Sheets, vault.App.Folders, vault.App.Templates, vault.Opener, TimeProvider.System);
        page.RefreshCommand.Execute(null);
        UiThread.Until(() => page.Rows.Count > 0);
        var view = new SheetsPage { DataContext = page };

        Assert.IsFalse(page.CanManage);
        CollectionAssert.AreEqual(OpeningMenu, Headers(view.RowMenuItems(page.Rows.OfType<SheetRow>().First())));
        CollectionAssert.AreEqual(FreeSpaceMenu, Headers(view.RowMenuItems(page.Rows.OfType<SheetFolderRow>().First(row => row.IsFolder))));
    }

    [TestMethod]
    public async Task A_sheet_is_renamed_and_a_blank_name_is_refused()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();
        var row = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes");

        page.RequestRename(row);
        Assert.AreEqual(SheetActionsText.RenameTitle, page.ActionTitle);
        Assert.AreEqual(SheetActionsText.TitleLabel, page.ActionInputLabel);
        Assert.AreEqual("Atomes", page.ActionInput);
        Assert.IsTrue(page.HasActionInput);
        Assert.IsFalse(page.CanErase);

        page.ActionInput = "   ";
        await page.AnswerActionCommand.ExecuteAsync(true);
        Assert.IsTrue(page.IsActing, "un refus laisse la fenêtre ouverte");
        Assert.AreEqual(SheetActionsText.NameRequired, page.ActionError);
        Assert.IsTrue(page.HasActionError);

        page.ActionInput = "Les atomes";
        await page.AnswerActionCommand.ExecuteAsync(true);

        Assert.IsFalse(page.IsActing);
        Assert.AreEqual(SheetActionsText.Renamed, page.Done);
        Assert.IsTrue(page.HasDone);
        Assert.AreEqual("Les atomes", vault.App.Sheets.Open(row.Sheet.Id).Value.Title);
    }

    [TestMethod]
    public async Task Cancelling_changes_nothing()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();
        var row = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes");
        page.RequestRename(row);
        page.ActionInput = "Autre";

        await page.AnswerActionCommand.ExecuteAsync(false);

        Assert.IsFalse(page.IsActing);
        Assert.IsFalse(page.HasDone);
        Assert.AreEqual("Atomes", vault.App.Sheets.Open(row.Sheet.Id).Value.Title);
    }

    [TestMethod]
    public async Task The_tags_are_written_as_a_list()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();
        var row = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes");

        page.RequestTags(row);
        Assert.AreEqual("Étiquettes de « Atomes »", page.ActionTitle);
        page.ActionInput = " bio, ,partiel ";
        await page.AnswerActionCommand.ExecuteAsync(true);

        Assert.AreEqual(SheetActionsText.TagsSaved, page.Done);
        CollectionAssert.AreEqual(NewTags, vault.App.Sheets.Open(row.Sheet.Id).Value.Tags.ToArray());
    }

    [TestMethod]
    public async Task Tags_already_used_are_offered_while_typing()
    {
        using var vault = new SheetsVault();
        foreach (var (title, tags) in new[] { ("Membrane", new[] { "bio", "partiel" }), ("Lois", ["bio", "Physique"]), ("À la racine", ["brouillon"]) })
        {
            var id = vault.App.Sheets.List().Value.Single(sheet => sheet.Title == title).Id;
            Assert.IsTrue(vault.App.Sheets.SetTags(id, tags).Succeeded);
        }
        var page = await vault.LoadedAsync();
        var row = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes");
        Assert.IsFalse(page.HasTagSuggestions, "rien hors de la fenêtre des étiquettes");

        page.RequestTags(row);
        Assert.AreEqual("bio, brouillon, partiel, Physique", string.Join(", ", page.TagSuggestions), "la plus utilisée d'abord, puis l'ordre alphabétique");

        page.ActionInput = "B";
        Assert.AreEqual("bio, brouillon", string.Join(", ", page.TagSuggestions), "sans tenir compte de la casse");

        page.TakeTagCommand.Execute("bio");
        Assert.AreEqual("bio, ", page.ActionInput);
        Assert.AreEqual("brouillon, partiel, Physique", string.Join(", ", page.TagSuggestions), "celle qui est écrite n'est plus proposée");

        page.ActionInput = "bio, phy";
        Assert.AreEqual("Physique", page.TagSuggestions.Single());
        page.TakeTagCommand.Execute("Physique");
        Assert.AreEqual("bio, Physique, ", page.ActionInput);

        page.ActionInput = "bio, physique";
        Assert.IsFalse(page.HasTagSuggestions, "entièrement tapée, elle n'a plus à être proposée");

        await page.AnswerActionCommand.ExecuteAsync(false);
        Assert.IsFalse(page.HasTagSuggestions);
        page.RequestRename(row);
        Assert.IsFalse(page.HasTagSuggestions, "le nom d'une fiche n'a pas d'étiquettes à proposer");
    }

    [TestMethod]
    public async Task A_sheet_moves_to_any_folder_and_to_the_root()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();
        var row = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes");

        page.RequestMove(row);
        Assert.IsTrue(page.HasActionDestination);
        Assert.AreEqual("Sciences/Chimie", page.ActionDestination!.Value, "il part de son dossier");
        Assert.AreEqual(SheetsText.AtRoot, page.ActionDestinations[0].Label);
        Assert.IsFalse(page.HasDepthWarning, "une fiche ne fait pas un niveau de plus");
        page.ActionDestination = page.ActionDestinations.Single(choice => choice.Value == "Vide");
        await page.AnswerActionCommand.ExecuteAsync(true);

        Assert.AreEqual(SheetActionsText.Moved, page.Done);
        Assert.AreEqual("Vide", vault.App.Sheets.Open(row.Sheet.Id).Value.Folder);
    }

    [TestMethod]
    public async Task A_sheet_is_archived_then_restarted()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();
        var row = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes");

        page.RequestArchiveOrRestart(row);
        Assert.AreEqual(SheetActionsText.ArchiveTitle, page.ActionTitle);
        Assert.IsTrue(page.HasActionMessage);
        await page.AnswerActionCommand.ExecuteAsync(true);
        Assert.AreEqual(SheetActionsText.Archived, page.Done);

        var archived = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes");
        Assert.IsTrue(archived.Sheet.Archived);
        page.RequestArchiveOrRestart(archived);
        Assert.AreEqual(SheetActionsText.RestartTitle, page.ActionTitle);
        await page.AnswerActionCommand.ExecuteAsync(true);

        Assert.AreEqual(SheetActionsText.Restarted, page.Done);
        Assert.IsFalse(page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes").Sheet.Archived);
    }

    [TestMethod]
    public async Task Deleting_a_sheet_sends_it_to_the_trash_unless_erasing_is_ticked()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();
        var atoms = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes");

        page.RequestTrash(atoms);
        Assert.IsTrue(page.CanErase);
        Assert.IsFalse(page.ErasesForGood, "effacer ne se fait jamais par défaut");
        Assert.AreEqual(SheetActionsText.TrashConfirm, page.ActionConfirm);
        await page.AnswerActionCommand.ExecuteAsync(true);

        Assert.AreEqual(SheetActionsText.Trashed, page.Done);
        Assert.AreEqual(atoms.Sheet.Id, vault.App.Trash.TrashedSheets().Value.Single().Id);

        var laws = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Lois");
        page.RequestTrash(laws);
        page.ErasesForGood = true;
        Assert.AreEqual(SheetActionsText.EraseConfirm, page.ActionConfirm, "le bouton dit ce qui va se passer");
        await page.AnswerActionCommand.ExecuteAsync(true);

        Assert.AreEqual(SheetActionsText.Erased, page.Done);
        Assert.AreEqual(atoms.Sheet.Id, vault.App.Trash.TrashedSheets().Value.Single().Id, "l'autre n'est pas à la corbeille");
        Assert.IsFalse(File.Exists(vault.PathOf("fiches/Lois.md")));
        Assert.IsFalse(vault.App.Sheets.Open(laws.Sheet.Id).Succeeded);
    }

    [TestMethod]
    public async Task A_refused_action_keeps_the_window_open_with_its_reason()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();
        var row = page.Rows.OfType<SheetRow>().Single(line => line.Title == "Atomes");
        vault.App.Trash.TrashSheet(row.Sheet.Id).Value.ToString();

        page.RequestRename(row);
        page.ActionInput = "Autre";
        await page.AnswerActionCommand.ExecuteAsync(true);

        Assert.IsTrue(page.IsActing);
        Assert.IsTrue(page.HasActionError);

        await page.AnswerActionCommand.ExecuteAsync(false);
        Assert.IsFalse(page.HasActionError);
    }

    [TestMethod]
    public async Task A_folder_is_created_renamed_and_moved_and_too_deep_is_only_a_warning()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();
        var deep = page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences/Biologie/Cellule/Noyau");

        page.RequestNewFolder(deep.Path);
        Assert.IsFalse(page.HasDepthWarning, "rien n'est encore écrit");
        page.ActionInput = "Trop loin";
        Assert.IsTrue(page.HasDepthWarning);
        Assert.AreEqual(SheetActionsText.DepthWarningOf(5), page.DepthWarning);
        await page.AnswerActionCommand.ExecuteAsync(true);
        Assert.AreEqual(SheetActionsText.FolderCreated, page.Done, "un avertissement n'empêche rien");
        var created = page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences/Biologie/Cellule/Noyau/Trop loin");
        Assert.IsTrue(created.IsTooDeep);

        page.RequestRenameFolder(page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Vide"));
        Assert.AreEqual("Vide", page.ActionInput);
        page.ActionInput = "Rangement";
        await page.AnswerActionCommand.ExecuteAsync(true);
        Assert.AreEqual(SheetActionsText.FolderRenamed, page.Done);
        CollectionAssert.Contains(vault.App.Folders.Folders().Value.ToList(), "Rangement");

        var chemistry = page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences/Chimie");
        page.RequestMoveFolder(chemistry);
        Assert.AreEqual("Sciences", page.ActionDestination!.Value);
        Assert.IsFalse(page.ActionDestinations.Any(choice => choice.Value == "Sciences/Chimie"), "pas en lui-même");
        page.ActionDestination = page.ActionDestinations.Single(choice => choice.Value == "Rangement");
        await page.AnswerActionCommand.ExecuteAsync(true);
        Assert.AreEqual(SheetActionsText.Moved, page.Done);
        CollectionAssert.Contains(vault.App.Folders.Folders().Value.ToList(), "Rangement/Chimie");

        var rangement = page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Rangement/Chimie");
        page.RequestMoveFolder(rangement);
        page.ActionDestination = page.ActionDestinations.Single(choice => choice.Value == "Sciences/Biologie/Cellule/Noyau");
        Assert.IsTrue(page.HasDepthWarning, "déplacer peut aussi creuser trop");
        await page.AnswerActionCommand.ExecuteAsync(false);
    }

    [TestMethod]
    public async Task A_folder_goes_to_the_trash_or_is_erased_for_good()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();

        page.RequestTrashFolder(page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Vide"));
        Assert.AreEqual(SheetActionsText.TrashEmptyFolderMessageOf("Vide"), page.ActionMessage);
        await page.AnswerActionCommand.ExecuteAsync(true);
        Assert.AreEqual(SheetActionsText.Trashed, page.Done);
        Assert.IsFalse(vault.App.Folders.Folders().Value.Contains("Vide"));

        var chemistry = page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences/Chimie");
        page.RequestTrashFolder(chemistry);
        StringAssert.Contains(page.ActionMessage, "1 fiche", StringComparison.Ordinal);
        await page.AnswerActionCommand.ExecuteAsync(true);
        Assert.HasCount(1, vault.App.Trash.TrashedSheets().Value);

        var biology = page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences/Biologie");
        page.RequestTrashFolder(biology);
        page.ErasesForGood = true;
        await page.AnswerActionCommand.ExecuteAsync(true);

        Assert.AreEqual(SheetActionsText.Erased, page.Done);
        Assert.HasCount(1, vault.App.Trash.TrashedSheets().Value, "le dossier effacé ne passe pas par la corbeille de la liste");
        Assert.IsFalse(Directory.Exists(vault.PathOf("fiches/Sciences/Biologie")));
    }

    [TestMethod]
    public async Task A_new_sheet_starts_in_the_folder_it_was_asked_from()
    {
        using var vault = new SheetsVault();
        var page = await vault.LoadedAsync();

        await page.CreateInAsync("Sciences/Chimie");

        Assert.IsTrue(page.IsCreating);
        Assert.AreEqual("Sciences/Chimie", page.NewFolder.Value);
        await page.AnswerCreateCommand.ExecuteAsync(false);

        await page.RequestCreateCommand.ExecuteAsync(null);
        Assert.AreEqual(string.Empty, page.NewFolder.Value, "la fois d'après, le dossier habituel");
        await page.AnswerCreateCommand.ExecuteAsync(false);
    }

    [STATestMethod]
    public void The_list_folds_on_a_click_and_with_the_keyboard_and_answers_its_window()
    {
        using var ui = new UiThread();
        using var vault = new SheetsVault();
        var test = new TestShell();
        var model = vault.Loaded();
        var page = new SheetsPage { DataContext = model };
        var host = new Border { Width = 1200, Height = 900, Child = page };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        TestShell.Flush();
        try
        {
            var science = model.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences");
            var line = (ListBoxItem)page.Items.ItemContainerGenerator.ContainerFromItem(science);
            line.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseUpEvent });
            Assert.IsFalse(model.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences").IsExpanded);
            TestShell.Flush();

            var folded = (ListBoxItem)page.Items.ItemContainerGenerator.ContainerFromItem(model.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences"));
            folded.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(folded)!, 0, Key.Left) { RoutedEvent = UIElement.PreviewKeyDownEvent });
            TestShell.Flush();
            Assert.IsFalse(model.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences").IsExpanded, "la flèche gauche ne replie pas ce qui l'est déjà");

            folded.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(folded)!, 0, Key.Right) { RoutedEvent = UIElement.PreviewKeyDownEvent });
            TestShell.Flush();
            var opened = model.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences");
            Assert.IsTrue(opened.IsExpanded, "la flèche droite le déplie");

            var line2 = (ListBoxItem)page.Items.ItemContainerGenerator.ContainerFromItem(opened);
            line2.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(line2)!, 0, Key.Enter) { RoutedEvent = UIElement.PreviewKeyDownEvent });
            TestShell.Flush();
            Assert.IsFalse(model.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Sciences").IsExpanded, "Entrée aussi");

            var chemistry = model.Rows.OfType<SheetRow>().First(row => row.Title == "À la racine");
            model.RequestTrash(chemistry);
            TestShell.Flush();
            var erase = (Button)page.ActionDialog.Template.FindName(Dialog.PrimaryButtonPart, page.ActionDialog);
            Assert.IsTrue(erase.IsDefault, "mettre à la corbeille se rattrape : Entrée suffit");
            model.ErasesForGood = true;
            TestShell.Flush();
            Assert.IsFalse(erase.IsDefault, "effacer ne se fait jamais avec Entrée");
            Assert.AreSame(test.Design.Dictionary["Kyf.Button.Danger"], erase.Style);
            model.AnswerActionCommand.Execute(false);
            TestShell.Flush();

            model.RequestNewFolder(string.Empty);
            TestShell.Flush();
            Assert.IsTrue(page.ActionDialog.IsOpen);
            Assert.IsTrue(page.ActionInputBox.IsVisible);
            page.ActionDialog.Close(DialogChoice.Secondary);
            TestShell.Flush();
            Assert.IsFalse(model.IsActing);

            page.DataContext = null;
            page.ActionDialog.Close(DialogChoice.None);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public async Task An_element_of_the_trash_is_erased_only_after_a_yes()
    {
        using var vault = new SheetsVault();
        var app = vault.App;
        var atoms = app.Sheets.List().Value.Single(sheet => sheet.Title == "Atomes").Id;
        app.Trash.TrashSheet(atoms).Value.ToString();
        var data = new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, vault.Vaults.Picker, new FakeOpener(), () => { });
        await data.ReloadCommand.ExecuteAsync(null);
        var row = data.Trash.Single();
        Assert.AreEqual("Supprimer définitivement : Atomes", row.EraseName);

        await data.AnswerEraseCommand.ExecuteAsync(true);
        Assert.HasCount(1, data.Trash, "rien n'a été demandé");

        data.RequestEraseCommand.Execute(row);
        Assert.IsTrue(data.IsConfirmingErase);
        StringAssert.Contains(data.EraseMessage, "Cela ne s'annule pas", StringComparison.Ordinal);
        await data.AnswerEraseCommand.ExecuteAsync(false);
        Assert.IsFalse(data.IsConfirmingErase);
        Assert.HasCount(1, data.Trash);

        data.RequestEraseCommand.Execute(row);
        await data.AnswerEraseCommand.ExecuteAsync(true);

        Assert.IsEmpty(data.Trash);
        Assert.AreEqual(SettingsHubText.ErasedOf("Atomes"), data.Message);
        Assert.IsEmpty(app.Trash.TrashedSheets().Value);
        Assert.IsFalse(app.Sheets.Open(atoms).Succeeded);
    }

    [STATestMethod]
    public void The_data_tab_shows_an_erase_button_and_asks_first()
    {
        using var ui = new UiThread();
        using var vault = new SheetsVault();
        var app = vault.App;
        app.Trash.TrashSheet(app.Sheets.List().Value.Single(sheet => sheet.Title == "Atomes").Id).Value.ToString();
        var data = new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, vault.Vaults.Picker, new FakeOpener(), () => { });
        var test = new TestShell();
        var page = new DataSettingsPage { DataContext = data };
        var host = new Border { Width = 900, Height = 900, Child = page };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        data.OnNavigatedTo();
        try
        {
            UiThread.Until(() => data.HasTrash);
            TestShell.Flush();
            data.RequestEraseCommand.Execute(data.Trash.Single());
            TestShell.Flush();
            Assert.IsTrue(page.EraseConfirmation.IsOpen);
            page.EraseConfirmation.Close(DialogChoice.Primary);
            UiThread.Until(() => !data.HasTrash);

            page.DataContext = null;
            page.EraseConfirmation.Close(DialogChoice.None);
        }
        finally
        {
            window.Close();
        }
    }

    private static List<string> Labels(SheetsPageViewModel page) =>
        [.. page.Rows.Select(row => row is SheetRow sheet ? sheet.Title : row.ToString()!)];

    private static List<string> Headers(IReadOnlyList<Control> items) =>
        [.. items.OfType<MenuItem>().Select(item => (string)item.Header)];

    private static void Click(Control item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    /// <summary>Un coffre avec des dossiers imbriqués, et un dossier vide.</summary>
    private sealed class SheetsVault : IDisposable
    {
        public SheetsVault()
        {
            Vaults = new TestVaults(AppMode.Development);
            Root = Vaults.PathOf("Coffre");
            Vaults.App.Vaults.Create(Root);
            App.Sheets.Create(string.Empty, "À la racine", null).Value.ToString();
            App.Sheets.Create("Sciences/Biologie/Cellule/Noyau", "Membrane", null).Value.ToString();
            App.Sheets.Create("Sciences/Chimie", "Atomes", null).Value.ToString();
            App.Sheets.Create("Sciences", "Lois", null).Value.ToString();
            App.Folders.Create("Vide").Value.ToString();
        }

        public TestVaults Vaults { get; }

        public string Root { get; }

        public KyfRevApplication App => Vaults.App;

        public FakeOpener Opener { get; } = new();

        public string PathOf(string relative) => Path.Combine(Root, relative);

        public SheetsPageViewModel Page() => new(App.Sheets, App.Folders, App.Templates, Opener, TimeProvider.System, App.Reviews, App.Trash);

        public SheetsPageViewModel Loaded()
        {
            var page = Page();
            page.RefreshCommand.Execute(null);
            UiThread.Until(() => page.Rows.Count > 0);
            return page;
        }

        public async Task<SheetsPageViewModel> LoadedAsync()
        {
            var page = Page();
            await page.RefreshCommand.ExecuteAsync(null);
            return page;
        }

        public void Dispose() => Vaults.Dispose();
    }
}
