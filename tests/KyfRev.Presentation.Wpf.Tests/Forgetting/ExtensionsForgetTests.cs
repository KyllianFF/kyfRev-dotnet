using System.IO;
using KyfRev.Application.Model;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Les extensions oublient le coffre qui se verrouille : la carte des Paramètres perd son journal, les vues leur rendu et les
/// compteurs leur nombre ; les extensions installées restent, et tout se relit à la prochaine ouverture.
/// </summary>
[TestClass]
public sealed class ExtensionsForgetTests
{
    private static string Published => Path.Combine(Repository.Root, "tests", "fixtures", "extensions");

    [STATestMethod]
    public void Forgetting_the_card_drops_the_journal_and_the_faults_until_it_is_reloaded()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        var sheet = vaults.App.Sheets.Create("Biologie", "Mitose", null).Value;
        var data = new ExtensionDataUseCases(vaults.App.Host);
        Assert.IsTrue(data.Note("quiz", new ExtensionJournalEntry(DateTimeOffset.Now, ExtensionJournalEntry.Creation, sheet, "Mitose", "Biologie")).Succeeded);
        var library = new ExtensionLibrary(vaults.PathOf("Extensions"), TimeProvider.System);
        Assert.IsTrue(library.Install(Path.Combine(Published, "quiz")).Succeeded);
        var faults = new FakeFaults();
        var card = new ExtensionSettingsViewModel(library, vaults.Picker, data.Journal, faults);
        card.Reload();
        faults.Add("quiz", new ExtensionFault(DateTimeOffset.Now, "vue", "fiche « Mitose » introuvable"));

        // Avant l'oubli : le journal et l'erreur nomment la fiche.
        Assert.Contains("Mitose", card.Journal.Single().Text);
        Assert.Contains("Mitose", card.Rows.Single().Faults.Single().Text);

        card.Forget();

        Assert.IsEmpty(card.Journal);
        Assert.IsFalse(card.HasJournal);
        Assert.IsEmpty(card.Rows.Single().Faults);
        Assert.IsTrue(card.Rows.Single().HasNoFaults);

        // Les extensions sont sur ce poste, pas dans le coffre : elles restent.
        Assert.AreEqual("Quiz", card.Rows.Single().Name);
        Assert.IsFalse(card.IsEmpty);

        card.Reload();

        Assert.Contains("Mitose", card.Journal.Single().Text);
        Assert.HasCount(1, card.Rows.Single().Faults);
    }

    [EngineTest]
    public void Forgetting_the_pages_empties_the_views_and_the_counters_until_they_are_asked_again()
    {
        using var vaults = new TestVaults(AppMode.Development);
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        Assert.IsTrue(vaults.App.Sheets.Create("Biologie", "Mitose", null).Succeeded);
        var library = new ExtensionLibrary(vaults.PathOf("Extensions"), TimeProvider.System);
        var source = vaults.Folder("titres");
        File.WriteAllText(Path.Combine(source, ExtensionManifest.FileName), """{"api":2,"id":"titres","nom":"Titres","version":"1","permissions":["fiches:lecture"],"vue":{"titre":"Titres","compteur":true}}""");
        File.WriteAllText(Path.Combine(source, "extension.js"), """
            kyfrev.vue(async () => (await kyfrev.fiches.lister()).map((fiche) => fiche.titre).join(", "));
            kyfrev.compteur(async () => (await kyfrev.fiches.lister()).length);
            """);
        var installed = library.Install(source);
        Assert.IsTrue(installed.Succeeded, installed.Error);
        var data = new VaultExtensionData(vaults.App.Sheets, vaults.App.Editing, new ExtensionDataUseCases(vaults.App.Host), TimeProvider.System);
        using var engine = new ExtensionEngine(FakeEnvironment.ExtensionData, library, new ExtensionRequests(library, data));
        using var ui = new UiThread();
        var pages = new ExtensionPages(library, engine, () => false);
        var entry = pages.Entries().Single();
        var page = (ExtensionPageViewModel)entry.Page;
        Open(page);

        // Un rendu parti avant l'oubli n'y remet rien.
        var late = page.RenderAsync();
        page.Forget();
        Wait(late);
        Assert.IsNull(page.Content);
        Assert.IsNull(page.Error);

        Open(page);
        Wait(pages.RefreshCountsAsync());
        Assert.AreEqual("1", entry.Badge!.Text);

        pages.Forget();

        Assert.IsNull(page.Content);
        Assert.IsNull(page.Error);
        Assert.IsFalse(page.HasError);
        Assert.IsFalse(page.IsBusy);
        Assert.IsNull(page.LastAction);
        Assert.IsNull(entry.Badge.Text);
        Assert.IsFalse(entry.Badge.IsShown);

        // La vue et le compteur restent ceux du menu.
        var again = pages.Entries().Single();
        Assert.AreSame(page, again.Page);
        Assert.AreSame(entry.Badge, again.Badge);

        Open(page);
        Wait(pages.RefreshCountsAsync());
        Assert.AreEqual("1", entry.Badge.Text);
    }

    /// <summary>Ouvre la vue et attend son rendu : le titre de la fiche du coffre.</summary>
    private static void Open(ExtensionPageViewModel page)
    {
        page.OnNavigatedTo();
        UiThread.Until(() => page.Content is not null || page.HasError, TimeSpan.FromMinutes(2));
        Assert.IsFalse(page.HasError, page.Error);
        Assert.AreEqual("Mitose", page.Content!.Text.Trim());
    }

    private static void Wait(Task task) => UiThread.Until(() => task.IsCompleted, TimeSpan.FromMinutes(2));

    /// <summary>Des erreurs d'extensions, données à la main.</summary>
    private sealed class FakeFaults : IExtensionFaults
    {
        private readonly Dictionary<string, List<ExtensionFault>> _faults = new(StringComparer.Ordinal);

        public event EventHandler<ExtensionEventArgs>? FaultsChanged;

        public void Add(string id, ExtensionFault fault)
        {
            if (!_faults.TryGetValue(id, out var list))
            {
                list = [];
                _faults[id] = list;
            }
            list.Add(fault);
            FaultsChanged?.Invoke(this, new ExtensionEventArgs(id));
        }

        public IReadOnlyList<ExtensionFault> Faults(string id) => _faults.TryGetValue(id, out var list) ? list : [];

        public void ForgetFaults(string id) => _faults.Remove(id);
    }
}
