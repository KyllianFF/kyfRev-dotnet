using System.Text;
using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.Application.Tests.Support;
using KyfRev.Application.UseCases;
using KyfRev.Infrastructure.Vault;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.Modules.Schemas.Tests;

/// <summary>Le module Schémas réseau, sur de vrais coffres, assemblé comme le fera l'application.</summary>
[TestClass]
public sealed class SchemaModuleTests
{
    private const string Text = "{\"format\": 2, \"titre\": \"Réseau du lycée\", \"equipements\": [{\"id\": \"e1\"}, {\"id\": \"e2\"}]}";

    private static readonly byte[] Png = [1, 2, 3];

    private static readonly string[] CitingSheets = ["Abeille", "Zèbre"];

    private static readonly string[] Extensions = ["json", "svg", "csv"];

    private static readonly ModuleCatalog Catalog = new([new SchemasModule()]);

    [TestMethod]
    public void A_vault_without_schemas_does_not_load_the_module_unless_asked()
    {
        using var test = new SchemasTest();

        Assert.IsFalse(test.App.Settings.Get().Value.SchemasActive);
        using var inactive = test.Services(out var none);
        Assert.IsEmpty(none);
        Assert.IsNull(inactive.GetService<SchemaUseCases>());
        Assert.IsEmpty(inactive.GetServices<INavigationContributor>());

        Assert.IsTrue(test.App.Settings.Change(test.App.Settings.Get().Value with { SchemasActive = true }).Succeeded);
        using var active = test.Services(out var loaded);

        Assert.AreEqual(SchemasModule.ModuleId, loaded.Single().Id);
        Assert.AreEqual("Schémas réseau", loaded.Single().Name);
        Assert.AreEqual(new NavigationEntry("schemas", "Schémas réseau", 45), active.GetServices<INavigationContributor>().Single().Entry);
        Assert.ThrowsExactly<ArgumentNullException>(() => new SchemasModule().IsActiveFor(null!));
    }

    [TestMethod]
    public void A_schema_is_created_read_saved_and_copied()
    {
        using var test = new SchemasTest();
        var schemas = test.UseCases();

        var created = schemas.Create(Text).Value;
        StringAssert.StartsWith(created.Id, "s_", StringComparison.Ordinal);
        StringAssert.Contains(created.Text, created.Id, StringComparison.Ordinal);
        Assert.AreEqual(created, schemas.Read(created.Id).Value);

        var changed = created.Text.Replace("lycée", "collège", StringComparison.Ordinal);
        var saved = (SheetSaved)schemas.Save(created.Id, changed, created.Fingerprint).Value;
        Assert.AreEqual(saved.Fingerprint, schemas.Read(created.Id).Value.Fingerprint);

        var conflict = (SaveConflict)schemas.Save(created.Id, created.Text, created.Fingerprint).Value;
        Assert.AreEqual(saved.Fingerprint, conflict.DiskFingerprint);
        Assert.AreEqual(changed, conflict.DiskBody);
        Assert.AreEqual(changed, schemas.Read(created.Id).Value.Text, "Un conflit n'écrit rien.");

        var copy = schemas.Duplicate(created.Id).Value;
        Assert.AreNotEqual(created.Id, copy.Id);
        StringAssert.Contains(copy.Text, "(copie)", StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_schema_is_archived_or_put_in_the_bin_and_comes_back()
    {
        using var test = new SchemasTest();
        var schemas = test.UseCases();
        var kept = schemas.Create(Text).Value;
        var moved = schemas.Create(Text).Value;
        Assert.IsEmpty(schemas.Aside().Value);

        // Archivé, il quitte la liste et se retrouve à part ; rien n'est effacé, et il se lit encore.
        Assert.IsTrue(schemas.Move(moved.Id, SchemaPlace.Archived).Value);
        Assert.AreEqual(kept.Id, schemas.Schemas().Value.Single().Id);
        var aside = schemas.Aside().Value.Single();
        Assert.AreEqual((moved.Id, SchemaPlace.Archived), (aside.Id, aside.Place));
        Assert.AreEqual(moved.Text, schemas.Read(moved.Id).Value.Text);

        // À la corbeille, puis remis dans la liste.
        Assert.IsTrue(schemas.Move(moved.Id, SchemaPlace.Trash).Value);
        Assert.AreEqual(SchemaPlace.Trash, schemas.Aside().Value.Single().Place);
        Assert.IsTrue(schemas.Move(moved.Id, SchemaPlace.Active).Value);
        Assert.IsEmpty(schemas.Aside().Value);
        Assert.HasCount(2, schemas.Schemas().Value);
        Assert.IsTrue(schemas.Schemas().Value.All(schema => schema.Place == SchemaPlace.Active));

        // Un schéma qui n'existe pas : un refus, pas une erreur.
        Assert.IsFalse(schemas.Move("s_inconnu", SchemaPlace.Archived).Succeeded);
    }

    [TestMethod]
    public void The_list_gives_titles_counts_and_the_sheets_that_cite_each_schema()
    {
        using var test = new SchemasTest();
        var schemas = test.UseCases();
        Assert.IsEmpty(schemas.Schemas().Value, "Pas même le dossier.");

        var cited = schemas.Create(Text).Value;
        var alone = schemas.Create("{\"format\": 2, \"titre\": \"Annexe\"}").Value;
        test.Sheet("Zèbre", $"```reseau\nschema: {cited.Id}\n```\n\nEt encore : schema:   {cited.Id}\n");
        test.Sheet("Abeille", $"```reseau\nschema: {cited.Id}\n```\n");
        test.Sheet("Sans schéma", "schema: pas-un-identifiant\n");
        File.WriteAllText(test.PathOf("schemas/XXX.json"), "{}");
        File.WriteAllText(test.PathOf($"schemas/s_{new string('1', 26)}.json"), "{ cassé");

        var list = schemas.Schemas().Value;

        CollectionAssert.AreEqual(new[] { alone.Id, cited.Id }, list.Select(schema => schema.Id).ToList(), "Par titre, et les fichiers illisibles n'y sont pas.");
        var first = list[1];
        Assert.AreEqual("Réseau du lycée", first.Title);
        Assert.AreEqual(2, first.Devices);
        Assert.IsNotNull(first.ModifiedAt);
        CollectionAssert.AreEqual(CitingSheets, first.Sheets.Select(sheet => sheet.Title).ToList(), "Par titre, chacune une fois.");
        Assert.IsEmpty(list[0].Sheets);
        Assert.AreEqual(0, list[0].Devices);
    }

    [TestMethod]
    public void A_sheet_whose_file_is_gone_is_left_out_of_the_citations()
    {
        using var test = new SchemasTest();
        var schemas = test.UseCases();
        var cited = schemas.Create(Text).Value;
        var path = test.Sheet("Perdue", $"schema: {cited.Id}\n");
        File.Delete(path);
        test.Reopen();

        Assert.IsEmpty(test.UseCases().Schemas().Value.Single().Sheets);
    }

    [TestMethod]
    public void An_export_goes_outside_the_vault_and_never_replaces_a_file()
    {
        using var test = new SchemasTest();
        var schemas = test.UseCases();
        var outside = test.PathOf("../exports");
        Directory.CreateDirectory(outside);

        var first = schemas.Export(outside, "Réseau.png", Png).Value;
        var second = schemas.Export(outside, "Réseau.png", Png).Value;

        Assert.AreEqual("Réseau.png", Path.GetFileName(first));
        Assert.AreEqual("Réseau (2).png", Path.GetFileName(second));
        CollectionAssert.AreEqual(Png, File.ReadAllBytes(first));

        foreach (var extension in Extensions)
        {
            Assert.IsTrue(schemas.Export(outside, $"Réseau.{extension}", Png).Succeeded);
        }
        StringAssert.Contains(schemas.Export(outside, "Réseau.exe", Png).Error, "JSON, SVG, PNG ou CSV", StringComparison.Ordinal);
        Assert.IsFalse(schemas.Export(outside, "sans extension", Png).Succeeded);
        StringAssert.Contains(schemas.Export(outside, "Réseau.png", new byte[(32 * 1024 * 1024) + 1]).Error, "32 Mo", StringComparison.Ordinal);
        Assert.IsFalse(schemas.Export(test.PathOf("schemas"), "Réseau.png", Png).Succeeded, "Pas dans le coffre.");
        Assert.ThrowsExactly<ArgumentNullException>(() => schemas.Export(outside, null!, Png));
    }

    [TestMethod]
    public void An_import_is_only_read_and_refuses_what_is_not_a_schema_file()
    {
        using var test = new SchemasTest();
        var schemas = test.UseCases();
        var outside = test.PathOf("../imports");
        Directory.CreateDirectory(outside);
        var good = Path.Combine(outside, "reseau.JSON");
        File.WriteAllText(good, Text);

        Assert.AreEqual(Text, schemas.ReadImport(good).Value);
        Assert.IsEmpty(schemas.Schemas().Value, "Rien n'est écrit dans le coffre.");

        var other = Path.Combine(outside, "reseau.txt");
        File.WriteAllText(other, Text);
        StringAssert.Contains(schemas.ReadImport(other).Error, ".json", StringComparison.Ordinal);

        var binary = Path.Combine(outside, "binaire.json");
        File.WriteAllBytes(binary, [0x7B, 0xE9, 0x7D]);
        StringAssert.Contains(schemas.ReadImport(binary).Error, "UTF-8", StringComparison.Ordinal);

        var large = Path.Combine(outside, "gros.json");
        File.WriteAllText(large, new string('x', VaultSession.MaxSchemaBytes + 1), Encoding.UTF8);
        StringAssert.Contains(schemas.ReadImport(large).Error, "8 Mo", StringComparison.Ordinal);

        Assert.IsFalse(schemas.ReadImport(Path.Combine(outside, "absent.json")).Succeeded);
    }

    [TestMethod]
    public void Without_an_open_vault_everything_is_refused()
    {
        using var test = new SchemasTest();
        var schemas = test.UseCases();
        test.App.Host.Dispose();

        Assert.AreEqual(VaultHost.NoVaultOpen, schemas.Schemas().Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, schemas.Read("s_x").Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, schemas.Create(Text).Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, schemas.Save("s_x", Text, "f").Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, schemas.Duplicate("s_x").Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, schemas.Export("d", "n.png", Png).Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, schemas.ReadImport("c.json").Error);
    }

    /// <summary>Un vrai coffre, et le module assemblé comme dans l'application.</summary>
    private sealed class SchemasTest : IDisposable
    {
        private readonly TestFolder _folder = new();
        private readonly TestApplication _test = new(FakeTime.AtLocal(2026, 10, 1, 9, 0));

        public SchemasTest()
        {
            Assert.IsNull(App.Vaults.Create(_folder.Vault).OpenError);
        }

        public KyfRevApplication App => _test.App;

        public string PathOf(string relative) => Path.GetFullPath(Path.Combine(_folder.Vault, relative));

        public SchemaUseCases UseCases() => new(App.Host);

        public ServiceProvider Services(out IReadOnlyList<IKyfRevModule> loaded)
        {
            var services = new ServiceCollection();
            services.AddSingleton(App.Host);
            loaded = Catalog.Register(services, App.Settings.Get().Value);
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        }

        public string Sheet(string title, string body)
        {
            var id = App.Sheets.Create(null, title, null).Value;
            var opened = App.Sheets.Open(id).Value;
            Assert.IsTrue(App.Editing.Save(id, body, opened.Fingerprint, 1).Succeeded);
            return PathOf(opened.Path);
        }

        public void Reopen() => Assert.IsNull(App.Vaults.Open(_folder.Vault).OpenError);

        public void Dispose()
        {
            _test.Dispose();
            _folder.Dispose();
        }
    }
}
