using System.IO;
using System.Windows;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// « Renouveler toutes les clés… » (rangée 29B) : le secret actuel, le mot de passe gardé ou nouveau, la nouvelle clé de
/// récupération, puis tout le coffre chiffré de nouveau ; le coffre garde son nom, l'ancien est mis à côté, et le coffre
/// se rouvre avec le mot de passe donné, sa clé retenue si l'ancienne l'était.
/// </summary>
public sealed partial class ConversionTests
{
    [STATestMethod]
    public void Renewing_the_keys_writes_the_vault_again_keeps_its_name_and_reopens_it()
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: true, remember: true);
        Assert.IsTrue(scene.App.Encryption.IsRemembered);
        var page = scene.Show(ConversionDirection.Renew);
        Assert.AreEqual(ConversionStage.Password, page.Stage);
        Assert.IsTrue(page.IsRenewal);
        Assert.IsTrue(page.ShowsSteps);
        Assert.IsTrue(page.KeepsPassword, "garder le mot de passe, par défaut");
        Assert.IsFalse(page.ShowsNewPassword);
        Assert.AreEqual(EncryptionText.RenewHeading, page.Heading);
        Assert.AreEqual(EncryptionText.RenewIntro, page.Intro);
        Assert.AreEqual(EncryptionText.RenewStepHelp, page.PasswordHelp);
        Assert.AreEqual(EncryptionText.StepRenewal, page.StepThreeLabel);
        Assert.AreEqual(EncryptionText.RenewNow, page.EncryptNowLabel);
        Assert.AreEqual(EncryptionText.RenewalTitle, page.ConversionTitle);
        Assert.AreEqual(EncryptionText.FilesRenewed, page.WritingLabel);
        Assert.AreEqual(EncryptionText.ReopenRenewed, page.OpenNewLabel);
        Assert.AreEqual(EncryptionText.CurrentPassword, page.CurrentSecretLabel);

        // Rien de tapé, un mauvais mot de passe : la page le dit, l'étape reste, le coffre reste ouvert.
        Run(page.PrepareRenewalCommand.ExecuteAsync(new RenewalSecrets(string.Empty, false, null, null)));
        Assert.AreEqual(EncryptionText.EnterCurrentPassword, page.Error);
        Run(page.PrepareRenewalCommand.ExecuteAsync(new RenewalSecrets("pas le bon mot de passe", false, null, null)));
        Assert.IsTrue(page.HasError);
        Assert.AreEqual(ConversionStage.Password, page.Stage);
        Assert.AreEqual(scene.Source, scene.App.Vaults.State().Vault);

        // La clé de récupération ne tient pas lieu de mot de passe : il en faut un nouveau.
        page.UsesRecoveryKey = true;
        Assert.IsTrue(page.ShowsNewPassword);
        Assert.AreEqual(EncryptionText.RecoveryKey, page.CurrentSecretLabel);
        Run(page.PrepareRenewalCommand.ExecuteAsync(new RenewalSecrets(string.Empty, true, null, null)));
        Assert.AreEqual(EncryptionText.EnterRecoveryKey, page.Error);
        Run(page.PrepareRenewalCommand.ExecuteAsync(new RenewalSecrets(TestVaults.RecoveryKey, true, null, null)));
        Assert.AreEqual(VaultUseCases.RenewalNeedsPassword, page.Error);
        page.UsesRecoveryKey = false;

        // Un nouveau mot de passe tapé deux fois différemment.
        page.ChoosesNewPassword = true;
        Assert.IsFalse(page.KeepsPassword);
        Assert.IsTrue(page.ShowsNewPassword);
        Run(page.PrepareRenewalCommand.ExecuteAsync(new RenewalSecrets(TestVaults.Password, false, NewPassword, "un autre mot de passe")));
        Assert.AreEqual("Les deux mots de passe ne sont pas les mêmes.", page.Error);
        page.KeepsPassword = true;
        Assert.IsFalse(page.ShowsNewPassword);

        // Le mot de passe gardé : une nouvelle clé de récupération, puis le renouvellement.
        Run(page.PrepareRenewalCommand.ExecuteAsync(new RenewalSecrets(TestVaults.Password, false, null, null)));
        Assert.IsNull(page.Error);
        Assert.AreEqual(ConversionStage.RecoveryKey, page.Stage);
        var key = page.RecoveryKey;
        Assert.AreNotEqual(TestVaults.RecoveryKey, key, "une nouvelle clé de récupération");
        page.IsKeyStored = true;
        Run(page.EncryptCommand.ExecuteAsync(null));

        Assert.AreEqual(ConversionStage.Done, page.Stage, page.Result);
        var retired = Path.Combine(scene.Root, "Coffre" + KyfRev.Conversion.VaultConversion.RetiredSuffix);
        Assert.AreEqual(retired, page.Destination, "le rapport donne l'ancien coffre, à supprimer");
        Assert.IsTrue(File.Exists(Path.Combine(retired, "kyfrev-chiffre.json")));
        Assert.IsFalse(Directory.Exists(Path.Combine(scene.Root, "Coffre" + VaultUseCases.RenewedSuffix)), "le dossier temporaire a pris le nom du coffre");
        Assert.Contains(EncryptionText.RenewedNext, page.Result);
        Assert.DoesNotContain(VaultUseCases.RenewedSuffix, page.Result, "le dossier temporaire n'existe plus : le message ne le cite pas");
        Assert.IsNull(scene.App.Vaults.State().Vault, "le coffre est fermé pendant le renouvellement");

        // « Rouvrir le coffre » : à son nom, avec le mot de passe gardé ; la clé est retenue comme l'ancienne.
        Run(page.OpenNewCommand.ExecuteAsync(null));
        Assert.AreEqual(scene.Source, scene.App.Vaults.State().Vault);
        Assert.IsTrue(scene.App.Vaults.State().Encrypted);
        Assert.AreEqual(Scene.Title, scene.App.Sheets.Open(scene.Sheet).Value.Title, "la fiche se lit");
        Assert.IsTrue(scene.App.Encryption.IsRemembered, "la nouvelle clé est retenue");

        // L'ancienne clé de récupération n'ouvre plus le coffre ; la nouvelle, oui. L'ancien coffre s'ouvre encore avec elle.
        Assert.IsNotNull(scene.App.Vaults.Close().Version);
        Assert.IsNull(scene.App.Vaults.Unlock(scene.Source, VaultSecret.RecoveryKey(TestVaults.RecoveryKey), remember: false).Vault);
        Assert.AreEqual(scene.Source, scene.App.Vaults.Unlock(scene.Source, VaultSecret.RecoveryKey(key), remember: false).Vault);
        Assert.IsNotNull(scene.App.Vaults.Close().Version);
        Assert.AreEqual(retired, scene.App.Vaults.Unlock(retired, VaultSecret.RecoveryKey(TestVaults.RecoveryKey), remember: false).Vault);
    }

    [STATestMethod]
    public void A_new_password_replaces_the_old_one_and_a_plain_vault_is_not_renewed()
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: true);
        var page = scene.Show(ConversionDirection.Renew);
        page.UsesRecoveryKey = true;
        Run(page.PrepareRenewalCommand.ExecuteAsync(new RenewalSecrets(TestVaults.RecoveryKey, true, NewPassword, NewPassword)));
        Assert.AreEqual(ConversionStage.RecoveryKey, page.Stage, page.Error);

        // « Retour » oublie la clé préparée, et le secret donné.
        page.BackToPasswordCommand.Execute(null);
        Assert.AreEqual(ConversionStage.Password, page.Stage);
        Run(page.PrepareRenewalCommand.ExecuteAsync(new RenewalSecrets(TestVaults.RecoveryKey, true, NewPassword, NewPassword)));
        Assert.AreEqual(EncryptionText.KeyReplaced, page.KeyMessage);
        page.IsKeyStored = true;
        Run(page.EncryptCommand.ExecuteAsync(null));
        Assert.AreEqual(ConversionStage.Done, page.Stage, page.Result);
        Run(page.OpenNewCommand.ExecuteAsync(null));
        Assert.AreEqual(scene.Source, scene.App.Vaults.State().Vault, "le nouveau mot de passe l'ouvre");
        Assert.IsFalse(scene.App.Encryption.IsRemembered, "la clé n'était pas retenue : elle ne l'est pas davantage");
        Assert.IsNotNull(scene.App.Vaults.Close().Version);
        Assert.IsNull(scene.App.Vaults.Unlock(scene.Source, VaultSecret.Password(TestVaults.Password), remember: false).Vault, "l'ancien mot de passe ne l'ouvre plus");

        // Un coffre en clair n'a pas de clés à renouveler.
        using var plain = new Scene(encrypted: false);
        var refused = plain.Show(ConversionDirection.Renew);
        Run(refused.PrepareRenewalCommand.ExecuteAsync(new RenewalSecrets(NewPassword, false, null, null)));
        Assert.AreEqual(VaultUseCases.NotEncrypted, refused.Error);
        Assert.IsNotNull(plain.App.Vaults.Close().Version);
        Assert.AreEqual(VaultHost.NoVaultOpen, plain.App.Vaults.PrepareRenewal(VaultSecret.Password(NewPassword), null, null).Error);

        // La carte Chiffrement propose « Renouveler toutes les clés… » si la composition le lui donne.
        var renewing = 0;
        Assert.IsNotNull(scene.App.Vaults.Unlock(scene.Source, VaultSecret.Password(NewPassword), remember: false).Vault);
        Assert.AreEqual("Le mot de passe doit faire au moins 12 caractères.", scene.App.Vaults.PrepareRenewal(VaultSecret.Password(NewPassword), "trop court", "trop court").Error);
        var card = new EncryptionSettingsViewModel(scene.App.Encryption, scene.App.Settings, () => scene.App.Vaults.State().Encrypted, null, _ => { })
        {
            RenewalAsked = () => renewing++,
        };
        Assert.IsTrue(card.CanRenew);
        card.RequestRenewalCommand.Execute(null);
        Assert.AreEqual(0, renewing, "rien tant que la carte n'a rien lu");
        card.Reload();
        card.RequestRenewalCommand.Execute(null);
        Assert.AreEqual(1, renewing);
        var bare = new EncryptionSettingsViewModel(scene.App.Encryption, scene.App.Settings, () => true, null, _ => { });
        Assert.IsFalse(bare.CanRenew);
        Assert.IsFalse(bare.RequestRenewalCommand.CanExecute(null));
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void The_renewal_page_fits_a_narrow_window_and_clears_every_secret(bool dark)
    {
        using var ui = new UiThread();
        using var scene = new Scene(encrypted: true, dark);
        var page = scene.Show(ConversionDirection.Renew);
        var window = new ShellWindow(scene.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 470, Height = 540, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(scene.Test.Design.Dictionary);
        window.Show();
        var theme = dark ? "sombre" : "clair";
        try
        {
            TestShell.Flush();
            var view = Descendants(window).OfType<ConversionPage>().Single();
            Assert.IsTrue(view.RenewalPart.IsVisible);
            Assert.IsFalse(view.NewPasswordFields.IsVisible, "le mot de passe gardé ne demande rien de plus");
            Assert.IsTrue(view.Steps.IsVisible);
            UiThread.Until(() => FocusedIn(view.CurrentPasswordBox) == view.CurrentPasswordBox);
            AssertFits(view.Scroller);
            AssertNamed(view);
            view.Capture($"renouveler-{theme}-470-garder");

            // « En choisir un nouveau » : ses deux champs paraissent, sans prendre le focus.
            view.NewPasswordChoice.IsChecked = true;
            TestShell.Flush();
            Assert.IsTrue(view.NewPasswordFields.IsVisible);
            Assert.IsFalse(page.KeepsPassword);
            AssertFits(view.Scroller);
            AssertNamed(view);
            view.Capture($"renouveler-{theme}-470-nouveau");
            view.ConfirmPasswordBox.BringIntoView();
            TestShell.Flush();
            view.Capture($"renouveler-{theme}-470-etape-1");

            // Avec la clé de récupération, garder le mot de passe n'est plus possible.
            view.CurrentWithRecoveryBox.IsChecked = true;
            TestShell.Flush();
            Assert.IsTrue(view.CurrentRecoveryBox.IsVisible);
            Assert.IsFalse(view.KeepPasswordChoice.IsEnabled);
            UiThread.Until(() => FocusedIn(view.CurrentRecoveryBox) == view.CurrentRecoveryBox);
            var copied = new DataObject(DataFormats.UnicodeText, TestVaults.RecoveryKey);
            view.CurrentRecoveryBox.RaiseEvent(new DataObjectCopyingEventArgs(copied, isDragDrop: false));
            Assert.IsTrue(copied.GetDataPresent(SecretClipboard.CanIncludeInHistory), "la clé copiée reste hors de l'historique");
            AssertFits(view.Scroller);
            view.CurrentWithRecoveryBox.IsChecked = false;
            TestShell.Flush();

            // « Continuer » : le secret actuel et les nouveaux champs se vident ; la nouvelle clé paraît.
            view.CurrentPasswordBox.Password = TestVaults.Password;
            view.NewPasswordBox.Password = NewPassword;
            view.ConfirmPasswordBox.Password = NewPassword;
            Press(view.ContinueButton);
            Assert.IsEmpty(view.CurrentPasswordBox.Password);
            Assert.IsEmpty(view.NewPasswordBox.Password);
            Assert.IsEmpty(view.ConfirmPasswordBox.Password);
            UiThread.Until(() => page.IsRecoveryKey, Slow);
            TestShell.Flush();
            Assert.IsTrue(view.RecoveryPart.IsVisible);
            Assert.AreEqual(page.EncryptNowLabel, AutomationPropertiesName(view.EncryptButton));
            AssertFits(view.Scroller);
            AssertNamed(view);

            // Le renouvellement, puis « Rouvrir le coffre ».
            view.KeyStoredBox.IsChecked = true;
            Press(view.EncryptButton);
            UiThread.Until(() => page.IsFinished, Slow);
            TestShell.Flush();
            Assert.IsTrue(view.DoneMessage.IsOpen, page.Result);
            Assert.IsTrue(view.OpenNewButton.IsVisible);
            AssertFits(view.Scroller);
            AssertNamed(view);
            view.Capture($"renouveler-{theme}-470-fait");
        }
        finally
        {
            window.Close();
        }
    }

    private static string AutomationPropertiesName(DependencyObject element) => System.Windows.Automation.AutomationProperties.GetName(element);
}
