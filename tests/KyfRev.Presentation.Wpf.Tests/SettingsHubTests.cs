using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Les onglets de la vue « Paramètres » : Données et À propos, sur de vrais coffres.</summary>
[TestClass]
public sealed class SettingsHubTests
{
    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose", "Étapes"], 5, "sha256:aa");

    private static readonly string[] Tabs = ["Général", "Révisions", "Modules et extensions", "Données", "À propos"];

    [TestMethod]
    public async Task Looking_for_an_update_says_what_exists_and_opens_only_its_page()
    {
        using var vault = new DataVault();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var manifest = UpdateChecker.Write("9.0.0", new DateOnly(2026, 11, 8));
        var online = new AnnouncedVersion(manifest, ExtensionCatalogue.Sign(manifest, key, "essai"));
        var links = new RecordingLinks();
        var about = new AboutSettingsViewModel(vault.App.Vaults, new UpdateChecker(online, [new("essai", key.ExportSubjectPublicKeyInfoPem())], "1.0.0"), links);
        Assert.IsTrue(about.CanCheckUpdates);
        Assert.AreEqual(0, online.Reads, "rien n'est lu sans le demander");

        await about.CheckUpdateCommand.ExecuteAsync(null);

        Assert.AreEqual(SettingsHubText.UpdateAvailableOf("9.0.0", "1.0.0"), about.UpdateMessage);
        Assert.IsTrue(about.HasRelease && about.HasUpdateMessage);
        Assert.AreEqual(SettingsHubText.CheckUpdate, about.CheckLabel);
        about.OpenReleaseCommand.Execute(null);
        Assert.AreEqual("https://github.com/KyllianFF/kyfRev-dotnet/releases/tag/v9.0.0", links.Opened.Single());
    }

    [TestMethod]
    public async Task An_update_search_that_fails_says_why_and_offers_nothing()
    {
        using var vault = new DataVault();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var about = new AboutSettingsViewModel(vault.App.Vaults, new UpdateChecker(new AnnouncedVersion([], []), [new("essai", key.ExportSubjectPublicKeyInfoPem())], "1.0.0"), new RecordingLinks());

        await about.CheckUpdateCommand.ExecuteAsync(null);

        Assert.IsTrue(about.HasUpdateError);
        Assert.IsFalse(about.HasRelease || about.HasUpdateMessage);
    }

    [TestMethod]
    public async Task Without_a_checker_nothing_is_searched()
    {
        using var vault = new DataVault();
        var about = new AboutSettingsViewModel(vault.App.Vaults);

        await about.CheckUpdateCommand.ExecuteAsync(null);
        about.OpenReleaseCommand.Execute(null);

        Assert.IsFalse(about.CanCheckUpdates || about.HasUpdateMessage || about.HasUpdateError);
    }

    [TestMethod]
    public void The_old_version_is_offered_for_removal_and_only_through_windows()
    {
        using var vault = new DataVault();
        var legacy = new FakeLegacy { Installed = new LegacyInstall("0.7.0", @"C:\kyfRev") };
        var about = new AboutSettingsViewModel(vault.App.Vaults, legacy: legacy);

        about.OnNavigatedTo();
        Assert.AreEqual(SettingsHubText.LegacyInstalledOf("0.7.0"), about.LegacyMessage);
        Assert.IsTrue(about.HasLegacy);
        about.OpenInstalledAppsCommand.Execute(null);
        Assert.AreEqual(1, legacy.Opened);

        legacy.Installed = null;
        about.OnNavigatedTo();
        Assert.IsFalse(about.HasLegacy, "une fois retirée, plus rien ne la propose");
    }

    [TestMethod]
    public void An_old_version_in_this_folder_is_said_to_remove_this_one_too()
    {
        using var vault = new DataVault();
        var legacy = new FakeLegacy { Installed = new LegacyInstall("0.7.0", @"C:\kyfRev", SharesThisFolder: true) };
        var about = new AboutSettingsViewModel(vault.App.Vaults, legacy: legacy);

        about.OnNavigatedTo();

        Assert.AreEqual(SettingsHubText.LegacySharedOf("0.7.0"), about.LegacyMessage);
    }

    [TestMethod]
    [DataRow(8, "kyfRev 1.0.0-essai, publiée le 8 novembre 2026")]
    [DataRow(1, "kyfRev 1.0.0-essai, publiée le 1er novembre 2026")]
    public void The_tab_says_the_day_the_version_was_published(int day, string expected)
    {
        using var vault = new DataVault();
        var about = new AboutSettingsViewModel(vault.App.Vaults, released: new DateOnly(2026, 11, day));

        about.OnNavigatedTo();

        Assert.AreEqual(expected, about.Version);
    }

    [TestMethod]
    public void Without_the_port_nothing_is_offered()
    {
        using var vault = new DataVault();
        var about = new AboutSettingsViewModel(vault.App.Vaults);

        about.OnNavigatedTo();
        about.OpenInstalledAppsCommand.Execute(null);

        Assert.IsFalse(about.HasLegacy);
    }

    [TestMethod]
    public void The_view_needs_its_tabs()
    {
        using var vault = new DataVault();
        var revisions = new SettingsPageViewModel(vault.App.Settings, TimeProvider.System);
        var about = new AboutSettingsViewModel(vault.App.Vaults);
        var general = DataVault.General();
        var modules = new ModulesSettingsViewModel(vault.App.Settings, _ => { }, FakeExtensions.Settings());
        Assert.ThrowsExactly<ArgumentNullException>(() => new SettingsHubViewModel(null!, revisions, modules, vault.Page, about));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SettingsHubViewModel(general, null!, modules, vault.Page, about));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SettingsHubViewModel(general, revisions, null!, vault.Page, about));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SettingsHubViewModel(general, revisions, modules, null!, about));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SettingsHubViewModel(general, revisions, modules, vault.Page, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AboutSettingsViewModel(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => about.Show(null!));
    }

    [TestMethod]
    public void The_data_tab_needs_everything_it_uses()
    {
        using var vault = new DataVault();
        var app = vault.App;
        var (picker, opener) = (vault.Picker, new FakeOpener());
        Assert.ThrowsExactly<ArgumentNullException>(() => new DataSettingsViewModel(null!, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, picker, opener, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DataSettingsViewModel(app.Vaults, null!, app.Trash, app.Annotations, app.Exports, app.Settings, picker, opener, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DataSettingsViewModel(app.Vaults, app.Backups, null!, app.Annotations, app.Exports, app.Settings, picker, opener, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, null!, app.Exports, app.Settings, picker, opener, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, null!, app.Settings, picker, opener, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, null!, picker, opener, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, null!, opener, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, picker, null!, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, picker, opener, null!));
    }

    [TestMethod]
    public void The_view_opens_on_the_first_tab_and_keeps_one_selected()
    {
        using var vault = new DataVault();
        var hub = vault.Hub();
        Assert.AreEqual("Paramètres", SettingsHubViewModel.Title);
        CollectionAssert.AreEqual(Tabs, hub.Tabs.Select(tab => tab.Label).ToArray());
        Assert.AreSame(hub.Tabs[0], hub.Selected);
        Assert.IsTrue(hub.Tabs[0].IsSelected);
        hub.OnNavigatedTo();

        hub.Tabs[4].IsSelected = true;
        Assert.AreSame(hub.Tabs[4], hub.Selected);
        Assert.IsFalse(hub.Tabs[0].IsSelected);
        Assert.AreEqual("kyfRev 1.0.0-essai", ((AboutSettingsViewModel)hub.Selected.Page).Version, "l'onglet se lit quand on l'ouvre");

        hub.Tabs[4].IsSelected = false;
        Assert.AreSame(hub.Tabs[4], hub.Selected, "décocher ne choisit rien d'autre");
        hub.Tabs[4].IsSelected = true;
        Assert.AreSame(hub.Tabs[4], hub.Selected);
    }

    [TestMethod]
    [DataRow(AppMode.Portable, "portable (tout reste dans le dossier MesRévisions)")]
    [DataRow(AppMode.Installed, "installé (le programme dans son dossier, vos données dans votre profil)")]
    [DataRow(AppMode.Development, "développement")]
    public void About_says_what_runs_and_where(AppMode mode, string expected)
    {
        using var vaults = new TestVaults(AppMode.Development);
        var about = new AboutSettingsViewModel(vaults.App.Vaults);

        about.Show(new AppState("1.2.3", mode, "D:\\Coffre", null, null));

        Assert.AreEqual("kyfRev 1.2.3", about.Version);
        Assert.AreEqual(expected, about.Mode);
        Assert.AreEqual("D:\\Coffre", about.Vault);

        about.Show(new AppState("1.2.3", mode, null, null, null));
        Assert.AreEqual(string.Empty, about.Vault);
    }

    [TestMethod]
    public async Task The_data_tab_shows_the_vault_and_its_backups()
    {
        using var vault = new DataVault();
        var page = await vault.LoadedAsync();

        StringAssert.Contains(page.VaultLine, vault.Root, StringComparison.Ordinal);
        StringAssert.StartsWith(page.VaultLine, "Ouvert : ", StringComparison.Ordinal);
        StringAssert.Contains(page.VaultHelp, "rouvrira le dernier coffre", StringComparison.Ordinal);
        StringAssert.StartsWith(page.BackupHelp, "Une sauvegarde .zip quotidienne, vérifiée, rangée hors du coffre dans « ", StringComparison.Ordinal);
        Assert.IsTrue(page.HasBackups, "la sauvegarde du jour a été faite à l'ouverture");
        Assert.IsFalse(page.HasNoBackup);
        Assert.IsTrue(page.HasNoTrash);
        Assert.IsFalse(page.HasTrash);
        Assert.IsFalse(page.HasError);
        Assert.IsTrue(page.IsIdle);
    }

    [TestMethod]
    public async Task The_portable_mode_says_where_the_vault_reopens()
    {
        using var vaults = new TestVaults(AppMode.Portable);
        vaults.App.Vaults.Create(vaults.Planned);
        var page = DataVault.PageOn(vaults, () => { });

        await page.ReloadCommand.ExecuteAsync(null);

        StringAssert.Contains(page.VaultHelp, "En version portable, kyfRev rouvre toujours MesRévisions\\Coffre au démarrage.", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task A_closed_vault_says_why_nothing_shows()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = DataVault.PageOn(vaults, () => { });

        await page.ReloadCommand.ExecuteAsync(null);

        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.HasNoBackup, "rien n'a été lu : l'onglet ne prétend pas qu'il n'y a rien");
        Assert.IsFalse(page.HasNoTrash);
    }

    [TestMethod]
    public async Task Backing_up_adds_a_line_and_the_rotation_is_saved()
    {
        using var vault = new DataVault();
        var page = await vault.LoadedAsync();
        var before = page.Backups.Count;
        foreach (var file in Directory.GetFiles(vault.App.Backups.List().Value.Folder))
        {
            File.Delete(file);
        }

        await page.BackupNowCommand.ExecuteAsync(null);

        Assert.IsFalse(page.HasError, page.Error);
        Assert.AreEqual("Sauvegarde faite.", page.Message);
        Assert.IsTrue(page.HasMessage);
        Assert.HasCount(1, page.Backups);
        Assert.IsGreaterThanOrEqualTo(1, before);
        var line = page.Backups[0];
        StringAssert.StartsWith(line.RestoreName, "Restaurer la sauvegarde du ", StringComparison.Ordinal);
        StringAssert.Contains(line.RestoreName, line.Moment, StringComparison.Ordinal);

        var rotation = !page.Rotation;
        page.Rotation = rotation;
        page.SaveRotationCommand.Execute(null);

        Assert.IsFalse(page.HasError, page.Error);
        Assert.AreEqual(rotation, vault.App.Settings.Get().Value.BackupRotation);
        Assert.AreEqual(rotation, vault.App.Backups.List().Value.Rotation);
    }

    [TestMethod]
    public async Task Without_any_backup_the_tab_says_so()
    {
        using var vault = new DataVault();
        foreach (var file in Directory.GetFiles(vault.App.Backups.List().Value.Folder))
        {
            File.Delete(file);
        }

        var page = await vault.LoadedAsync();

        Assert.IsEmpty(page.Backups);
        Assert.IsTrue(page.HasNoBackup);
        Assert.IsFalse(page.HasBackups);
    }

    [TestMethod]
    public async Task A_refused_restoration_changes_nothing()
    {
        using var vault = new DataVault();
        var page = await vault.LoadedAsync();

        await page.AnswerRestoreCommand.ExecuteAsync(true);
        Assert.IsFalse(page.HasRestoredVault, "rien n'a été demandé");

        page.RequestRestoreCommand.Execute(page.Backups[0]);
        Assert.IsTrue(page.IsConfirming);
        Assert.AreEqual($"La sauvegarde du {page.Backups[0].Moment} sera extraite dans un nouveau dossier, à côté du coffre. Le coffre actuel ne sera pas modifié.", page.ConfirmMessage);

        await page.AnswerRestoreCommand.ExecuteAsync(false);

        Assert.IsFalse(page.IsConfirming);
        Assert.IsFalse(page.HasRestoredVault);
    }

    [TestMethod]
    public async Task A_restored_backup_lands_beside_the_vault_and_can_be_opened()
    {
        using var vault = new DataVault();
        var page = await vault.LoadedAsync();

        page.RequestRestoreCommand.Execute(page.Backups[0]);
        await page.AnswerRestoreCommand.ExecuteAsync(true);

        Assert.IsFalse(page.HasError, page.Error);
        Assert.IsTrue(page.HasRestoredVault);
        Assert.AreEqual(vault.Root, vault.App.Vaults.State().Vault, "le coffre actuel reste ouvert");
        Assert.IsTrue(Directory.Exists(page.RestoredVault));
        Assert.AreEqual($"Sauvegarde restaurée dans « {page.RestoredVault} ». Le coffre actuel n'a pas été modifié.", page.Message);
        var restored = page.RestoredVault;

        await page.OpenRestoredVaultCommand.ExecuteAsync(null);

        Assert.AreEqual(1, vault.Changes);
        Assert.AreEqual(restored, vault.App.Vaults.State().Vault);
        Assert.IsFalse(page.HasRestoredVault);

        await page.OpenRestoredVaultCommand.ExecuteAsync(null);
        Assert.AreEqual(1, vault.Changes, "plus rien à ouvrir");
    }

    [TestMethod]
    public async Task Restoring_an_unknown_backup_says_why()
    {
        using var vault = new DataVault();
        var page = await vault.LoadedAsync();

        page.RequestRestoreCommand.Execute(new BackupRow("absente.zip", "hier", "1 Ko"));
        await page.AnswerRestoreCommand.ExecuteAsync(true);

        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.HasRestoredVault);
    }

    [TestMethod]
    public async Task What_was_deleted_is_listed_and_comes_back()
    {
        using var vault = new DataVault();
        var app = vault.App;
        var gone = app.Sheets.Create("Biologie/Cellule", "Mitose", null).Value;
        app.Annotations.Create(gone, Passage, "Avant la métaphase").Value.ToString();
        app.Trash.TrashSheet(gone).Value.ToString();
        var kept = app.Sheets.Create("Chimie", "Atome", null).Value;
        var passage = app.Annotations.Create(kept, Passage, "Un commentaire").Value;
        app.Annotations.Trash(passage).Value.ToString();
        var silent = app.Annotations.Create(kept, Passage with { Exact = "précède", Start = 12, End = 19 }, null).Value;
        app.Annotations.Trash(silent).Value.ToString();
        var page = await vault.LoadedAsync();

        Assert.HasCount(3, page.Trash);
        Assert.IsTrue(page.HasTrash);
        Assert.IsFalse(page.HasNoTrash);
        var sheet = page.Trash[0];
        Assert.AreEqual("Mitose", sheet.Title);
        StringAssert.StartsWith(sheet.Detail, "Fiche · Biologie › Cellule · supprimée le ", StringComparison.Ordinal);
        StringAssert.EndsWith(sheet.Detail, " · 1 annotation", StringComparison.Ordinal);
        Assert.IsFalse(sheet.HasSheet);
        Assert.AreEqual("Restaurer : Mitose", sheet.RestoreName);

        var quoted = page.Trash.Single(row => row.Detail == "Atome · Un commentaire");
        Assert.AreEqual("« La prophase »", quoted.Title);
        Assert.IsTrue(quoted.HasSheet);
        Assert.AreEqual("Ouvrir la fiche de : Atome · Un commentaire", quoted.OpenSheetName);
        quoted.OpenSheet!.Execute(null);
        CollectionAssert.AreEqual(new object[] { kept }, vault.Opener.Opened);
        Assert.AreEqual("Atome", page.Trash.Single(row => row.Title == "« précède »").Detail, "sans commentaire, le titre seul");

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)quoted.Restore).ExecuteAsync(null);
        Assert.AreEqual("Passage restauré.", page.Message);
        Assert.HasCount(2, page.Trash);

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)page.Trash[0].Restore).ExecuteAsync(null);
        Assert.AreEqual("« Mitose » est restaurée.", page.Message);
        Assert.HasCount(1, page.Trash);
        Assert.IsTrue(app.Sheets.Open(gone).Succeeded);
    }

    [TestMethod]
    public async Task A_sheet_without_a_folder_or_annotations_says_only_what_it_is()
    {
        using var vault = new DataVault();
        var gone = vault.App.Sheets.Create("", "Libre", null).Value;
        vault.App.Trash.TrashSheet(gone).Value.ToString();

        var page = await vault.LoadedAsync();

        StringAssert.StartsWith(page.Trash[0].Detail, "Fiche · supprimée le ", StringComparison.Ordinal);
        Assert.DoesNotContain("annotation", page.Trash[0].Detail);
    }

    [TestMethod]
    public async Task A_restoration_refused_says_why_and_keeps_the_line()
    {
        using var vault = new DataVault();
        var gone = vault.App.Sheets.Create("", "Libre", null).Value;
        vault.App.Trash.TrashSheet(gone).Value.ToString();
        var page = await vault.LoadedAsync();
        var line = page.Trash[0];
        vault.App.Trash.RestoreSheet(gone).Value.ToString();

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)line.Restore).ExecuteAsync(null);

        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.HasMessage);
        Assert.IsTrue(page.IsIdle);
    }

    [TestMethod]
    [DataRow(ExportKind.Tracking)]
    [DataRow(ExportKind.Annotations)]
    [DataRow(ExportKind.Vault)]
    public async Task An_export_goes_to_the_chosen_folder(ExportKind kind)
    {
        using var vault = new DataVault();
        var page = await vault.LoadedAsync();
        var folder = vault.Vaults.Folder("Exports");

        vault.Picker.Answers.Enqueue(null);
        await page.ExportCommand.ExecuteAsync(kind);
        Assert.IsEmpty(Directory.GetFiles(folder), "le choix du dossier a été annulé");
        Assert.AreEqual("Choisissez le dossier de l'export", vault.Picker.Titles[0]);

        vault.Picker.Answers.Enqueue(folder);
        await page.ExportCommand.ExecuteAsync(kind);

        Assert.IsFalse(page.HasError, page.Error);
        Assert.AreEqual($"Exporté dans « {folder} ».", page.Message);
        Assert.IsNotEmpty(Directory.GetFiles(folder));
        Assert.IsTrue(page.IsIdle);
    }

    [TestMethod]
    public async Task An_export_refused_says_why()
    {
        using var vault = new DataVault();
        var page = await vault.LoadedAsync();
        vault.Picker.Answers.Enqueue(vault.Vaults.PathOf("Absent"));

        await page.ExportCommand.ExecuteAsync(ExportKind.Tracking);

        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.HasMessage);
    }

    [TestMethod]
    public async Task Another_vault_opens_and_the_application_follows()
    {
        using var vault = new DataVault();
        var page = await vault.LoadedAsync();
        var other = vault.Vaults.CreateVault("Autre");

        vault.Picker.Answers.Enqueue(null);
        await page.OpenVaultCommand.ExecuteAsync(null);
        Assert.AreEqual(0, vault.Changes, "le choix du dossier a été annulé");
        Assert.AreEqual("Choisissez le dossier du coffre", vault.Picker.Titles[0]);

        vault.Picker.Answers.Enqueue(vault.Root);
        await page.OpenVaultCommand.ExecuteAsync(null);
        Assert.AreEqual(0, vault.Changes);
        Assert.AreEqual("Ce coffre est déjà ouvert.", page.Message);

        vault.Picker.Answers.Enqueue(other);
        await page.OpenVaultCommand.ExecuteAsync(null);

        Assert.AreEqual(1, vault.Changes);
        Assert.AreEqual(other, vault.App.Vaults.State().Vault);
        Assert.IsTrue(page.IsIdle);
    }

    [TestMethod]
    public async Task A_new_vault_is_created_in_an_empty_folder_and_a_refusal_keeps_the_current_one()
    {
        using var vault = new DataVault();
        var page = await vault.LoadedAsync();

        vault.Picker.Answers.Enqueue(null);
        await page.CreateVaultCommand.ExecuteAsync(null);
        Assert.AreEqual(0, vault.Changes);
        Assert.AreEqual("Choisissez un dossier vide pour le nouveau coffre", vault.Picker.Titles[0]);

        var taken = vault.Vaults.Folder("Pris");
        await File.WriteAllTextAsync(Path.Combine(taken, "déjà là.txt"), "x");
        vault.Picker.Answers.Enqueue(taken);
        await page.CreateVaultCommand.ExecuteAsync(null);
        Assert.AreEqual(0, vault.Changes);
        Assert.IsTrue(page.HasError);
        Assert.AreEqual(vault.Root, vault.App.Vaults.State().Vault);

        var empty = vault.Vaults.PathOf("Neuf");
        vault.Picker.Answers.Enqueue(empty);
        await page.CreateVaultCommand.ExecuteAsync(null);

        Assert.AreEqual(1, vault.Changes);
        Assert.IsFalse(page.HasError);
        Assert.AreEqual(empty, vault.App.Vaults.State().Vault);
    }

    [TestMethod]
    public async Task Coming_back_to_the_tab_forgets_the_last_message()
    {
        using var vault = new DataVault();
        var page = await vault.LoadedAsync();
        await page.BackupNowCommand.ExecuteAsync(null);
        Assert.IsTrue(page.HasMessage);

        page.OnNavigatedTo();

        Assert.IsFalse(page.HasMessage);
        Assert.IsTrue(page.ReloadCommand.ExecutionTask is not null);
        await page.ReloadCommand.ExecutionTask!;
    }

    [TestMethod]
    [DataRow(512L, "512 octets")]
    [DataRow(2048L, "2 Ko")]
    [DataRow(1536L, "1,5 Ko")]
    [DataRow(2621440L, "2,5 Mo")]
    public void A_size_reads_in_french(long bytes, string expected)
    {
        Assert.AreEqual(expected, DataSettingsViewModel.SizeOf(bytes));
    }

    [TestMethod]
    public async Task What_was_typed_in_revisions_is_forgotten_when_the_vault_changes()
    {
        using var vault = new DataVault();
        var settings = new SettingsPageViewModel(vault.App.Settings, TimeProvider.System);
        await settings.LoadCommand.ExecuteAsync(null);
        settings.Steps = "2, 4";
        Assert.IsTrue(settings.HasChanges);

        settings.Forget();
        Assert.IsFalse(settings.HasChanges);
        await settings.LoadCommand.ExecuteAsync(null);

        Assert.AreEqual("1, 3, 7, 14, 30", settings.Steps);
    }

    [STATestMethod]
    public void The_tabs_change_the_page_shown_without_horizontal_scrolling()
    {
        using var ui = new UiThread();
        using var vault = new DataVault();
        var test = new TestShell();
        var hub = vault.Hub();
        hub.OnNavigatedTo();
        var host = new Border { Width = 640, Height = 700, Child = new SettingsHubPage { DataContext = hub } };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 800, Height = 800, ShowActivated = false };
        window.Show();
        TestShell.Flush();
        try
        {
            Assert.AreEqual("Paramètres", Named<TextBlock>(window, "Heading").Text);
            var radios = Descendants<RadioButton>(window).ToList();
            Assert.HasCount(5, radios);
            Assert.IsTrue(radios[0].IsChecked);
            Assert.IsNotNull(Find<FrameworkElement>(window, "ThemeChoice"), "Général s'ouvre d'abord");

            radios[1].IsChecked = true;
            TestShell.Flush();
            Assert.IsNotNull(Find<FrameworkElement>(window, "StepsBox"));

            radios[2].IsChecked = true;
            TestShell.Flush();
            Assert.IsNotNull(Find<FrameworkElement>(window, "QuotesBox"));

            radios[3].IsChecked = true;
            TestShell.Flush();
            UiThread.Until(() => vault.Page.IsLoaded);
            TestShell.Flush();
            Assert.IsFalse(hub.Tabs[0].IsSelected);
            Assert.IsNull(Find<FrameworkElement>(window, "StepsBox"));
            Assert.AreEqual(Visibility.Visible, Named<FrameworkElement>(window, "BackupsList").Visibility);
            Assert.AreEqual(Visibility.Collapsed, Named<FrameworkElement>(window, "TrashList").Visibility, "une liste vide ne s'affiche pas");
            Assert.AreEqual(Visibility.Visible, Named<FrameworkElement>(window, "NoTrash").Visibility);
            var scroller = Named<ScrollViewer>(window, "Scroller");
            Assert.IsLessThanOrEqualTo(scroller.ViewportWidth, scroller.ExtentWidth);

            radios[4].IsChecked = true;
            TestShell.Flush();
            Assert.AreEqual("kyfRev 1.0.0-essai", Named<TextBlock>(window, "VersionLine").Text);
            Assert.AreEqual(vault.Root, Named<TextBlock>(window, "VaultLine").Text);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_confirmation_answers_the_tab_and_the_rows_have_their_buttons()
    {
        using var ui = new UiThread();
        using var vault = new DataVault();
        var kept = vault.App.Sheets.Create("Chimie", "Atome", null).Value;
        vault.App.Annotations.Trash(vault.App.Annotations.Create(kept, Passage, null).Value).Value.ToString();
        var gone = vault.App.Sheets.Create("", "Libre", null).Value;
        vault.App.Trash.TrashSheet(gone).Value.ToString();
        var test = new TestShell();
        var page = new DataSettingsPage { DataContext = vault.Page };
        vault.Page.OnNavigatedTo();
        var host = new Border { Width = 900, Height = 900, Child = page };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 1000, Height = 1000, ShowActivated = false };
        window.Show();
        TestShell.Flush();
        try
        {
            UiThread.Until(() => vault.Page.IsLoaded);
            TestShell.Flush();
            var sheets = Descendants<Button>(window).Where(button => button.Name == "OpenItemSheet").ToList();
            Assert.HasCount(2, sheets);
            Assert.AreEqual(1, sheets.Count(button => button.Visibility == Visibility.Visible), "seul un passage a une fiche à ouvrir");

            vault.Page.RequestRestoreCommand.Execute(vault.Page.Backups[0]);
            TestShell.Flush();
            var dialog = Descendants<Dialog>(window).Single(one => one.Name == "Confirmation");
            Assert.IsTrue(dialog.IsOpen);
            dialog.Close(DialogChoice.Secondary);
            TestShell.Flush();
            Assert.IsFalse(vault.Page.IsConfirming);
            Assert.IsFalse(vault.Page.HasRestoredVault);

            vault.Page.RequestRestoreCommand.Execute(vault.Page.Backups[0]);
            TestShell.Flush();
            dialog.Close(DialogChoice.Primary);
            UiThread.Until(() => vault.Page.HasRestoredVault);
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, Named<FrameworkElement>(window, "OpenRestored").Visibility);

            page.DataContext = null;
            dialog.Close(DialogChoice.None);
        }
        finally
        {
            window.Close();
        }
    }

    private static T Named<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        var found = Find<T>(root, name);
        Assert.IsNotNull(found, name);
        return found;
    }

    private static T? Find<T>(DependencyObject root, string name)
        where T : FrameworkElement =>
        Descendants<T>(root).FirstOrDefault(element => element.Name == name);

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T element)
            {
                yield return element;
            }
            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>L'onglet « Données » sur un vrai coffre.</summary>
    private sealed class DataVault : IDisposable
    {
        public DataVault()
        {
            Vaults = new TestVaults(AppMode.Development);
            Root = Vaults.PathOf("Coffre");
            Vaults.App.Vaults.Create(Root);
            Page = PageOn(Vaults, () => Changes++, Opener);
        }

        public TestVaults Vaults { get; }

        public string Root { get; }

        public FakeOpener Opener { get; } = new();

        public FakePicker Picker => Vaults.Picker;

        public KyfRevApplication App => Vaults.App;

        public DataSettingsViewModel Page { get; }

        public int Changes { get; private set; }

        public static DataSettingsViewModel PageOn(TestVaults vaults, Action changed, FakeOpener? opener = null)
        {
            var app = vaults.App;
            return new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, vaults.Picker, opener ?? new FakeOpener(), changed);
        }

        public static GeneralSettingsViewModel General() => new(new MachineSettings(new MemoryMachinePreferences()), new FakeVoices("Microsoft Hortense Desktop"));

        public SettingsHubViewModel Hub() => new(General(), new SettingsPageViewModel(App.Settings, TimeProvider.System), new ModulesSettingsViewModel(App.Settings, _ => { }, FakeExtensions.Settings()), Page, new AboutSettingsViewModel(App.Vaults));

        public async Task<DataSettingsViewModel> LoadedAsync()
        {
            await Page.ReloadCommand.ExecuteAsync(null);
            return Page;
        }

        public void Dispose() => Vaults.Dispose();
    }
}

/// <summary>Le dépôt, qui sert une annonce de version.</summary>
internal sealed class AnnouncedVersion(byte[] manifest, byte[] signature) : IOnlineReader
{
    public int Reads { get; private set; }

    public Task<string> ReadTextAsync(Uri address, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<byte[]> ReadBytesAsync(Uri address, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(address.AbsolutePath.EndsWith(".sig", StringComparison.Ordinal) ? signature : manifest);
    }
}

/// <summary>La 0.x, simulée.</summary>
internal sealed class FakeLegacy : ILegacyInstallation
{
    public LegacyInstall? Installed { get; set; }

    public int Opened { get; private set; }

    public LegacyInstall? Find() => Installed;

    public bool OpenInstalledApps()
    {
        Opened++;
        return true;
    }
}
