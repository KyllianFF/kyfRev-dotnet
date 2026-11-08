using System.Text.Json;
using KyfRev.Application.Model;
using KyfRev.Application.Tests.Support;
using KyfRev.Application.UseCases;
using KyfRev.Rendering;

namespace KyfRev.Application.Tests;

/// <summary>Les scénarios de la conception que le plan exige automatisés à J9, avec le vrai coffre sur le disque.</summary>
[TestClass]
public sealed class PlanScenarioTests
{
    private const string Exact = "la prophase";

    [TestMethod]
    public void T13_renaming_or_moving_a_sheet_keeps_annotations_tracking_images_and_incoming_links()
    {
        using var folder = new TestFolder();
        using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0));
        var app = test.App;
        Assert.IsNull(app.Vaults.Create(folder.Vault).OpenError);
        var (target, source, cycle) = Prepared(app, folder);

        Assert.IsTrue(app.Sheets.ChangeTitle(target, "Cible renommée").Succeeded);
        Intact(app, folder, target, source, cycle, "fiches/Biologie/Cible renommée.md");

        Assert.IsTrue(app.Sheets.Move(target, "Cours").Succeeded);
        Intact(app, folder, target, source, cycle, "fiches/Cours/Cible renommée.md");

        Assert.IsTrue(app.Folders.Move("Cours", "Semestre").Succeeded);
        Intact(app, folder, target, source, cycle, "fiches/Semestre/Cible renommée.md");
    }

    [TestMethod]
    public void T13_a_sheet_moved_in_the_explorer_is_found_again_by_its_id()
    {
        using var folder = new TestFolder();
        using var test = new TestApplication(FakeTime.AtLocal(2026, 10, 1, 9, 0));
        var app = test.App;
        Assert.IsNull(app.Vaults.Create(folder.Vault).OpenError);
        var (target, _, cycle) = Prepared(app, folder);
        app.Vaults.Close();
        var moved = Path.Combine(folder.Vault, "fiches", "Déplacée.md");
        File.Move(Path.Combine(folder.Vault, "fiches", "Biologie", "Cible.md"), moved);

        Assert.IsNull(app.Vaults.Open(folder.Vault).OpenError);

        var opened = app.Sheets.Open(target).Value;
        Assert.AreEqual("fiches/Déplacée.md", opened.Path);
        Assert.AreEqual(AnchorState.Anchored, opened.Annotations.Single().AnchorState);
        Assert.AreEqual(cycle, CycleOf(folder, target));
    }

    private static (string Target, string Source, string Cycle) Prepared(KyfRevApplication app, TestFolder folder)
    {
        var target = app.Sheets.Create("Biologie", "Cible", null).Value;
        var image = app.Editing.AddImage(target, "Carte.png", new byte[] { 1, 2, 3 }).Value;
        Save(app, target, $"La mitose commence par la prophase.\n\n![carte]({image})\n");
        var read = app.Sheets.Open(target).Value;
        var map = TextMap.Of(SheetRenderer.Render(read.Body, read.Title));
        var start = map.Text.IndexOf(Exact, StringComparison.Ordinal);
        var anchor = Anchoring.AnchorOf(map.Text, new TextPosition(start, start + Exact.Length), map.SectionAt(start), map.SourceLineAt(start, read.FirstBodyLine), read.Fingerprint);
        Assert.IsTrue(app.Annotations.Create(target, anchor, "Le début.").Succeeded);
        var source = app.Sheets.Create(string.Empty, "Source", null).Value;
        Save(app, source, "Voir [la cible](Biologie/Cible.md).\n");
        return (target, source, CycleOf(folder, target));
    }

    private static void Save(KyfRevApplication app, string id, string body) =>
        Assert.IsInstanceOfType<SheetSaved>(app.Editing.Save(id, body, app.Sheets.Open(id).Value.Fingerprint, 1).Value);

    private static void Intact(KyfRevApplication app, TestFolder folder, string target, string source, string cycle, string path)
    {
        var opened = app.Sheets.Open(target).Value;
        Assert.AreEqual(path, opened.Path);
        var annotation = opened.Annotations.Single();
        Assert.AreEqual(AnchorState.Anchored, annotation.AnchorState, "l'annotation reste ancrée");
        Assert.AreEqual(cycle, CycleOf(folder, target), "le calendrier de la fiche ne bouge pas");

        var images = Directory.GetFiles(folder.Vault, "carte.png", SearchOption.AllDirectories);
        Assert.HasCount(1, images, "l'image suit sa fiche");
        var sheetFile = Path.Combine([folder.Vault, .. path.Split('/')]);
        StringAssert.StartsWith(images[0], Path.ChangeExtension(sheetFile, null), StringComparison.Ordinal);

        var link = string.Join('/', path.Split('/').Skip(1)).Replace(" ", "%20", StringComparison.Ordinal);
        Assert.Contains(link, app.Sheets.Open(source).Value.Body, "le lien entrant suit la fiche");

        var health = app.Maintenance.Health().Value;
        Assert.AreEqual(0, health.Problems, string.Join(" | ", health.BrokenLinks.Select(broken => broken.ToString())));
        Assert.IsEmpty(health.BrokenLinks);
    }

    private static string CycleOf(TestFolder folder, string sheet)
    {
        using var tracking = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder.Vault, ".suivi", "fiches", $"{sheet}.json")));
        return tracking.RootElement.GetProperty("cycle").GetRawText();
    }
}
