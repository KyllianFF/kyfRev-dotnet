using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Le déverrouillage d'un coffre chiffré, sur de vrais coffres : au démarrage, depuis l'accueil ou depuis les paramètres,
/// il attend son mot de passe ou sa clé de récupération, sans menu ni recherche ; sa clé peut être retenue ; un autre coffre
/// peut être ouvert à la place, ou celui qui restait ouvert retrouvé.
/// </summary>
[TestClass]
public sealed class UnlockTests
{
    [TestMethod]
    public void The_unlock_screen_checks_what_it_is_given()
    {
        using var test = new TestVaults(AppMode.Development);
        var locked = test.App.Vaults.State() with { Locked = test.PathOf("Coffre") };
        Action<AppState> none = _ => { };

        Assert.ThrowsExactly<ArgumentNullException>(() => new UnlockPageViewModel(null!, test.Picker, locked, none, none));
        Assert.ThrowsExactly<ArgumentNullException>(() => new UnlockPageViewModel(test.App.Vaults, null!, locked, none, none));
        Assert.ThrowsExactly<ArgumentNullException>(() => new UnlockPageViewModel(test.App.Vaults, test.Picker, null!, none, none));
        Assert.ThrowsExactly<ArgumentNullException>(() => new UnlockPageViewModel(test.App.Vaults, test.Picker, locked, null!, none));
        Assert.ThrowsExactly<ArgumentNullException>(() => new UnlockPageViewModel(test.App.Vaults, test.Picker, locked, none, null!));
        Assert.ThrowsExactly<ArgumentException>(() => new UnlockPageViewModel(test.App.Vaults, test.Picker, test.App.Vaults.State(), none, none));
        Assert.ThrowsExactly<ArgumentNullException>(() => VaultStartup.ShowUnlock(null!, test.App.Vaults, test.Picker, locked));
        Assert.ThrowsExactly<ArgumentNullException>(() => VaultStartup.ShowUnlock(new TestShell().Shell, test.App.Vaults, test.Picker, null!));

        var page = new UnlockPageViewModel(test.App.Vaults, test.Picker, locked with { OpenError = "Mot de passe incorrect." }, none, none);
        Assert.AreEqual(test.PathOf("Coffre"), page.Vault);
        Assert.AreEqual("Coffre", page.VaultName);
        Assert.AreEqual("Mot de passe incorrect.", page.Error, "la raison d'un refus s'affiche");
        Assert.IsFalse(page.CanGoBack);
        Assert.IsFalse(page.Remember, "décoché par défaut");
        Assert.AreEqual(@"C:\", new UnlockPageViewModel(test.App.Vaults, test.Picker, locked with { Locked = @"C:\" }, none, none).VaultName);
    }

    [TestMethod]
    public async Task At_startup_an_encrypted_vault_waits_for_its_password_without_menu_or_search()
    {
        using var test = new TestVaults(AppMode.Portable);
        test.CreateEncryptedVault(test.Planned);
        var shell = new TestShell().Shell;

        var state = VaultStartup.Run(shell, test.App.Vaults, test.Picker);

        Assert.AreEqual(test.Planned, state.Locked);
        Assert.IsNull(state.Vault);
        Assert.IsTrue(shell.IsWelcome);
        Assert.IsFalse(shell.IsMenuVisible);
        Assert.AreEqual(EncryptionText.LockedPage, shell.Current.Label);
        var page = (UnlockPageViewModel)shell.Current.Page;
        shell.PaletteQuery = "aller";
        Assert.IsTrue(shell.HasNoResult, "rien du coffre n'est cherché");

        await page.UnlockCommand.ExecuteAsync(" ");
        Assert.AreEqual(EncryptionText.EnterPassword, page.Error);
        await page.UnlockCommand.ExecuteAsync("pas le bon");
        Assert.AreEqual("Mot de passe incorrect.", page.Error);
        Assert.IsTrue(shell.IsWelcome);
        Assert.IsFalse(page.IsBusy);

        await page.UnlockCommand.ExecuteAsync(TestVaults.Password);

        Assert.IsFalse(shell.IsWelcome);
        Assert.AreSame(shell.Home, shell.Current);
        Assert.AreEqual(test.Planned, test.App.Vaults.State().Vault);
    }

    [TestMethod]
    public async Task Its_recovery_key_opens_it_when_the_password_is_lost()
    {
        using var test = new TestVaults(AppMode.Portable);
        test.CreateEncryptedVault(test.Planned);
        var shell = new TestShell().Shell;
        VaultStartup.Run(shell, test.App.Vaults, test.Picker);
        var page = (UnlockPageViewModel)shell.Current.Page;
        Assert.AreEqual(EncryptionText.LockedIntro, page.Intro);
        await page.UnlockCommand.ExecuteAsync("pas le bon");

        page.UseRecoveryKeyCommand.Execute(null);
        Assert.IsTrue(page.UsesRecoveryKey);
        Assert.IsFalse(page.UsesPassword);
        Assert.AreEqual(EncryptionText.RecoveryIntro, page.Intro);
        Assert.IsNull(page.Error, "changer de façon d'ouvrir efface la raison du refus précédent");
        await page.UnlockCommand.ExecuteAsync(string.Empty);
        Assert.AreEqual(EncryptionText.EnterRecoveryKey, page.Error);
        await page.UnlockCommand.ExecuteAsync("ABCD-EFGH");
        Assert.Contains("faute de frappe", page.Error ?? string.Empty);

        page.UsePasswordCommand.Execute(null);
        Assert.IsTrue(page.UsesPassword);
        Assert.IsNull(page.Error);
        page.UseRecoveryKeyCommand.Execute(null);
        await page.UnlockCommand.ExecuteAsync(TestVaults.RecoveryKey);

        Assert.IsFalse(shell.IsWelcome);
        Assert.AreEqual(test.Planned, test.App.Vaults.State().Vault);
    }

    [TestMethod]
    public async Task A_remembered_key_opens_it_at_the_next_start_without_the_screen()
    {
        using var test = new TestVaults(AppMode.Portable);
        test.CreateEncryptedVault(test.Planned);
        var shell = new TestShell().Shell;
        VaultStartup.Run(shell, test.App.Vaults, test.Picker);
        var page = (UnlockPageViewModel)shell.Current.Page;

        page.Remember = true;
        await page.UnlockCommand.ExecuteAsync(TestVaults.Password);
        Assert.IsTrue(File.Exists(Path.Combine(test.LocalData, "cles-retenues.json")));

        var next = test.Restart();
        var nextShell = new TestShell().Shell;
        Assert.AreEqual(test.Planned, VaultStartup.Run(nextShell, next.Vaults, test.Picker).Vault);
        Assert.IsFalse(nextShell.IsWelcome);
    }

    [TestMethod]
    public async Task Another_vault_can_be_opened_instead_encrypted_or_not()
    {
        using var test = new TestVaults(AppMode.Portable);
        test.CreateEncryptedVault(test.Planned);
        var other = test.CreateEncryptedVault(test.PathOf("Autre"));
        var plain = test.CreateVault("Clair");
        var shell = new TestShell().Shell;
        VaultStartup.Run(shell, test.App.Vaults, test.Picker);
        var page = (UnlockPageViewModel)shell.Current.Page;

        // Rien de choisi : rien ne change.
        test.Picker.Answers.Enqueue(null);
        await page.OtherVaultCommand.ExecuteAsync(null);
        Assert.AreSame(page, shell.Current.Page);
        Assert.AreEqual(ShellText.ChooseVaultFolder, test.Picker.Titles.Single());

        // Un dossier qui n'est pas un coffre : la raison s'affiche.
        test.Picker.Answers.Enqueue(test.Folder("Vide"));
        await page.OtherVaultCommand.ExecuteAsync(null);
        Assert.Contains("n'est pas un coffre", page.Error ?? string.Empty);

        // Un autre coffre chiffré : son propre écran.
        test.Picker.Answers.Enqueue(other);
        await page.OtherVaultCommand.ExecuteAsync(null);
        var otherPage = (UnlockPageViewModel)shell.Current.Page;
        Assert.AreNotSame(page, otherPage);
        Assert.AreEqual(other, otherPage.Vault);

        // Un coffre en clair : il s'ouvre.
        test.Picker.Answers.Enqueue(plain);
        await otherPage.OtherVaultCommand.ExecuteAsync(null);
        Assert.IsFalse(shell.IsWelcome);
        Assert.AreEqual(plain, test.App.Vaults.State().Vault);
    }

    [TestMethod]
    public async Task From_the_welcome_an_encrypted_vault_shows_its_unlock_screen()
    {
        using var test = new TestVaults(AppMode.Development);
        var encrypted = test.CreateEncryptedVault(test.PathOf("Chiffré"));
        var shell = new TestShell().Shell;
        VaultStartup.Run(shell, test.App.Vaults, test.Picker);
        var welcome = (WelcomePageViewModel)shell.Current.Page;

        test.Picker.Answers.Enqueue(encrypted);
        await welcome.OpenCommand.ExecuteAsync(null);

        var page = (UnlockPageViewModel)shell.Current.Page;
        Assert.AreEqual(encrypted, page.Vault);
        Assert.IsFalse(page.CanGoBack, "aucun coffre n'est ouvert derrière");
        await page.UnlockCommand.ExecuteAsync(TestVaults.Password);
        Assert.IsFalse(shell.IsWelcome);
    }

    [TestMethod]
    public async Task From_the_settings_the_open_vault_stays_open_behind_the_unlock_screen()
    {
        using var test = new TestVaults(AppMode.Development);
        var current = test.PathOf("Coffre");
        Assert.AreEqual(current, test.App.Vaults.Create(current).Vault);
        var encrypted = test.CreateEncryptedVault(test.PathOf("Chiffré"));
        var shell = new TestShell().Shell;
        shell.ShowWorkspace();
        var changes = 0;
        var settings = new DataSettingsViewModel(test.App.Vaults, test.App.Backups, test.App.Trash, test.App.Annotations, test.App.Exports, test.App.Settings, test.Picker, new FakeOpener(), () => changes++)
        {
            VaultLocked = state => VaultStartup.ShowUnlock(shell, test.App.Vaults, test.Picker, state, () => changes++),
        };

        test.Picker.Answers.Enqueue(encrypted);
        await settings.OpenVaultCommand.ExecuteAsync(null);

        Assert.IsTrue(shell.IsWelcome);
        var page = (UnlockPageViewModel)shell.Current.Page;
        Assert.AreEqual(current, test.App.Vaults.State().Vault, "le coffre ouvert le reste");
        Assert.IsTrue(page.CanGoBack);
        Assert.AreEqual(0, changes);

        // Y revenir ne change rien.
        page.BackCommand.Execute(null);
        Assert.IsFalse(shell.IsWelcome);
        Assert.AreSame(shell.Home, shell.Current);
        Assert.AreEqual(current, test.App.Vaults.State().Vault);

        // Déverrouillé, il le remplace, comme tout changement de coffre.
        test.Picker.Answers.Enqueue(encrypted);
        await settings.OpenVaultCommand.ExecuteAsync(null);
        await ((UnlockPageViewModel)shell.Current.Page).UnlockCommand.ExecuteAsync(TestVaults.Password);
        Assert.AreEqual(1, changes);
        Assert.AreEqual(encrypted, test.App.Vaults.State().Vault);
    }

    [TestMethod]
    public async Task Without_an_unlock_screen_the_settings_say_the_encrypted_vault_did_not_open()
    {
        using var test = new TestVaults(AppMode.Development);
        test.App.Vaults.Create(test.PathOf("Coffre"));
        var encrypted = test.CreateEncryptedVault(test.PathOf("Chiffré"));
        var settings = new DataSettingsViewModel(test.App.Vaults, test.App.Backups, test.App.Trash, test.App.Annotations, test.App.Exports, test.App.Settings, test.Picker, new FakeOpener(), () => { });

        test.Picker.Answers.Enqueue(encrypted);
        await settings.OpenVaultCommand.ExecuteAsync(null);

        Assert.AreEqual(EncryptionText.NotUnlocked, settings.Error);
        Assert.AreEqual(test.PathOf("Coffre"), test.App.Vaults.State().Vault);
    }

    [TestMethod]
    public void The_unlock_screen_closes_the_search_and_the_help_and_silences_the_open_vault()
    {
        using var test = new TestVaults(AppMode.Development);
        var shell = new TestShell().Shell;
        shell.ShowWorkspace();
        shell.OpenPalette();
        Assert.IsTrue(shell.IsPaletteOpen);
        shell.ShowHelp();
        Assert.IsTrue(shell.IsHelpOpen);

        VaultStartup.ShowUnlock(shell, test.App.Vaults, test.Picker, test.App.Vaults.State() with { Locked = test.PathOf("Coffre") });

        Assert.IsFalse(shell.IsPaletteOpen);
        Assert.IsFalse(shell.IsHelpOpen);
        Assert.IsTrue(shell.IsWelcome);
        Assert.IsNull(shell.SelectedMain);
        Assert.AreEqual(Icons.Lock, shell.Current.Glyph);
        Assert.ThrowsExactly<ArgumentNullException>(() => shell.ShowUnlock(null!));
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void The_unlock_screen_fits_a_narrow_window_names_its_fields_and_forgets_the_password(bool dark)
    {
        using var ui = new UiThread();
        using var test = new TestVaults(AppMode.Portable);
        test.CreateEncryptedVault(test.Planned);
        var shell = new TestShell(dark);
        VaultStartup.Run(shell.Shell, test.App.Vaults, test.Picker);
        var window = new ShellWindow(shell.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 470, Height = 540, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(shell.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            var scroller = Named<ScrollViewer>(window, "Scroller");
            Assert.IsLessThanOrEqualTo(scroller.ViewportWidth, scroller.ExtentWidth, "rien ne défile de côté");
            Assert.AreEqual(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(Named<TextBlock>(window, "Heading")));
            Assert.AreEqual(test.Planned, Named<TextBlock>(window, "VaultPath").Text);

            var password = Named<PasswordBox>(window, "PasswordBox");
            Assert.AreEqual(EncryptionText.Password, AutomationProperties.GetName(password));
            Assert.AreEqual(Visibility.Collapsed, Named<StackPanel>(window, "RecoveryPart").Visibility);
            UiThread.Until(() => password.IsFocused);
            foreach (var name in new[] { "UnlockButton", "LostPasswordButton", "OtherVaultButton" })
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(AutomationProperties.GetName(Named<ButtonBase>(window, name))), name);
            }
            Assert.AreEqual(Visibility.Collapsed, Named<Button>(window, "BackButton").Visibility, "aucun coffre n'est ouvert derrière");
            Assert.AreEqual(EncryptionText.Remember, AutomationProperties.GetName(Named<CheckBox>(window, "RememberBox")));
            Assert.IsFalse(Named<CheckBox>(window, "RememberBox").IsChecked);

            // Un mot de passe faux : le champ est vidé, la raison annoncée.
            password.Password = "pas le bon";
            Press(Named<Button>(window, "UnlockButton"));
            var page = (UnlockPageViewModel)shell.Shell.Current.Page;
            UiThread.Until(() => page.HasError);
            Assert.IsEmpty(password.Password, "le champ ne garde pas le mot de passe");
            Assert.IsTrue(Named<Notification>(window, "ErrorMessage").IsOpen);

            // La clé de récupération, à la place : son champ prend le focus.
            Press(Named<Button>(window, "LostPasswordButton"));
            TestShell.Flush();
            var recovery = Named<TextBox>(window, "RecoveryBox");
            Assert.AreEqual(Visibility.Collapsed, Named<StackPanel>(window, "PasswordPart").Visibility);
            UiThread.Until(() => recovery.IsFocused);
            Assert.AreEqual(EncryptionText.RecoveryHelp, AutomationProperties.GetHelpText(recovery));
            recovery.Text = TestVaults.RecoveryKey;
            Press(Named<Button>(window, "UnlockButton"));
            UiThread.Until(() => !shell.Shell.IsWelcome);
            Assert.AreEqual(test.Planned, test.App.Vaults.State().Vault);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_default_button_opens_the_vault_and_does_nothing_without_its_screen()
    {
        using var ui = new UiThread();
        using var test = new TestVaults(AppMode.Portable);
        test.CreateEncryptedVault(test.Planned);
        var shell = new TestShell();
        VaultStartup.Run(shell.Shell, test.App.Vaults, test.Picker);
        var window = new ShellWindow(shell.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 800, Height = 700, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(shell.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            Assert.IsTrue(Named<Button>(window, "UnlockButton").IsDefault, "Entrée ouvre le coffre");
            var alone = new UnlockPage { DataContext = "pas un écran de déverrouillage" };
            Press(Named<Button>(alone, "UnlockButton", visual: false));
            Assert.IsTrue(shell.Shell.IsWelcome, "sans son écran, rien ne s'ouvre");
            Named<PasswordBox>(window, "PasswordBox").Password = TestVaults.Password;
            Press(Named<Button>(window, "UnlockButton"));
            UiThread.Until(() => !shell.Shell.IsWelcome);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Comme un clic, ou un lecteur d'écran : le bouton est invoqué, sa commande ou son gestionnaire s'exécute.</summary>
    private static void Press(ButtonBase button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        TestShell.Flush();
    }

    private static T Named<T>(DependencyObject root, string name, bool visual = true)
        where T : FrameworkElement =>
        visual
            ? Descendants(root).OfType<T>().Single(element => element.Name == name)
            : (T)((FrameworkElement)root).FindName(name);

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
