using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Le volet des dossiers du Carnet (rangée 21B des maquettes) : à gauche de la liste quand il tient, dans un tiroir qui
/// vient de la gauche ou du bas sinon ; les intertitres de la liste mènent à leur dossier.
/// </summary>
[TestClass]
public sealed class SheetsPaneTests
{
    [TestMethod]
    public void The_layout_follows_the_width_of_the_page()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults);

        Assert.IsFalse(page.ShowsPane, "tant que la largeur n'est pas connue, la disposition est la plus simple");
        Assert.IsFalse(page.IsWide);

        page.PageWidth = SheetsPageViewModel.PaneMinimum;
        Assert.IsTrue(page.ShowsPane);
        Assert.IsFalse(page.IsWide);
        Assert.IsFalse(page.PlacesFromBottom);

        page.PageWidth = SheetsPageViewModel.WideMinimum;
        Assert.IsTrue(page.IsWide);

        page.PageWidth = SheetsPageViewModel.PaneMinimum - 1;
        Assert.IsFalse(page.ShowsPane);
        Assert.IsFalse(page.PlacesFromBottom, "le tiroir vient de la gauche");
        page.TogglePlacesCommand.Execute(null);
        Assert.IsTrue(page.ShowsPlacesSheet);

        page.PageWidth = SheetsPageViewModel.RoomMinimum - 1;
        Assert.IsTrue(page.PlacesFromBottom, "en fenêtre étroite, il vient du bas");
        Assert.IsTrue(page.ShowsPlacesSheet);

        page.PageWidth = SheetsPageViewModel.PaneMinimum;
        Assert.IsFalse(page.ArePlacesOpen, "le volet tient de nouveau à côté : le tiroir se referme");
        Assert.IsFalse(page.ShowsPlacesSheet);
    }

    [TestMethod]
    public void The_gaps_of_the_pane_come_before_the_folders_and_before_non_rangees()
    {
        Assert.AreEqual(PlacesList.FoldersGap, PlacesList.GapAbove(new SheetFolderRow("Biologie", 1) { Path = "Biologie", StartsFolders = true }));
        Assert.AreEqual(PlacesList.UnfiledGap, PlacesList.GapAbove(new SheetFolderRow(SheetsText.NoFolder, 0) { Place = SheetPlace.Unfiled, StartsFolders = true }));
        Assert.AreEqual(0, PlacesList.GapAbove(new SheetFolderRow("Cellule", 1) { Path = "Biologie/Cellule" }));
        Assert.AreEqual(0, PlacesList.GapAbove("autre chose"));
    }

    [TestMethod]
    public async Task Non_rangees_says_when_everything_is_filed_and_offers_to_write_there()
    {
        using var vaults = new TestVaults(AppMode.Development);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        vaults.App.Sheets.Create("Biologie", "Mitose", null).Value.ToString();
        var page = Page(vaults);
        await page.RefreshCommand.ExecuteAsync(null);

        page.SelectedPlace = page.Unfiled();

        Assert.IsTrue(page.IsPlaceEmpty);
        Assert.AreEqual(SheetsText.AllFiled, page.PlaceEmptyText);
        Assert.IsTrue(page.PlaceTakesSheets);
        page.ArePlacesOpen = true;
        page.SelectedPlace = page.Folder("Biologie");
        Assert.IsFalse(page.ArePlacesOpen, "choisir un dossier referme le tiroir");
    }

    [TestMethod]
    public async Task A_header_of_the_list_leads_to_its_folder_and_ends_a_search()
    {
        using var vaults = new TestVaults(AppMode.Development);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        vaults.App.Sheets.Create("Biologie/Cellule", "Mitose", null).Value.ToString();
        vaults.App.Sheets.Create("Histoire", "Révolution", null).Value.ToString();
        var page = Page(vaults);
        await page.RefreshCommand.ExecuteAsync(null);
        var arrived = 0;
        page.PlaceChanged += (_, _) => arrived++;
        page.ToggleFolderCommand.Execute(page.Folder("Biologie"));
        Assert.IsFalse(page.Places.Any(row => row.Path == "Biologie/Cellule"), "plié");

        page.GoTo("Biologie/Cellule");

        Assert.AreEqual("Biologie/Cellule", page.SelectedPlace!.Path, "ses parents se déplient pour le montrer choisi");
        Assert.AreEqual("Cellule", page.ListTitle);
        Assert.AreEqual(1, arrived);

        page.Bar.Search = "révolution";
        Assert.AreEqual(SheetsText.SearchEverywhere, page.ListTitle);
        page.GoTo("Biologie/Cellule");
        Assert.AreEqual(string.Empty, page.Bar.Search, "on veut voir ce dossier : la recherche s'arrête");
        Assert.AreEqual("Cellule", page.ListTitle);
        Assert.AreEqual(2, arrived);

        page.GoTo("Biologie/Cellule");
        Assert.AreEqual(2, arrived, "déjà là : rien ne bouge");
    }

    [STATestMethod]
    public void Without_room_the_pane_goes_into_a_drawer_that_closes_on_escape_a_click_beside_or_a_choice()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        vaults.App.Sheets.Create("Biologie/Cellule", "Mitose", null).Value.ToString();
        vaults.App.Sheets.Create("Histoire", "Révolution", null).Value.ToString();
        var model = Page(vaults);
        model.RefreshCommand.Execute(null);
        UiThread.Until(() => model.Rows.Count > 0);
        var test = new TestShell();
        var page = new SheetsPage { DataContext = model };
        var host = new Border { Width = 900, Height = 800, Child = page };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        TestShell.Flush();
        try
        {
            Assert.IsFalse(model.ShowsPane);
            Assert.AreSame(page.PlacesDrawerHost, page.PlacesPanel.Parent, "sans la place, le volet est dans le tiroir");
            Assert.IsTrue(page.PlacesButtonHost.IsVisible);
            Assert.IsFalse(page.PaneHost.IsVisible);
            Assert.AreEqual(PlacesList.EntryHeight, ((ListBoxItem)page.PlacesList.ItemContainerGenerator.ContainerFromIndex(0)).MinHeight);

            // Le bouton ouvre le tiroir ; Échap le referme.
            page.PlacesButton.Command.Execute(null);
            TestShell.Flush();
            Assert.IsTrue(model.ShowsPlacesSheet);
            Assert.IsTrue(page.PlacesOverlay.IsHitTestVisible);
            page.PlacesDrawer.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(page.PlacesDrawer)!, 0, Key.Escape) { RoutedEvent = UIElement.PreviewKeyDownEvent });
            Assert.IsFalse(model.ArePlacesOpen);
            page.PlacesDrawer.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(page.PlacesDrawer)!, 0, Key.Escape) { RoutedEvent = UIElement.PreviewKeyDownEvent });
            Assert.IsFalse(model.ArePlacesOpen, "fermé, Échap ne fait rien de plus");

            // Un clic à côté le referme.
            model.ArePlacesOpen = true;
            TestShell.Flush();
            page.PlacesScrim.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            Assert.IsFalse(model.ArePlacesOpen);

            // Un dossier choisi dans le tiroir : il se referme, la liste montre le dossier.
            model.ArePlacesOpen = true;
            TestShell.Flush();
            page.PlacesList.SelectedItem = model.Folder("Histoire");
            TestShell.Flush();
            Assert.IsFalse(model.ArePlacesOpen);
            Assert.AreEqual("Histoire", model.ListTitle);
            Assert.AreEqual("Histoire", model.PlaceName);

            // Un clic sur l'intertitre d'un sous-dossier mène à ce dossier.
            model.SelectedPlace = model.Folder("Biologie");
            TestShell.Flush();
            var header = model.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Biologie/Cellule");
            var line = (ListBoxItem)page.Items.ItemContainerGenerator.ContainerFromItem(header);
            line.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseUpEvent });
            Assert.AreEqual("Biologie/Cellule", model.SelectedPlace!.Path);
            model.SelectedPlace = model.Folder("Biologie");
            TestShell.Flush();
            header = model.Rows.OfType<SheetFolderRow>().Single(row => row.Path == "Biologie/Cellule");
            line = (ListBoxItem)page.Items.ItemContainerGenerator.ContainerFromItem(header);
            Assert.IsTrue(line.Focus());
            line.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(line)!, 0, Key.Enter) { RoutedEvent = UIElement.PreviewKeyDownEvent });
            Assert.AreEqual("Biologie/Cellule", model.SelectedPlace!.Path, "Entrée aussi");

            // La page s'élargit : le volet revient à gauche de la liste.
            host.Width = 1300;
            TestShell.Flush();
            window.UpdateLayout();
            Assert.IsTrue(model.ShowsPane);
            Assert.AreSame(page.PaneHost, page.PlacesPanel.Parent);
            Assert.IsTrue(page.PaneHost.IsVisible);
            Assert.IsFalse(page.PlacesButtonHost.IsVisible);

            page.DataContext = null;
            Assert.AreSame(page.PaneHost, page.PlacesPanel.Parent);
        }
        finally
        {
            window.Close();
        }
    }

    private static SheetsPageViewModel Page(TestVaults vaults) =>
        new(vaults.App.Sheets, vaults.App.Folders, vaults.App.Templates, new FakeOpener(), TimeProvider.System, vaults.App.Reviews, vaults.App.Trash);
}
