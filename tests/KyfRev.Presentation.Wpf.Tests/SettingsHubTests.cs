using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Les onglets de la vue « Paramètres » : Données et À propos, sur de vrais coffres.</summary>
[TestClass]
public sealed class SettingsHubTests
{
    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose", "Étapes"], 5, "sha256:aa");

    private static readonly string[] AllTabs = ["Apparence", "Lecture", "Feuilles", "Menu", "Raccourcis", "Révisions", "Modules et extensions", "Coffre et chiffrement", "Sauvegardes et exports", "À propos"];

    private static readonly string[] Tabs = ["Apparence", "Lecture", "Révisions", "Modules et extensions", "Coffre et chiffrement", "Sauvegardes et exports", "À propos"];

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

        Assert.AreSame(hub.Tabs[0].Page, hub.Tabs[1].Page, "Apparence et Lecture sont deux parts de la même page");
        Assert.AreEqual((SettingsHubViewModel.AppearancePart, SettingsHubViewModel.ReadingPart), (hub.Tabs[0].Part, hub.Tabs[1].Part));
        Assert.IsTrue(hub.Tabs[0].StartsGroup && !hub.Tabs[1].StartsGroup && hub.Tabs[2].StartsGroup && hub.Tabs[6].StartsGroup, "trois groupes : l'ordinateur, le coffre, l'application");
        hub.Tabs[6].IsSelected = true;
        Assert.AreSame(hub.Tabs[6], hub.Selected);
        Assert.IsFalse(hub.Tabs[0].IsSelected);
        Assert.AreEqual("kyfRev 1.0.0-essai", ((AboutSettingsViewModel)hub.Selected.Page).Version, "l'onglet se lit quand on l'ouvre");

        hub.Tabs[6].IsSelected = false;
        Assert.AreSame(hub.Tabs[6], hub.Selected, "décocher ne choisit rien d'autre");
        hub.Tabs[6].IsSelected = true;
        Assert.AreSame(hub.Tabs[6], hub.Selected);
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

    [TestMethod]
    public void Searching_finds_a_setting_whatever_the_accents_and_opens_its_section()
    {
        using var vault = new DataVault();
        var hub = vault.Hub(new MachineSettings(new MemoryMachinePreferences()));
        CollectionAssert.AreEqual(AllTabs, hub.Tabs.Select(tab => tab.Label).ToArray());
        Assert.IsFalse(hub.IsSearching);
        Assert.IsEmpty(hub.Results);

        hub.Query = "theme";
        Assert.IsTrue(hub.IsSearching);
        var theme = hub.Results.Single();
        Assert.AreEqual((GeneralSettingsText.Theme, "Apparence"), (theme.Label, theme.Where));
        Assert.AreSame(hub.Tabs[0], theme.Tab);
        Assert.AreEqual($"{GeneralSettingsText.Theme}, dans Apparence", theme.Spoken);

        hub.Query = "SOMBRE";
        Assert.AreEqual(GeneralSettingsText.Theme, hub.Results.Single().Label, "une valeur du réglage y mène aussi");

        hub.Query = "taille fiches";
        Assert.AreEqual(GeneralSettingsText.TextSize, hub.Results.Single().Label, "tous les mots doivent s'y trouver");

        hub.Query = "chiffrement";
        CollectionAssert.AreEqual(new[] { "Coffre et chiffrement", EncryptionText.CardTitle }, hub.Results.Select(hit => hit.Label).ToArray(), "la section, puis le réglage de ce nom : pas tout ce que la section contient");
        Assert.AreEqual("Dans ce coffre", hub.Results[0].Where, "une section dit son groupe");
        Assert.IsTrue(hub.Results.All(hit => hit.Tab.Label == "Coffre et chiffrement"));

        hub.Query = "raccourcis recto";
        Assert.AreEqual((ShellText.ShortcutPrint, "Raccourcis"), (hub.Results.Single().Label, hub.Results.Single().Where), "un mot du réglage, un mot de sa section");

        hub.Query = "imprimer";
        CollectionAssert.AreEqual(new[] { ShellText.ShortcutPrint, ShellText.ShortcutShare }, hub.Results.Select(hit => hit.Label).ToArray(), "les actions qui ont un raccourci se trouvent par leur nom");

        hub.Query = "papier";
        Assert.AreEqual((MoreSettingsText.PaperTitle, "Feuilles"), (hub.Results.Single().Label, hub.Results.Single().Where));

        hub.Query = "zzz";
        Assert.IsTrue(hub.HasNoResult);
        hub.Query = "   ";
        Assert.IsFalse(hub.IsSearching || hub.HasNoResult, "des espaces ne cherchent rien");

        hub.Query = "anki";
        hub.OpenCommand.Execute(hub.Results.Single());
        Assert.AreEqual("Sauvegardes et exports", hub.Selected.Label);
        Assert.AreEqual(string.Empty, hub.Query, "la liste des sections revient");
        Assert.IsFalse(hub.IsSearching);

        hub.Query = "voix";
        hub.OpenFirstCommand.Execute(null);
        Assert.AreEqual("Lecture", hub.Selected.Label);
        hub.Query = "zzz";
        hub.OpenFirstCommand.Execute(null);
        Assert.AreEqual(("Lecture", "zzz"), (hub.Selected.Label, hub.Query), "rien à ouvrir : rien ne bouge");
        hub.ClearSearchCommand.Execute(null);
        Assert.AreEqual(string.Empty, hub.Query);

        // Sans les sections Feuilles, Menu et Raccourcis, leurs réglages ne se trouvent pas.
        var small = vault.Hub();
        small.Query = "papier";
        Assert.IsTrue(small.HasNoResult);
    }

    [TestMethod]
    public void A_section_kept_on_the_computer_restores_its_own_settings_and_can_take_them_back()
    {
        using var vault = new DataVault();
        var store = new MemoryMachinePreferences
        {
            Stored = new MachinePreferences { Theme = ThemePreference.Dark, ReducedMotion = true, TextSize = ReadingTextSize.Large, MarkDefinedWords = false, Shortcuts = "aide=F2", GlobalCapture = true, MenuOrder = "carnet,aujourdhui" },
        };
        var machine = new MachineSettings(store);
        var hub = vault.Hub(machine);
        var general = (GeneralSettingsViewModel)hub.Tabs[0].Page;
        hub.OnNavigatedTo();
        Assert.IsTrue(hub.OffersRestore);
        Assert.IsTrue(hub.RestoreCommand.CanExecute(null));
        Assert.IsFalse(hub.HasRestoreNote || hub.UndoRestoreCommand.CanExecute(null));

        hub.RestoreCommand.Execute(null);
        Assert.AreEqual((ThemePreference.System, false, ReadingTextSize.Normal), (machine.Current.Theme, machine.Current.ReducedMotion, machine.Current.TextSize), "le texte des fiches est un réglage d'Apparence");
        Assert.AreEqual((false, "aide=F2", true, "carnet,aujourdhui"), (machine.Current.MarkDefinedWords, machine.Current.Shortcuts, machine.Current.GlobalCapture, machine.Current.MenuOrder), "les autres sections gardent leurs réglages");
        Assert.AreEqual(machine.Current, store.Stored, "retenu sur l'ordinateur");
        Assert.AreEqual(ThemePreference.System, general.Theme.Value, "la section montre ce qui est en vigueur");
        Assert.IsFalse(general.ReducedMotion);
        Assert.AreEqual(MoreSettingsText.Restored, hub.RestoreNote);
        Assert.IsFalse(hub.RestoreCommand.CanExecute(null), "plus rien à rétablir ici");
        Assert.IsTrue(hub.UndoRestoreCommand.CanExecute(null));

        hub.UndoRestoreCommand.Execute(null);
        Assert.AreEqual((ThemePreference.Dark, true, ReadingTextSize.Large), (machine.Current.Theme, machine.Current.ReducedMotion, machine.Current.TextSize));
        Assert.AreEqual(ThemePreference.Dark, general.Theme.Value);
        Assert.AreEqual(ReadingTextSize.Large, general.TextSize.Value);
        Assert.IsTrue(general.ReducedMotion);
        Assert.IsFalse(hub.HasRestoreNote || hub.UndoRestoreCommand.CanExecute(null));
        Assert.IsTrue(hub.RestoreCommand.CanExecute(null));

        // Un réglage changé depuis : ce qui était proposé d'annuler ne correspond plus.
        hub.RestoreCommand.Execute(null);
        general.HighContrast = true;
        Assert.IsFalse(hub.HasRestoreNote);
        Assert.IsTrue(hub.RestoreCommand.CanExecute(null));

        // Changer de section retire aussi la proposition.
        hub.RestoreCommand.Execute(null);
        hub.Tabs[1].IsSelected = true;
        Assert.IsFalse(hub.HasRestoreNote);
        Assert.IsTrue(hub.RestoreCommand.CanExecute(null), "Lecture ne marque plus les mots définis");
        hub.RestoreCommand.Execute(null);
        Assert.IsTrue(machine.Current.MarkDefinedWords);
        Assert.IsTrue(general.MarkDefinedWords);

        var menu = hub.Tabs.Single(tab => tab.Label == "Menu");
        menu.IsSelected = true;
        hub.RestoreCommand.Execute(null);
        Assert.AreEqual((string.Empty, "aide=F2"), (machine.Current.MenuOrder, machine.Current.Shortcuts), "Menu et Raccourcis partagent une page, pas leurs réglages");

        hub.Tabs.Single(tab => tab.Label == "Raccourcis").IsSelected = true;
        store.Refuses = true;
        hub.RestoreCommand.Execute(null);
        Assert.AreEqual((string.Empty, false), (machine.Current.Shortcuts, machine.Current.GlobalCapture));
        Assert.AreEqual(MoreSettingsText.RestoredNotKept, hub.RestoreNote, "le fichier des préférences n'a pas pu être écrit, et la vue le dit");
        store.Refuses = false;

        // Ce qui appartient au coffre ne se rétablit pas d'ici.
        foreach (var tab in hub.Tabs.Where(tab => tab.Group != MoreSettingsText.GroupMachine))
        {
            tab.IsSelected = true;
            Assert.IsFalse(hub.OffersRestore || hub.RestoreCommand.CanExecute(null), tab.Label);
        }
        Assert.IsFalse(vault.Hub().OffersRestore, "sans les préférences de l'ordinateur, rien n'est proposé");
    }

    [STATestMethod]
    public void The_view_shows_what_the_search_found_and_offers_to_restore_a_section()
    {
        using var ui = new UiThread();
        using var vault = new DataVault();
        var test = new TestShell();
        var machine = new MachineSettings(new MemoryMachinePreferences { Stored = new MachinePreferences { HighContrast = true } });
        var hub = vault.Hub(machine);
        hub.OnNavigatedTo();
        var host = new Border { Width = 980, Height = 700, Child = new SettingsHubPage { DataContext = hub } };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 1100, Height = 800, ShowActivated = false };
        window.Show();
        TestShell.Flush();
        try
        {
            var search = Named<TextBox>(window, "SettingSearch");
            Assert.IsTrue(search.IsVisible);
            Assert.AreEqual(MoreSettingsText.SearchLabel, AutomationProperties.GetName(search));
            Assert.IsTrue(Named<FrameworkElement>(window, "SectionList").IsVisible);
            Assert.IsEmpty(Hits(window));

            search.Text = "étapes";
            TestShell.Flush();
            Assert.AreEqual("étapes", hub.Query);
            Assert.IsFalse(Find<FrameworkElement>(window, "SectionList") is { IsVisible: true }, "les résultats prennent la place des sections");
            var hit = Hits(window).Single();
            Assert.AreEqual($"{SettingsText.Steps}, dans Révisions", AutomationProperties.GetName(hit));
            Assert.IsTrue(Named<FrameworkElement>(window, "SectionArea").IsVisible, "la section ouverte reste en vue, à droite");

            hit.Command.Execute(hit.CommandParameter);
            TestShell.Flush();
            Assert.AreEqual("Révisions", Named<TextBlock>(window, "SectionTitle").Text);
            Assert.AreEqual(string.Empty, search.Text);
            Assert.IsTrue(Named<FrameworkElement>(window, "SectionList").IsVisible);
            Assert.IsNotNull(Find<FrameworkElement>(window, "StepsBox"));
            Assert.IsFalse(Named<Button>(window, "RestoreButton").IsVisible, "une section du coffre ne se rétablit pas d'ici");

            search.Text = "zzz";
            TestShell.Flush();
            Assert.IsEmpty(Hits(window));
            Assert.IsTrue(Descendants<TextBlock>(window).Any(text => text.IsVisible && text.Text == MoreSettingsText.NoResult));
            search.Text = string.Empty;
            TestShell.Flush();

            hub.Tabs[0].IsSelected = true;
            TestShell.Flush();
            var restore = Named<Button>(window, "RestoreButton");
            Assert.IsTrue(restore.IsVisible && restore.IsEnabled);
            Assert.AreEqual(MoreSettingsText.RestoreSection, restore.Content);
            Assert.IsFalse(Find<FrameworkElement>(window, "RestoreBar") is { IsVisible: true });
            Assert.IsTrue(Named<CheckBox>(window, "HighContrastBox").IsChecked);

            restore.Command.Execute(null);
            TestShell.Flush();
            Assert.IsFalse(machine.Current.HighContrast);
            Assert.IsFalse(Named<CheckBox>(window, "HighContrastBox").IsChecked, "la case suit");
            Assert.IsFalse(restore.IsEnabled, "tout est d'origine");
            Assert.IsTrue(Named<FrameworkElement>(window, "RestoreBar").IsVisible);
            var undo = Named<Button>(window, "UndoRestoreButton");
            Assert.AreEqual(MoreSettingsText.UndoRestore, undo.Content);

            undo.Command.Execute(null);
            TestShell.Flush();
            Assert.IsTrue(machine.Current.HighContrast);
            Assert.IsTrue(Named<CheckBox>(window, "HighContrastBox").IsChecked);
            Assert.IsFalse(Find<FrameworkElement>(window, "RestoreBar") is { IsVisible: true });
            Assert.IsTrue(restore.IsEnabled);

            // En fenêtre étroite, la recherche est sous le titre, et ses résultats prennent la place de la section.
            host.Width = 560;
            TestShell.Flush();
            var narrow = Named<TextBox>(window, "NarrowSettingSearch");
            Assert.IsTrue(narrow.IsVisible);
            Assert.IsTrue(Named<FrameworkElement>(window, "SectionArea").IsVisible);
            Assert.IsTrue(restore.IsVisible, "« Rétablir » passe sous la ligne qui décrit la section");
            Assert.AreEqual((0, 1), (Grid.GetColumn(restore), Grid.GetRow(restore)));
            narrow.Text = "voix";
            TestShell.Flush();
            Assert.IsFalse(Find<FrameworkElement>(window, "SectionArea") is { IsVisible: true });
            var found = Hits(window).Single();
            found.Command.Execute(found.CommandParameter);
            TestShell.Flush();
            Assert.AreEqual("Lecture", hub.Selected.Label);
            Assert.IsTrue(Named<FrameworkElement>(window, "SectionArea").IsVisible);
            Assert.IsTrue(Named<FrameworkElement>(window, "RateChoice").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    [DataRow(false, 1280)]
    [DataRow(true, 1280)]
    [DataRow(false, 520)]
    [DataRow(true, 2600)]
    [DataRow(false, 3440)]
    public void Every_section_reads_in_a_centred_column_with_its_text_on_the_left(bool dark, int width)
    {
        using var ui = new UiThread();
        using var vault = new DataVault();
        var design = new DesignResources(dark ? ThemeKind.Dark : ThemeKind.Light, reducedMotion: false);
        var hub = vault.Hub(new MachineSettings(new MemoryMachinePreferences()));
        var view = new SettingsHubPage { DataContext = hub };
        var host = new Border { Width = width, Height = 860, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = view };
        host.Resources.MergedDictionaries.Add(design.Dictionary);
        host.SetResourceReference(Border.BackgroundProperty, "Kyf.Brush.Background");
        var window = new Window { Content = new Canvas { Children = { host } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        TestShell.Flush();
        try
        {
            var area = Named<FrameworkElement>(window, "SectionArea");
            foreach (var tab in hub.Tabs)
            {
                tab.IsSelected = true;
                TestShell.Flush();
                host.UpdateLayout();
                TestShell.Flush();
                var head = Named<FrameworkElement>(window, "SectionHead");
                var title = Named<TextBlock>(window, "SectionTitle");
                var (left, right) = (head.TranslatePoint(default, area).X, head.TranslatePoint(new Point(head.ActualWidth, 0), area).X);
                Assert.IsLessThanOrEqualTo(tab.ColumnWidth + 0.5, head.ActualWidth, $"{tab.Label} : la colonne ne dépasse pas sa largeur");
                Assert.AreEqual(left, area.ActualWidth - right, 1.5, $"{tab.Label} : la colonne est centrée");
                Assert.AreEqual(left, title.TranslatePoint(default, area).X, 0.5, $"{tab.Label} : le titre est à gauche de la colonne, pas centré");
                Assert.AreEqual(TextAlignment.Left, title.TextAlignment);

                // Les blocs de la section sont dans la même colonne que son titre, à la barre de défilement près.
                // Une section lue dans le coffre (Révisions) montre ses blocs une fois ses réglages lus : on les attend, au lieu
                // de compter sur un poste assez rapide pour qu'ils soient déjà là.
                List<Card> Cards() => [.. Descendants<Card>(Named<FrameworkElement>(window, "TabContent")).Where(card => card.IsVisible && card.ActualWidth > 200)];
                UiThread.Until(() => Cards().Count > 0, TimeSpan.FromSeconds(30));
                host.UpdateLayout();
                TestShell.Flush();
                var cards = Cards();
                Assert.IsNotEmpty(cards, tab.Label);
                foreach (var card in cards.Where(card => Descendants<Card>(card).All(inner => ReferenceEquals(inner, card) || !inner.IsVisible)))
                {
                    var cardLeft = card.TranslatePoint(default, area).X;
                    var cardRight = card.TranslatePoint(new Point(card.ActualWidth, 0), area).X;
                    Assert.IsGreaterThanOrEqualTo(left - 12, cardLeft, $"{tab.Label} : un bloc sort de la colonne à gauche");
                    Assert.IsLessThanOrEqualTo(right + 12, cardRight, $"{tab.Label} : un bloc sort de la colonne à droite");
                }
                if (tab.Label is "Apparence" or "Lecture" or "Raccourcis" or "Modules et extensions" or "Coffre et chiffrement" or "À propos")
                {
                    host.Capture($"parametres-{width}-{(dark ? "sombre" : "clair")}-{tab.Label.Replace(' ', '-')}");
                }
            }
            Assert.AreEqual(SettingsTab.WideColumn, hub.Tabs.Single(tab => tab.Label == "Feuilles").ColumnWidth, "Feuilles garde ses deux volets");
            Assert.IsTrue(hub.Tabs.Where(tab => tab.Label != "Feuilles").All(tab => tab.ColumnWidth == SettingsTab.Column));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void The_sheets_section_shows_its_choices_as_the_mockup_does(bool dark)
    {
        using var ui = new UiThread();
        using var vault = new DataVault();
        var design = new DesignResources(dark ? ThemeKind.Dark : ThemeKind.Light, reducedMotion: false);
        var own = SheetPageLayouts.None.With("f_1", new PageLayout(PageFormat.A5)).With("f_2", new PageLayout(PageFormat.A6)).With("f_3", new PageLayout(PageFormat.A4, true));
        var store = new MemoryMachinePreferences
        {
            Stored = new MachinePreferences
            {
                Page = new PageLayout(PageFormat.A4),
                Paper = new PaperChoices(IsOn: true, Endless: PaperStyle.Plain, A4: PaperStyle.LargeSquares, A5: PaperStyle.SmallSquares, A6: PaperStyle.Dots),
                SheetPages = own,
            },
        };
        var machine = new MachineSettings(store);
        var hub = vault.Hub(machine);
        hub.Tabs.Single(tab => tab.Label == "Feuilles").IsSelected = true;
        var host = new Border { Width = 1460, Height = 900, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = new SettingsHubPage { DataContext = hub } };
        host.Resources.MergedDictionaries.Add(design.Dictionary);
        host.SetResourceReference(Border.BackgroundProperty, "Kyf.Brush.Background");
        var window = new Window { Content = new Canvas { Children = { host } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        TestShell.Flush();
        try
        {
            host.Capture(dark ? "parametres-feuilles-sombre" : "parametres-feuilles");

            // La feuille : quatre boutons, avec leurs dimensions ; l'orientation et les marges de même.
            var formats = Named<ListBox>(window, "FormatChoice");
            Assert.HasCount(4, formats.Items);
            Assert.AreEqual(PageFormat.A4, ((SettingChoice<PageFormat>)formats.SelectedItem).Value);
            Assert.IsTrue(Descendants<TextBlock>(formats).Any(text => text.Text == "21 × 29,7 cm"));
            var margins = Named<ListBox>(window, "MarginChoice");
            Assert.HasCount(3, margins.Items);
            Assert.AreEqual(MoreSettingsText.MarginsNormalShort, ((SettingChoice<double>)margins.SelectedItem).Label);
            Assert.AreEqual("2", Named<TextBox>(window, "CustomMarginBox").Text);

            formats.SelectedIndex = 2;
            TestShell.Flush();
            Assert.AreEqual(PageFormat.A5, machine.Current.Page.Format);
            Named<ListBox>(window, "OrientationChoice").SelectedIndex = 1;
            TestShell.Flush();
            Assert.IsTrue(machine.Current.Page.Landscape);

            // Des marges sur mesure : tapées dans leur champ, elles n'allument aucun des trois boutons.
            var page = (SheetsSettingsViewModel)hub.Selected.Page;
            page.CustomMargin = "1,5";
            TestShell.Flush();
            Assert.AreEqual(1.5, machine.Current.Page.MarginCm);
            Assert.HasCount(4, margins.Items, "la marge en vigueur est dans la liste…");
            Assert.AreEqual(3, Descendants<ListBoxItem>(margins).Count(item => item.IsVisible), "… mais sans bouton à elle");
            Assert.IsFalse(Descendants<ListBoxItem>(margins).Any(item => item.IsVisible && item.IsSelected));
            page.CustomMargin = "abc";
            Assert.AreEqual(1.5, machine.Current.Page.MarginCm, "un texte qui n'est pas un nombre ne change rien");
            page.CustomMargin = "99";
            Assert.AreEqual(machine.Current.Page.MaximumMarginCm, machine.Current.Page.MarginCm, "pas plus que la feuille ne permet");
            margins.SelectedIndex = 1;
            TestShell.Flush();
            Assert.AreEqual(PageLayout.NormalMargin, machine.Current.Page.MarginCm);

            // Le papier : une rangée par type de feuille, six échantillons, et un interrupteur.
            var a5 = Named<ListBox>(window, "PaperA5Choice");
            Assert.HasCount(6, a5.Items);
            Assert.AreEqual(PaperStyle.SmallSquares, ((SettingChoice<PaperStyle>)a5.SelectedItem).Value);
            Assert.HasCount(6, Descendants<PaperSwatch>(a5).ToList());
            a5.SelectedIndex = 3;
            TestShell.Flush();
            Assert.AreEqual(PaperStyle.Lines, machine.Current.Paper.A5);

            // L'aperçu : la feuille en vigueur (A5 en paysage), son papier, et une ligne qui le dit.
            var preview = Named<SheetPaperPreview>(window, "Preview");
            Assert.AreEqual(21 / 14.8, preview.Sheet.Width / preview.Sheet.Height, 0.02);
            Assert.IsGreaterThan(0, preview.Step);
            Assert.AreEqual("A5 paysage · marges de 2 cm · lignes", Named<TextBlock>(window, "PreviewCaption").Text);
            var paperOn = Named<CheckBox>(window, "PaperOnBox");
            Assert.IsTrue(paperOn.IsChecked);
            paperOn.IsChecked = false;
            TestShell.Flush();
            Assert.IsFalse(machine.Current.Paper.IsOn);
            Assert.AreEqual(0, preview.Step);
            Assert.AreEqual("A5 paysage · marges de 2 cm · papier éteint", Named<TextBlock>(window, "PreviewCaption").Text);
            paperOn.IsChecked = true;
            TestShell.Flush();

            // Les fiches qui ont leur propre mise en page : les aligner, puis s'en dédire.
            Assert.AreEqual(MoreSettingsText.OwnLayouts(3), Named<TextBlock>(window, "OwnLayoutsNote").Text);
            var align = Named<Button>(window, "AlignButton");
            align.Command.Execute(null);
            TestShell.Flush();
            Assert.IsEmpty(machine.Current.SheetPages.Items);
            Assert.IsFalse(Find<FrameworkElement>(window, "OwnLayouts") is { IsVisible: true });
            Assert.AreEqual(MoreSettingsText.Aligned(3), Named<TextBlock>(window, "AlignedNote").Text);
            Named<Button>(window, "UndoAlignButton").Command.Execute(null);
            TestShell.Flush();
            Assert.AreEqual(own, machine.Current.SheetPages);
            Assert.IsTrue(Named<FrameworkElement>(window, "OwnLayouts").IsVisible);

            // Le zoom des fiches, de 50 à 200 %.
            Named<Button>(window, "ZoomInButton").Command.Execute(null);
            Assert.AreEqual(1.1, machine.Current.SheetZoom, 0.001);
            Assert.AreEqual("110 %", Named<Button>(window, "ZoomResetButton").Content);
            page.ZoomPercent = 500;
            Assert.AreEqual(2, machine.Current.SheetZoom, 0.001);
            Named<Button>(window, "ZoomResetButton").Command.Execute(null);
            Assert.AreEqual(1, machine.Current.SheetZoom, 0.001);
            Named<Button>(window, "ZoomOutButton").Command.Execute(null);
            Assert.AreEqual(0.9, machine.Current.SheetZoom, 0.001);
            page.ZoomPercent = 10;
            Assert.AreEqual(0.5, machine.Current.SheetZoom, 0.001);
            Assert.IsFalse(Named<Button>(window, "ZoomOutButton").Command.CanExecute(null), "pas plus petit que 50 %");
            Named<Button>(window, "ZoomResetButton").Command.Execute(null);

            // La police : la liste, et celle que le papier de la feuille conseille.
            Assert.HasCount(5, Named<ComboBox>(window, "FontChoice").Items);
            Assert.AreEqual(MoreSettingsText.RecommendedFont(MoreSettingsText.Lines, GeneralSettingsText.FontSerif), Named<TextBlock>(window, "RecommendedFont").Text);
            var use = Named<Button>(window, "UseFontButton");
            Assert.IsTrue(use.IsEnabled);
            use.Command.Execute(null);
            TestShell.Flush();
            Assert.AreEqual(ReadingFont.Serif, machine.Current.Font);
            Assert.IsFalse(use.IsEnabled, "elle est déjà prise");
            hub.Tabs[1].IsSelected = true;
            Assert.AreEqual(ReadingFont.Serif, ((GeneralSettingsViewModel)hub.Tabs[1].Page).Font.Value, "la section Lecture montre la même police");
            hub.Tabs.Single(tab => tab.Label == "Feuilles").IsSelected = true;
            TestShell.Flush();

            // « Rétablir » remet la feuille, le papier et le zoom, pas la police, qui est à la section Lecture.
            page.ZoomPercent = 150;
            hub.RestoreCommand.Execute(null);
            Assert.AreEqual((PageLayout.Default, PaperChoices.Default, 1d, ReadingFont.Serif), (machine.Current.Page, machine.Current.Paper, machine.Current.SheetZoom, machine.Current.Font));

            // En fenêtre étroite, l'aperçu passe sous les réglages.
            host.Width = 1000;
            TestShell.Flush();
            Assert.AreEqual((0, 1), (Grid.GetColumn(Named<StackPanel>(window, "Side")), Grid.GetRow(Named<StackPanel>(window, "Side"))));
            var scroller = Descendants<ScrollViewer>(window).First(view => view.Name == "Scroller" && view.IsVisible);
            Assert.IsLessThanOrEqualTo(scroller.ViewportWidth, scroller.ExtentWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_menu_and_shortcut_sections_show_their_rows_and_take_the_keys_typed()
    {
        using var ui = new UiThread();
        using var vault = new DataVault();
        var test = new TestShell();
        var store = new MemoryMachinePreferences();
        var machine = new MachineSettings(store);
        var shortcuts = new ShortcutsSettingsViewModel(machine, () => test.Shell.MainNavigation);
        var hub = new SettingsHubViewModel(
            new GeneralSettingsViewModel(machine, new FakeVoices("Microsoft Hortense Desktop")),
            new SettingsPageViewModel(vault.App.Settings, TimeProvider.System),
            new ModulesSettingsViewModel(vault.App.Settings, _ => { }, FakeExtensions.Settings()),
            vault.Page,
            new AboutSettingsViewModel(vault.App.Vaults),
            new SheetsSettingsViewModel(machine),
            shortcuts,
            machine);
        var host = new Border { Width = 1200, Height = 800, Child = new SettingsHubPage { DataContext = hub } };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var window = new Window { Content = new Canvas { Children = { host } }, Left = -10000, Top = -10000, Width = 1300, Height = 900, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        TestShell.Flush();
        try
        {
            // Menu : une rangée par vue, avec ses deux flèches nommées.
            hub.Tabs.Single(tab => tab.Label == "Menu").IsSelected = true;
            TestShell.Flush();
            Assert.IsTrue(Named<FrameworkElement>(window, "MenuCard").IsVisible);
            Assert.IsFalse(Find<FrameworkElement>(window, "ShortcutsCard") is { IsVisible: true }, "les raccourcis sont une autre section de la même page");
            var first = shortcuts.Menu[0];
            Assert.AreEqual(MoreSettingsText.MoveUp(first.Label), first.UpLabel);
            Assert.IsTrue(Descendants<Button>(window).Any(button => button.IsVisible && AutomationProperties.GetName(button) == first.DownLabel));

            // Raccourcis : « Modifier », puis la touche tapée devient le raccourci.
            hub.Tabs.Single(tab => tab.Label == "Raccourcis").IsSelected = true;
            TestShell.Flush();
            Assert.IsTrue(Named<FrameworkElement>(window, "ShortcutsCard").IsVisible);
            Assert.IsTrue(Named<FrameworkElement>(window, "CaptureCard").IsVisible);
            var row = shortcuts.Rows.First(candidate => candidate.Id == "aide");
            Assert.AreEqual(MoreSettingsText.Change, row.ChangeLabel);
            shortcuts.ChangeCommand.Execute(row);
            Assert.AreEqual(MoreSettingsText.Waiting, row.ChangeLabel);
            var view = Descendants<ShortcutsSettingsPage>(window).Single();
            var typed = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view)!, 0, Key.F9) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            view.RaiseEvent(typed);
            Assert.IsTrue(typed.Handled, "la touche est prise : elle ne déclenche rien d'autre");
            Assert.AreEqual("F9", row.Keys);
            Assert.AreEqual("aide=F9", machine.Current.Shortcuts);
            // Hors de l'attente d'une touche, la vue laisse passer la frappe.
            var free = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view)!, 0, Key.F8) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            view.RaiseEvent(free);
            Assert.IsFalse(free.Handled);

            // Feuilles : des marges tapées, puis Entrée, sans quitter le champ.
            hub.Tabs.Single(tab => tab.Label == "Feuilles").IsSelected = true;
            TestShell.Flush();
            var margin = Named<TextBox>(window, "CustomMarginBox");
            margin.Text = "1,5";
            Assert.AreEqual(PageLayout.NormalMargin, machine.Current.Page.MarginCm, "rien ne s'applique avant Entrée");
            margin.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(margin)!, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.AreEqual(1.5, machine.Current.Page.MarginCm);

            // Un résultat de la recherche choisi au clavier : le focus reprend sur la section qu'il ouvre.
            hub.Query = "contraste";
            TestShell.Flush();
            var hit = Hits(window).Single();
            var scope = FocusManager.GetFocusScope(hit);
            FocusManager.SetFocusedElement(scope, hit);
            hit.Command.Execute(hit.CommandParameter);
            TestShell.Flush();
            Assert.AreEqual("Apparence", hub.Selected.Label);
            Assert.IsTrue(FocusManager.GetFocusedElement(scope) is RadioButton { IsChecked: true }, $"le clavier reprend sur la section ouverte, pas sur {FocusManager.GetFocusedElement(scope)?.GetType().Name ?? "rien"}");

            // En fenêtre étroite, sur la liste déroulante des sections.
            host.Width = 560;
            TestShell.Flush();
            hub.Query = "voix";
            TestShell.Flush();
            var narrow = Hits(window).Single();
            FocusManager.SetFocusedElement(scope, narrow);
            narrow.Command.Execute(narrow.CommandParameter);
            TestShell.Flush();
            Assert.AreEqual("Lecture", hub.Selected.Label);
            Assert.AreSame(Named<ComboBox>(window, "SectionChoice"), FocusManager.GetFocusedElement(scope));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void In_a_narrow_window_the_sections_are_chosen_in_a_drop_down_list()
    {
        using var ui = new UiThread();
        using var vault = new DataVault();
        var test = new TestShell();
        var hub = vault.Hub();
        hub.OnNavigatedTo();
        var host = new Border { Width = 560, Height = 700, Child = new SettingsHubPage { DataContext = hub } };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 700, Height = 800, ShowActivated = false };
        window.Show();
        TestShell.Flush();
        try
        {
            Assert.IsFalse(Find<FrameworkElement>(window, "Side") is { IsVisible: true }, "la liste de gauche ne tient plus");
            var heading = Named<TextBlock>(window, "NarrowHeading");
            Assert.IsTrue(heading.IsVisible);
            Assert.AreEqual("Paramètres", heading.Text);
            Assert.AreEqual("Heading", AutomationProperties.GetAutomationId(heading), "le titre de la vue se retrouve sous le même identifiant que dans la liste");
            Assert.IsFalse(Find<FrameworkElement>(window, "SectionTitle") is { IsVisible: true }, "la liste déroulante nomme déjà la section");
            var choice = Named<ComboBox>(window, "SectionChoice");
            Assert.IsTrue(choice.IsVisible);
            Assert.AreSame(hub.Tabs[0], choice.SelectedItem);

            // Par son rang, comme le fait un choix dans la liste ouverte : écrire la valeur liée elle-même romprait son lien.
            choice.SelectedIndex = 1;
            TestShell.Flush();
            Assert.AreSame(hub.Tabs[1], hub.Selected);
            Assert.IsTrue(Named<FrameworkElement>(window, "RateChoice").IsVisible);
            Assert.IsFalse(Find<FrameworkElement>(window, "ThemeChoice") is { IsVisible: true });

            hub.Tabs[2].IsSelected = true;
            TestShell.Flush();
            Assert.AreSame(hub.Tabs[2], choice.SelectedItem, "la liste suit un choix fait ailleurs");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_tabs_change_the_page_shown_without_horizontal_scrolling()
    {
        using var ui = new UiThread();
        using var vault = new DataVault();
        var test = new TestShell();
        var hub = vault.Hub();
        hub.OnNavigatedTo();
        var host = new Border { Width = 980, Height = 700, Child = new SettingsHubPage { DataContext = hub } };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 1100, Height = 800, ShowActivated = false };
        window.Show();
        TestShell.Flush();
        try
        {
            Assert.AreEqual("Paramètres", Named<TextBlock>(window, "Heading").Text);
            var radios = Descendants<RadioButton>(window).ToList();
            Assert.HasCount(7, radios, "les sections, dans la liste de gauche");
            Assert.IsTrue(radios[0].IsChecked);
            Assert.AreEqual("Apparence", Named<TextBlock>(window, "SectionTitle").Text);
            Assert.IsTrue(Named<FrameworkElement>(window, "ThemeChoice").IsVisible, "Apparence s'ouvre d'abord");
            Assert.IsTrue(Named<FrameworkElement>(window, "TextSizeChoice").IsVisible, "le texte des fiches se règle dans Apparence");
            Assert.IsFalse(Find<FrameworkElement>(window, "RateChoice") is { IsVisible: true }, "la voix est dans Lecture");

            // Lecture est une autre part de la même page : ses cartes se montrent, celles d'Apparence se cachent.
            radios[1].IsChecked = true;
            TestShell.Flush();
            Assert.AreEqual("Lecture", Named<TextBlock>(window, "SectionTitle").Text);
            Assert.IsTrue(Named<FrameworkElement>(window, "DefinedWordsBox").IsVisible);
            Assert.IsTrue(Named<FrameworkElement>(window, "RateChoice").IsVisible);
            Assert.IsFalse(Find<FrameworkElement>(window, "ThemeChoice") is { IsVisible: true });
            Assert.IsFalse(Find<FrameworkElement>(window, "TextSizeChoice") is { IsVisible: true });

            radios[2].IsChecked = true;
            TestShell.Flush();
            Assert.IsNotNull(Find<FrameworkElement>(window, "StepsBox"));

            radios[3].IsChecked = true;
            TestShell.Flush();
            Assert.IsNotNull(Find<FrameworkElement>(window, "QuotesBox"));

            // Coffre et chiffrement, puis Sauvegardes et exports : deux parts de la page des données.
            radios[4].IsChecked = true;
            TestShell.Flush();
            UiThread.Until(() => vault.Page.IsLoaded);
            TestShell.Flush();
            Assert.IsTrue(Named<FrameworkElement>(window, "VaultCard").IsVisible);
            Assert.IsFalse(Find<FrameworkElement>(window, "BackupsCard") is { IsVisible: true });
            radios[5].IsChecked = true;
            TestShell.Flush();
            Assert.IsFalse(Find<FrameworkElement>(window, "VaultCard") is { IsVisible: true });
            Assert.IsTrue(Named<FrameworkElement>(window, "BackupsCard").IsVisible);
            Assert.IsFalse(hub.Tabs[0].IsSelected);
            Assert.IsNull(Find<FrameworkElement>(window, "StepsBox"));
            Assert.AreEqual(Visibility.Visible, Named<FrameworkElement>(window, "BackupsList").Visibility);
            Assert.AreEqual(Visibility.Collapsed, Named<FrameworkElement>(window, "TrashList").Visibility, "une liste vide ne s'affiche pas");
            Assert.AreEqual(Visibility.Visible, Named<FrameworkElement>(window, "NoTrash").Visibility);
            var scroller = Named<ScrollViewer>(window, "Scroller");
            Assert.IsLessThanOrEqualTo(scroller.ViewportWidth, scroller.ExtentWidth);

            radios[6].IsChecked = true;
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

    /// <summary>Les boutons des résultats de la recherche, ceux que l'on voit.</summary>
    private static List<Button> Hits(DependencyObject root) => [.. Descendants<Button>(root).Where(button => button is { IsVisible: true, DataContext: SettingsHit })];

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

        /// <summary>La vue entière, comme l'application la compose : avec Feuilles, Menu et Raccourcis, sur ces préférences.</summary>
        public SettingsHubViewModel Hub(MachineSettings machine) =>
            new(
                new GeneralSettingsViewModel(machine, new FakeVoices("Microsoft Hortense Desktop")),
                new SettingsPageViewModel(App.Settings, TimeProvider.System),
                new ModulesSettingsViewModel(App.Settings, _ => { }, FakeExtensions.Settings()),
                Page,
                new AboutSettingsViewModel(App.Vaults),
                new SheetsSettingsViewModel(machine),
                new ShortcutsSettingsViewModel(machine),
                machine);

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
