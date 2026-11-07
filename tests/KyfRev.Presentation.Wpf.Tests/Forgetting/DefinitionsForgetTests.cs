using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Définitions » oublie le glossaire quand le coffre se verrouille, et le relit à sa prochaine ouverture.</summary>
[TestClass]
public sealed class DefinitionsForgetTests
{
    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose", "Étapes"], 5, "sha256:aa");

    private static readonly string[] Terms = ["Mitose", "Noyau"];

    private static readonly string[] FolderPaths = ["", "Sciences", "Sciences/Cellule"];

    [TestMethod]
    public async Task Forgetting_drops_the_glossary_and_the_next_load_reads_it_again()
    {
        using var glossary = new Glossary();
        var page = await glossary.LoadedAsync();
        page.SelectedFolder = page.Folders.Single(folder => folder.Path == "Sciences/Cellule");
        page.Search = "mitose";
        var mitose = page.Rows.Single();
        mitose.EditCommand.Execute(null);
        mitose.EditedExplanation = "Mon brouillon.";
        page.RequestDelete(mitose);

        // Une case que le coffre refuse laisse un message.
        var absent = new DefinitionRow(new VaultDefinition("a_absente", "fiche", "La cellule", "Sciences", "Noyau", "Il renferme l'ADN.", [], AnchorState.Anchored, null, null), page);
        absent.IsMarkedEverywhere = false;
        await Waiting.UntilAsync(() => page.HasError);

        page.Forget();

        Assert.IsEmpty(page.Rows);
        Assert.HasCount(1, page.Folders, "seul « Tout le coffre » reste : les noms des dossiers viennent du coffre");
        Assert.AreEqual(string.Empty, page.SelectedFolder!.Path);
        Assert.AreEqual(string.Empty, page.Search, "ce qui était cherché part aussi");
        Assert.IsNull(page.Error);
        Assert.IsFalse(page.HasError);
        Assert.IsFalse(page.IsConfirmingDelete);
        Assert.IsFalse(page.IsLoading);
        Assert.IsFalse(page.HasNoMatch);
        Assert.AreEqual(string.Empty, page.Summary);

        // La confirmation qui attendait ne vise plus rien : la définition reste dans le coffre.
        await page.AnswerDeleteCommand.ExecuteAsync(true);
        Assert.HasCount(2, glossary.App.Annotations.Definitions().Value);

        await page.LoadCommand.ExecuteAsync(null);

        CollectionAssert.AreEqual(Terms, page.Rows.Select(row => row.Term).ToArray(), "relire ramène tout le glossaire");
        CollectionAssert.AreEqual(FolderPaths, page.Folders.Select(folder => folder.Path).ToArray());
        Assert.AreEqual(string.Empty, page.SelectedFolder!.Path);
        Assert.AreEqual("2 définitions", page.Summary);
        var again = page.Rows.Single(row => row.Term == "Mitose");
        Assert.AreNotSame(mitose, again);
        Assert.IsTrue(again.IsShown, "le brouillon est oublié");
        Assert.AreEqual("Division de la cellule.", again.EditedExplanation);
    }

    [STATestMethod]
    public void A_read_started_before_forgetting_puts_nothing_back()
    {
        using var ui = new UiThread();
        using var glossary = new Glossary();
        var page = new DefinitionsPageViewModel(glossary.App.Annotations, glossary.Opener);

        // La lecture part, puis le coffre se verrouille avant qu'elle revienne sur le fil de l'interface.
        page.LoadCommand.Execute(null);
        page.Forget();
        UiThread.Until(() => !page.LoadCommand.IsRunning);

        Assert.IsEmpty(page.Rows);
        Assert.HasCount(1, page.Folders);
        Assert.AreEqual(string.Empty, page.Summary);
        Assert.IsFalse(page.IsLoading);
        Assert.IsNull(page.Error);

        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.LoadCommand.IsRunning);

        CollectionAssert.AreEqual(Terms, page.Rows.Select(row => row.Term).ToArray(), "une lecture partie après l'oubli remplit la vue");
    }

    /// <summary>Un coffre jetable, avec deux définitions dans la fiche « La cellule ».</summary>
    private sealed class Glossary : IDisposable
    {
        private readonly TestVaults _vaults;

        public Glossary()
        {
            _vaults = new TestVaults(AppMode.Development, new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));
            Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Coffre")).Vault);
            var cell = App.Sheets.Create("Sciences/Cellule", "La cellule", null).Value;
            Assert.IsTrue(App.Annotations.CreateDefinition(cell, Passage, new Definition("Mitose", "Division de la cellule.")).Succeeded);
            Assert.IsTrue(App.Annotations.CreateDefinition(cell, Passage, new Definition("Noyau", "Il renferme l'ADN.")).Succeeded);
        }

        public KyfRevApplication App => _vaults.App;

        public FakeOpener Opener { get; } = new();

        public async Task<DefinitionsPageViewModel> LoadedAsync()
        {
            var page = new DefinitionsPageViewModel(App.Annotations, Opener);
            await page.LoadCommand.ExecuteAsync(null);
            return page;
        }

        public void Dispose() => _vaults.Dispose();
    }
}
