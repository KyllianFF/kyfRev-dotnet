using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La carte « Chiffrement » de l'onglet « Données », sur de vrais coffres : elle ne se montre que pour un coffre chiffré ;
/// le délai du verrouillage s'enregistre aussitôt choisi ; la clé se retient avec le mot de passe et s'oublie aussitôt ; le
/// mot de passe ne change qu'avec l'actuel et un nouveau confirmé, assez long ; la nouvelle clé de récupération ne se montre
/// qu'une fois, et sa fenêtre ne se ferme qu'une fois la clé rangée ; tout s'oublie quand le coffre se verrouille.
/// </summary>
[TestClass]
public sealed class EncryptionSettingsTests
{
    private const string NewPassword = "un nouveau mot de passe";

    /// <summary>Une dérivation calibrée prend de l'ordre d'une seconde, davantage sur un poste chargé.</summary>
    private static readonly TimeSpan Slow = TimeSpan.FromSeconds(30);

    [TestMethod]
    public void The_card_checks_what_it_is_given()
    {
        using var vault = new CardVault();
        var app = vault.App;
        Action<string> copy = _ => { };

        Assert.ThrowsExactly<ArgumentNullException>(() => new EncryptionSettingsViewModel(null!, app.Settings, Encrypted, null, copy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new EncryptionSettingsViewModel(app.Encryption, null!, Encrypted, null, copy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new EncryptionSettingsViewModel(app.Encryption, app.Settings, null!, null, copy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new EncryptionSettingsViewModel(app.Encryption, app.Settings, Encrypted, null, null!));

        var card = new EncryptionSettingsViewModel(app.Encryption, app.Settings, Encrypted, null, copy);
        Assert.IsFalse(card.CanLockNow, "sans verrouillage donné, le bouton n'est pas proposé");
        Assert.IsFalse(card.LockNowCommand.CanExecute(null));
        Assert.IsFalse(card.IsEncrypted, "rien n'est lu avant la première ouverture de l'onglet");
        Assert.IsNull(card.LockAfter);
        Assert.IsEmpty(card.LockAfterChoices);
        Assert.AreEqual("Au moins 12 caractères.", EncryptionSettingsViewModel.PasswordLength);

        static bool Encrypted() => true;
    }

    [TestMethod]
    public void A_plaintext_vault_has_no_encryption_card()
    {
        using var vault = new CardVault(encrypted: false);

        vault.Card.Reload();

        Assert.IsFalse(vault.Card.IsEncrypted);
        Assert.IsFalse(vault.Card.IsRemembered);
        Assert.IsNull(vault.Card.LockAfter);
        Assert.IsEmpty(vault.Card.LockAfterChoices);
        Assert.IsNull(vault.Card.Error);
    }

    [TestMethod]
    public void An_encrypted_vault_shows_its_card_unremembered_with_the_default_delay()
    {
        using var vault = new CardVault();

        vault.Card.Reload();

        Assert.IsTrue(vault.Card.IsEncrypted);
        Assert.IsFalse(vault.Card.IsRemembered, "décoché par défaut");
        Assert.AreEqual(VaultPreferences.DefaultLockAfterMinutes, vault.Card.LockAfter!.Value);
        Assert.AreEqual("5 minutes, 15 minutes, 30 minutes, 1 heure, Jamais", Labels(vault.Card.LockAfterChoices));
        Assert.IsNull(vault.Card.Error);
        Assert.IsTrue(vault.Card.CanLockNow);
    }

    [TestMethod]
    public void The_delay_is_saved_as_soon_as_it_is_chosen_and_read_back()
    {
        using var vault = new CardVault();
        var card = vault.Card;
        card.Reload();

        card.LockAfter = card.LockAfterChoices.Single(choice => choice.Value == 30);
        Assert.AreEqual(30, vault.App.Settings.Get().Value.LockAfterMinutes, "enregistré aussitôt, comme la rotation");
        Assert.IsNull(card.Error);
        var other = vault.NewCard();
        other.Reload();
        Assert.AreEqual("30 minutes", other.LockAfter!.Label);

        card.LockAfter = card.LockAfterChoices.Single(choice => choice.Label == "Jamais");
        Assert.AreEqual(0, vault.App.Settings.Get().Value.LockAfterMinutes);

        // Un délai changé ailleurs se montre à la relecture.
        vault.App.Settings.Update(preferences => preferences with { LockAfterMinutes = 60 }).Value.ToString();
        card.Reload();
        Assert.AreEqual("1 heure", card.LockAfter!.Label);
        Assert.AreEqual(60, vault.App.Settings.Get().Value.LockAfterMinutes);

        // Il est celui du coffre : le démarrage suivant le retrouve.
        Assert.AreEqual(vault.Root, vault.Reopen(VaultSecret.Password(TestVaults.Password)).Vault);
        Assert.AreEqual(60, vault.App.Settings.Get().Value.LockAfterMinutes);
    }

    [TestMethod]
    [DataRow(0, "Jamais")]
    [DataRow(1, "1 minute")]
    [DataRow(5, "5 minutes")]
    [DataRow(60, "1 heure")]
    [DataRow(90, "90 minutes")]
    [DataRow(120, "2 heures")]
    public void A_delay_reads_in_french(int minutes, string expected)
    {
        Assert.AreEqual(expected, EncryptionSettingsViewModel.LabelOf(minutes));
    }

    [TestMethod]
    public void A_delay_set_elsewhere_keeps_its_place_among_the_choices()
    {
        Assert.AreEqual("5 minutes, 10 minutes, 15 minutes, 30 minutes, 1 heure, Jamais", Labels(EncryptionSettingsViewModel.ChoicesWith(10)));
        Assert.AreEqual("5 minutes, 15 minutes, 30 minutes, 1 heure, 2 heures, Jamais", Labels(EncryptionSettingsViewModel.ChoicesWith(120)));
        Assert.AreEqual("5 minutes, 15 minutes, 30 minutes, 1 heure, Jamais", Labels(EncryptionSettingsViewModel.ChoicesWith(15)));
    }

    [TestMethod]
    public async Task Remembering_asks_the_password_and_forgetting_is_at_once()
    {
        using var vault = new CardVault();
        var card = vault.Card;
        card.Reload();

        // Cochée, la case demande le mot de passe ; la fenêtre fermée sans, rien n'est retenu et la case se décoche.
        card.IsRemembered = true;
        Assert.IsTrue(card.IsAskingRemember);
        Assert.IsFalse(vault.App.Encryption.IsRemembered);
        await card.AnswerRememberCommand.ExecuteAsync(null);
        Assert.IsFalse(card.IsAskingRemember);
        Assert.IsFalse(card.IsRemembered);
        Assert.IsFalse(vault.App.Encryption.IsRemembered);

        // Rien de tapé, puis un mauvais mot de passe : la fenêtre se rouvre, avec sa raison.
        card.IsRemembered = true;
        await card.AnswerRememberCommand.ExecuteAsync(string.Empty);
        Assert.AreEqual(EncryptionText.EnterPassword, card.DialogError);
        Assert.IsTrue(card.IsAskingRemember);
        await card.AnswerRememberCommand.ExecuteAsync("pas le bon");
        Assert.IsTrue(card.HasDialogError);
        Assert.AreNotEqual(EncryptionText.EnterPassword, card.DialogError);
        Assert.IsTrue(card.IsAskingRemember, "un refus garde la fenêtre ouverte");
        Assert.IsFalse(card.IsBusy);
        Assert.IsFalse(vault.App.Encryption.IsRemembered);

        var remembering = card.AnswerRememberCommand.ExecuteAsync(TestVaults.Password);
        Assert.IsTrue(card.IsBusy, "la clé se tire du mot de passe hors du fil de l'interface");
        Assert.IsFalse(card.IsIdle);
        await remembering;
        Assert.IsFalse(card.IsBusy);
        Assert.IsFalse(card.IsAskingRemember);
        Assert.IsNull(card.DialogError);
        Assert.IsTrue(card.IsRemembered);
        Assert.AreEqual(EncryptionText.Remembered, card.Message);
        Assert.IsTrue(vault.App.Encryption.IsRemembered);
        Assert.IsTrue(File.Exists(Path.Combine(vault.Vaults.LocalData, "cles-retenues.json")));
        var other = vault.NewCard();
        other.Reload();
        Assert.IsTrue(other.IsRemembered, "relue, la case est cochée");

        // Décochée, la clé est oubliée aussitôt, sans rien demander : le démarrage suivant demande le mot de passe.
        card.IsRemembered = false;
        Assert.IsFalse(card.IsAskingRemember);
        Assert.IsFalse(vault.App.Encryption.IsRemembered);
        Assert.AreEqual(EncryptionText.Forgotten, card.Message);
        Assert.IsNull(card.Error);
        Assert.AreEqual(vault.Root, vault.Vaults.Restart().Vaults.OpenAtStartup().Locked);
    }

    [TestMethod]
    public async Task The_password_changes_only_with_the_current_one_and_a_confirmed_long_enough_one()
    {
        using var vault = new CardVault();
        var card = vault.Card;
        card.Reload();

        // Fermée sans, rien ne change.
        card.RequestChangePasswordCommand.Execute(null);
        Assert.IsTrue(card.IsChangingPassword);
        await card.AnswerChangePasswordCommand.ExecuteAsync(null);
        Assert.IsFalse(card.IsChangingPassword);
        Assert.IsNull(card.Message);

        card.RequestChangePasswordCommand.Execute(null);
        await card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange(string.Empty, NewPassword, NewPassword));
        Assert.AreEqual(EncryptionText.EnterCurrentPassword, card.DialogError);
        Assert.IsTrue(card.IsChangingPassword, "un refus garde la fenêtre ouverte");

        await card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange("pas le bon", NewPassword, NewPassword));
        Assert.IsTrue(card.HasDialogError);
        Assert.IsTrue(card.IsChangingPassword);

        await card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange(TestVaults.Password, NewPassword, "un autre mot de passe"));
        Assert.AreEqual("Les deux mots de passe ne sont pas les mêmes.", card.DialogError);
        Assert.IsTrue(card.IsChangingPassword);

        await card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange(TestVaults.Password, "trop court", "trop court"));
        Assert.AreEqual("Le mot de passe doit faire au moins 12 caractères.", card.DialogError);
        Assert.IsTrue(card.IsChangingPassword);
        Assert.IsNull(card.Message);

        var changing = card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange(TestVaults.Password, NewPassword, NewPassword));
        Assert.IsTrue(card.IsBusy);
        await changing;
        Assert.IsFalse(card.IsBusy);
        Assert.IsFalse(card.IsChangingPassword);
        Assert.IsNull(card.DialogError);
        Assert.AreEqual(EncryptionText.PasswordChanged, card.Message);

        // Au démarrage suivant, l'ancien n'ouvre plus le coffre, le nouveau si ; la clé de récupération reste valable.
        var refused = vault.Reopen(VaultSecret.Password(TestVaults.Password));
        Assert.IsNull(refused.Vault);
        Assert.AreEqual(vault.Root, refused.Locked);
        Assert.AreEqual(vault.Root, vault.App.Vaults.Unlock(vault.Root, VaultSecret.Password(NewPassword), remember: false).Vault);
        Assert.AreEqual(vault.Root, vault.Reopen(VaultSecret.RecoveryKey(TestVaults.RecoveryKey)).Vault);
    }

    [TestMethod]
    public async Task A_new_recovery_key_shows_once_and_its_window_closes_only_once_it_is_stored()
    {
        using var vault = new CardVault();
        var card = vault.Card;
        card.Reload();

        // Fermée sans, rien n'est créé.
        card.RequestNewRecoveryKeyCommand.Execute(null);
        Assert.IsTrue(card.IsAskingRecoveryKey);
        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(null);
        Assert.IsFalse(card.IsAskingRecoveryKey);
        Assert.IsFalse(card.IsShowingRecoveryKey);

        card.RequestNewRecoveryKeyCommand.Execute(null);
        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(string.Empty);
        Assert.AreEqual(EncryptionText.EnterPassword, card.DialogError);
        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync("pas le bon");
        Assert.IsTrue(card.HasDialogError);
        Assert.IsTrue(card.IsAskingRecoveryKey, "un refus garde la fenêtre ouverte");
        Assert.IsFalse(card.IsShowingRecoveryKey);
        Assert.IsEmpty(card.RecoveryKey);

        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(TestVaults.Password);
        Assert.IsFalse(card.IsAskingRecoveryKey);
        Assert.IsTrue(card.IsShowingRecoveryKey);
        var key = card.RecoveryKey;
        var groups = key.Split('-');
        Assert.HasCount(14, groups);
        Assert.IsTrue(groups.All(group => group.Length == 4), "14 groupes de 4");
        Assert.AreNotEqual(TestVaults.RecoveryKey, key);
        Assert.IsFalse(card.IsKeyStored, "décochée à chaque nouvelle clé");

        // Copiée ; puis le presse-papiers refuse : la clé reste affichée, à recopier à la main.
        card.CopyRecoveryKeyCommand.Execute(null);
        Assert.AreEqual(key, vault.Copied.Single());
        Assert.AreEqual(EncryptionText.KeyCopied, card.KeyMessage);
        vault.ClipboardRefuses = true;
        card.CopyRecoveryKeyCommand.Execute(null);
        Assert.AreEqual(EncryptionText.KeyNotCopied, card.KeyMessage);
        Assert.AreEqual(key, card.RecoveryKey);

        // Sans « J'ai rangé ma clé de récupération », la fenêtre ne se ferme pas.
        card.CloseRecoveryKeyCommand.Execute(null);
        Assert.IsTrue(card.IsShowingRecoveryKey);
        Assert.AreEqual(EncryptionText.KeyStoredFirst, card.KeyMessage);
        Assert.AreEqual(key, card.RecoveryKey);

        card.IsKeyStored = true;
        card.CloseRecoveryKeyCommand.Execute(null);
        Assert.IsFalse(card.IsShowingRecoveryKey);
        Assert.IsEmpty(card.RecoveryKey, "la clé n'est plus nulle part dans la carte");
        Assert.IsFalse(card.IsKeyStored);
        Assert.IsNull(card.KeyMessage);
        Assert.AreEqual(EncryptionText.RecoveryKeyCreated, card.Message);

        // L'ancienne n'ouvre plus le coffre, la nouvelle si.
        Assert.IsNull(vault.Reopen(VaultSecret.RecoveryKey(TestVaults.RecoveryKey)).Vault);
        Assert.AreEqual(vault.Root, vault.App.Vaults.Unlock(vault.Root, VaultSecret.RecoveryKey(key), remember: false).Vault);
    }

    [TestMethod]
    public async Task Lock_now_calls_what_locks_the_vault()
    {
        using var vault = new CardVault();
        vault.Card.Reload();

        Assert.IsTrue(vault.Card.LockNowCommand.CanExecute(null));
        await vault.Card.LockNowCommand.ExecuteAsync(null);

        Assert.AreEqual(1, vault.Locks);
    }

    [TestMethod]
    public async Task Forgetting_drops_everything_until_the_tab_reads_again()
    {
        using var vault = new CardVault();
        var data = vault.DataTab();
        data.OnNavigatedTo();
        await data.ReloadCommand.ExecutionTask!;
        var card = vault.Card;
        Assert.IsTrue(card.IsEncrypted, "l'onglet relit sa carte");

        // Le délai, la clé retenue, la clé de récupération montrée et copiée, une fenêtre rouverte sur un refus.
        card.LockAfter = card.LockAfterChoices.Single(choice => choice.Value == 5);
        card.IsRemembered = true;
        await card.AnswerRememberCommand.ExecuteAsync(TestVaults.Password);
        card.RequestNewRecoveryKeyCommand.Execute(null);
        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(TestVaults.Password);
        card.CopyRecoveryKeyCommand.Execute(null);
        card.IsKeyStored = true;
        card.RequestChangePasswordCommand.Execute(null);
        await card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange(string.Empty, NewPassword, NewPassword));
        Assert.IsTrue(card.IsRemembered);
        Assert.IsTrue(card.IsShowingRecoveryKey);
        Assert.IsNotEmpty(card.RecoveryKey);
        Assert.IsTrue(card.HasKeyMessage);
        Assert.IsTrue(card.IsChangingPassword);
        Assert.IsTrue(card.HasDialogError);

        data.Forget();

        Assert.IsFalse(card.IsEncrypted);
        Assert.IsFalse(card.IsRemembered);
        Assert.IsNull(card.LockAfter);
        Assert.IsEmpty(card.LockAfterChoices);
        Assert.IsFalse(card.IsAskingRemember);
        Assert.IsFalse(card.IsChangingPassword);
        Assert.IsFalse(card.IsAskingRecoveryKey);
        Assert.IsFalse(card.IsShowingRecoveryKey);
        Assert.IsEmpty(card.RecoveryKey);
        Assert.IsFalse(card.IsKeyStored);
        Assert.IsNull(card.KeyMessage);
        Assert.IsNull(card.DialogError);
        Assert.IsNull(card.Error);
        Assert.IsNull(card.Message);
        Assert.IsTrue(vault.App.Encryption.IsRemembered, "oublier ce que montre la carte n'oublie pas la clé retenue");
        Assert.AreEqual(5, vault.App.Settings.Get().Value.LockAfterMinutes, "ni le délai");

        // Une réponse arrivée après l'oubli ne fait rien.
        card.CloseRecoveryKeyCommand.Execute(null);
        await card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange(TestVaults.Password, NewPassword, NewPassword));
        await card.AnswerRememberCommand.ExecuteAsync(null);
        Assert.IsNull(card.Message);
        Assert.IsFalse(card.IsBusy);
        Assert.IsTrue(vault.App.Encryption.IsRemembered);

        data.OnNavigatedTo();
        await data.ReloadCommand.ExecutionTask!;
        Assert.IsTrue(card.IsEncrypted);
        Assert.IsTrue(card.IsRemembered);
        Assert.AreEqual(5, card.LockAfter!.Value);
        Assert.IsFalse(card.IsShowingRecoveryKey);
    }

    [TestMethod]
    public async Task A_key_created_while_forgetting_is_not_shown_and_the_next_reading_says_so()
    {
        using var vault = new CardVault();
        var card = vault.Card;
        card.Reload();
        card.RequestNewRecoveryKeyCommand.Execute(null);

        var creating = card.AnswerNewRecoveryKeyCommand.ExecuteAsync(TestVaults.Password);
        card.Forget();
        await creating;

        Assert.IsFalse(card.IsShowingRecoveryKey, "le coffre verrouillé ne montre pas de clé");
        Assert.IsEmpty(card.RecoveryKey);
        Assert.IsNull(card.Error);
        Assert.IsFalse(card.IsBusy);
        card.Reload();
        Assert.AreEqual(EncryptionText.RecoveryKeyUnseen, card.Error, "l'ancienne clé n'ouvre plus le coffre : la carte le dit");
        card.Reload();
        Assert.IsNull(card.Error, "une seule fois");
        Assert.IsNull(vault.Reopen(VaultSecret.RecoveryKey(TestVaults.RecoveryKey)).Vault);
    }

    [STATestMethod]
    public void The_card_is_hidden_for_a_plaintext_vault_and_without_encryption()
    {
        using var ui = new UiThread();
        using var vault = new CardVault(encrypted: false);
        var data = vault.DataTab();
        var page = new DataSettingsPage { DataContext = data };
        var window = Host(page);
        try
        {
            data.OnNavigatedTo();
            UiThread.Until(() => data.IsLoaded);
            TestShell.Flush();
            Assert.IsTrue(page.VaultCard.IsVisible);
            Assert.AreEqual(Visibility.Collapsed, page.EncryptionCard.Visibility, "un coffre en clair n'a pas de carte « Chiffrement »");

            var app = vault.App;
            page.DataContext = new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, vault.Vaults.Picker, new FakeOpener(), () => { });
            TestShell.Flush();
            Assert.AreEqual(Visibility.Collapsed, page.EncryptionCard.Visibility, "assemblé sans chiffrement, l'onglet ne la propose pas");
            Assert.IsFalse(page.RememberDialog.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void The_card_and_its_windows_fit_a_narrow_window_and_name_their_controls(bool dark)
    {
        using var ui = new UiThread();
        using var vault = new CardVault();
        var app = vault.App;
        var data = vault.DataTab();
        var hub = new SettingsHubViewModel(
            new GeneralSettingsViewModel(new MachineSettings(new MemoryMachinePreferences()), new FakeVoices("Microsoft Hortense Desktop")),
            new SettingsPageViewModel(app.Settings, TimeProvider.System),
            new ModulesSettingsViewModel(app.Settings, _ => { }, FakeExtensions.Settings()),
            data,
            new AboutSettingsViewModel(app.Vaults));
        var test = new TestShell(dark);
        test.Shell.Navigate(new NavigationItem("parametres", SettingsText.Title, Icons.Settings, int.MaxValue, hub));
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 470, Height = 540, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            hub.Tabs[3].IsSelected = true;
            UiThread.Until(() => data.IsLoaded && vault.Card.IsEncrypted);
            TestShell.Flush();
            var page = Descendants(window).OfType<DataSettingsPage>().Single();
            Assert.IsTrue(page.EncryptionCard.IsVisible);
            Assert.AreEqual(AutomationHeadingLevel.Level2, AutomationProperties.GetHeadingLevel(page.EncryptionTitle));
            Assert.AreEqual(Visibility.Visible, page.LockNowButton.Visibility);
            AssertFits(page.Scroller);
            AssertNamed(page.EncryptionCard);
            Assert.AreEqual(EncryptionText.LockAfter, AutomationProperties.GetName(page.LockAfterChoice));
            Assert.AreEqual(EncryptionText.Remember, AutomationProperties.GetName(page.RememberKeyBox));

            // Chaque fenêtre tient dans la largeur, et chacun de ses champs et boutons a son nom.
            page.RememberKeyBox.IsChecked = true;
            TestShell.Flush();
            AssertDialogFits(page.RememberDialog);
            page.RememberDialog.Close(DialogChoice.Secondary);
            TestShell.Flush();
            Assert.IsFalse(page.RememberKeyBox.IsChecked, "fermée sans, rien n'est retenu");

            Press(page.ChangePasswordButton);
            AssertDialogFits(page.ChangePasswordDialog);
            page.ChangePasswordDialog.Close(DialogChoice.Secondary);

            Press(page.NewRecoveryKeyButton);
            AssertDialogFits(page.RecoveryPromptDialog);
            page.RecoveryPasswordBox.Password = TestVaults.Password;
            page.RecoveryPromptDialog.Close(DialogChoice.Primary);
            UiThread.Until(() => vault.Card.IsShowingRecoveryKey, Slow);
            TestShell.Flush();
            AssertDialogFits(page.RecoveryKeyDialog);
            Assert.AreEqual(vault.Card.RecoveryKey, page.RecoveryKeyText.Text);
            Assert.IsTrue(page.RecoveryKeyText.IsReadOnly);
            Assert.AreEqual(page.FindResource("Kyf.Font.Code"), page.RecoveryKeyText.FontFamily, "la clé s'affiche en chasse fixe");
            Assert.IsTrue(page.OldRecoveryKeyWarning.IsVisible, "l'ancienne clé n'ouvre plus le coffre, mais les anciennes sauvegardes");
            Assert.AreEqual(Visibility.Collapsed, ((Button)page.RecoveryKeyDialog.Template.FindName(Dialog.SecondaryButtonPart, page.RecoveryKeyDialog)).Visibility, "rien ne l'annule");

            Press(page.CopyRecoveryKeyButton);
            Assert.AreEqual(vault.Card.RecoveryKey, vault.Copied.Single());
            Assert.IsTrue(page.RecoveryKeyStatus.IsVisible);
            page.RecoveryKeyDialog.Close(DialogChoice.Primary);
            TestShell.Flush();
            Assert.IsTrue(page.RecoveryKeyDialog.IsOpen, "la clé n'est pas rangée : la fenêtre reste");
            page.KeyStoredBox.IsChecked = true;
            page.RecoveryKeyDialog.Close(DialogChoice.Primary);
            TestShell.Flush();
            Assert.IsFalse(page.RecoveryKeyDialog.IsOpen);
            Assert.IsEmpty(page.RecoveryKeyText.Text, "la clé ne reste pas dans la vue");
            AssertFits(page.Scroller);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_windows_pass_their_passwords_to_the_card_and_keep_nothing_typed()
    {
        using var ui = new UiThread();
        using var vault = new CardVault();
        var data = vault.DataTab();
        var page = new DataSettingsPage { DataContext = data };
        var window = Host(page);
        try
        {
            data.OnNavigatedTo();
            UiThread.Until(() => data.IsLoaded);
            TestShell.Flush();
            var card = vault.Card;

            // Retenir la clé : un mot de passe faux rouvre la fenêtre, son champ vidé ; le bon la retient.
            page.RememberKeyBox.IsChecked = true;
            TestShell.Flush();
            Assert.IsTrue(page.RememberDialog.IsOpen);
            page.RememberPasswordBox.Password = "pas le bon";
            page.RememberDialog.Close(DialogChoice.Primary);
            Assert.IsEmpty(page.RememberPasswordBox.Password, "le champ ne garde pas le mot de passe");
            UiThread.Until(() => card.IsIdle && card.IsAskingRemember, Slow);
            TestShell.Flush();
            Assert.IsTrue(page.RememberDialog.IsOpen, "un refus rouvre la fenêtre");
            Assert.IsTrue(page.RememberError.IsOpen);
            page.RememberPasswordBox.Password = TestVaults.Password;
            page.RememberDialog.Close(DialogChoice.Primary);
            UiThread.Until(() => card.IsIdle && !card.IsAskingRemember, Slow);
            TestShell.Flush();
            Assert.IsFalse(page.RememberDialog.IsOpen);
            Assert.IsTrue(page.RememberKeyBox.IsChecked);
            Assert.IsTrue(vault.App.Encryption.IsRemembered);
            Assert.IsEmpty(page.RememberPasswordBox.Password);

            // Changer le mot de passe : deux saisies différentes rouvrent la fenêtre, ses trois champs vidés.
            Press(page.ChangePasswordButton);
            Assert.IsTrue(page.ChangePasswordDialog.IsOpen);
            page.CurrentPasswordBox.Password = TestVaults.Password;
            page.NewPasswordBox.Password = NewPassword;
            page.ConfirmPasswordBox.Password = "un autre mot de passe";
            page.ChangePasswordDialog.Close(DialogChoice.Primary);
            Assert.IsEmpty(page.CurrentPasswordBox.Password);
            Assert.IsEmpty(page.NewPasswordBox.Password);
            Assert.IsEmpty(page.ConfirmPasswordBox.Password);
            UiThread.Until(() => card.IsIdle && card.IsChangingPassword, Slow);
            TestShell.Flush();
            Assert.IsTrue(page.ChangePasswordDialog.IsOpen);
            Assert.AreEqual("Les deux mots de passe ne sont pas les mêmes.", card.DialogError);
            Assert.IsTrue(page.ChangePasswordError.IsOpen);

            // Le coffre se verrouille pendant la saisie : la fenêtre se ferme, et rien de ce qui y a été tapé ne reste.
            page.CurrentPasswordBox.Password = TestVaults.Password;
            page.NewPasswordBox.Password = NewPassword;
            data.Forget();
            TestShell.Flush();
            Assert.IsFalse(page.ChangePasswordDialog.IsOpen);
            Assert.IsEmpty(page.CurrentPasswordBox.Password);
            Assert.IsEmpty(page.NewPasswordBox.Password);
            Assert.AreEqual(Visibility.Collapsed, page.EncryptionCard.Visibility);

            // Relue, la carte revient ; « Verrouiller maintenant » verrouille, et décochée, la clé est oubliée aussitôt.
            data.OnNavigatedTo();
            UiThread.Until(() => data.IsLoaded && card.IsEncrypted);
            TestShell.Flush();
            Assert.IsTrue(page.EncryptionCard.IsVisible);
            Assert.IsTrue(page.RememberKeyBox.IsChecked);
            Press(page.LockNowButton);
            Assert.AreEqual(1, vault.Locks);
            page.RememberKeyBox.IsChecked = false;
            TestShell.Flush();
            Assert.IsFalse(page.RememberDialog.IsOpen);
            Assert.IsFalse(vault.App.Encryption.IsRemembered);
            Assert.IsTrue(page.EncryptionDone.IsOpen);

            page.DataContext = null;
            page.RememberDialog.Close(DialogChoice.None);
            page.RecoveryKeyDialog.Close(DialogChoice.None);
        }
        finally
        {
            window.Close();
        }
    }

    private static string Labels(IEnumerable<SettingChoice<int>> choices) => string.Join(", ", choices.Select(choice => choice.Label));

    /// <summary>La vue dans une fenêtre hors de l'écran, aux ressources de l'application.</summary>
    private static Window Host(FrameworkElement page)
    {
        var test = new TestShell();
        var host = new Border { Width = 900, Height = 900, Child = page };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 1000, Height = 1000, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        TestShell.Flush();
        return window;
    }

    /// <summary>Rien ne dépasse à droite de la zone qui défile : ni champ, ni bouton, ni texte.</summary>
    private static void AssertFits(ScrollViewer scroller)
    {
        var content = (FrameworkElement)scroller.Content;
        foreach (var element in Descendants(content).OfType<FrameworkElement>().Where(element => element.IsVisible && element is Control or TextBlock))
        {
            var right = element.TranslatePoint(new Point(element.ActualWidth, 0), scroller).X;
            Assert.IsLessThanOrEqualTo(scroller.ViewportWidth + 0.5, right, $"{element.Name} dépasse à droite");
        }
    }

    /// <summary>La fenêtre est ouverte, tient dans la vue sans défiler de côté, et chacun de ses champs et boutons a son nom.</summary>
    private static void AssertDialogFits(Dialog dialog)
    {
        TestShell.Flush();
        Assert.IsTrue(dialog.IsOpen, dialog.Name);
        AssertFits((ScrollViewer)dialog.Template.FindName("PART_Content", dialog));
        foreach (var element in Descendants(dialog).OfType<FrameworkElement>().Where(element => element.IsVisible && element is Control or TextBlock))
        {
            Assert.IsGreaterThanOrEqualTo(-0.5, element.TranslatePoint(default, dialog).X, $"{element.Name} dépasse à gauche de {dialog.Name}");
            Assert.IsLessThanOrEqualTo(dialog.ActualWidth + 0.5, element.TranslatePoint(new Point(element.ActualWidth, 0), dialog).X, $"{element.Name} dépasse à droite de {dialog.Name}");
        }
        AssertNamed(dialog);
    }

    /// <summary>Chaque champ, bouton, case et liste visible a un nom pour le lecteur d'écran, et un nom dans la vue.</summary>
    private static void AssertNamed(DependencyObject root)
    {
        var controls = Descendants(root).OfType<Control>().Where(control => control is ButtonBase or TextBox or PasswordBox or ComboBox && control.IsVisible && control.TemplatedParent is not (ComboBox or ScrollBar)).ToList();
        Assert.IsNotEmpty(controls);
        foreach (var control in controls)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(control.Name), $"Sans x:Name : {control.GetType().Name}");
            var name = UIElementAutomationPeer.CreatePeerForElement(control).GetName();
            Assert.IsFalse(string.IsNullOrWhiteSpace(name), $"Sans nom : {control.GetType().Name} « {control.Name} »");
            Assert.DoesNotContain("KyfRev.", name, $"Le nom d'une classe : {name}");
        }
    }

    /// <summary>Comme un clic, ou un lecteur d'écran : le bouton est invoqué, sa commande s'exécute.</summary>
    private static void Press(ButtonBase button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        TestShell.Flush();
    }

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

    /// <summary>Un coffre ouvert, chiffré ou en clair, et sa carte « Chiffrement » telle que l'application la compose.</summary>
    private sealed class CardVault : IDisposable
    {
        public CardVault(bool encrypted = true)
        {
            Vaults = new TestVaults(AppMode.Development);
            if (encrypted)
            {
                Root = Vaults.CreateEncryptedVault(Vaults.PathOf("Coffre"));
                Assert.AreEqual(Root, App.Vaults.Unlock(Root, VaultSecret.Password(TestVaults.Password), remember: false).Vault);
            }
            else
            {
                Root = Vaults.CreateVault("Coffre");
                Assert.AreEqual(Root, App.Vaults.Open(Root).Vault);
            }
            Card = NewCard();
        }

        public TestVaults Vaults { get; }

        public string Root { get; }

        public KyfRevApplication App => Vaults.App;

        public EncryptionSettingsViewModel Card { get; }

        /// <summary>Ce qui a été copié dans le presse-papiers.</summary>
        public List<string> Copied { get; } = [];

        /// <summary>Le presse-papiers est pris par un autre programme : la copie échoue.</summary>
        public bool ClipboardRefuses { get; set; }

        /// <summary>Le nombre de verrouillages demandés.</summary>
        public int Locks { get; private set; }

        /// <summary>Une carte sur l'application du moment, comme la compose la racine de composition.</summary>
        public EncryptionSettingsViewModel NewCard()
        {
            var app = App;
            return new(app.Encryption, app.Settings, () => app.Vaults.State().Encrypted, LockAsync, Copy);
        }

        /// <summary>L'onglet « Données » avec sa carte.</summary>
        public DataSettingsViewModel DataTab()
        {
            var app = App;
            return new(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, Vaults.Picker, new FakeOpener(), () => { })
            {
                Encryption = Card,
            };
        }

        /// <summary>Le démarrage suivant, puis le coffre ouvert avec <paramref name="secret"/> : l'état de l'ouverture.</summary>
        public AppState Reopen(VaultSecret secret) => Vaults.Restart().Vaults.Unlock(Root, secret, remember: false);

        public void Dispose() => Vaults.Dispose();

        private Task LockAsync()
        {
            Locks++;
            return Task.CompletedTask;
        }

        private void Copy(string text)
        {
            if (ClipboardRefuses)
            {
                throw new System.ComponentModel.Win32Exception("Le presse-papiers est pris par un autre programme.");
            }
            Copied.Add(text);
        }
    }
}
