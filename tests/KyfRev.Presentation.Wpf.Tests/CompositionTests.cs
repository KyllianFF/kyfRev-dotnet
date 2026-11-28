using System.IO;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Infrastructure.Vault;
using KyfRev.Infrastructure.Windows;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La racine de composition, et les règles qui séparent la présentation de l'infrastructure.</summary>
[TestClass]
public sealed class CompositionTests
{
    [STATestMethod]
    public void The_application_assembles_the_shell_with_the_windows_services()
    {
        using var services = KyfRev.App.Composition.Build();

        Assert.IsInstanceOfType<WindowsAppearance>(services.GetRequiredService<ISystemAppearance>());
        Assert.IsInstanceOfType<WindowChrome>(services.GetRequiredService<IWindowChrome>());
        var window = services.GetRequiredService<ShellWindow>();
        var shell = (ShellViewModel)window.DataContext;
        Assert.AreSame(services.GetRequiredService<ShellViewModel>(), shell);
        Assert.HasCount(5, shell.MainNavigation);
        Assert.IsInstanceOfType<SheetsPageViewModel>(shell.MainNavigation.Single(item => item.Id == CorePages.Sheets).Page);
        Assert.IsInstanceOfType<ArchivesPageViewModel>(shell.MainNavigation.Single(item => item.Id == CorePages.Archives).Page);
        Assert.HasCount(2, shell.SecondaryNavigation);
        Assert.IsNotNull(shell.Locking, "Ctrl+Maj+L verrouille le coffre chiffré");
        Assert.IsNotNull(shell.Search, "Ctrl+K cherche dans le coffre");
        Assert.IsNotNull(shell.WelcomeOffers, "l'accueil propose le coffre d'exemple et le guide");
        Assert.IsEmpty(shell.Search.Providers(), "aucun module actif");
        Assert.IsInstanceOfType<UserPresence>(services.GetRequiredService<IUserPresence>());
        Assert.IsNotNull(services.GetRequiredService<VaultLocking>());
        Assert.IsNotNull(services.GetRequiredService<AutoLock>());
        window.Close();
    }

    [TestMethod]
    public void The_welcome_creates_the_sample_vault_beside_the_planned_one_and_reopens_it_as_it_is()
    {
        using var test = new TestVaults(AppMode.Installed);
        var guides = 0;
        var offers = KyfRev.App.Composition.Welcome(test.App.Vaults, TimeProvider.System, test.Planned, () => guides++);

        // À côté du coffre prévu, dans son propre dossier : ses sauvegardes ne se mêlent pas à celles du vrai coffre.
        var sample = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(test.Planned)!, "Exemple", "Coffre");
        Assert.AreEqual(sample, offers.SampleFolder);
        Assert.IsNull(KyfRev.App.Composition.SampleFolderOf(null), "sans coffre prévu, le dossier se choisit");
        Assert.IsNull(KyfRev.App.Composition.SampleFolderOf(@"C:\"));
        offers.OpenGuide!();
        Assert.AreEqual(1, guides);

        var created = offers.CreateSample(sample);
        Assert.IsTrue(created.Succeeded, created.Error);
        Assert.AreEqual(sample, created.Value.Vault);
        Assert.IsFalse(System.IO.Directory.Exists(test.Planned), "le coffre prévu n'est pas créé pour autant");
        var sheets = System.IO.Directory.GetFiles(System.IO.Path.Combine(sample, "fiches"), "*.md", System.IO.SearchOption.AllDirectories);
        Assert.HasCount(10, sheets);

        // Déjà là : il est rouvert tel qu'il est, pas recréé ni écrasé.
        System.IO.File.AppendAllText(sheets[0], Environment.NewLine + "Une ligne à moi." + Environment.NewLine);
        var again = offers.CreateSample(sample);
        Assert.AreEqual(sample, again.Value.Vault);
        StringAssert.Contains(System.IO.File.ReadAllText(sheets[0]), "Une ligne à moi.", StringComparison.Ordinal);

        // Un dossier qui a autre chose qu'un coffre : rien n'y est écrit, et la raison se lit.
        var other = test.Folder("Autre");
        System.IO.File.WriteAllText(System.IO.Path.Combine(other, "notes.txt"), "à moi");
        var refused = offers.CreateSample(other);
        Assert.IsTrue(refused.Succeeded && refused.Value.OpenError is not null, "ce dossier ne s'ouvre pas comme un coffre, et l'état dit pourquoi");
        Assert.HasCount(1, System.IO.Directory.GetFileSystemEntries(other));
    }

    [TestMethod]
    public async Task The_first_steps_open_the_sample_vault_apart_and_leave_the_open_vault_untouched()
    {
        using var test = new TestVaults(AppMode.Installed);
        var own = test.App.Vaults.Create(test.Planned);
        Assert.AreEqual(test.Planned, own.Vault);
        // Fiches, suivi et réglages ; pas la présence du poste, qui note sa sortie comme à tout changement de coffre.
        var presence = System.IO.Path.Combine(test.Planned, ".suivi", "postes");
        string Snapshot() => string.Join('|', System.IO.Directory.GetFiles(test.Planned, "*", System.IO.SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(presence, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).Select(file => file + "@" + System.IO.File.GetLastWriteTimeUtc(file).Ticks + "#" + new System.IO.FileInfo(file).Length));
        var before = Snapshot();
        var changed = 0;
        var data = new DataSettingsViewModel(test.App.Vaults, test.App.Backups, test.App.Trash, test.App.Annotations, test.App.Exports, test.App.Settings, test.Picker, new FakeOpener(), () => changed++);
        var offers = KyfRev.App.Composition.Welcome(test.App.Vaults, TimeProvider.System, test.Planned, () => { });

        Assert.IsNull(KyfRev.App.Composition.SampleOpener(offers with { SampleFolder = null }, () => data), "sans dossier prévu, le lien ne se propose pas");
        var open = KyfRev.App.Composition.SampleOpener(offers, () => data)!;
        Assert.IsNull(await open(), "ouvert sans rien à dire");
        Assert.AreEqual(offers.SampleFolder, test.App.Vaults.State().Vault);
        Assert.AreEqual(1, changed, "les vues relisent le coffre ouvert");
        Assert.HasCount(10, System.IO.Directory.GetFiles(System.IO.Path.Combine(offers.SampleFolder!, "fiches"), "*.md", System.IO.SearchOption.AllDirectories));
        Assert.AreEqual(before, Snapshot(), "le coffre qu'on quitte n'est pas touché");

        Assert.AreEqual(SettingsHubText.VaultAlreadyOpen, await open(), "déjà ouvert : il le dit, rien ne change");
        Assert.AreEqual(1, changed);

        // Refusé : la raison remonte, le coffre ouvert reste.
        Assert.AreEqual("refusé", await data.OpenSampleAsync(() => KyfRev.Application.Outcome.Refused<AppState>("refusé")));
        Assert.AreEqual(offers.SampleFolder, test.App.Vaults.State().Vault);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => data.OpenSampleAsync(null!));
    }

    [TestMethod]
    public void The_application_uses_the_vault_locations_of_the_running_program()
    {
        using var services = KyfRev.App.Composition.Build();

        Assert.IsInstanceOfType<VaultLocations>(services.GetRequiredService<IVaultLocations>());
        Assert.IsInstanceOfType<FolderPicker>(services.GetRequiredService<IFolderPicker>());
        Assert.IsInstanceOfType<StudySessionFile>(services.GetRequiredService<IStudySessionStore>());
        var vaults = services.GetRequiredService<VaultUseCases>();
        Assert.AreSame(services.GetRequiredService<KyfRevApplication>().Vaults, vaults);
        // Ni portable.json ni installation.json à côté des essais : mode développement, aucun coffre ouvert.
        var state = vaults.State();
        Assert.AreEqual(AppMode.Development, state.Mode);
        Assert.IsNull(state.Vault);
        Assert.AreEqual(KyfRev.App.Composition.Version, state.Version);
        Assert.DoesNotContain("+", state.Version);
        Assert.StartsWith("1.1.0", state.Version);
    }

    [TestMethod]
    public void The_release_date_comes_from_the_metadata_of_the_executable()
    {
        Assert.AreEqual(new DateOnly(2026, 11, 8), KyfRev.App.Composition.ReleaseDateOf([new("Autre", "2020-01-01"), new("ReleaseDate", "2026-11-08")]));
        Assert.IsNull(KyfRev.App.Composition.ReleaseDateOf([new("Autre", "2020-01-01")]), "une construction hors publication n'a pas de date");
        Assert.IsNull(KyfRev.App.Composition.ReleaseDateOf([new("ReleaseDate", "8 novembre")]), "une date illisible n'est pas affichée");
        Assert.IsNull(KyfRev.App.Composition.ReleasedOn, "les essais ne sont pas une publication");
    }

    [TestMethod]
    public void The_application_writes_its_diagnostic_log_next_to_its_local_data()
    {
        using var services = KyfRev.App.Composition.Build();

        var log = (DiagnosticLog)services.GetRequiredService<ILoggerFactory>();
        Assert.AreEqual(Path.Combine(services.GetRequiredService<VaultLocations>().LocalData, "kyfRev.log"), log.LogFile);
        StringAssert.Contains(File.ReadAllText(log.LogFile), "(développement)", StringComparison.Ordinal);
        Assert.AreEqual("portable", KyfRev.App.Composition.ModeName(AppMode.Portable));
        Assert.AreEqual("installée", KyfRev.App.Composition.ModeName(AppMode.Installed));
        Assert.AreEqual("développement", KyfRev.App.Composition.ModeName(AppMode.Development));
    }

    [TestMethod]
    public void Every_top_level_folder_of_a_vault_is_masked_in_the_log()
    {
        var folder = Directory.CreateTempSubdirectory("kyfrev-journal-").FullName;
        try
        {
            using var log = new DiagnosticLog(folder, string.Empty, TimeProvider.System);
            var logger = log.CreateLogger("essai");
            foreach (var name in new[] { VaultLayout.Sheets, VaultLayout.Quotes, VaultLayout.Schemas, VaultLayout.Tracking })
            {
                logger.Log(LogLevel.Error, default, $@"C:\Coffre\{name}\Titre confidentiel.md", null, (text, _) => text);
            }

            Assert.DoesNotContain("confidentiel", File.ReadAllText(log.LogFile));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(AppMode.Portable, false, true)]
    [DataRow(AppMode.Installed, true, true)]
    [DataRow(AppMode.Installed, false, false)]
    [DataRow(AppMode.Development, false, false)]
    public void A_word_stays_in_memory_on_an_encrypted_vault_and_in_portable_mode(AppMode mode, bool encrypted, bool inMemory) =>
        Assert.AreEqual(inMemory, KyfRev.App.Composition.WordsStayInMemory(mode, encrypted));

    [TestMethod]
    public void The_presentation_references_no_infrastructure()
    {
        var references = typeof(ShellViewModel).Assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToList();
        Assert.IsFalse(references.Any(name => name.StartsWith("KyfRev.Infrastructure.", StringComparison.Ordinal)), string.Join(", ", references));
        CollectionAssert.DoesNotContain(references, "KyfRev.Conversion", "la conversion passe par le port de l'application");
        CollectionAssert.Contains(references, "KyfRev.Application");
        CollectionAssert.Contains(references, "KyfRev.DesignSystem");
        var designSystem = typeof(KyfRev.DesignSystem.DesignResources).Assembly.GetReferencedAssemblies().Select(reference => reference.Name!);
        Assert.IsFalse(designSystem.Any(name => name.StartsWith("KyfRev.", StringComparison.Ordinal)));
    }
}
