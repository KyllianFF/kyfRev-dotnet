using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le glisser-déposer de la vue « Fiches ».</summary>
[TestClass]
public sealed class SheetDragTests
{
    [TestMethod]
    [DataRow(0, -3)]
    [DataRow(15, -3)]
    [DataRow(16, -1)]
    [DataRow(47, -1)]
    [DataRow(48, 0)]
    [DataRow(250, 0)]
    [DataRow(452, 0)]
    [DataRow(453, 1)]
    [DataRow(484, 1)]
    [DataRow(485, 3)]
    [DataRow(500, 3)]
    public void The_list_scrolls_near_its_edges_and_faster_at_the_very_edge(int y, int lines)
    {
        Assert.AreEqual(lines, SheetsPage.ScrollLinesAt(y, 500));
    }

    [TestMethod]
    public async Task A_sheet_can_go_to_any_other_folder_but_not_where_it_is()
    {
        using var vault = new DragVault();
        var page = await vault.LoadedAsync();
        var atoms = page.Sheet("Atomes");

        Assert.AreEqual("Vide", page.DropFolderOf(atoms, page.Folder("Vide")));
        Assert.AreEqual(string.Empty, page.DropFolderOf(atoms, page.Rows[0]), "« Sans dossier » est une destination.");
        Assert.AreEqual("Sciences", page.DropFolderOf(atoms, page.Sheet("Lois")), "Sur une fiche : dans son dossier.");
        Assert.IsNull(page.DropFolderOf(atoms, page.Folder("Sciences/Chimie")), "Elle y est déjà.");
        Assert.IsNull(page.DropFolderOf(atoms, null), "Sur la place libre : rien.");
        Assert.IsNull(page.DropFolderOf(null, page.Folder("Vide")));
        Assert.IsNull(page.DropFolderOf("autre chose", page.Folder("Vide")));
    }

    [TestMethod]
    public async Task A_folder_cannot_go_into_itself_nor_stay_where_it_is()
    {
        using var vault = new DragVault();
        var page = await vault.LoadedAsync();
        var science = page.Folder("Sciences");

        Assert.AreEqual("Vide", page.DropFolderOf(science, page.Folder("Vide")));
        Assert.IsNull(page.DropFolderOf(science, science));
        Assert.IsNull(page.DropFolderOf(science, page.Folder("Sciences/Chimie")), "Pas dans ses propres sous-dossiers.");
        Assert.IsNull(page.DropFolderOf(science, page.Rows[0]), "Déjà à la racine.");
        Assert.AreEqual(string.Empty, page.DropFolderOf(page.Folder("Sciences/Chimie"), page.Rows[0]));
        Assert.IsNull(page.DropFolderOf(page.Rows[0], page.Folder("Vide")), "« Sans dossier » ne se déplace pas.");
    }

    [TestMethod]
    public async Task The_hint_says_where_the_line_would_go()
    {
        using var vault = new DragVault();
        var page = await vault.LoadedAsync();
        var atoms = page.Sheet("Atomes");

        Assert.AreEqual("Sciences", page.ShowDrop(atoms, page.Folder("Sciences")));
        Assert.AreEqual(SheetActionsText.DropInOf("Sciences"), page.DropHint);
        Assert.IsTrue(page.HasDropHint);

        Assert.AreEqual(string.Empty, page.ShowDrop(atoms, page.Rows[0]));
        Assert.AreEqual(SheetActionsText.DropAtRoot, page.DropHint);

        Assert.IsNull(page.ShowDrop(null, null));
        Assert.IsFalse(page.HasDropHint);
    }

    [TestMethod]
    public async Task Dropping_moves_the_sheet_and_says_so()
    {
        using var vault = new DragVault();
        var page = await vault.LoadedAsync();
        var atoms = page.Sheet("Atomes");

        await page.DropAsync(atoms, "Vide");

        Assert.AreEqual(SheetActionsText.Moved, page.Done);
        Assert.IsFalse(page.HasDropHint);
        Assert.AreEqual("Vide", vault.App.Sheets.Open(atoms.Sheet.Id).Value.Folder);
        Assert.AreEqual("Vide", page.Sheet("Atomes").Sheet.Folder);

        await page.DropAsync(page.Sheet("Atomes"), string.Empty);
        Assert.AreEqual(string.Empty, vault.App.Sheets.Open(atoms.Sheet.Id).Value.Folder);
    }

    [TestMethod]
    public async Task Dropping_moves_a_folder_with_what_it_holds()
    {
        using var vault = new DragVault();
        var page = await vault.LoadedAsync();
        var atoms = page.Sheet("Atomes").Sheet.Id;

        await page.DropAsync(page.Folder("Sciences/Chimie"), "Vide");

        Assert.AreEqual(SheetActionsText.Moved, page.Done);
        Assert.AreEqual("Vide/Chimie", vault.App.Sheets.Open(atoms).Value.Folder);
        Assert.IsNotNull(page.Folder("Vide/Chimie"));
    }

    [TestMethod]
    public async Task A_folder_too_deep_goes_through_the_dialog_and_its_warning()
    {
        using var vault = new DragVault();
        var page = await vault.LoadedAsync();
        var atoms = page.Sheet("Atomes").Sheet.Id;

        await page.DropAsync(page.Folder("Sciences/Chimie"), "Sciences/Biologie/Cellule/Noyau");

        Assert.IsTrue(page.IsActing, "Rien n'est encore déplacé.");
        Assert.AreEqual("Sciences/Biologie/Cellule/Noyau", page.ActionDestination!.Value);
        Assert.IsTrue(page.HasDepthWarning);
        Assert.AreEqual("Sciences/Chimie", vault.App.Sheets.Open(atoms).Value.Folder);

        await page.AnswerActionCommand.ExecuteAsync(true);

        Assert.AreEqual("Sciences/Biologie/Cellule/Noyau/Chimie", vault.App.Sheets.Open(atoms).Value.Folder);
    }

    [TestMethod]
    public async Task A_refused_drop_is_told_and_nothing_else()
    {
        using var vault = new DragVault();
        var page = await vault.LoadedAsync();
        var atoms = page.Sheet("Atomes");
        vault.App.Trash.TrashSheet(atoms.Sheet.Id).Value.ToString();

        await page.DropAsync(atoms, "Vide");

        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.HasDone);
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => page.DropAsync("autre chose", "Vide"));
    }

    [TestMethod]
    public async Task Sorted_otherwise_a_sheet_is_no_longer_a_destination()
    {
        using var vault = new DragVault();
        var page = await vault.LoadedAsync();
        var atoms = page.Sheet("Atomes");
        Assert.IsNotNull(page.DropFolderOf(atoms, page.Sheet("Lois")));

        var sort = page.Bar.Filters.Single(filter => filter.Label == SheetsText.FilterSort);
        sort.Selected = sort.Choices.Single(choice => choice.Label == SheetsText.SortTitle);

        Assert.IsEmpty(page.Rows.OfType<SheetFolderRow>().ToList());
        Assert.IsNull(page.DropFolderOf(page.Sheet("Atomes"), page.Sheet("Lois")));
    }

    [TestMethod]
    public async Task Sans_dossier_stays_a_destination_even_empty_unless_filtered()
    {
        using var vault = new DragVault(withRootSheet: false);
        var page = await vault.LoadedAsync();

        Assert.AreEqual(SheetsText.FolderCountOf(SheetsText.NoFolder, 0), page.Rows[0].ToString());

        page.Bar.Search = "atomes";

        Assert.AreNotEqual(SheetsText.FolderCountOf(SheetsText.NoFolder, 0), page.Rows[0].ToString());
    }

    [STATestMethod]
    public void The_gesture_starts_past_the_threshold_and_ends_clean()
    {
        using var ui = new UiThread();
        using var vault = new DragVault();
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
            var started = new List<DragDropEffects>();
            object? hovered = null;
            page.StartDrag = (source, _, effects) =>
            {
                started.Add(effects);
                Assert.AreSame(page.Items, source);
                Assert.IsTrue(page.DraggedOver(hovered, 200));
                Assert.AreEqual(Visibility.Visible, page.DropHintBox.Visibility);
                Assert.AreEqual(VerticalAlignment.Bottom, page.DropHintBox.VerticalAlignment, "Le pointeur est en haut : l'indication est en bas.");
                var header = (ListBoxItem)page.Items.ItemContainerGenerator.ContainerFromItem(hovered);
                Assert.AreEqual(test.Design.Dictionary["Kyf.Brush.NavigationActiveBackground"], header.Background, "Le dossier visé ressort.");
                Assert.IsTrue(page.DraggedOver(hovered, page.Items.ActualHeight - 1));
                Assert.AreEqual(VerticalAlignment.Top, page.DropHintBox.VerticalAlignment);
                Assert.IsFalse(page.DraggedOver(null, 200), "Hors d'une ligne : pas de dépôt.");
                Assert.AreEqual(Brushes.Transparent, header.Background);
                Assert.AreEqual(Visibility.Collapsed, page.DropHintBox.Visibility);
                Assert.IsTrue(page.DraggedOver(hovered, 200));
                page.DroppedOn(hovered).GetAwaiter();
                return effects;
            };

            var row = page.Items.ItemContainerGenerator.ContainerFromItem(model.Sheet("Atomes")) as ListBoxItem;
            Assert.IsNotNull(row);
            hovered = model.Folder("Vide");
            Press(row);

            var pressedAt = Mouse.GetPosition(page.Items);
            page.Moved(pressedAt, isPressed: true);
            Assert.IsEmpty(started, "Sous le seuil : un clic, pas un geste.");

            page.Moved(pressedAt + new Vector(0, 40), isPressed: false);
            page.Moved(pressedAt + new Vector(0, 80), isPressed: true);
            Assert.IsEmpty(started, "Le bouton relâché annule le geste.");

            Press(row);
            page.Moved(pressedAt + new Vector(0, 80), isPressed: true);

            Assert.HasCount(1, started);
            Assert.AreEqual(DragDropEffects.Move, started[0]);
            UiThread.Until(() => vault.App.Sheets.List().Value.Single(sheet => sheet.Title == "Atomes").Folder == "Vide");
            Assert.IsFalse(model.HasDropHint);

            page.Moved(pressedAt + new Vector(0, 160), isPressed: true);
            Assert.HasCount(1, started, "Le geste est fini.");

            page.DataContext = null;
            Assert.IsFalse(page.DraggedOver(hovered, 200));
            page.DroppedOn(hovered).GetAwaiter().GetResult();
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Near_an_edge_the_list_really_scrolls()
    {
        using var ui = new UiThread();
        using var vault = new DragVault(extraSheets: 60);
        var test = new TestShell();
        var model = vault.Loaded();
        var page = new SheetsPage { DataContext = model };
        var host = new Border { Width = 1200, Height = 500, Child = page };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        TestShell.Flush();
        try
        {
            var scroller = Inside<ScrollViewer>(page.Items)!;
            Assert.AreEqual(0, scroller.VerticalOffset);

            page.DraggedOver(null, page.Items.ActualHeight - 1);
            TestShell.Flush();
            var down = scroller.VerticalOffset;
            Assert.IsGreaterThan(0, down);

            page.DraggedOver(null, page.Items.ActualHeight - 30);
            TestShell.Flush();
            Assert.IsGreaterThan(down, scroller.VerticalOffset);

            page.DraggedOver(null, 30);
            page.DraggedOver(null, 1);
            TestShell.Flush();
            Assert.IsLessThan(down + 16, scroller.VerticalOffset);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Press(ListBoxItem row) =>
        row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });

    private static T? Inside<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if ((child as T ?? Inside<T>(child)) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>Un coffre avec des dossiers imbriqués, et un dossier vide.</summary>
    private sealed class DragVault : IDisposable
    {
        private readonly TestVaults _vaults = new(AppMode.Development);
        private readonly FakeOpener _opener = new();

        public DragVault(bool withRootSheet = true, int extraSheets = 0)
        {
            App.Vaults.Create(_vaults.PathOf("Coffre"));
            if (withRootSheet)
            {
                App.Sheets.Create(string.Empty, "À la racine", null).Value.ToString();
            }
            App.Sheets.Create("Sciences/Biologie/Cellule/Noyau", "Membrane", null).Value.ToString();
            App.Sheets.Create("Sciences/Chimie", "Atomes", null).Value.ToString();
            App.Sheets.Create("Sciences", "Lois", null).Value.ToString();
            App.Folders.Create("Vide").Value.ToString();
            for (var i = 0; i < extraSheets; i++)
            {
                App.Sheets.Create("Sciences", $"Fiche {i}", null).Value.ToString();
            }
        }

        public KyfRevApplication App => _vaults.App;

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

        public void Dispose() => _vaults.Dispose();

        private SheetsPageViewModel Page() => new(App.Sheets, App.Folders, App.Templates, _opener, TimeProvider.System, App.Reviews, App.Trash);
    }
}

/// <summary>Trouver une ligne de la liste par ce qu'elle montre.</summary>
internal static class SheetRowsExtensions
{
    public static SheetRow Sheet(this SheetsPageViewModel page, string title) => page.Rows.OfType<SheetRow>().Single(row => row.Title == title);

    public static SheetFolderRow Folder(this SheetsPageViewModel page, string path) => page.Rows.OfType<SheetFolderRow>().Single(row => row.Path == path);
}
