using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La conversion du coffre ouvert, sur sa page, avec de vrais coffres et le vrai verrouillage : « Chiffrer ce coffre » en
/// trois étapes (le mot de passe, la clé de récupération, la conversion), « Déchiffrer vers un dossier… », et ce qui suit.
/// La source n'est jamais touchée, la copie s'écrit dans un nouveau dossier voisin, et le coffre est fermé pendant la
/// copie : la page ouvre ensuite le nouveau, ou rouvre l'ancien.
/// </summary>
[TestClass]
public sealed class ConversionTests
{
    private const string NewPassword = "le mot de passe du coffre chiffré";

    /// <summary>Une dérivation prend de l'ordre d'une seconde, davantage sur un poste chargé.</summary>
    private static readonly TimeSpan Slow = TimeSpan.FromSeconds(30);

    [STATestMethod]
    public void A_plain_vault_encrypts_in_three_steps_into_a_new_sibling_folder_and_stays_untouched()
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: false);
        var before = scene.SourceFiles();
        var page = scene.Show(ConversionDirection.Encrypt);
        Assert.AreSame(page, scene.Shell.Current.Page, "la page remplace les vues");
        Assert.IsTrue(scene.Shell.IsWelcome, "ni menu ni recherche");
        Assert.AreEqual(ConversionStage.Password, page.Stage);
        Assert.IsTrue(page.ShowsSteps);
        Assert.IsTrue(page.IsStepOne);
        Assert.AreEqual(scene.Source, scene.App.Vaults.State().Vault, "le coffre reste ouvert derrière la page");

        // Étape 1 : deux saisies différentes, puis trop court ; la page le dit, et l'étape reste.
        Run(page.PrepareCommand.ExecuteAsync(new NewPassword(NewPassword, "un autre mot de passe")));
        Assert.AreEqual("Les deux mots de passe ne sont pas les mêmes.", page.Error);
        Run(page.PrepareCommand.ExecuteAsync(new NewPassword("trop court", "trop court")));
        Assert.AreEqual("Le mot de passe doit faire au moins 12 caractères.", page.Error);
        Assert.AreEqual(ConversionStage.Password, page.Stage);

        // Étape 2 : la clé de récupération, une fois ; « Retour » l'oublie, et une autre vient.
        Run(page.PrepareCommand.ExecuteAsync(new NewPassword(NewPassword, NewPassword)));
        Assert.IsNull(page.Error);
        Assert.AreEqual(ConversionStage.RecoveryKey, page.Stage);
        Assert.IsTrue(page.IsStepTwo);
        var first = page.RecoveryKey;
        Assert.HasCount(14, first.Split('-'), "14 groupes de quatre caractères");
        page.BackToPasswordCommand.Execute(null);
        Assert.AreEqual(ConversionStage.Password, page.Stage);
        Assert.IsEmpty(page.RecoveryKey);
        Run(page.PrepareCommand.ExecuteAsync(new NewPassword(NewPassword, NewPassword)));
        var key = page.RecoveryKey;
        Assert.AreNotEqual(first, key, "une nouvelle clé, la première étant oubliée");
        page.CopyRecoveryKeyCommand.Execute(null);
        Assert.AreEqual(key, scene.Copied.Single());
        Assert.AreEqual(EncryptionText.KeyCopied, page.KeyMessage);
        scene.ClipboardRefuses = true;
        page.CopyRecoveryKeyCommand.Execute(null);
        Assert.AreEqual(EncryptionText.KeyNotCopied, page.KeyMessage);

        // Sans la clé rangée, rien ne commence.
        Run(page.EncryptCommand.ExecuteAsync(null));
        Assert.AreEqual(EncryptionText.KeyStoredBeforeEncrypting, page.KeyMessage);
        Assert.AreEqual(ConversionStage.RecoveryKey, page.Stage);
        Assert.AreEqual(scene.Source, scene.App.Vaults.State().Vault);
        CollectionAssert.AreEquivalent(before.Keys.ToList(), scene.SourceFiles().Keys.ToList(), "rien n'est écrit avant la conversion");

        // Étape 3 : le coffre se ferme, les vues oublient, puis la copie s'écrit et se relit.
        page.IsKeyStored = true;
        Run(page.EncryptCommand.ExecuteAsync(null));
        Assert.AreEqual(ConversionStage.Done, page.Stage, page.Result);
        Assert.IsTrue(page.IsStepThree);
        Assert.IsTrue(page.IsFinished);
        Assert.IsFalse(page.CanCancel);
        Assert.IsEmpty(page.RecoveryKey, "la clé n'est plus dans la page");
        Assert.AreEqual(1, scene.Forgotten, "les vues ont oublié le coffre fermé");
        Assert.IsNull(scene.App.Vaults.State().Vault, "aucun coffre n'est ouvert après la conversion");
        Assert.IsFalse(scene.Shell.CanLock);
        Assert.AreEqual(Path.Combine(scene.Root, "Coffre chiffré"), page.Destination);
        Assert.IsTrue(File.Exists(Path.Combine(page.Destination!, "kyfrev-chiffre.json")), "un coffre chiffré");
        Assert.IsGreaterThan(0, page.Total);
        Assert.AreEqual(page.Total, page.Written);
        UiThread.Until(() => page.Verified == page.Total);
        Assert.AreEqual($"{page.Total} / {page.Total}", page.VerifyingCount);
        Assert.Contains(EncryptionText.EncryptedNext, page.Result);
        CollectionAssert.AreEqual(before, scene.SourceFiles(), "le coffre en clair n'a pas changé");

        // « Ouvrir le coffre chiffré » : son mot de passe l'ouvre, sur l'écran de déverrouillage ; la fiche y est.
        Run(page.OpenNewCommand.ExecuteAsync(null));
        var unlock = (UnlockPageViewModel)scene.Shell.Current.Page;
        Assert.AreEqual(page.Destination, unlock.Vault);
        Run(unlock.UnlockCommand.ExecuteAsync(NewPassword));
        Assert.AreEqual(page.Destination, scene.App.Vaults.State().Vault);
        Assert.IsTrue(scene.App.Vaults.State().Encrypted);
        Assert.AreEqual(1, scene.Changes);
        Assert.AreEqual(Scene.Title, scene.App.Sheets.Open(scene.Sheet).Value.Title);

        // La clé de récupération montrée à l'étape 2 l'ouvre aussi.
        Assert.AreEqual(page.Destination, scene.Vaults.Restart().Vaults.Unlock(page.Destination!, VaultSecret.RecoveryKey(key), remember: false).Vault);
    }

    [STATestMethod]
    public void Cancelling_before_the_conversion_comes_back_to_the_open_vault()
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: false);
        var page = scene.Show(ConversionDirection.Encrypt);
        Run(page.PrepareCommand.ExecuteAsync(new NewPassword(NewPassword, NewPassword)));
        Assert.IsTrue(page.CanCancel);

        page.CancelCommand.Execute(null);

        Assert.IsFalse(scene.Shell.IsWelcome, "l'espace de travail revient");
        Assert.AreEqual(scene.Source, scene.App.Vaults.State().Vault);
        Assert.IsEmpty(page.RecoveryKey);
        Assert.AreEqual(0, scene.Forgotten);
        Assert.IsFalse(Directory.Exists(Path.Combine(scene.Root, "Coffre chiffré")), "rien n'est écrit");
    }

    [STATestMethod]
    public void A_taken_name_moves_to_the_next_one_and_a_refused_conversion_reopens_the_source()
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: false);
        Directory.CreateDirectory(Path.Combine(scene.Root, "Coffre chiffré"));
        var page = scene.Show(ConversionDirection.Encrypt);
        Encrypt(page);
        Assert.AreEqual(ConversionStage.Done, page.Stage, page.Result);
        Assert.AreEqual(Path.Combine(scene.Root, "Coffre chiffré (2)"), page.Destination, "le nom suivant");

        // « Revenir au coffre en clair » : il se rouvre, intact.
        Run(page.ReturnCommand.ExecuteAsync(null));
        Assert.AreEqual(scene.Source, scene.App.Vaults.State().Vault);
        Assert.IsFalse(scene.App.Vaults.State().Encrypted);
        Assert.AreEqual(1, scene.Changes);

        // Le dossier temporaire d'une conversion coupée vers « Coffre chiffré (3) » : la conversion refuse et le nomme.
        var leftover = Directory.CreateDirectory(Path.Combine(scene.Root, ".Coffre chiffré (3).conversion-abcdefgh.ijk")).FullName;
        var again = scene.Show(ConversionDirection.Encrypt);
        Encrypt(again);
        Assert.AreEqual(ConversionStage.Failed, again.Stage);
        Assert.IsTrue(again.IsFinished);
        Assert.IsNull(again.Destination);
        Assert.Contains(leftover, again.Result);
        Assert.IsTrue(Directory.Exists(leftover), "le reste d'une autre conversion n'est jamais effacé");
        Assert.IsFalse(Directory.Exists(Path.Combine(scene.Root, "Coffre chiffré (3)")));
        Run(again.OpenNewCommand.ExecuteAsync(null));
        Assert.IsNull(scene.App.Vaults.State().Vault, "rien à ouvrir après un refus");
        Run(again.ReturnCommand.ExecuteAsync(null));
        Assert.AreEqual(scene.Source, scene.App.Vaults.State().Vault, "le coffre en clair se rouvre");
    }

    [STATestMethod]
    public void An_encrypted_vault_decrypts_into_the_chosen_folder_with_its_password_or_its_recovery_key()
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: true);
        var page = scene.Show(ConversionDirection.Decrypt);
        Assert.AreEqual(ConversionStage.Secret, page.Stage);
        Assert.IsFalse(page.ShowsSteps);
        Assert.AreEqual(scene.Root, page.Parent, "à côté du coffre chiffré, par défaut");
        Assert.AreEqual(EncryptionText.DecryptionTitle, page.ConversionTitle);

        // Un champ vide, puis un mauvais mot de passe : le coffre reste ouvert, l'étape reste.
        Run(page.DecryptCommand.ExecuteAsync(string.Empty));
        Assert.AreEqual(EncryptionText.EnterPassword, page.Error);
        Run(page.DecryptCommand.ExecuteAsync("pas le bon"));
        Assert.IsTrue(page.HasError);
        Assert.AreEqual(ConversionStage.Secret, page.Stage);
        Assert.AreEqual(scene.Source, scene.App.Vaults.State().Vault);

        // Un autre dossier : le choix annulé ne change rien, un dossier choisi oui.
        var elsewhere = scene.Vaults.Folder("Ailleurs");
        scene.Vaults.Picker.Answers.Enqueue(null);
        page.PickParentCommand.Execute(null);
        Assert.AreEqual(scene.Root, page.Parent);
        scene.Vaults.Picker.Answers.Enqueue(elsewhere);
        page.PickParentCommand.Execute(null);
        Assert.AreEqual(elsewhere, page.Parent);
        Assert.AreEqual(EncryptionText.PickDecryptedParent, scene.Vaults.Picker.Titles.Last());

        // La clé de récupération, au lieu du mot de passe.
        page.UsesRecoveryKey = true;
        Assert.AreEqual(EncryptionText.RecoveryKey, page.SecretLabel);
        Run(page.DecryptCommand.ExecuteAsync(string.Empty));
        Assert.AreEqual(EncryptionText.EnterRecoveryKey, page.Error);
        Run(page.DecryptCommand.ExecuteAsync(TestVaults.RecoveryKey));

        Assert.AreEqual(ConversionStage.Done, page.Stage, page.Result);
        Assert.AreEqual(Path.Combine(elsewhere, "Coffre (en clair)"), page.Destination);
        Assert.IsTrue(File.Exists(Path.Combine(page.Destination!, ".suivi", "coffre.json")), "un coffre en clair");
        Assert.IsTrue(File.Exists(Path.Combine(scene.Source, "kyfrev-chiffre.json")), "le coffre chiffré reste");
        Assert.Contains(EncryptionText.DecryptedNext, page.Result);
        Assert.DoesNotContain(EncryptionText.PortableNext, page.Result);
        Assert.AreEqual(EncryptionText.FilesDecrypted, page.WritingLabel);

        // « Revenir au coffre chiffré » : l'écran de déverrouillage le demande ; « Ouvrir le coffre en clair » l'ouvre.
        Run(page.ReturnCommand.ExecuteAsync(null));
        Assert.AreEqual(scene.Source, ((UnlockPageViewModel)scene.Shell.Current.Page).Vault);
        Run(page.OpenNewCommand.ExecuteAsync(null));
        Assert.AreEqual(page.Destination, scene.App.Vaults.State().Vault);
        Assert.IsFalse(scene.App.Vaults.State().Encrypted);
        Assert.AreEqual(Scene.Title, scene.App.Sheets.Open(scene.Sheet).Value.Title, "la fiche se lit en clair");
    }

    [STATestMethod]
    public void The_vault_stays_open_when_the_page_is_gone_or_a_draft_cannot_be_saved()
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: false);
        var page = scene.Show(ConversionDirection.Encrypt);
        Run(page.PrepareCommand.ExecuteAsync(new NewPassword(NewPassword, NewPassword)));
        page.IsKeyStored = true;

        // Une vue garde un brouillon qui ne s'enregistre pas : le coffre reste ouvert, la page le dit.
        scene.Drafts.Add(new RefusingDraft());
        Run(page.EncryptCommand.ExecuteAsync(null));
        Assert.AreEqual(EncryptionText.NotClosed, page.Error);
        Assert.AreEqual(ConversionStage.RecoveryKey, page.Stage, "l'étape reste : rien n'a commencé");
        Assert.AreEqual(scene.Source, scene.App.Vaults.State().Vault);
        Assert.AreEqual(0, scene.Forgotten);
        scene.Drafts.Clear();

        // Un brouillon qu'un verrouillage abandonnerait : une conversion peut attendre, et il n'est pas perdu.
        scene.Drafts.Add(new UnsavedDraft());
        Run(page.EncryptCommand.ExecuteAsync(null));
        Assert.AreEqual(EncryptionText.NotClosed, page.Error);
        Assert.AreEqual(scene.Source, scene.App.Vaults.State().Vault);
        Assert.AreEqual(0, scene.Forgotten);
        scene.Drafts.Clear();

        // La page n'est plus affichée : rien ne se ferme pour elle.
        scene.Shell.ShowWorkspace();
        Assert.IsFalse(Run(scene.Locking.CloseForAsync(page)));
        Assert.AreEqual(scene.Source, scene.App.Vaults.State().Vault);

        // Sans coffre ouvert, rien ne s'affiche.
        Assert.IsNotNull(scene.App.Vaults.Close().Version);
        Assert.IsNull(VaultConversionScreen.Show(scene.Shell, scene.App.Vaults, scene.App.Encryption, scene.Locking, scene.Vaults.Picker, _ => { }, ConversionDirection.Encrypt));
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void The_page_fits_a_narrow_window_names_its_controls_and_keeps_nothing_typed(bool dark)
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: false, dark);
        var page = scene.Show(ConversionDirection.Encrypt);
        var window = new ShellWindow(scene.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 470, Height = 540, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(scene.Test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            var view = Descendants(window).OfType<ConversionPage>().Single();
            Assert.IsTrue(view.PasswordPart.IsVisible);
            Assert.IsFalse(view.RecoveryPart.IsVisible);
            Assert.AreEqual(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(view.Heading));
            UiThread.Until(() => FocusedIn(view.NewPasswordBox) == view.NewPasswordBox);
            AssertFits(view.Scroller);
            AssertNamed(view);

            // « Continuer » passe le mot de passe à la page, et les champs se vident.
            view.NewPasswordBox.Password = NewPassword;
            view.ConfirmPasswordBox.Password = NewPassword;
            Press(view.ContinueButton);
            Assert.IsEmpty(view.NewPasswordBox.Password);
            Assert.IsEmpty(view.ConfirmPasswordBox.Password);
            UiThread.Until(() => page.IsRecoveryKey, Slow);
            TestShell.Flush();
            Assert.IsTrue(view.RecoveryPart.IsVisible);
            UiThread.Until(() => FocusedIn(view.RecoveryKeyText) == view.RecoveryKeyText);
            Assert.IsFalse(view.DecryptWarning.IsOpen, "l'avertissement du déchiffrement n'est pas pour un chiffrement");
            Assert.AreEqual(page.RecoveryKey, view.RecoveryKeyText.Text);
            Assert.AreEqual(view.FindResource("Kyf.Font.Code"), view.RecoveryKeyText.FontFamily);
            Assert.IsTrue(view.StepTwo.Tag is true, "l'étape montrée se distingue");
            AssertFits(view.Scroller);
            AssertNamed(view);

            // La clé copiée au clavier ou par le menu reste hors de l'historique, comme par « Copier ».
            foreach (var box in new TextBox[] { view.RecoveryKeyText, view.DecryptRecoveryBox })
            {
                var copied = new DataObject(DataFormats.UnicodeText, page.RecoveryKey);
                box.RaiseEvent(new DataObjectCopyingEventArgs(copied, isDragDrop: false));
                Assert.IsTrue(copied.GetDataPresent(SecretClipboard.CanIncludeInHistory), box.Name);
            }

            // La conversion : les barres, puis le résultat ; tout tient dans la largeur.
            view.KeyStoredBox.IsChecked = true;
            Press(view.EncryptButton);
            UiThread.Until(() => page.IsFinished, Slow);
            TestShell.Flush();
            Assert.IsTrue(view.ProgressPart.IsVisible);
            Assert.IsTrue(view.DoneMessage.IsOpen, page.Result);
            Assert.IsTrue(view.OpenNewButton.IsVisible);
            Assert.AreEqual(page.Maximum, view.WritingBar.Maximum);
            AssertFits(view.Scroller);
            AssertNamed(view);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_decryption_page_fits_a_narrow_window_and_clears_its_secret()
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: true);
        var page = scene.Show(ConversionDirection.Decrypt);
        var window = new ShellWindow(scene.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 470, Height = 540, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(scene.Test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            var view = Descendants(window).OfType<ConversionPage>().Single();
            Assert.IsTrue(view.SecretPart.IsVisible);
            Assert.IsFalse(view.Steps.IsVisible, "pas d'étapes numérotées pour un déchiffrement");
            Assert.IsTrue(view.DecryptWarning.IsOpen, "le coffre en clair se lit sans mot de passe : la page le dit");
            AssertFits(view.Scroller);
            AssertNamed(view);

            // Un mauvais mot de passe : le champ se vide, et la page le dit.
            view.DecryptPasswordBox.Password = "pas le bon";
            Press(view.DecryptButton);
            Assert.IsEmpty(view.DecryptPasswordBox.Password);
            UiThread.Until(() => page.IsIdle && page.HasError, Slow);
            TestShell.Flush();
            Assert.IsTrue(view.ErrorMessage.IsOpen);
            UiThread.Until(() => FocusedIn(view.DecryptPasswordBox) == view.DecryptPasswordBox);

            // La clé de récupération : son champ la montre, puis se vide.
            view.DecryptWithRecoveryBox.IsChecked = true;
            TestShell.Flush();
            Assert.IsTrue(view.DecryptRecoveryBox.IsVisible);
            UiThread.Until(() => FocusedIn(view.DecryptRecoveryBox) == view.DecryptRecoveryBox);
            view.DecryptRecoveryBox.Text = TestVaults.RecoveryKey;
            Press(view.DecryptButton);
            Assert.IsEmpty(view.DecryptRecoveryBox.Text);
            UiThread.Until(() => page.IsFinished, Slow);
            TestShell.Flush();
            Assert.IsTrue(view.DoneMessage.IsOpen, page.Result);
            AssertFits(view.Scroller);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_entries_show_only_for_the_vault_they_convert()
    {
        using var ui = new UiThread();
        using var plain = new Scene(encrypted: false);
        var asked = 0;
        var data = plain.DataTab(() => asked++);
        data.OnNavigatedTo();
        Assert.IsTrue(data.CanEncrypt, "un coffre en clair se chiffre");
        data.RequestEncryptionCommand.Execute(null);
        Assert.AreEqual(1, asked);
        data.Forget();
        Assert.IsFalse(data.CanEncrypt);
        var without = plain.DataTab(null);
        without.OnNavigatedTo();
        Assert.IsFalse(without.CanEncrypt, "assemblé sans conversion, l'onglet ne la propose pas");

        using var encrypted = new Scene(encrypted: true);
        var data2 = encrypted.DataTab(() => asked++);
        data2.OnNavigatedTo();
        Assert.IsFalse(data2.CanEncrypt, "un coffre chiffré ne se chiffre pas de nouveau");
        data2.RequestEncryptionCommand.Execute(null);
        Assert.AreEqual(1, asked);

        // La carte Chiffrement propose « Déchiffrer vers un dossier… » pour son coffre chiffré seulement.
        var decrypting = 0;
        var card = new EncryptionSettingsViewModel(encrypted.App.Encryption, encrypted.App.Settings, () => encrypted.App.Vaults.State().Encrypted, null, _ => { })
        {
            DecryptionAsked = () => decrypting++,
        };
        Assert.IsTrue(card.CanDecrypt);
        card.RequestDecryptionCommand.Execute(null);
        Assert.AreEqual(0, decrypting, "rien tant que la carte n'a rien lu");
        card.Reload();
        card.RequestDecryptionCommand.Execute(null);
        Assert.AreEqual(1, decrypting);
        var bare = new EncryptionSettingsViewModel(encrypted.App.Encryption, encrypted.App.Settings, () => true, null, _ => { });
        Assert.IsFalse(bare.CanDecrypt);
        Assert.IsFalse(bare.RequestDecryptionCommand.CanExecute(null));
    }

    [STATestMethod]
    public void What_comes_next_names_the_plain_backups_and_the_portable_folder()
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: false, mode: AppMode.Portable);
        var backups = Path.Combine(scene.Root, "Sauvegardes");
        Directory.CreateDirectory(backups);
        var page = scene.Show(ConversionDirection.Encrypt);

        Encrypt(page);

        Assert.AreEqual(ConversionStage.Done, page.Stage, page.Result);
        Assert.Contains(EncryptionText.PlainBackupsOf(backups), page.Result);
        Assert.Contains(EncryptionText.PortableNext, page.Result);
    }

    [STATestMethod]
    public void A_vault_that_no_longer_opens_leads_to_the_welcome_page_which_says_why()
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: false);
        var page = scene.Show(ConversionDirection.Encrypt);
        Encrypt(page);
        Assert.AreEqual(ConversionStage.Done, page.Stage, page.Result);
        Directory.Move(scene.Source, scene.Source + " déplacé");

        Run(page.ReturnCommand.ExecuteAsync(null));

        var welcome = (WelcomePageViewModel)scene.Shell.Current.Page;
        Assert.IsTrue(welcome.HasError, "l'accueil dit pourquoi");
        Assert.IsNull(scene.App.Vaults.State().Vault);
    }

    [STATestMethod]
    public void Nothing_cancels_the_page_once_the_vault_is_closing()
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: false);
        var closing = new TaskCompletionSource<bool>();
        var cancelled = 0;
        var page = new ConversionPageViewModel(scene.App.Vaults, scene.App.Encryption, ConversionDirection.Encrypt, scene.Source, _ => closing.Task, _ => { }, () => cancelled++, scene.Vaults.Picker, _ => { });
        Run(page.PrepareCommand.ExecuteAsync(new NewPassword(NewPassword, NewPassword)));
        var key = page.RecoveryKey;
        page.IsKeyStored = true;

        var encrypting = page.EncryptCommand.ExecuteAsync(null);

        Assert.IsTrue(page.IsBusy, "la fermeture occupe la page");
        Assert.AreEqual(EncryptionText.Closing, page.BusyMessage);
        page.CancelCommand.Execute(null);
        page.BackToPasswordCommand.Execute(null);
        Assert.AreEqual(0, cancelled);
        Assert.AreEqual(ConversionStage.RecoveryKey, page.Stage);
        Assert.AreEqual(key, page.RecoveryKey, "la clé préparée reste");

        closing.SetResult(false);
        Run(encrypting);
        Assert.IsFalse(page.IsBusy);
        Assert.AreEqual(EncryptionText.NotClosed, page.Error);
        Assert.AreEqual(ConversionStage.RecoveryKey, page.Stage);
    }

    [STATestMethod]
    public void The_window_stays_open_while_a_conversion_runs()
    {
        using var ui = new UiThread();
        using var blocking = new BlockingConversion();
        using var scene = new Scene(encrypted: false, conversion: blocking);
        var page = scene.Show(ConversionDirection.Encrypt);
        var window = new ShellWindow(scene.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 800, Height = 700, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(scene.Test.Design.Dictionary);
        window.Show();
        try
        {
            Run(page.PrepareCommand.ExecuteAsync(new NewPassword(NewPassword, NewPassword)));
            page.IsKeyStored = true;
            var converting = page.EncryptCommand.ExecuteAsync(null);
            UiThread.Until(() => page.IsConverting && blocking.Started, Slow);

            blocking.Show(new ConversionProgress(ConversionStep.Writing, 1, 2));
            UiThread.Until(() => page.Written == 1);
            Assert.AreEqual("1 / 2", page.WritingCount);
            Assert.AreEqual(2, page.Maximum);

            window.Close();
            TestShell.Flush();
            Assert.IsTrue(window.IsVisible, "coupée, une conversion laisse un dossier temporaire");

            blocking.Show(new ConversionProgress(ConversionStep.Verifying, 2, 2));
            UiThread.Until(() => page.Verified == 2);
            Assert.AreEqual(2, page.Written, "l'écriture est finie quand la relecture commence");
            blocking.Release();
            Run(converting);
            Assert.AreEqual(ConversionStage.Done, page.Stage, page.Result);
        }
        finally
        {
            window.Close();
        }
        TestShell.Flush();
        Assert.IsFalse(window.IsVisible, "une fois la conversion finie, la fenêtre se ferme");
    }

    [TestMethod]
    public void The_page_checks_what_it_is_given()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var app = vaults.App;
        Func<object, Task<bool>> close = _ => Task.FromResult(false);
        Action<AppState> opened = _ => { };
        Action cancel = () => { };
        Action<string> copy = _ => { };
        Assert.ThrowsExactly<ArgumentNullException>(() => new ConversionPageViewModel(null!, app.Encryption, ConversionDirection.Encrypt, "C:\\Coffre", close, opened, cancel, vaults.Picker, copy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ConversionPageViewModel(app.Vaults, null!, ConversionDirection.Encrypt, "C:\\Coffre", close, opened, cancel, vaults.Picker, copy));
        Assert.ThrowsExactly<ArgumentException>(() => new ConversionPageViewModel(app.Vaults, app.Encryption, ConversionDirection.Encrypt, string.Empty, close, opened, cancel, vaults.Picker, copy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ConversionPageViewModel(app.Vaults, app.Encryption, ConversionDirection.Encrypt, "C:\\Coffre", null!, opened, cancel, vaults.Picker, copy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ConversionPageViewModel(app.Vaults, app.Encryption, ConversionDirection.Encrypt, "C:\\Coffre", close, null!, cancel, vaults.Picker, copy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ConversionPageViewModel(app.Vaults, app.Encryption, ConversionDirection.Encrypt, "C:\\Coffre", close, opened, null!, vaults.Picker, copy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ConversionPageViewModel(app.Vaults, app.Encryption, ConversionDirection.Encrypt, "C:\\Coffre", close, opened, cancel, null!, copy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ConversionPageViewModel(app.Vaults, app.Encryption, ConversionDirection.Encrypt, "C:\\Coffre", close, opened, cancel, vaults.Picker, null!));

        // Les commandes hors de leur étape ne font rien.
        var page = new ConversionPageViewModel(app.Vaults, app.Encryption, ConversionDirection.Decrypt, "C:\\Coffre", close, opened, cancel, vaults.Picker, copy);
        Assert.AreEqual("C:\\", page.Parent);
        page.CopyRecoveryKeyCommand.Execute(null);
        page.BackToPasswordCommand.Execute(null);
        Assert.IsTrue(page.PrepareCommand.ExecuteAsync(new NewPassword(NewPassword, NewPassword)).IsCompleted);
        Assert.IsTrue(page.EncryptCommand.ExecuteAsync(null).IsCompleted);
        Assert.IsTrue(page.OpenNewCommand.ExecuteAsync(null).IsCompleted);
        Assert.IsTrue(page.ReturnCommand.ExecuteAsync(null).IsCompleted);
        Assert.IsTrue(page.DecryptCommand.ExecuteAsync(null).IsCompleted);
        Assert.AreEqual(ConversionStage.Secret, page.Stage);
        Assert.AreEqual(EncryptionText.DecryptHeading, page.Heading);
        Assert.AreEqual(EncryptionText.OpenDecrypted, page.OpenNewLabel);
        Assert.AreEqual(EncryptionText.BackToEncrypted, page.ReturnLabel);
        Assert.AreEqual("Au moins 12 caractères.", ConversionPageViewModel.PasswordLength);
        Assert.AreEqual("0 / 0", page.WritingCount);
        Assert.AreEqual(1, page.Maximum);
    }

    /// <summary>Attend une tâche du fil de l'interface, qui pompe les messages pendant ce temps.</summary>
    private static T Run<T>(Task<T> task)
    {
        UiThread.Until(() => task.IsCompleted, Slow);
        return task.Result;
    }

    private static void Run(Task task)
    {
        UiThread.Until(() => task.IsCompleted, Slow);
        task.GetAwaiter().GetResult();
    }

    /// <summary>La conversion tout entière, étape par étape, jusqu'à sa fin.</summary>
    private static void Encrypt(ConversionPageViewModel page)
    {
        Run(page.PrepareCommand.ExecuteAsync(new NewPassword(NewPassword, NewPassword)));
        page.IsKeyStored = true;
        Run(page.EncryptCommand.ExecuteAsync(null));
    }

    private static IInputElement? FocusedIn(DependencyObject element) => FocusManager.GetFocusedElement(FocusManager.GetFocusScope(element));

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

    /// <summary>Chaque champ, bouton et case visible a un nom pour le lecteur d'écran, et un nom dans la vue.</summary>
    private static void AssertNamed(DependencyObject root)
    {
        var controls = Descendants(root).OfType<Control>().Where(control => control is ButtonBase or TextBox or PasswordBox or ProgressBar && control.IsVisible && control.TemplatedParent is not ScrollBar).ToList();
        Assert.IsNotEmpty(controls);
        foreach (var control in controls)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(control.Name), $"Sans x:Name : {control.GetType().Name}");
            var name = UIElementAutomationPeer.CreatePeerForElement(control).GetName();
            Assert.IsFalse(string.IsNullOrWhiteSpace(name), $"Sans nom : {control.GetType().Name} « {control.Name} »");
        }
    }

    /// <summary>Comme un clic, ou un lecteur d'écran : le bouton est invoqué.</summary>
    private static void Press(ButtonBase button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button);
        ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
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

    /// <summary>Un brouillon que <see cref="ILeaving.LeaveAsync"/> n'a pas pu enregistrer, et qui reste en cours.</summary>
    private sealed class UnsavedDraft : ILeaving
    {
        public bool HasUnsavedDraft => true;

        public Task<bool> LeaveAsync() => Task.FromResult(true);
    }

    /// <summary>Un chiffrement préparé qui ne garde rien.</summary>
    private sealed class FakeDraft(string source) : EncryptionDraft(source, "ABCD")
    {
        protected override void Dispose(bool disposing)
        {
        }
    }

    /// <summary>Une conversion qui attend qu'on la laisse finir, et qui dit où elle en est à la demande.</summary>
    private sealed class BlockingConversion : IVaultConversion, IDisposable
    {
        private readonly ManualResetEventSlim _released = new();
        private IProgress<ConversionProgress>? _progress;

        public bool Started => Volatile.Read(ref _progress) is not null;

        public void Show(ConversionProgress progress) => _progress!.Report(progress);

        public void Release() => _released.Set();

        public void Dispose() => _released.Dispose();

        public EncryptionDraft PrepareEncryption(string source, string password) => new FakeDraft(source);

        public string FreeDestination(string parent, string name) => Path.Combine(parent, name);

        public ConversionReport Encrypt(EncryptionDraft draft, string destination, IProgress<ConversionProgress>? progress = null)
        {
            Volatile.Write(ref _progress, progress);
            _released.Wait(Slow);
            return new ConversionReport(true, "Fait.", destination, 2);
        }

        public ConversionReport Decrypt(string source, string destination, VaultSecret secret, IProgress<ConversionProgress>? progress = null) => throw new NotSupportedException();
    }

    /// <summary>Un brouillon qui ne s'enregistre pas : il garde le coffre ouvert.</summary>
    private sealed class RefusingDraft : ILeaving
    {
        public Task<bool> LeaveAsync() => Task.FromResult(false);
    }

    /// <summary>Un coffre ouvert avec une fiche, en clair ou chiffré, la coquille, et le verrouillage assemblé comme sur le poste.</summary>
    private sealed class Scene : IDisposable
    {
        public const string Title = "La Révolution française";

        public Scene(bool encrypted, bool dark = false, AppMode mode = AppMode.Development, IVaultConversion? conversion = null)
        {
            Vaults = new TestVaults(mode, conversion: conversion);
            Root = Vaults.Root;
            if (encrypted)
            {
                Source = Vaults.CreateEncryptedVault(Vaults.PathOf("Coffre"));
                Assert.AreEqual(Source, App.Vaults.Unlock(Source, VaultSecret.Password(TestVaults.Password), remember: false).Vault);
            }
            else
            {
                Source = Vaults.CreateVault("Coffre");
                Assert.AreEqual(Source, App.Vaults.Open(Source).Vault);
            }
            Sheet = App.Sheets.Create(null, Title, "Les trois ordres de la société.").Value;
            Test = new TestShell(dark);
            Shell = Test.Shell;
            Shell.ShowWorkspace();
            Shell.CanLock = App.Vaults.State().Encrypted;
            Locking = new VaultLocking(Shell, App.Vaults, Vaults.Picker, () => Forgotten++, () =>
            {
                Changes++;
                Shell.ShowWorkspace();
            }, () => Drafts);
        }

        public TestVaults Vaults { get; }

        public KyfRevApplication App => Vaults.App;

        public string Root { get; }

        public string Source { get; }

        public string Sheet { get; }

        public TestShell Test { get; }

        public ShellViewModel Shell { get; }

        public VaultLocking Locking { get; }

        public int Forgotten { get; private set; }

        public int Changes { get; private set; }

        public List<ILeaving> Drafts { get; } = [];

        public List<string> Copied { get; } = [];

        public bool ClipboardRefuses { get; set; }

        /// <summary>La page de conversion, telle que la composition la montre.</summary>
        public ConversionPageViewModel Show(ConversionDirection direction) =>
            VaultConversionScreen.Show(Shell, App.Vaults, App.Encryption, Locking, Vaults.Picker, Copy, direction, () =>
            {
                Changes++;
                Shell.ShowWorkspace();
            }) ?? throw new InvalidOperationException("Aucun coffre ouvert.");

        /// <summary>L'onglet « Données », avec ou sans « Chiffrer ce coffre… ».</summary>
        public DataSettingsViewModel DataTab(Action? encryptionAsked)
        {
            var app = App;
            return new(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, Vaults.Picker, new FakeOpener(), () => { })
            {
                EncryptionAsked = encryptionAsked,
            };
        }

        /// <summary>Le contenu du coffre en clair, fichier par fichier, hors de son verrou (l'état de l'application).</summary>
        public SortedDictionary<string, string> SourceFiles()
        {
            var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(Source, "*", SearchOption.AllDirectories).Where(file => !Path.GetFileName(file).StartsWith("verrou", StringComparison.Ordinal)))
            {
                files[Path.GetRelativePath(Source, file)] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            }
            return files;
        }

        public void Dispose() => Vaults.Dispose();

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
