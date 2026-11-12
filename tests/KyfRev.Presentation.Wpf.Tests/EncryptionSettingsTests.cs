using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
    public async Task A_new_recovery_key_shows_once_and_replaces_the_old_one_only_once_stored_and_saved()
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

        var before = vault.HeaderBytes();
        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(TestVaults.Password);
        Assert.IsFalse(card.IsAskingRecoveryKey);
        Assert.IsTrue(card.IsShowingRecoveryKey);
        CollectionAssert.AreEqual(before, vault.HeaderBytes(), "préparée, elle n'a encore rien changé");
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

        // Sans « J'ai rangé ma clé de récupération », elle ne s'enregistre pas.
        await card.SaveRecoveryKeyCommand.ExecuteAsync(null);
        Assert.IsTrue(card.IsShowingRecoveryKey);
        Assert.AreEqual(EncryptionText.KeyStoredFirst, card.KeyMessage);
        Assert.AreEqual(key, card.RecoveryKey);
        CollectionAssert.AreEqual(before, vault.HeaderBytes());

        card.IsKeyStored = true;
        var saving = card.SaveRecoveryKeyCommand.ExecuteAsync(null);
        Assert.AreEqual(EncryptionText.SavingRecoveryKey, card.BusyMessage);
        await saving;
        Assert.IsFalse(card.IsBusy);
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

        // Une réponse arrivée après l'oubli ne fait rien, et la clé préparée n'a rien changé.
        await card.SaveRecoveryKeyCommand.ExecuteAsync(null);
        card.CancelRecoveryKeyCommand.Execute(null);
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
    public async Task Cancelling_or_locking_a_new_recovery_key_changes_nothing()
    {
        using var vault = new CardVault();
        var card = vault.Card;
        card.Reload();
        var before = vault.HeaderBytes();

        // Annulée, même rangée : l'ancienne reste la bonne, et la carte le dit.
        card.RequestNewRecoveryKeyCommand.Execute(null);
        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(TestVaults.Password);
        card.IsKeyStored = true;
        card.CancelRecoveryKeyCommand.Execute(null);
        Assert.IsFalse(card.IsShowingRecoveryKey);
        Assert.IsEmpty(card.RecoveryKey);
        Assert.IsFalse(card.IsKeyStored);
        Assert.AreEqual(EncryptionText.RecoveryKeyKept, card.Message);
        card.CancelRecoveryKeyCommand.Execute(null);
        await card.SaveRecoveryKeyCommand.ExecuteAsync(null);
        Assert.AreEqual(EncryptionText.RecoveryKeyKept, card.Message, "fermée, la fenêtre ne répond plus");
        CollectionAssert.AreEqual(before, vault.HeaderBytes(), "rien n'est écrit");

        // Le coffre se verrouille pendant qu'elle est montrée, rangée ou non : rien n'est écrit, rien n'est à dire.
        foreach (var stored in new[] { false, true })
        {
            card.RequestNewRecoveryKeyCommand.Execute(null);
            await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(TestVaults.Password);
            card.IsKeyStored = stored;
            card.Forget();
            Assert.IsFalse(card.IsShowingRecoveryKey);
            Assert.IsEmpty(card.RecoveryKey);
            await card.SaveRecoveryKeyCommand.ExecuteAsync(null);
            card.Reload();
            Assert.IsNull(card.Error);
            Assert.IsNull(card.Message);
        }

        // Préparée pendant le verrouillage : elle ne se montre pas, et rien n'est écrit.
        card.RequestNewRecoveryKeyCommand.Execute(null);
        var preparing = card.AnswerNewRecoveryKeyCommand.ExecuteAsync(TestVaults.Password);
        card.Forget();
        await preparing;
        Assert.IsFalse(card.IsShowingRecoveryKey);
        Assert.IsFalse(card.IsBusy);
        card.Reload();
        Assert.IsNull(card.Error);

        CollectionAssert.AreEqual(before, vault.HeaderBytes(), "l'en-tête n'a pas changé");
        Assert.AreEqual(vault.Root, vault.Reopen(VaultSecret.RecoveryKey(TestVaults.RecoveryKey)).Vault, "l'ancienne clé ouvre toujours le coffre");
    }

    [TestMethod]
    public async Task A_key_whose_saving_a_lock_interrupts_is_said_at_the_next_reading_of_its_vault()
    {
        using var vault = new CardVault();
        var card = vault.Card;
        card.Reload();
        // Le verrouillage commence dès que l'écriture part.
        var lockWhenBusy = false;
        card.PropertyChanged += (_, e) =>
        {
            if (lockWhenBusy && e.PropertyName == nameof(EncryptionSettingsViewModel.IsBusy) && card.IsBusy)
            {
                lockWhenBusy = false;
                card.Forget();
            }
        };

        // Écrite pendant le verrouillage : la prochaine lecture le dit, une fois.
        card.RequestNewRecoveryKeyCommand.Execute(null);
        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(TestVaults.Password);
        var key = card.RecoveryKey;
        card.IsKeyStored = true;
        lockWhenBusy = true;
        await card.SaveRecoveryKeyCommand.ExecuteAsync(null);
        Assert.IsNull(card.Message, "le coffre verrouillé ne dit rien");
        card.Reload();
        Assert.AreEqual(EncryptionText.RecoveryKeySavedWhileLocking, card.Message);
        card.Reload();
        Assert.IsNull(card.Message, "une seule fois");

        // Refusée pendant le verrouillage (l'en-tête tenu par un autre programme) : la prochaine lecture dit que l'ancienne reste.
        card.RequestNewRecoveryKeyCommand.Execute(null);
        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(TestVaults.Password);
        card.IsKeyStored = true;
        using (new FileStream(vault.Header, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            lockWhenBusy = true;
            await card.SaveRecoveryKeyCommand.ExecuteAsync(null);
        }
        card.Reload();
        Assert.AreEqual(EncryptionText.RecoveryKeyNotSaved, card.Error);
        Assert.IsNull(card.Message);

        // Refusée sans verrouillage : la raison est dite tout de suite.
        card.RequestNewRecoveryKeyCommand.Execute(null);
        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(TestVaults.Password);
        card.IsKeyStored = true;
        using (new FileStream(vault.Header, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await card.SaveRecoveryKeyCommand.ExecuteAsync(null);
        }
        Assert.IsFalse(card.IsShowingRecoveryKey);
        Assert.Contains("l'ancienne reste valable", card.Error ?? string.Empty);
        Assert.AreEqual(vault.Root, vault.Reopen(VaultSecret.RecoveryKey(key)).Vault, "la clé écrite pendant le verrouillage est la bonne");
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
            hub.Tabs[4].IsSelected = true;
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
            UiThread.Until(() => FocusedIn(page.RecoveryPasswordBox) == page.RecoveryPasswordBox);
            page.PromptWithRecoveryBox.IsChecked = true;
            UiThread.Until(() => FocusedIn(page.PromptRecoveryBox) == page.PromptRecoveryBox);
            page.PromptWithRecoveryBox.IsChecked = false;
            TestShell.Flush();
            page.RecoveryPasswordBox.Password = TestVaults.Password;
            page.RecoveryPromptDialog.Close(DialogChoice.Primary);
            UiThread.Until(() => vault.Card.IsShowingRecoveryKey, Slow);
            TestShell.Flush();
            AssertDialogFits(page.RecoveryKeyDialog);
            Assert.AreEqual(vault.Card.RecoveryKey, page.RecoveryKeyText.Text);
            Assert.IsTrue(page.RecoveryKeyText.IsReadOnly);
            Assert.AreEqual(page.FindResource("Kyf.Font.Code"), page.RecoveryKeyText.FontFamily, "la clé s'affiche en chasse fixe");
            foreach (var box in new TextBox[] { page.RecoveryKeyText, page.RememberRecoveryBox, page.CurrentRecoveryBox, page.PromptRecoveryBox })
            {
                // Copiée au clavier ou par le menu, la clé reste hors de l'historique, comme par « Copier ».
                var copied = new DataObject(DataFormats.UnicodeText, vault.Card.RecoveryKey);
                box.RaiseEvent(new DataObjectCopyingEventArgs(copied, isDragDrop: false));
                Assert.IsTrue(copied.GetDataPresent(SecretClipboard.CanIncludeInHistory), box.Name);
                Assert.IsTrue(copied.GetDataPresent(SecretClipboard.CanUploadToCloud), box.Name);
            }
            Assert.IsTrue(page.OldRecoveryKeyWarning.IsVisible, "l'ancienne clé n'ouvre plus le coffre, mais les anciennes sauvegardes");
            var cancel = (Button)page.RecoveryKeyDialog.Template.FindName(Dialog.SecondaryButtonPart, page.RecoveryKeyDialog);
            Assert.AreEqual(Visibility.Visible, cancel.Visibility, "« Annuler » garde l'ancienne clé");
            Assert.AreEqual(EncryptionText.SaveRecoveryKey, ((Button)page.RecoveryKeyDialog.Template.FindName(Dialog.PrimaryButtonPart, page.RecoveryKeyDialog)).Content);

            Press(page.CopyRecoveryKeyButton);
            Assert.AreEqual(vault.Card.RecoveryKey, vault.Copied.Single());
            Assert.IsTrue(page.RecoveryKeyStatus.IsVisible);
            page.RecoveryKeyDialog.Close(DialogChoice.Primary);
            TestShell.Flush();
            Assert.IsTrue(page.RecoveryKeyDialog.IsOpen, "la clé n'est pas rangée : la fenêtre reste");
            page.KeyStoredBox.IsChecked = true;
            page.RecoveryKeyDialog.Close(DialogChoice.Primary);
            UiThread.Until(() => vault.Card.IsIdle && vault.Card.HasMessage, Slow);
            TestShell.Flush();
            Assert.IsFalse(page.RecoveryKeyDialog.IsOpen);
            Assert.IsEmpty(page.RecoveryKeyText.Text, "la clé ne reste pas dans la vue");
            Assert.AreEqual(EncryptionText.RecoveryKeyCreated, vault.Card.Message);
            AssertFits(page.Scroller);

            // Une autre, annulée par Échap : rien n'a changé.
            Press(page.NewRecoveryKeyButton);
            page.RecoveryPasswordBox.Password = TestVaults.Password;
            page.RecoveryPromptDialog.Close(DialogChoice.Primary);
            UiThread.Until(() => vault.Card.IsShowingRecoveryKey, Slow);
            TestShell.Flush();
            page.RecoveryKeyDialog.Close(DialogChoice.None);
            TestShell.Flush();
            Assert.IsFalse(page.RecoveryKeyDialog.IsOpen);
            Assert.AreEqual(EncryptionText.RecoveryKeyKept, vault.Card.Message);
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

            // Le mot de passe perdu : la clé de récupération, dans un champ qui la montre, change le mot de passe ; ce champ
            // aussi est vidé.
            page.ChangeWithRecoveryBox.IsChecked = true;
            TestShell.Flush();
            Assert.IsTrue(page.CurrentRecoveryBox.IsVisible);
            Assert.IsFalse(page.CurrentPasswordBox.IsVisible);
            Assert.AreEqual(EncryptionText.RecoveryKey, page.CurrentPasswordLabel.Text);
            page.CurrentRecoveryBox.Text = TestVaults.RecoveryKey;
            page.NewPasswordBox.Password = NewPassword;
            page.ConfirmPasswordBox.Password = NewPassword;
            page.ChangePasswordDialog.Close(DialogChoice.Primary);
            Assert.IsEmpty(page.CurrentRecoveryBox.Text, "le champ ne garde pas la clé");
            UiThread.Until(() => card.IsIdle && !card.IsChangingPassword, Slow);
            TestShell.Flush();
            Assert.AreEqual(EncryptionText.PasswordChanged, card.Message);
            Assert.IsTrue(page.EncryptionDone.IsOpen);

            // Rouverte, la fenêtre revient au mot de passe.
            Press(page.ChangePasswordButton);
            TestShell.Flush();
            Assert.IsTrue(page.ChangePasswordDialog.IsOpen);
            Assert.IsFalse(page.ChangeWithRecoveryBox.IsChecked);
            Assert.IsTrue(page.CurrentPasswordBox.IsVisible);
            Assert.AreEqual(EncryptionText.CurrentPassword, page.CurrentPasswordLabel.Text);

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

    [TestMethod]
    public async Task Each_window_takes_the_recovery_key_when_the_password_is_lost()
    {
        using var vault = new CardVault();
        var card = vault.Card;
        card.Reload();

        // Changer le mot de passe avec la clé de récupération : le champ se nomme autrement, et un champ vide le dit.
        card.RequestChangePasswordCommand.Execute(null);
        Assert.IsFalse(card.UsesRecoveryKey, "chaque fenêtre s'ouvre sur le mot de passe");
        Assert.IsTrue(card.UsesPassword);
        Assert.AreEqual(EncryptionText.CurrentPassword, card.CurrentSecretLabel);
        card.UsesRecoveryKey = true;
        Assert.IsFalse(card.UsesPassword);
        Assert.AreEqual(EncryptionText.RecoveryKey, card.CurrentSecretLabel);
        Assert.AreEqual(EncryptionText.RecoveryKey, card.SecretLabel);
        await card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange(string.Empty, NewPassword, NewPassword));
        Assert.AreEqual(EncryptionText.EnterRecoveryKey, card.DialogError);
        await card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange(TestVaults.Password, NewPassword, NewPassword));
        Assert.IsTrue(card.IsChangingPassword, "le mot de passe n'est pas la clé de récupération");
        await card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange(TestVaults.RecoveryKey, NewPassword, NewPassword));
        Assert.IsFalse(card.IsChangingPassword);
        Assert.AreEqual(EncryptionText.PasswordChanged, card.Message);
        Assert.AreEqual(vault.Root, vault.Reopen(VaultSecret.Password(NewPassword)).Vault, "le nouveau mot de passe ouvre le coffre");

        // Retenir la clé avec la clé de récupération.
        var card2 = vault.NewCard();
        card2.Reload();
        card2.IsRemembered = true;
        Assert.AreEqual(EncryptionText.Password, card2.SecretLabel);
        card2.UsesRecoveryKey = true;
        await card2.AnswerRememberCommand.ExecuteAsync(string.Empty);
        Assert.AreEqual(EncryptionText.EnterRecoveryKey, card2.DialogError);
        var remembering = card2.AnswerRememberCommand.ExecuteAsync(TestVaults.RecoveryKey);
        Assert.AreEqual(EncryptionText.CheckingRecoveryKey, card2.BusyMessage, "c'est la clé de récupération qui est vérifiée");
        await remembering;
        Assert.IsFalse(card2.IsAskingRemember);
        Assert.IsTrue(vault.App.Encryption.IsRemembered);

        // Une nouvelle clé de récupération avec l'actuelle ; la fenêtre suivante revient au mot de passe.
        card2.RequestNewRecoveryKeyCommand.Execute(null);
        Assert.IsFalse(card2.UsesRecoveryKey);
        card2.UsesRecoveryKey = true;
        await card2.AnswerNewRecoveryKeyCommand.ExecuteAsync(string.Empty);
        Assert.AreEqual(EncryptionText.EnterRecoveryKey, card2.DialogError);
        await card2.AnswerNewRecoveryKeyCommand.ExecuteAsync(TestVaults.RecoveryKey);
        Assert.IsTrue(card2.IsShowingRecoveryKey);
        var key = card2.RecoveryKey;
        card2.IsKeyStored = true;
        await card2.SaveRecoveryKeyCommand.ExecuteAsync(null);
        Assert.AreEqual(vault.Root, vault.Reopen(VaultSecret.RecoveryKey(key)).Vault);
        card2.RequestChangePasswordCommand.Execute(null);
        Assert.IsFalse(card2.UsesRecoveryKey);
    }

    [TestMethod]
    public async Task A_refusal_is_said_again_each_time_its_window_reopens()
    {
        using var vault = new CardVault();
        var card = vault.Card;
        card.Reload();
        var said = new List<string>();
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EncryptionSettingsViewModel.DialogError) && card.DialogError is { } error)
            {
                said.Add(error);
            }
            if (e.PropertyName == nameof(EncryptionSettingsViewModel.KeyMessage) && card.KeyMessage is { } message)
            {
                said.Add(message);
            }
        };

        // Deux refus pareils : la fenêtre rouverte le dit deux fois, après s'être rouverte.
        card.RequestNewRecoveryKeyCommand.Execute(null);
        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(string.Empty);
        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(string.Empty);
        Assert.AreEqual(2, said.Count(text => text == EncryptionText.EnterPassword));
        Assert.IsTrue(card.IsAskingRecoveryKey);
        await card.AnswerNewRecoveryKeyCommand.ExecuteAsync(TestVaults.Password);
        Assert.IsNull(card.DialogError, "la fenêtre fermée ne garde pas le refus");

        // La fenêtre de la clé : chaque copie et chaque enregistrement refusé se redisent.
        card.CopyRecoveryKeyCommand.Execute(null);
        card.CopyRecoveryKeyCommand.Execute(null);
        Assert.AreEqual(2, said.Count(text => text == EncryptionText.KeyCopied));
        await card.SaveRecoveryKeyCommand.ExecuteAsync(null);
        await card.SaveRecoveryKeyCommand.ExecuteAsync(null);
        Assert.AreEqual(2, said.Count(text => text == EncryptionText.KeyStoredFirst));
        Assert.IsTrue(card.IsShowingRecoveryKey);
    }

    [TestMethod]
    public async Task A_password_changed_while_the_vault_locks_is_said_at_its_next_reading()
    {
        using var vault = new CardVault();
        var card = vault.Card;
        card.Reload();
        card.RequestChangePasswordCommand.Execute(null);

        var changing = card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange(TestVaults.Password, NewPassword, NewPassword));
        card.Forget();
        await changing;

        Assert.IsNull(card.Message, "le coffre verrouillé ne dit rien");
        Assert.IsFalse(card.IsChangingPassword);
        card.Reload();
        Assert.AreEqual(EncryptionText.PasswordChangedWhileLocking, card.Message);
        Assert.IsNull(card.Error);
        card.Reload();
        Assert.IsNull(card.Message, "une seule fois");
        Assert.AreEqual(vault.Root, vault.Reopen(VaultSecret.Password(NewPassword)).Vault);

        // Refusé pendant le verrouillage, rien n'est dit.
        var card2 = vault.NewCard();
        card2.Reload();
        card2.RequestChangePasswordCommand.Execute(null);
        var refused = card2.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange("pas le bon", NewPassword, NewPassword));
        card2.Forget();
        await refused;
        card2.Reload();
        Assert.IsNull(card2.Message);
        Assert.IsNull(card2.Error);
    }

    [TestMethod]
    public async Task What_a_lock_interrupted_is_said_only_for_its_own_vault()
    {
        using var vault = new CardVault();
        var card = vault.Card;
        card.Reload();
        card.RequestChangePasswordCommand.Execute(null);
        var changing = card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange(TestVaults.Password, NewPassword, NewPassword));
        card.Forget();
        await changing;

        // Un autre coffre chiffré s'ouvre : ce n'est pas le sien. Son mot de passe aussi change pendant un verrouillage.
        var other = vault.Vaults.CreateEncryptedVault(vault.Vaults.PathOf("Autre"));
        Assert.AreEqual(other, vault.App.Vaults.Unlock(other, VaultSecret.Password(TestVaults.Password), remember: false).Vault);
        card.Reload();
        Assert.IsTrue(card.IsEncrypted);
        Assert.IsNull(card.Message, "rien n'a changé pour l'autre coffre");
        card.RequestChangePasswordCommand.Execute(null);
        changing = card.AnswerChangePasswordCommand.ExecuteAsync(new PasswordChange(TestVaults.Password, NewPassword, NewPassword));
        card.Forget();
        await changing;
        card.Reload();
        Assert.AreEqual(EncryptionText.PasswordChangedWhileLocking, card.Message, "celui de l'autre coffre");

        // Le premier, rouvert avec son nouveau mot de passe, le dit encore : l'un n'a pas remplacé l'autre.
        Assert.AreEqual(vault.Root, vault.App.Vaults.Unlock(vault.Root, VaultSecret.Password(NewPassword), remember: false).Vault);
        card.Reload();
        Assert.AreEqual(EncryptionText.PasswordChangedWhileLocking, card.Message);
        card.Reload();
        Assert.IsNull(card.Message);
    }

    [TestMethod]
    public void The_delay_in_effect_follows_what_the_card_reads_and_saves()
    {
        using var vault = new CardVault();
        var delay = new LockDelay();
        var card = vault.NewCard(delay);

        vault.App.Settings.Update(preferences => preferences with { LockAfterMinutes = 30 }).Value.ToString();
        card.Reload();
        Assert.AreEqual(TimeSpan.FromMinutes(30), delay.Value, "lu avec la carte");

        card.LockAfter = card.LockAfterChoices.Single(choice => choice.Value == 0);
        Assert.IsNull(delay.Value, "« Jamais »");
        card.LockAfter = card.LockAfterChoices.Single(choice => choice.Value == 5);
        Assert.AreEqual(TimeSpan.FromMinutes(5), delay.Value);
    }

    [TestMethod]
    public void A_refused_delay_shows_the_one_still_in_effect()
    {
        using var vault = new CardVault(encrypted: false);
        var app = vault.App;
        var delay = new LockDelay();
        var card = new EncryptionSettingsViewModel(app.Encryption, app.Settings, () => true, null, _ => { }) { LockDelay = delay };
        app.Settings.Update(preferences => preferences with { LockAfterMinutes = 30 }).Value.ToString();
        card.Reload();
        Assert.AreEqual(30, card.LockAfter!.Value);

        // Un autre programme tient le fichier des réglages : rien ne s'enregistre.
        var settings = Path.Combine(vault.Root, ".suivi", "coffre.json");
        Assert.IsTrue(File.Exists(settings));
        var refusals = 0;
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EncryptionSettingsViewModel.HasError) && card.HasError)
            {
                refusals++;
            }
        };
        using (new FileStream(settings, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            card.LockAfter = card.LockAfterChoices.Single(choice => choice.Value == 5);
            card.LockAfter = card.LockAfterChoices.Single(choice => choice.Value == 5);
        }

        Assert.IsTrue(card.HasError, "la raison est dite");
        Assert.AreEqual(2, refusals, "chaque refus se redit");
        Assert.AreEqual(30, card.LockAfter!.Value, "la liste remontre le délai en vigueur");
        Assert.AreEqual(30, app.Settings.Get().Value.LockAfterMinutes);
        Assert.AreEqual(TimeSpan.FromMinutes(30), delay.Value, "le verrouillage suit le délai enregistré");
    }

    /// <summary>L'élément qui a le focus dans la portée de <paramref name="element"/>.</summary>
    private static IInputElement? FocusedIn(DependencyObject element) => FocusManager.GetFocusedElement(FocusManager.GetFocusScope(element));

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

        /// <summary>L'en-tête du coffre chiffré : il ne change qu'à l'enregistrement d'une nouvelle clé ou d'un nouveau mot de passe.</summary>
        public string Header => Path.Combine(Root, "kyfrev-chiffre.json");

        /// <summary>Les octets de l'en-tête, tels qu'ils sont sur le disque.</summary>
        public byte[] HeaderBytes() => File.ReadAllBytes(Header);

        /// <summary>Ce qui a été copié dans le presse-papiers.</summary>
        public List<string> Copied { get; } = [];

        /// <summary>Le presse-papiers est pris par un autre programme : la copie échoue.</summary>
        public bool ClipboardRefuses { get; set; }

        /// <summary>Le nombre de verrouillages demandés.</summary>
        public int Locks { get; private set; }

        /// <summary>Une carte sur l'application du moment, comme la compose la racine de composition.</summary>
        public EncryptionSettingsViewModel NewCard(LockDelay? delay = null)
        {
            var app = App;
            return new(app.Encryption, app.Settings, () => app.Vaults.State().Encrypted, LockAsync, Copy) { LockDelay = delay };
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
