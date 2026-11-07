using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Les onglets « Données » et « À propos » oublient ce qu'ils ont lu du coffre quand il se verrouille.</summary>
[TestClass]
public sealed class SettingsForgetTests
{
    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose", "Étapes"], 5, "sha256:aa");

    [TestMethod]
    public async Task Forgetting_the_data_tab_drops_what_it_read_from_the_vault_until_it_reads_again()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var root = OpenVault(vaults);
        var app = vaults.App;
        var gone = app.Sheets.Create("Biologie/Cellule", "Mitose", null).Value;
        app.Trash.TrashSheet(gone).Value.ToString();
        var kept = app.Sheets.Create("Chimie", "Atome", null).Value;
        app.Annotations.Trash(app.Annotations.Create(kept, Passage, "Avant la métaphase").Value).Value.ToString();
        app.Settings.Update(preferences => preferences with { BackupRotation = true }).Value.ToString();
        var page = DataTab(vaults);
        await page.ReloadCommand.ExecuteAsync(null);

        // Une sauvegarde restaurée, puis deux confirmations qui attendent leur réponse.
        page.RequestRestoreCommand.Execute(page.Backups[0]);
        await page.AnswerRestoreCommand.ExecuteAsync(true);
        page.RequestRestoreCommand.Execute(page.Backups[0]);
        page.RequestEraseCommand.Execute(page.Trash.Single(row => row.Title == "Mitose"));
        Assert.HasCount(2, page.Trash);
        Assert.IsTrue(page.HasBackups);
        Assert.IsTrue(page.Rotation);
        Assert.IsTrue(page.HasRestoredVault);
        Assert.IsTrue(page.HasMessage);
        Assert.IsTrue(page.IsConfirming);
        Assert.IsTrue(page.IsConfirmingErase);
        StringAssert.Contains(page.EraseMessage, "Mitose", StringComparison.Ordinal);
        StringAssert.Contains(page.VaultLine, root, StringComparison.Ordinal);

        page.Forget();

        Assert.IsEmpty(page.Backups);
        Assert.IsEmpty(page.Trash);
        Assert.IsFalse(page.HasBackups);
        Assert.IsFalse(page.HasTrash);
        Assert.IsFalse(page.HasNoBackup, "rien n'est lu : l'onglet ne prétend pas qu'il n'y a rien");
        Assert.IsFalse(page.HasNoTrash);
        Assert.IsFalse(page.IsLoaded);
        Assert.IsFalse(page.Rotation);
        Assert.AreEqual(string.Empty, page.VaultLine);
        Assert.AreEqual(string.Empty, page.VaultHelp);
        Assert.AreEqual(string.Empty, page.BackupHelp);
        Assert.IsNull(page.RestoredVault);
        Assert.IsFalse(page.HasRestoredVault);
        Assert.IsNull(page.Message);
        Assert.IsNull(page.Error);
        Assert.IsFalse(page.IsConfirming);
        Assert.AreEqual(string.Empty, page.ConfirmMessage);
        Assert.IsFalse(page.IsConfirmingErase);
        Assert.AreEqual(string.Empty, page.EraseMessage);

        // Les confirmations oubliées ne visent plus rien : un oui n'efface ni ne restaure.
        await page.AnswerEraseCommand.ExecuteAsync(true);
        await page.AnswerRestoreCommand.ExecuteAsync(true);
        Assert.HasCount(1, app.Trash.TrashedSheets().Value);
        Assert.IsNull(page.Message);
        Assert.IsNull(page.RestoredVault);

        await page.ReloadCommand.ExecuteAsync(null);

        Assert.IsTrue(page.IsLoaded);
        Assert.IsTrue(page.HasBackups);
        Assert.IsTrue(page.Rotation);
        StringAssert.Contains(page.VaultLine, root, StringComparison.Ordinal);
        var titles = page.Trash.Select(row => row.Title).ToList();
        Assert.Contains("Mitose", titles);
        Assert.Contains("« La prophase »", titles);
    }

    [STATestMethod]
    public void A_read_or_an_action_started_before_forgetting_puts_nothing_back()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        OpenVault(vaults);
        var app = vaults.App;
        var gone = app.Sheets.Create("Biologie/Cellule", "Mitose", null).Value;
        app.Trash.TrashSheet(gone).Value.ToString();
        var page = DataTab(vaults);
        page.ReloadCommand.Execute(null);
        UiThread.Until(() => page.IsLoaded);
        var backup = page.Backups[0];

        // La fiche est restaurée pendant l'oubli : ni son message ni la relecture qui suit ne reviennent.
        var restoring = ((IAsyncRelayCommand)page.Trash.Single().Restore).ExecuteAsync(null);
        page.Forget();
        UiThread.Until(() => restoring.IsCompleted);
        Assert.IsTrue(app.Sheets.Open(gone).Succeeded, "l'action, elle, a eu lieu");
        Assert.IsNull(page.Message);
        Assert.IsEmpty(page.Trash);
        Assert.IsFalse(page.IsLoaded);
        Assert.IsTrue(page.IsIdle);

        // Une lecture partie avant l'oubli n'y remet rien.
        var reading = page.ReloadCommand.ExecuteAsync(null);
        page.Forget();
        UiThread.Until(() => reading.IsCompleted);
        Assert.IsEmpty(page.Backups);
        Assert.IsEmpty(page.Trash);
        Assert.AreEqual(string.Empty, page.VaultLine);
        Assert.AreEqual(string.Empty, page.BackupHelp);
        Assert.IsFalse(page.IsLoaded);

        // Une sauvegarde extraite pendant l'oubli ne montre pas son dossier.
        page.RequestRestoreCommand.Execute(backup);
        var extracting = page.AnswerRestoreCommand.ExecuteAsync(true);
        page.Forget();
        UiThread.Until(() => extracting.IsCompleted);
        Assert.IsNull(page.RestoredVault);
        Assert.IsNull(page.Message);
        Assert.IsTrue(page.IsIdle);
    }

    [TestMethod]
    public void Forgetting_the_about_tab_drops_the_vault_path_until_it_opens_again()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var root = OpenVault(vaults);
        var about = new AboutSettingsViewModel(vaults.App.Vaults);
        about.OnNavigatedTo();
        var (version, mode) = (about.Version, about.Mode);
        Assert.AreEqual(root, about.Vault);

        about.Forget();

        Assert.AreEqual(string.Empty, about.Vault);
        Assert.AreEqual(version, about.Version, "ce qui tourne ne vient pas du coffre");
        Assert.AreEqual(mode, about.Mode);

        about.OnNavigatedTo();
        Assert.AreEqual(root, about.Vault);
    }

    /// <summary>Un coffre en clair, créé et ouvert.</summary>
    private static string OpenVault(TestVaults vaults)
    {
        var root = vaults.PathOf("Coffre");
        vaults.App.Vaults.Create(root);
        return root;
    }

    /// <summary>L'onglet « Données » sur ce coffre, comme le compose l'application sans le module Citations.</summary>
    private static DataSettingsViewModel DataTab(TestVaults vaults)
    {
        var app = vaults.App;
        return new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, vaults.Picker, new FakeOpener(), () => { });
    }
}
