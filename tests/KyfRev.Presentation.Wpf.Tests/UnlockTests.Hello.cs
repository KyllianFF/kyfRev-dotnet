using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Windows Hello sur l'écran de déverrouillage (rangée 29C) : sa clé retenue scellée par lui, le coffre propose d'abord
/// « Ouvrir avec Windows Hello », au démarrage comme après un verrouillage ; le mot de passe reste à un clic.
/// </summary>
public sealed partial class UnlockTests
{
    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void With_windows_hello_the_screen_offers_it_first_and_the_password_one_click_away(bool dark)
    {
        using var ui = new UiThread();
        var hello = new FakeHello();
        using var test = new TestVaults(AppMode.Portable, hello: hello);
        test.CreateEncryptedVault(test.Planned);
        Assert.AreEqual(test.Planned, test.App.Vaults.Unlock(test.Planned, VaultSecret.Password(TestVaults.Password), remember: true).Vault);
        var shell = new TestShell(dark);
        VaultStartup.ShowUnlock(shell.Shell, test.App.Vaults, test.Picker, test.App.Vaults.Lock());
        var window = new ShellWindow(shell.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 470, Height = 540, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(shell.Design.Dictionary);
        window.Show();
        var theme = dark ? "sombre" : "clair";
        try
        {
            TestShell.Flush();
            var page = (UnlockPageViewModel)shell.Shell.Current.Page;
            Assert.IsTrue(page.OffersHello);
            Assert.IsTrue(page.Remember, "ouvrir une fois par le mot de passe n'oublie pas Windows Hello");
            Assert.AreEqual(EncryptionText.HelloIntro, page.Intro);
            var scroller = Named<ScrollViewer>(window, "Scroller");
            var helloButton = Named<Button>(window, "HelloButton");
            Assert.AreEqual(Visibility.Visible, Named<StackPanel>(window, "HelloPart").Visibility);
            Assert.AreEqual(Visibility.Collapsed, Named<StackPanel>(window, "SecretPart").Visibility);
            Assert.AreEqual(EncryptionText.OpenWithHello, AutomationProperties.GetName(helloButton));
            Assert.IsTrue(helloButton.IsDefault, "Entrée ouvre avec Windows Hello");
            UiThread.Until(() => helloButton.IsFocused);
            AssertFits(scroller);
            scroller.Capture($"hello-{theme}-470");

            // L'utilisateur renonce : rien ne se dit. Windows refuse : la raison se dit.
            hello.Cancels = true;
            Press(helloButton);
            UiThread.Until(() => page.IsIdle);
            Assert.IsFalse(page.HasError);
            Assert.IsTrue(shell.Shell.IsWelcome);
            hello.Cancels = false;
            hello.Refuses = true;
            Press(helloButton);
            UiThread.Until(() => page.HasError);
            Assert.AreEqual(FakeHello.Refusal, page.Error);
            Assert.IsTrue(Named<Notification>(window, "ErrorMessage").IsOpen);
            hello.Refuses = false;

            // Le geste : le coffre s'ouvre.
            Press(helloButton);
            UiThread.Until(() => !shell.Shell.IsWelcome);
            Assert.AreEqual(test.Planned, test.App.Vaults.State().Vault);
            Assert.AreEqual(3, hello.Asked);

            // Verrouillé de nouveau, « Utiliser le mot de passe » : ses champs, sa case cochée, le focus au mot de passe.
            VaultStartup.ShowUnlock(shell.Shell, test.App.Vaults, test.Picker, test.App.Vaults.Lock());
            TestShell.Flush();
            var again = (UnlockPageViewModel)shell.Shell.Current.Page;
            scroller = Named<ScrollViewer>(window, "Scroller");
            Press(Named<Button>(window, "TypePasswordButton"));
            TestShell.Flush();
            Assert.IsFalse(again.OffersHello);
            var password = Named<PasswordBox>(window, "PasswordBox");
            Assert.AreEqual(Visibility.Collapsed, Named<StackPanel>(window, "HelloPart").Visibility);
            UiThread.Until(() => password.IsFocused);
            var remember = Named<CheckBox>(window, "RememberBox");
            Assert.AreEqual(EncryptionText.RememberHello, AutomationProperties.GetName(remember));
            Assert.AreEqual(EncryptionText.RememberHelloHelp, Named<TextBlock>(window, "RememberHelp").Text);
            Assert.IsTrue(remember.IsChecked);
            Assert.IsTrue(Named<Button>(window, "UnlockButton").IsDefault);
            Assert.AreEqual(EncryptionText.LockedIntro, again.Intro);
            AssertFits(scroller);
            scroller.Capture($"hello-{theme}-470-mot-de-passe");

            password.Password = TestVaults.Password;
            Press(Named<Button>(window, "UnlockButton"));
            UiThread.Until(() => !shell.Shell.IsWelcome);
            Assert.IsTrue(test.App.Vaults.OffersHello(test.Planned), "toujours retenue avec Windows Hello");
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void The_remember_box_names_windows_hello_only_where_it_is_configured()
    {
        using var withHello = new TestVaults(AppMode.Development, hello: new FakeHello());
        var card = new EncryptionSettingsViewModel(withHello.App.Encryption, withHello.App.Settings, () => true, null, _ => { });
        Assert.AreEqual(EncryptionText.RememberHello, card.RememberLabel);
        Assert.AreEqual(EncryptionText.RememberHelloHelp, card.RememberHelp);

        using var without = new TestVaults(AppMode.Development);
        var plain = new EncryptionSettingsViewModel(without.App.Encryption, without.App.Settings, () => true, null, _ => { });
        Assert.AreEqual(EncryptionText.Remember, plain.RememberLabel);
        Assert.AreEqual(EncryptionText.RememberHelp, plain.RememberHelp);
        var locked = without.App.Vaults.State() with { Locked = without.PathOf("Coffre") };
        var page = new UnlockPageViewModel(without.App.Vaults, without.Picker, locked, _ => { }, _ => { });
        Assert.IsFalse(page.OffersHello);
        Assert.IsTrue(page.ShowsSecret);
        Assert.AreEqual(EncryptionText.Remember, page.RememberLabel);
        Assert.AreEqual(EncryptionText.RememberHelp, page.RememberHelp);
        page.UnlockWithHelloCommand.Execute(null);
        Assert.IsNull(page.Error, "sans Windows Hello, le bouton ne fait rien");
    }

    /// <summary>Windows Hello simulé : sceller inverse les octets, rouvrir les remet, et compte les fois où il demande.</summary>
    private sealed class FakeHello : IWindowsHello
    {
        public const string Refusal = "Windows Hello a refusé.";

        public bool Cancels { get; set; }

        public bool Refuses { get; set; }

        public int Asked { get; private set; }

        public bool IsAvailable => true;

        public byte[]? Seal(ReadOnlySpan<byte> secret)
        {
            var reversed = secret.ToArray();
            Array.Reverse(reversed);
            return reversed;
        }

        public byte[]? Open(byte[] sealedSecret, string message)
        {
            Asked++;
            if (Refuses)
            {
                throw new KyfRev.Infrastructure.Crypto.VaultCryptoException(Refusal);
            }
            if (Cancels)
            {
                return null;
            }
            var reversed = (byte[])sealedSecret.Clone();
            Array.Reverse(reversed);
            return reversed;
        }
    }
}
