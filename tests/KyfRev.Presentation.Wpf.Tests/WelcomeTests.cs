using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// L'accueil et le démarrage, sur de vrais coffres : créer le coffre prévu, choisir un dossier,
/// afficher un refus, et le menu qui ne revient qu'une fois un coffre ouvert.
/// </summary>
[TestClass]
public sealed class WelcomeTests
{
    [TestMethod]
    public async Task The_planned_vault_is_created_where_it_is_planned_without_asking()
    {
        using var test = new TestVaults(AppMode.Installed);
        var opened = new List<AppState>();
        var welcome = test.Welcome(opened.Add);
        var changes = new List<string>();
        welcome.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        Assert.IsTrue(welcome.HasVaultToCreate);
        Assert.AreEqual(test.Planned, welcome.VaultToCreate);
        Assert.AreEqual("Créer le coffre ici", welcome.CreateLabel);
        Assert.IsTrue(welcome.CanOpenExisting, "installé : un coffre existant peut aussi être repris");
        Assert.IsFalse(welcome.ExplainsNewVault);

        await welcome.CreateCommand.ExecuteAsync(null);

        Assert.IsEmpty(test.Picker.Titles);
        Assert.AreEqual(test.Planned, opened.Single().Vault);
        Assert.IsTrue(Directory.Exists(Path.Combine(test.Planned, ".suivi")));
        CollectionAssert.IsSubsetOf(new[] { nameof(WelcomePageViewModel.IsBusy) }, changes);
        Assert.IsFalse(welcome.IsBusy);
        Assert.IsTrue(welcome.CreateCommand.CanExecute(null));
    }

    [TestMethod]
    public void The_portable_welcome_offers_only_its_planned_vault()
    {
        using var test = new TestVaults(AppMode.Portable);

        var welcome = test.Welcome(_ => { });

        Assert.IsTrue(welcome.HasVaultToCreate);
        Assert.IsFalse(welcome.CanOpenExisting);
    }

    [TestMethod]
    public async Task Without_a_planned_vault_a_folder_is_chosen()
    {
        using var test = new TestVaults(AppMode.Development);
        var opened = new List<AppState>();
        var welcome = test.Welcome(opened.Add);
        Assert.IsFalse(welcome.HasVaultToCreate);
        Assert.AreEqual("Créer un coffre", welcome.CreateLabel);
        Assert.IsTrue(welcome.CanOpenExisting);
        Assert.IsTrue(welcome.ExplainsNewVault);

        // Renoncer ne fait rien.
        test.Picker.Answers.Enqueue(null);
        await welcome.CreateCommand.ExecuteAsync(null);
        Assert.AreEqual("Choisissez un dossier vide pour le nouveau coffre", test.Picker.Titles.Single());
        Assert.IsEmpty(opened);
        Assert.IsFalse(welcome.HasError);

        // Un dossier qui n'est pas vide est refusé, avec le message du coffre.
        var notEmpty = test.Folder("Notes");
        await File.WriteAllTextAsync(Path.Combine(notEmpty, "note.txt"), "Une note.");
        test.Picker.Answers.Enqueue(notEmpty);
        await welcome.CreateCommand.ExecuteAsync(null);
        Assert.IsTrue(welcome.HasError);
        Assert.IsFalse(string.IsNullOrWhiteSpace(welcome.Error));
        Assert.IsEmpty(opened);

        // Un coffre existant est ouvert ; le message précédent est effacé dès la nouvelle tentative.
        var existing = test.CreateVault("Existant");
        test.Picker.Answers.Enqueue(existing);
        await welcome.OpenCommand.ExecuteAsync(null);
        Assert.AreEqual("Choisissez le dossier du coffre", test.Picker.Titles[^1]);
        Assert.IsNull(welcome.Error);
        Assert.AreEqual(existing, opened.Single().Vault);
    }

    [TestMethod]
    public async Task A_folder_that_is_not_a_vault_is_refused()
    {
        using var test = new TestVaults(AppMode.Development);
        var opened = new List<AppState>();
        var welcome = test.Welcome(opened.Add);

        test.Picker.Answers.Enqueue(test.Folder("Pas un coffre"));
        await welcome.OpenCommand.ExecuteAsync(null);

        Assert.IsTrue(welcome.HasError);
        Assert.IsEmpty(opened);
    }

    [TestMethod]
    public async Task The_startup_error_is_shown_until_the_next_attempt()
    {
        using var test = new TestVaults(AppMode.Development);
        var state = new AppState("1.0.0-essai", AppMode.Development, null, null, "Le coffre est déjà ouvert ailleurs.");
        var welcome = new WelcomePageViewModel(test.App.Vaults, test.Picker, state, _ => { });

        Assert.AreEqual("Le coffre est déjà ouvert ailleurs.", welcome.Error);
        Assert.IsTrue(welcome.HasError);

        test.Picker.Answers.Enqueue(null);
        await welcome.OpenCommand.ExecuteAsync(null);
        Assert.IsFalse(welcome.HasError);
    }

    [TestMethod]
    public void The_welcome_needs_its_services()
    {
        using var test = new TestVaults(AppMode.Development);
        var state = test.App.Vaults.State();
        Assert.ThrowsExactly<ArgumentNullException>(() => new WelcomePageViewModel(null!, test.Picker, state, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WelcomePageViewModel(test.App.Vaults, null!, state, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WelcomePageViewModel(test.App.Vaults, test.Picker, null!, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WelcomePageViewModel(test.App.Vaults, test.Picker, state, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => VaultStartup.Run(null!, test.App.Vaults, test.Picker));
        Assert.ThrowsExactly<ArgumentNullException>(() => VaultStartup.Run(new TestShell().Shell, null!, test.Picker));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TestShell().Shell.ShowWelcome(null!));
    }

    [TestMethod]
    public void The_welcome_hides_the_menu_and_the_views_until_a_vault_is_open()
    {
        var shell = new TestShell().Shell;
        var page = new object();

        shell.ShowWelcome(page);

        Assert.IsTrue(shell.IsWelcome);
        Assert.IsFalse(shell.IsMenuVisible);
        Assert.AreSame(page, shell.Current.Page);
        Assert.AreEqual("Bienvenue dans kyfRev", shell.Current.Label);
        Assert.IsNull(shell.SelectedMain);
        shell.Navigate(shell.MainNavigation[1]);
        shell.NavigateToPosition("2");
        shell.Navigate(shell.Gallery);
        Assert.AreSame(page, shell.Current.Page);

        // Sans coffre, la recherche ne propose que les commandes de la coquille.
        shell.OpenPalette();
        shell.PaletteQuery = "aller";
        Assert.IsTrue(shell.HasNoResult);
        shell.PaletteQuery = "interface";
        Assert.HasCount(2, shell.PaletteResults);
        shell.ClosePalette();

        shell.ShowWorkspace();

        Assert.IsFalse(shell.IsWelcome);
        Assert.IsTrue(shell.IsMenuVisible);
        Assert.AreSame(shell.Home, shell.Current);
        Assert.AreSame(shell.Home, shell.SelectedMain);
        shell.PaletteQuery = "aller";
        Assert.IsFalse(shell.HasNoResult);
        shell.ToggleFocusMode();
        Assert.IsFalse(shell.IsMenuVisible);
    }

    [TestMethod]
    public async Task At_startup_the_remembered_vault_opens_and_the_welcome_waits_otherwise()
    {
        using var test = new TestVaults(AppMode.Development);
        var shell = new TestShell().Shell;

        var state = VaultStartup.Run(shell, test.App.Vaults, test.Picker);

        Assert.IsNull(state.Vault);
        Assert.IsTrue(shell.IsWelcome);
        var welcome = (WelcomePageViewModel)shell.Current.Page;
        test.Picker.Answers.Enqueue(test.PathOf("Coffre"));
        await welcome.CreateCommand.ExecuteAsync(null);
        Assert.IsFalse(shell.IsWelcome);
        Assert.AreSame(shell.Home, shell.Current);

        // Au démarrage suivant, le coffre retenu est rouvert : pas d'accueil.
        var next = test.Restart();
        var nextShell = new TestShell().Shell;
        Assert.AreEqual(test.PathOf("Coffre"), VaultStartup.Run(nextShell, next.Vaults, test.Picker).Vault);
        Assert.IsFalse(nextShell.IsWelcome);
    }

    [TestMethod]
    public void The_folder_picker_returns_the_chosen_folder_only()
    {
        string? title = null;
        var chosen = new FolderPicker(dialog =>
        {
            title = dialog.Title;
            Assert.IsFalse(dialog.Multiselect);
            dialog.FolderName = @"C:\Coffre";
            return true;
        });
        Assert.AreEqual(@"C:\Coffre", chosen.Pick("Choisissez"));
        Assert.AreEqual("Choisissez", title);

        Assert.IsNull(new FolderPicker(_ => false).Pick("Choisissez"));
        Assert.IsNull(new FolderPicker(_ => null).Pick("Choisissez"));
        Assert.IsInstanceOfType<IFolderPicker>(new FolderPicker());
    }

    [STATestMethod]
    public void The_welcome_page_shows_the_planned_vault_and_the_error()
    {
        using var test = new TestVaults(AppMode.Installed);
        var shell = new TestShell();
        var state = test.App.Vaults.State() with { OpenError = "Le coffre est déjà ouvert ailleurs." };
        shell.Shell.ShowWelcome(new WelcomePageViewModel(test.App.Vaults, test.Picker, state, _ => { }));
        var window = new ShellWindow(shell.Shell) { ShowActivated = false, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(shell.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            Assert.AreNotEqual(0, new WindowInteropHelper(window).Handle);
            Assert.AreEqual(Visibility.Collapsed, Named<FrameworkElement>(window, "Sidebar").Visibility);
            var create = Named<Button>(window, "CreateButton");
            Assert.AreEqual("Créer le coffre ici", AutomationProperties.GetName(create));
            Assert.AreEqual(Visibility.Visible, Named<Button>(window, "OpenButton").Visibility);
            Assert.AreEqual(test.Planned, Named<TextBlock>(window, "PlannedVault").Text);
            Assert.AreEqual(Visibility.Collapsed, Named<TextBlock>(window, "NewVaultHelp").Visibility);
            var error = Named<Notification>(window, "ErrorMessage");
            Assert.IsTrue(error.IsOpen);
            Assert.AreEqual("Le coffre est déjà ouvert ailleurs.", error.Message);
            Assert.AreEqual(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(Named<TextBlock>(window, "Heading")));
        }
        finally
        {
            window.Close();
        }
    }

    private static T Named<T>(DependencyObject root, string name)
        where T : FrameworkElement =>
        Descendants(root).OfType<T>().Single(element => element.Name == name);

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
