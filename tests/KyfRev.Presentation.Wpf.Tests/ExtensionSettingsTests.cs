using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
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
        StringAssert.StartsWith(scene.Page.InstallSummary, "Quiz 1.0.0, par kyfRev. 2 fichier(s), ", StringComparison.Ordinal);
        Assert.AreEqual("Bloc « quiz » | Vue « Quiz » dans le menu", string.Join(" | ", scene.Page.InstallAdds));
        Assert.IsTrue(scene.Page.InstallHasAdds);
        Assert.AreEqual(
            $"{ModulesSettingsText.ExtensionRightReadSheets} | {ModulesSettingsText.ExtensionRightStorage}",
            string.Join(" | ", scene.Page.InstallRights.Select(right => right.Text)));
        Assert.IsTrue(scene.Page.InstallRights.All(right => right.IsGranted && !right.IsNew));
        Assert.IsFalse(scene.Page.InstallHasNoRights);
        Assert.IsFalse(scene.Page.InstallHasHighRisk);
        Assert.IsFalse(scene.Page.InstallHasNewRights);
        Assert.AreEqual(ModulesSettingsText.ExtensionLimits, scene.Page.InstallLimits);
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
        Assert.AreEqual("Ajoute : bloc « quiz », vue « Quiz » dans le menu", row.Adds);
        Assert.IsTrue(row.HasRights);
        Assert.AreEqual("Lire toutes vos fiches, sans pouvoir les modifier, permission sensible | Garder ses propres données dans le coffre", string.Join(" | ", row.Rights.Select(right => right.Spoken)));
        Assert.IsTrue(row.Rights.All(right => right.IsGranted));
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

        StringAssert.StartsWith(scene.Page.InstallSummary, "Simple 1. 2 fichier(s), ", StringComparison.Ordinal);
        Assert.IsTrue(scene.Page.InstallHasNoRights);
        scene.Answer(true);

        var row = scene.Page.Rows.Single();
        Assert.AreEqual("1", row.Version);
        Assert.IsFalse(row.HasDescription);
        Assert.IsFalse(row.HasRights);
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
        Assert.AreEqual($"Quiz 2. {ModulesSettingsText.ExtensionReplacesOf("1")}", scene.Page.InstallSummary);
    }

    [STATestMethod]
    public void Writing_in_the_sheets_raises_a_red_alert_and_a_new_right_is_marked()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Library.Install(scene.Write("v1", """{"api":2,"id":"quiz","nom":"Quiz","version":"1","blocs":["quiz"],"permissions":["fiches:lecture","stockage"]}"""));
        scene.Library.SetGranted("quiz", ExtensionRight.ReadSheets, false);
        scene.Picker.Answers.Enqueue(scene.Write("v2", """
            {"api":2,"id":"quiz","nom":"Quiz","version":"2","blocs":["quiz"],"vue":{"titre":"Quiz"},
             "permissions":["fiches:lecture","stockage","selection","fiches:ecriture"],
             "commandes":[{"id":"nouveau","titre":"Nouveau quiz"}],
             "menus":{"fiche":[{"commande":"nouveau","titre":"Créer un quiz"}],"dossier":[{"commande":"nouveau","titre":"Quiz du dossier"}]},
             "encart":{"titre":"À revoir"}}
            """));

        scene.Install();

        Assert.IsTrue(scene.Page.InstallHasHighRisk);
        Assert.IsTrue(scene.Page.InstallHasNewRights);
        Assert.AreEqual(ModulesSettingsText.ExtensionLimitsWriting, scene.Page.InstallLimits);
        Assert.AreEqual(
            "Bloc « quiz » | Vue « Quiz » dans le menu | Commande « Nouveau quiz » dans la recherche | « Créer un quiz » au clic droit des fiches | « Quiz du dossier » au clic droit des dossiers | Encart « À revoir » sur Aujourd'hui",
            string.Join(" | ", scene.Page.InstallAdds));
        var rights = scene.Page.InstallRights;
        Assert.AreEqual((false, true, false), (rights[0].IsGranted, rights[0].IsWithdrawn, rights[0].IsNew), "Ce qui a été retiré le reste.");
        Assert.AreEqual((ExtensionRisk.Low, Icons.Info, ModulesSettingsText.ExtensionRightStorage), (rights[1].Risk, rights[1].Glyph, rights[1].Spoken));
        Assert.AreEqual((ExtensionRisk.Notable, Icons.Warning, true), (rights[2].Risk, rights[2].Glyph, rights[2].IsNew));
        StringAssert.EndsWith(rights[2].Spoken, ModulesSettingsText.ExtensionRiskNotable, StringComparison.Ordinal);
        Assert.AreEqual((ExtensionRisk.High, Icons.Error, true), (rights[3].Risk, rights[3].Glyph, rights[3].IsNew));
        Assert.AreEqual(ModulesSettingsText.ExtensionRightWriteSheets, rights[3].Text);
        StringAssert.EndsWith(rights[3].Spoken, ModulesSettingsText.ExtensionRiskHigh, StringComparison.Ordinal);

        scene.Answer(true);
        CollectionAssert.AreEqual(new[] { ExtensionRight.ReadSheets }, scene.Library.Withdrawn("quiz").ToArray());
        var row = scene.Page.Rows.Single();
        Assert.IsFalse(row.Rights[0].IsGranted);
        Assert.AreEqual(ModulesSettingsText.ExtensionRightCreateSheets, ExtensionRightRow.TextOf(ExtensionRight.CreateSheets));
    }

    [STATestMethod]
    public void Many_commands_are_counted_and_a_right_withdrawn_by_the_user_keeps_writing_from_the_alert()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Library.Install(scene.Write("v1", """{"api":2,"id":"quiz","nom":"Quiz","version":"1","blocs":["quiz"],"permissions":["fiches:ecriture"]}"""));
        scene.Library.SetGranted("quiz", ExtensionRight.WriteSheets, false);
        scene.Picker.Answers.Enqueue(scene.Write("v2", """
            {"api":2,"id":"quiz","nom":"Quiz","version":"2","permissions":["fiches:ecriture"],
             "commandes":[{"id":"a","titre":"A"},{"id":"b","titre":"B"},{"id":"c","titre":"C"}]}
            """));

        scene.Install();

        Assert.AreEqual("3 commandes dans la recherche", scene.Page.InstallAdds.Single());
        Assert.IsFalse(scene.Page.InstallHasHighRisk, "Retirée, l'écriture n'est pas accordée par la mise à jour.");
        Assert.IsFalse(scene.Page.InstallHasNewRights);
        Assert.AreEqual(ModulesSettingsText.ExtensionLimits, scene.Page.InstallLimits);
    }

    [STATestMethod]
    public void A_right_is_withdrawn_from_the_card_and_given_back()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Library.Install(Path.Combine(Published, "quiz"));
        scene.Page.Reload();
        var right = scene.Page.Rows.Single().Rights[0];

        right.IsGranted = false;
        Assert.AreEqual(ModulesSettingsText.ExtensionRightWithdrawnOf("Quiz", ModulesSettingsText.ExtensionRightReadSheets), scene.Page.Message);
        CollectionAssert.AreEqual(new[] { ExtensionRight.ReadSheets }, scene.Library.Withdrawn("quiz").ToArray());
        scene.Page.Reload();
        Assert.IsFalse(scene.Page.Rows.Single().Rights[0].IsGranted);

        scene.Page.Rows.Single().Rights[0].IsGranted = true;
        Assert.AreEqual(ModulesSettingsText.ExtensionRightGrantedOf("Quiz", ModulesSettingsText.ExtensionRightReadSheets), scene.Page.Message);
        Assert.IsEmpty(scene.Library.Withdrawn("quiz"));
    }

    [STATestMethod]
    public void A_right_that_cannot_be_withdrawn_goes_back_and_says_why()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Library.Install(Path.Combine(Published, "quiz"));
        scene.Page.Reload();
        var right = scene.Page.Rows.Single().Rights[0];
        scene.Library.Retire("quiz");

        right.IsGranted = false;

        Assert.IsTrue(scene.Page.HasError);
        Assert.IsTrue(right.IsGranted);
    }

    [STATestMethod]
    public void The_confirmation_shows_each_right_and_turns_red_for_writing()
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
            var boxes = Tree<CheckBox>(page).Where(box => box.DataContext is ExtensionRightRow).ToList();
            Assert.HasCount(2, boxes);
            Assert.AreEqual(scene.Page.Rows.Single().Rights[0].Spoken, AutomationProperties.GetName(boxes[0]));

            scene.Picker.Answers.Enqueue(scene.Write("ecrit", """{"api":2,"id":"ecrit","nom":"Écrit","version":"1","blocs":["ecrit"],"permissions":["fiches:ecriture","stockage"]}"""));
            page.InstallExtension.Command.Execute(null);
            UiThread.Until(() => scene.Page.IsConfirmingInstall);
            TestShell.Flush();

            Assert.IsTrue(page.InstallConfirmation.IsDestructive, "Le bouton se fait celui d'une action qui compte, et Entrée ne le déclenche pas.");
            Assert.IsTrue(page.InstallHighRisk.IsOpen);
            Assert.IsFalse(page.InstallNewRights.IsOpen);
            Assert.AreEqual(2, page.InstallRights.Items.Count);
            Assert.AreEqual(Visibility.Collapsed, page.InstallNoRights.Visibility);
            Assert.AreEqual(ModulesSettingsText.ExtensionLimitsWriting, page.InstallLimits.Text);
            page.InstallConfirmation.Close(DialogChoice.Secondary);
            TestShell.Flush();
            Assert.IsFalse(scene.Page.IsConfirmingInstall);
        }
        finally
        {
            window.Close();
        }
    }

    private static List<T> Tree<T>(DependencyObject root)
        where T : DependencyObject
    {
        var found = new List<T>();
        void Walk(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    found.Add(match);
                }
                Walk(child);
            }
        }
        Walk(root);
        return found;
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
