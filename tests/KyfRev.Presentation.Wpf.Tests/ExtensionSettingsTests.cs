using System.IO;
using System.Windows;
using System.Windows.Controls;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La carte « Extensions » des Paramètres : rien ne s'installe sans avoir montré ce que l'extension demande.</summary>
[TestClass]
public sealed class ExtensionSettingsTests
{
    private static string Published => Path.Combine(Repository.Root, "tests", "fixtures", "extensions");

    [TestMethod]
    public void The_card_needs_its_library_and_its_picker()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionSettingsViewModel(null!, new FakePicker()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionSettingsViewModel(new ExtensionLibrary("x", TimeProvider.System), null!));
    }

    [STATestMethod]
    public void With_nothing_installed_the_card_says_so()
    {
        using var ui = new UiThread();
        using var scene = new Scene();

        scene.Page.Reload();

        Assert.IsTrue(scene.Page.IsEmpty);
        Assert.IsEmpty(scene.Page.Rows);
        Assert.IsFalse(scene.Page.HasError);
        Assert.IsFalse(scene.Page.HasMessage);
    }

    [STATestMethod]
    public void Giving_up_the_choice_of_a_folder_does_nothing()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Picker.Answers.Enqueue(null);

        scene.Install();

        Assert.AreEqual(ModulesSettingsText.ExtensionPick, scene.Picker.Titles.Single());
        Assert.IsFalse(scene.Page.IsConfirmingInstall);
        Assert.IsTrue(scene.Page.IsEmpty);
    }

    [STATestMethod]
    public void A_folder_that_will_not_do_says_why()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Picker.Answers.Enqueue(Path.Combine(scene.Root, "absent"));

        scene.Install();

        Assert.IsTrue(scene.Page.HasError);
        StringAssert.Contains(scene.Page.Error, "introuvable", StringComparison.Ordinal);
        Assert.IsFalse(scene.Page.IsConfirmingInstall);
    }

    [STATestMethod]
    public void Installing_shows_what_it_asks_before_anything_is_copied()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Picker.Answers.Enqueue(Path.Combine(Published, "quiz"));

        scene.Install();

        Assert.IsTrue(scene.Page.IsConfirmingInstall);
        Assert.AreEqual(ModulesSettingsText.ExtensionInstallTitle, scene.Page.InstallTitle);
        Assert.AreEqual(ModulesSettingsText.ExtensionInstall, scene.Page.InstallAction);
        StringAssert.Contains(scene.Page.InstallDetails, "Quiz 1.0.0, par kyfRev", StringComparison.Ordinal);
        StringAssert.Contains(scene.Page.InstallDetails, "bloc « quiz », vue « Quiz »", StringComparison.Ordinal);
        StringAssert.Contains(scene.Page.InstallDetails, ModulesSettingsText.ExtensionReadSheets, StringComparison.Ordinal);
        StringAssert.Contains(scene.Page.InstallDetails, ModulesSettingsText.ExtensionStorage, StringComparison.Ordinal);
        StringAssert.Contains(scene.Page.InstallDetails, ModulesSettingsText.ExtensionLimits, StringComparison.Ordinal);
        StringAssert.Contains(scene.Page.InstallDetails, "2 fichier(s)", StringComparison.Ordinal);
        StringAssert.Contains(scene.Page.InstallDetails, ModulesSettingsText.ExtensionTrust, StringComparison.Ordinal);
        Assert.IsEmpty(scene.Library.Installed(), "Rien n'est copié avant la confirmation.");

        scene.Answer(false);

        Assert.IsFalse(scene.Page.IsConfirmingInstall);
        Assert.IsEmpty(scene.Library.Installed());

        scene.Picker.Answers.Enqueue(Path.Combine(Published, "quiz"));
        scene.Install();
        scene.Answer(true);

        var row = scene.Page.Rows.Single();
        Assert.AreEqual(ModulesSettingsText.ExtensionInstalledOf("Quiz", "1.0.0"), scene.Page.Message);
        Assert.AreEqual("Quiz", row.Name);
        Assert.AreEqual("1.0.0 · kyfRev", row.Version);
        Assert.IsTrue(row.HasDescription);
        Assert.IsTrue(row.IsActive);
        Assert.AreEqual("Ajoute : bloc « quiz », vue « Quiz »", row.Adds);
        Assert.IsTrue(row.HasPermissions);
        StringAssert.Contains(row.Permissions, ModulesSettingsText.ExtensionAnd, StringComparison.Ordinal);
        Assert.AreEqual(ModulesSettingsText.ExtensionRemoveNameOf("Quiz"), row.RemoveName);
        Assert.IsFalse(scene.Page.IsEmpty);
        Assert.IsFalse(scene.Page.IsBusy);
    }

    [STATestMethod]
    public void An_extension_without_author_nor_permission_is_said_as_such()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Picker.Answers.Enqueue(scene.Write("simple", """{"id":"simple","nom":"Simple","version":"1","blocs":["simple"]}"""));

        scene.Install();

        StringAssert.Contains(scene.Page.InstallDetails, "Simple 1 : bloc « simple ».", StringComparison.Ordinal);
        StringAssert.Contains(scene.Page.InstallDetails, ModulesSettingsText.ExtensionNoPermission, StringComparison.Ordinal);
        scene.Answer(true);

        var row = scene.Page.Rows.Single();
        Assert.AreEqual("1", row.Version);
        Assert.IsFalse(row.HasDescription);
        Assert.IsFalse(row.HasPermissions);
    }

    [STATestMethod]
    public void A_new_version_says_it_replaces_the_old_one()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Library.Install(scene.Write("v1", """{"id":"quiz","nom":"Quiz","version":"1","blocs":["quiz"]}"""));
        scene.Picker.Answers.Enqueue(scene.Write("v2", """{"id":"quiz","nom":"Quiz","version":"2","blocs":["quiz"]}"""));

        scene.Install();

        Assert.AreEqual(ModulesSettingsText.ExtensionUpdateTitle, scene.Page.InstallTitle);
        Assert.AreEqual(ModulesSettingsText.ExtensionUpdate, scene.Page.InstallAction);
        StringAssert.Contains(scene.Page.InstallDetails, ModulesSettingsText.ExtensionReplacesOf("1"), StringComparison.Ordinal);
    }

    [STATestMethod]
    public void An_installation_that_fails_at_the_end_says_why()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var source = scene.Write("v1", """{"id":"quiz","nom":"Quiz","version":"1","blocs":["quiz"]}""");
        scene.Picker.Answers.Enqueue(source);
        scene.Install();

        Directory.Delete(source, recursive: true);
        scene.Answer(true);

        Assert.IsTrue(scene.Page.HasError);
        Assert.IsEmpty(scene.Page.Rows);
    }

    [STATestMethod]
    public void Switching_it_off_and_on_is_kept_and_said()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Library.Install(Path.Combine(Published, "bilan"));
        scene.Page.Reload();
        var row = scene.Page.Rows.Single();

        row.IsActive = false;
        Assert.AreEqual(ModulesSettingsText.ExtensionDisabledOf("Bilan du coffre"), scene.Page.Message);
        Assert.IsNull(scene.Library.Active("bilan"));

        row.IsActive = true;
        Assert.AreEqual(ModulesSettingsText.ExtensionEnabledOf("Bilan du coffre"), scene.Page.Message);
        Assert.IsNotNull(scene.Library.Active("bilan"));
    }

    [STATestMethod]
    public void A_switch_that_fails_goes_back_and_says_why()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Library.Install(Path.Combine(Published, "bilan"));
        scene.Page.Reload();
        var row = scene.Page.Rows.Single();
        scene.Library.Retire("bilan");

        row.IsActive = false;

        Assert.IsTrue(scene.Page.HasError);
        Assert.IsTrue(row.IsActive);
    }

    [STATestMethod]
    public void Removing_asks_first_and_stores_it()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Library.Install(Path.Combine(Published, "bilan"));
        scene.Page.Reload();
        var row = scene.Page.Rows.Single();

        scene.Page.RequestRemoveCommand.Execute(row);
        Assert.IsTrue(scene.Page.IsConfirmingRemove);
        StringAssert.Contains(scene.Page.RemoveDetails, "Bilan du coffre", StringComparison.Ordinal);

        scene.Page.AnswerRemoveCommand.Execute(false);
        Assert.IsFalse(scene.Page.IsConfirmingRemove);
        Assert.HasCount(1, scene.Library.Installed());

        scene.Page.RequestRemoveCommand.Execute(row);
        scene.Page.AnswerRemoveCommand.Execute(true);
        Assert.AreEqual(ModulesSettingsText.ExtensionRemovedOf("Bilan du coffre"), scene.Page.Message);
        Assert.IsEmpty(scene.Page.Rows);
        Assert.IsTrue(scene.Page.IsEmpty);
        Assert.IsTrue(Directory.Exists(Path.Combine(scene.Folder, ".anciennes")));
    }

    [STATestMethod]
    public void A_removal_that_is_refused_says_why()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Library.Install(Path.Combine(Published, "bilan"));
        scene.Page.Reload();
        var row = scene.Page.Rows.Single();
        scene.Page.RequestRemoveCommand.Execute(row);
        var state = Path.Combine(scene.Folder, "etat.json");
        using (new FileStream(state, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            scene.Page.AnswerRemoveCommand.Execute(true);
        }

        Assert.IsTrue(scene.Page.HasError);
        Assert.HasCount(1, scene.Page.Rows);
    }

    [STATestMethod]
    public void The_card_shows_the_extensions_and_asks_in_a_dialog()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        using var vault = new ReadingVault("Texte", passage: false);
        scene.Library.Install(Path.Combine(Published, "quiz"));
        var test = new TestShell();
        var model = new ModulesSettingsViewModel(vault.App.Settings, _ => { }, scene.Page);
        var page = new ModulesSettingsPage { DataContext = model };
        var host = new Border { Width = 640, Height = 900, Child = page };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 800, Height = 1000, ShowActivated = false };
        window.Show();
        TestShell.Flush();
        try
        {
            model.OnNavigatedTo();
            TestShell.Flush();

            Assert.AreEqual(Visibility.Collapsed, page.NoExtension.Visibility);
            Assert.AreEqual(1, page.ExtensionsList.Items.Count);
            Assert.AreEqual(Visibility.Visible, page.ExtensionsNotShownYet.Visibility);
            Assert.IsTrue(page.InstallExtension.IsEnabled);

            scene.Picker.Answers.Enqueue(Path.Combine(Published, "bilan"));
            page.InstallExtension.Command.Execute(null);
            UiThread.Until(() => scene.Page.IsConfirmingInstall);
            TestShell.Flush();
            Assert.IsTrue(page.InstallConfirmation.IsOpen);

            page.InstallConfirmation.Close(DialogChoice.Primary);
            UiThread.Until(() => scene.Page.Rows.Count == 2);

            scene.Page.RequestRemoveCommand.Execute(scene.Page.Rows[0]);
            TestShell.Flush();
            Assert.IsTrue(page.RemoveConfirmation.IsOpen);
            page.RemoveConfirmation.Close(DialogChoice.Secondary);
            TestShell.Flush();
            Assert.IsFalse(scene.Page.IsConfirmingRemove);
            Assert.HasCount(2, scene.Page.Rows);

            scene.Page.RequestRemoveCommand.Execute(scene.Page.Rows[0]);
            TestShell.Flush();
            page.RemoveConfirmation.Close(DialogChoice.Primary);
            TestShell.Flush();
            Assert.HasCount(1, scene.Page.Rows);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>La carte, sur une bibliothèque d'essai.</summary>
    private sealed class Scene : IDisposable
    {
        public Scene()
        {
            Root = Path.Combine(Path.GetTempPath(), "kyfrev-extensions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Library = new ExtensionLibrary(Folder, TimeProvider.System);
            Page = new ExtensionSettingsViewModel(Library, Picker);
        }

        public string Root { get; }

        public string Folder => Path.Combine(Root, "bibliotheque");

        public ExtensionLibrary Library { get; }

        public FakePicker Picker { get; } = new();

        public ExtensionSettingsViewModel Page { get; }

        public void Install()
        {
            var running = Page.InstallCommand.ExecuteAsync(null);
            UiThread.Until(() => running.IsCompleted);
        }

        public void Answer(bool confirmed)
        {
            var running = Page.AnswerInstallCommand.ExecuteAsync(confirmed);
            UiThread.Until(() => running.IsCompleted);
        }

        public string Write(string name, string manifest)
        {
            var folder = Path.Combine(Root, "sources", name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, ExtensionManifest.FileName), manifest);
            File.WriteAllText(Path.Combine(folder, "extension.js"), string.Empty);
            return folder;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
