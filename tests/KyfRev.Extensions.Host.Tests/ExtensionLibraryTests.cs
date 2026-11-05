using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KyfRev.Application;

namespace KyfRev.Extensions.Host.Tests;

/// <summary>Le manifeste et l'installation des extensions, sur de vrais dossiers.</summary>
[TestClass]
public sealed class ExtensionLibraryTests
{
    private const string Quiz = """{"id":"quiz","nom":"Quiz","version":"1.0.0","blocs":["quiz"],"permissions":["stockage"]}""";

    private static readonly DateTimeOffset Moment = new(2026, 11, 5, 18, 30, 0, TimeSpan.Zero);

    private static string Fixtures
    {
        get
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            {
                var candidate = Path.Combine(folder.FullName, "tests", "fixtures", "extensions");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
            throw new InvalidOperationException("Fichiers d'essai introuvables.");
        }
    }

    [TestMethod]
    public void A_manifest_gives_its_fields_and_defaults()
    {
        var manifest = ExtensionManifest.Parse(Encoding.UTF8.GetBytes(Quiz)).Value;

        Assert.AreEqual("extension.js", manifest.Entry);
        Assert.AreEqual(ExtensionRight.Storage, manifest.Permissions.Single());
        Assert.AreEqual("quiz", manifest.Blocks.Single());
        Assert.IsNull(manifest.ViewTitle);
        Assert.AreEqual(string.Empty, manifest.Description);
        Assert.IsNull(manifest.Author);
    }

    [TestMethod]
    public void A_manifest_reads_every_field()
    {
        var json = """{"id":"mon-outil","nom":"Outil","version":"2","description":"Fait","auteur":"Moi","entree":"src\\main.js","permissions":["fiches:lecture","stockage"],"vue":{"titre":"Vue","autre":1}}""";

        var manifest = ExtensionManifest.Parse(Encoding.UTF8.GetBytes(json)).Value;

        Assert.AreEqual("src/main.js", manifest.Entry);
        Assert.AreEqual("Moi", manifest.Author);
        Assert.AreEqual("Fait", manifest.Description);
        Assert.AreEqual("Vue", manifest.ViewTitle);
        Assert.IsTrue(manifest.Blocks.IsEmpty);
        CollectionAssert.AreEqual(new[] { ExtensionRight.ReadSheets, ExtensionRight.Storage }, manifest.Permissions.ToArray());
    }

    [TestMethod]
    [DataRow("""pas du json""", "illisible")]
    [DataRow("""[]""", "objet")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"executer":true}""", "« executer » inconnu")]
    [DataRow("""{"id":"quiz","id":"autre","nom":"Q","version":"1","blocs":["quiz"]}""", "en double")]
    [DataRow("""{"nom":"Q","version":"1","blocs":["quiz"]}""", "obligatoires")]
    [DataRow("""{"id":1,"nom":"Q","version":"1","blocs":["quiz"]}""", "obligatoires")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"description":1}""", "sont des textes")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"auteur":1}""", "sont des textes")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"entree":1}""", "sont des textes")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"permissions":"stockage"}""", "une liste")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"permissions":["reseau"]}""", "« reseau » inconnue")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"permissions":[1]}""", "inconnue")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":"quiz"}""", "liste de textes")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":[1]}""", "liste de textes")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","vue":"Q"}""", "« titre »")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","vue":{}}""", "« titre »")]
    [DataRow("""{"id":"Quiz","nom":"Q","version":"1","blocs":["quiz"]}""", "identifiant")]
    [DataRow("""{"id":"q","nom":"Q","version":"1","blocs":["quiz"]}""", "identifiant")]
    [DataRow("""{"id":"-quiz","nom":"Q","version":"1","blocs":["quiz"]}""", "identifiant")]
    [DataRow("""{"id":"quiz-","nom":"Q","version":"1","blocs":["quiz"]}""", "identifiant")]
    [DataRow("""{"id":"quiz","nom":" ","version":"1","blocs":["quiz"]}""", "nom vide")]
    [DataRow("""{"id":"quiz","nom":"Q","version":" ","blocs":["quiz"]}""", "version vide")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"123456789012345678901","blocs":["quiz"]}""", "version vide")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["mermaid"]}""", "réservé")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["smartart"]}""", "réservé")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["Quiz"]}""", "réservé")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":[""]}""", "réservé")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["a","b","c","d","e","f","g","h","i","j","k"]}""", "10 types")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"entree":"../x.js"}""", "refusé")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"entree":"/x.js"}""", "refusé")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"entree":"x.exe"}""", "fichier .js")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"entree":".cache/x.js"}""", "caché")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","vue":{"titre":" "}}""", "titre de vue")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1"}""", "ni bloc, ni vue")]
    public void A_bad_manifest_is_refused(string json, string reason)
    {
        var outcome = ExtensionManifest.Parse(Encoding.UTF8.GetBytes(json));

        Assert.IsFalse(outcome.Succeeded, json);
        StringAssert.Contains(outcome.Error, reason, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Long_texts_of_a_manifest_are_refused_by_letters_and_not_by_bytes()
    {
        Assert.IsTrue(ExtensionManifest.Parse(Manifest(name: new string('é', 60))).Succeeded);
        Assert.IsFalse(ExtensionManifest.Parse(Manifest(name: new string('a', 61))).Succeeded);
        Assert.IsTrue(ExtensionManifest.Parse(Manifest(description: new string('é', 300))).Succeeded);
        Assert.IsFalse(ExtensionManifest.Parse(Manifest(description: new string('a', 301))).Succeeded);
        Assert.IsTrue(ExtensionManifest.Parse(Manifest(view: new string('é', 40))).Succeeded);
        Assert.IsFalse(ExtensionManifest.Parse(Manifest(view: new string('a', 41))).Succeeded);
        Assert.IsFalse(ExtensionManifest.IsValidId(new string('a', 41)));
        Assert.IsTrue(ExtensionManifest.IsValidId(new string('a', 40)));
    }

    [TestMethod]
    [DataRow("a.js", null)]
    [DataRow("dossier/a.js", null)]
    [DataRow("/a.js", "absolu")]
    [DataRow("a//b.js", "vide")]
    [DataRow("../a.js", "« .. »")]
    [DataRow("./a.js", "« .. »")]
    [DataRow("c:/a.js", "interdit")]
    [DataRow("a?.js", "interdit")]
    [DataRow("a.js.", "final")]
    [DataRow("a.js ", "final")]
    [DataRow("CON.js", "réservé")]
    [DataRow("dossier/lpt1", "réservé")]
    public void A_path_of_an_extension_is_checked(string path, string? reason)
    {
        if (reason is null)
        {
            Assert.IsNull(ExtensionPath.Refusal(path));
        }
        else
        {
            StringAssert.Contains(ExtensionPath.Refusal(path), reason, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void A_control_character_or_a_too_long_name_is_refused()
    {
        StringAssert.Contains(ExtensionPath.Refusal("a\u0001.js"), "interdit", StringComparison.Ordinal);
        StringAssert.Contains(ExtensionPath.Refusal(new string('a', 256)), "trop long", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("a.js", "text/javascript; charset=utf-8")]
    [DataRow("a.MJS", "text/javascript; charset=utf-8")]
    [DataRow("a.css", "text/css; charset=utf-8")]
    [DataRow("a.json", "application/json")]
    [DataRow("a.md", "text/plain; charset=utf-8")]
    [DataRow("a.txt", "text/plain; charset=utf-8")]
    [DataRow("a.svg", "image/svg+xml")]
    [DataRow("a.png", "image/png")]
    [DataRow("a.jpg", "image/jpeg")]
    [DataRow("a.jpeg", "image/jpeg")]
    [DataRow("a.gif", "image/gif")]
    [DataRow("a.webp", "image/webp")]
    [DataRow("a.exe", null)]
    [DataRow("a.html", null)]
    [DataRow("sansextension", null)]
    public void Only_known_file_types_are_accepted(string name, string? type) =>
        Assert.AreEqual(type, ExtensionPath.ContentTypeOf(name));

    [TestMethod]
    public void The_four_published_extensions_are_read_as_they_are()
    {
        using var scene = new Scene();

        foreach (var (id, name, blocks, view) in new[]
        {
            ("quiz", "Quiz", "quiz", "Quiz"),
            ("chronologie", "Chronologie", "chronologie", "Chronologie"),
            ("vocabulaire", "Vocabulaire", "vocabulaire", "Vocabulaire"),
            ("bilan", "Bilan du coffre", string.Empty, "Bilan du coffre"),
        })
        {
            var preview = scene.Library.Read(Path.Combine(Fixtures, id));
            Assert.IsTrue(preview.Succeeded, preview.Error);
            Assert.AreEqual(name, preview.Value.Manifest.Name);
            Assert.AreEqual(blocks, string.Join(',', preview.Value.Manifest.Blocks));
            Assert.AreEqual(view, preview.Value.Manifest.ViewTitle);
            Assert.AreEqual(2, preview.Value.FileCount);
            Assert.IsNull(preview.Value.InstalledVersion);
        }
    }

    [TestMethod]
    public void The_fixtures_are_the_published_extensions_without_a_change()
    {
        using var catalogue = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Fixtures, "catalogue.json")));
        var checkedFiles = 0;
        foreach (var extension in catalogue.RootElement.GetProperty("extensions").EnumerateArray())
        {
            var id = extension.GetProperty("manifeste").GetProperty("id").GetString()!;
            foreach (var file in extension.GetProperty("fichiers").EnumerateArray())
            {
                var content = File.ReadAllBytes(Path.Combine(Fixtures, id, file.GetProperty("chemin").GetString()!));
                Assert.AreEqual(file.GetProperty("empreinte").GetString(), "sha256:" + Convert.ToHexStringLower(SHA256.HashData(content)), id);
                checkedFiles++;
            }
        }
        Assert.AreEqual(8, checkedFiles);
    }

    [TestMethod]
    public void Installing_copies_and_activates()
    {
        using var scene = new Scene();

        var installed = scene.Library.Install(Path.Combine(Fixtures, "quiz"));

        Assert.IsTrue(installed.Succeeded, installed.Error);
        Assert.IsTrue(installed.Value.IsActive);
        Assert.AreEqual("quiz", scene.Library.Installed().Single().Manifest.Id);
        Assert.IsTrue(scene.Library.Installed().Single().IsActive);
        Assert.AreEqual("quiz", scene.Library.Active("quiz")!.Id);
        CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(Fixtures, "quiz", "extension.js")), scene.Library.Content("quiz", "extension.js"));
    }

    [TestMethod]
    public void A_new_version_stores_the_old_one_and_nothing_is_erased()
    {
        using var scene = new Scene();
        var first = scene.Write("v1", Quiz);
        scene.Library.Install(first);
        var second = scene.Write("v2", Quiz.Replace("1.0.0", "2.0.0", StringComparison.Ordinal));

        var preview = scene.Library.Read(second);
        Assert.AreEqual("1.0.0", preview.Value.InstalledVersion);
        Assert.IsTrue(scene.Library.Install(second).Succeeded);

        Assert.AreEqual("2.0.0", scene.Library.Installed().Single().Manifest.Version);
        Assert.IsTrue(Directory.Exists(Path.Combine(scene.Folder, ".anciennes", "quiz-20261105-183000")));
        Assert.IsTrue(scene.Library.Install(first).Succeeded);
        Assert.IsTrue(Directory.Exists(Path.Combine(scene.Folder, ".anciennes", "quiz-20261105-183000-2")));
    }

    [TestMethod]
    public void An_interrupted_installation_is_stored_and_not_erased()
    {
        using var scene = new Scene();
        Directory.CreateDirectory(Path.Combine(scene.Folder, ".installation-quiz"));

        Assert.IsTrue(scene.Library.Install(scene.Write("v1", Quiz)).Succeeded);

        Assert.IsTrue(Directory.Exists(Path.Combine(scene.Folder, ".anciennes", ".installation-quiz-20261105-183000")));
        Assert.IsFalse(Directory.Exists(Path.Combine(scene.Folder, ".installation-quiz")));
    }

    [TestMethod]
    public void A_folder_that_will_not_do_is_not_installed()
    {
        using var scene = new Scene();
        var missing = Path.Combine(scene.Root, "absent");
        StringAssert.Contains(scene.Library.Read(missing).Error, "introuvable", StringComparison.Ordinal);
        StringAssert.Contains(scene.Library.Install(missing).Error, "introuvable", StringComparison.Ordinal);

        var noEntry = scene.Write("sans-script", Quiz);
        File.Delete(Path.Combine(noEntry, "extension.js"));
        StringAssert.Contains(scene.Library.Read(noEntry).Error, "absent", StringComparison.Ordinal);

        var program = scene.Write("avec-programme", Quiz);
        File.WriteAllText(Path.Combine(program, "outil.exe"), "MZ");
        StringAssert.Contains(scene.Library.Read(program).Error, "type de fichier refusé", StringComparison.Ordinal);
        StringAssert.Contains(scene.Library.Install(program).Error, "type de fichier refusé", StringComparison.Ordinal);

        Assert.IsEmpty(scene.Library.Installed());
    }

    [TestMethod]
    public void Hidden_files_are_left_out_and_folders_are_followed()
    {
        using var scene = new Scene();
        var source = scene.Write("v1", Quiz);
        File.WriteAllText(Path.Combine(source, ".cache"), "x");
        Directory.CreateDirectory(Path.Combine(source, "images"));
        File.WriteAllText(Path.Combine(source, "images", "logo.svg"), "<svg/>");

        Assert.AreEqual(3, scene.Library.Read(source).Value.FileCount);
        Assert.IsTrue(scene.Library.Install(source).Succeeded);

        Assert.IsNotNull(scene.Library.Content("quiz", "images/logo.svg"));
        Assert.IsFalse(File.Exists(Path.Combine(scene.Folder, "quiz", ".cache")));
    }

    [TestMethod]
    public void Too_deep_or_too_big_is_refused()
    {
        using var scene = new Scene();
        var deep = scene.Write("profond", Quiz);
        var folder = Path.Combine(deep, "1", "2", "3", "4", "5", "6");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "a.txt"), "x");
        StringAssert.Contains(scene.Library.Read(deep).Error, "niveaux", StringComparison.Ordinal);

        var many = scene.Write("nombreux", Quiz);
        for (var i = 0; i < 200; i++)
        {
            File.WriteAllText(Path.Combine(many, $"{i}.txt"), "x");
        }
        StringAssert.Contains(scene.Library.Read(many).Error, "trop grosse", StringComparison.Ordinal);

        var big = scene.Write("gros", Quiz);
        File.WriteAllBytes(Path.Combine(big, "gros.txt"), new byte[(10 * 1024 * 1024) + 1]);
        StringAssert.Contains(scene.Library.Read(big).Error, "trop grosse", StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_link_inside_an_extension_is_refused()
    {
        using var scene = new Scene();
        var source = scene.Write("lien", Quiz);
        var target = Path.Combine(scene.Root, "cible");
        Directory.CreateDirectory(target);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(source, "lien"), target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Inconclusive("Ce poste ne permet pas de créer un lien symbolique.");
        }

        StringAssert.Contains(scene.Library.Read(source).Error, "lien symbolique", StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_folder_of_the_library_is_not_a_source()
    {
        using var scene = new Scene();
        scene.Library.Install(scene.Write("v1", Quiz));

        StringAssert.Contains(scene.Library.Install(Path.Combine(scene.Folder, "quiz")).Error, "déjà celui", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Deactivating_hides_it_from_what_runs()
    {
        using var scene = new Scene();
        scene.Library.Install(scene.Write("v1", Quiz));

        Assert.IsTrue(scene.Library.SetActive("quiz", false).Succeeded);
        Assert.IsFalse(scene.Library.Installed().Single().IsActive);
        Assert.IsNull(scene.Library.Active("quiz"));
        Assert.IsNull(scene.Library.Content("quiz", "extension.js"));

        Assert.IsTrue(scene.Library.SetActive("quiz", true).Succeeded);
        Assert.IsTrue(scene.Library.Installed().Single().IsActive);
        StringAssert.Contains(scene.Library.SetActive("absente", true).Error, "introuvable", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Removing_stores_it_and_leaves_nothing_active()
    {
        using var scene = new Scene();
        scene.Library.Install(scene.Write("v1", Quiz));

        Assert.IsTrue(scene.Library.Retire("quiz").Succeeded);

        Assert.IsEmpty(scene.Library.Installed());
        Assert.IsNull(scene.Library.Active("quiz"));
        Assert.IsTrue(Directory.Exists(Path.Combine(scene.Folder, ".anciennes", "quiz-20261105-183000")));
        StringAssert.Contains(scene.Library.Retire("../dehors").Error, "invalide", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("../quiz/extension.js")]
    [DataRow("..\\extension.js")]
    [DataRow("etat.json/../extension.js")]
    [DataRow(".cache/a.js")]
    [DataRow("outil.exe")]
    [DataRow("absent.js")]
    public void Nothing_is_read_outside_of_the_accepted_files(string path)
    {
        using var scene = new Scene();
        var source = scene.Write("v1", Quiz);
        scene.Library.Install(source);
        File.WriteAllText(Path.Combine(scene.Folder, "quiz", "outil.exe"), "MZ");

        Assert.IsNull(scene.Library.Content("quiz", path));
        Assert.IsNull(scene.Library.Content("../quiz", "extension.js"));
        Assert.IsNull(scene.Library.Content("absente", "extension.js"));
    }

    [TestMethod]
    public void A_folder_that_does_not_carry_its_name_or_a_broken_manifest_is_ignored()
    {
        using var scene = new Scene();
        scene.Library.Install(scene.Write("v1", Quiz));
        Directory.Move(Path.Combine(scene.Folder, "quiz"), Path.Combine(scene.Folder, "autre"));
        Assert.IsEmpty(scene.Library.Installed());
        Assert.IsNull(scene.Library.Active("autre"));

        Directory.Move(Path.Combine(scene.Folder, "autre"), Path.Combine(scene.Folder, "quiz"));
        File.WriteAllText(Path.Combine(scene.Folder, "quiz", ExtensionManifest.FileName), "{");
        Assert.IsEmpty(scene.Library.Installed());
    }

    [TestMethod]
    public void A_broken_state_file_activates_nothing()
    {
        using var scene = new Scene();
        scene.Library.Install(scene.Write("v1", Quiz));
        File.WriteAllText(Path.Combine(scene.Folder, "etat.json"), "pas du json");

        Assert.IsFalse(scene.Library.Installed().Single().IsActive);
        Assert.IsNull(scene.Library.Active("quiz"));
    }

    [TestMethod]
    public void A_state_written_by_the_old_version_is_read()
    {
        using var scene = new Scene();
        scene.Library.Install(scene.Write("v1", Quiz));
        File.WriteAllText(Path.Combine(scene.Folder, "etat.json"), """{"actives":["quiz"]}""");
        Assert.IsTrue(scene.Library.Installed().Single().IsActive);

        scene.Library.SetActive("quiz", true);
        using var written = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(scene.Folder, "etat.json")));
        Assert.AreEqual("quiz", written.RootElement.GetProperty("actives")[0].GetString());
    }

    [TestMethod]
    public void The_first_launch_takes_the_extensions_of_the_old_version_without_touching_them()
    {
        using var scene = new Scene();
        var legacy = Path.Combine(scene.Root, "ancienne");
        Directory.CreateDirectory(legacy);
        foreach (var id in new[] { "quiz", "bilan" })
        {
            Directory.CreateDirectory(Path.Combine(legacy, id));
            foreach (var file in Directory.GetFiles(Path.Combine(Fixtures, id)))
            {
                File.Copy(file, Path.Combine(legacy, id, Path.GetFileName(file)));
            }
        }
        Directory.CreateDirectory(Path.Combine(legacy, ".anciennes", "vieux"));
        Directory.CreateDirectory(Path.Combine(legacy, "cassee"));
        File.WriteAllText(Path.Combine(legacy, "etat.json"), """{"actives":["quiz","absente"]}""");
        var before = File.ReadAllText(Path.Combine(legacy, "etat.json"));

        scene.Library.Import(legacy);

        Assert.AreEqual("Bilan du coffre, Quiz", string.Join(", ", scene.Library.Installed().Select(extension => extension.Manifest.Name)));
        Assert.IsNotNull(scene.Library.Active("quiz"));
        Assert.IsNull(scene.Library.Active("bilan"));
        Assert.IsFalse(Directory.Exists(Path.Combine(scene.Folder, ".anciennes")));
        Assert.AreEqual(before, File.ReadAllText(Path.Combine(legacy, "etat.json")));
        Assert.IsTrue(File.Exists(Path.Combine(legacy, "quiz", "extension.js")));

        // Une fois repris, la bibliothèque est la seule à compter.
        scene.Library.Retire("quiz");
        scene.Library.Import(legacy);
        Assert.AreEqual("bilan", scene.Library.Installed().Single().Manifest.Id);
    }

    [TestMethod]
    public void Nothing_to_take_from_the_old_version_creates_nothing()
    {
        using var scene = new Scene();

        scene.Library.Import(Path.Combine(scene.Root, "absent"));

        Assert.IsFalse(Directory.Exists(scene.Folder));
        Assert.IsEmpty(scene.Library.Installed());
    }

    [TestMethod]
    public void The_library_needs_a_folder_and_a_clock()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ExtensionLibrary(string.Empty, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionLibrary("x", null!));
    }

    private static byte[] Manifest(string name = "Q", string description = "", string view = "V") =>
        JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["id"] = "quiz",
            ["nom"] = name,
            ["version"] = "1",
            ["description"] = description,
            ["vue"] = new Dictionary<string, string> { ["titre"] = view },
        });

    /// <summary>Une bibliothèque dans un dossier d'essai, avec des dossiers d'extensions à installer.</summary>
    private sealed class Scene : IDisposable
    {
        public Scene()
        {
            Root = Path.Combine(Path.GetTempPath(), "kyfrev-extensions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Folder = Path.Combine(Root, "bibliotheque");
            Library = new ExtensionLibrary(Folder, new FixedTime());
        }

        public string Root { get; }

        public string Folder { get; }

        public ExtensionLibrary Library { get; }

        /// <summary>Un dossier d'extension à installer, avec son manifeste et son script.</summary>
        public string Write(string name, string manifest)
        {
            var folder = Path.Combine(Root, "sources", name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, ExtensionManifest.FileName), manifest);
            File.WriteAllText(Path.Combine(folder, "extension.js"), "kyfrev.bloc('quiz', () => '');");
            return folder;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private sealed class FixedTime : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Moment;

            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }
    }
}

/// <summary>Les demandes d'une extension : permissions vérifiées par l'hôte, à chaque fois.</summary>
[TestClass]
public sealed class ExtensionRequestsTests
{
    private static readonly JsonElement NoArguments = JsonDocument.Parse("[]").RootElement;

    [TestMethod]
    public async Task A_sheet_is_read_with_the_permission()
    {
        using var scene = new Scene("""["fiches:lecture"]""");

        var list = await scene.Requests.AnswerAsync("essai", "fiches.lister", NoArguments);
        Assert.AreEqual("""[{"id":"f_1","titre":"Une fiche","dossier":"Cours","etiquettes":["a"],"archivee":false}]""", list.Value);

        var text = await scene.Requests.AnswerAsync("essai", "fiches.lire", JsonDocument.Parse("""["f_1"]""").RootElement);
        Assert.AreEqual("""{"id":"f_1","titre":"Une fiche","texte":"Son texte"}""", text.Value);
    }

    [TestMethod]
    public async Task A_refusal_of_the_vault_comes_back_as_it_is()
    {
        using var scene = new Scene("""["fiches:lecture", "stockage"]""");
        scene.Data.Refusal = "coffre fermé";

        Assert.AreEqual("coffre fermé", (await scene.Requests.AnswerAsync("essai", "fiches.lister", NoArguments)).Error);
        Assert.AreEqual("coffre fermé", (await scene.Requests.AnswerAsync("essai", "fiches.lire", NoArguments)).Error);
        Assert.AreEqual("coffre fermé", (await scene.Requests.AnswerAsync("essai", "stockage.lire", NoArguments)).Error);
        Assert.AreEqual("coffre fermé", (await scene.Requests.AnswerAsync("essai", "stockage.ecrire", NoArguments)).Error);
        Assert.AreEqual(string.Empty, scene.Data.AskedSheet);
    }

    [TestMethod]
    public async Task Storage_keeps_what_the_extension_gives_with_the_permission()
    {
        using var scene = new Scene("""["stockage"]""");
        Assert.AreEqual("null", (await scene.Requests.AnswerAsync("essai", "stockage.lire", NoArguments)).Value);

        var stored = await scene.Requests.AnswerAsync("essai", "stockage.ecrire", JsonDocument.Parse("""[{"score":3}]""").RootElement);
        Assert.AreEqual("null", stored.Value);
        Assert.AreEqual("""{"score":3}""", (await scene.Requests.AnswerAsync("essai", "stockage.lire", NoArguments)).Value);

        await scene.Requests.AnswerAsync("essai", "stockage.ecrire", NoArguments);
        Assert.AreEqual("null", (await scene.Requests.AnswerAsync("essai", "stockage.lire", NoArguments)).Value);
    }

    [TestMethod]
    public async Task Storage_is_limited()
    {
        using var scene = new Scene("""["stockage"]""");
        var tooMuch = JsonDocument.Parse(JsonSerializer.Serialize(new[] { new string('a', ExtensionRequests.MaxStoredBytes) })).RootElement;

        StringAssert.Contains((await scene.Requests.AnswerAsync("essai", "stockage.ecrire", tooMuch)).Error, "1 Mo", StringComparison.Ordinal);
        Assert.IsNull(scene.Data.Stored);
    }

    [TestMethod]
    public async Task Without_the_permission_nothing_is_asked_of_the_vault()
    {
        using var scene = new Scene("[]");

        StringAssert.Contains((await scene.Requests.AnswerAsync("essai", "fiches.lister", NoArguments)).Error, "fiches:lecture", StringComparison.Ordinal);
        StringAssert.Contains((await scene.Requests.AnswerAsync("essai", "fiches.lire", NoArguments)).Error, "fiches:lecture", StringComparison.Ordinal);
        StringAssert.Contains((await scene.Requests.AnswerAsync("essai", "stockage.lire", NoArguments)).Error, "stockage", StringComparison.Ordinal);
        StringAssert.Contains((await scene.Requests.AnswerAsync("essai", "stockage.ecrire", NoArguments)).Error, "stockage", StringComparison.Ordinal);
        Assert.AreEqual(0, scene.Data.Calls);
    }

    [TestMethod]
    public async Task An_unknown_request_or_a_stopped_extension_gets_nothing()
    {
        using var scene = new Scene("""["fiches:lecture"]""");

        StringAssert.Contains((await scene.Requests.AnswerAsync("essai", "fiches.ecrire", NoArguments)).Error, "n'existe pas", StringComparison.Ordinal);

        scene.Library.SetActive("essai", false);
        Assert.AreEqual("extension inactive", (await scene.Requests.AnswerAsync("essai", "fiches.lister", NoArguments)).Error);
        Assert.AreEqual("extension inactive", (await scene.Requests.AnswerAsync("absente", "fiches.lister", NoArguments)).Error);
        Assert.AreEqual(0, scene.Data.Calls);
    }

    [TestMethod]
    public async Task A_bad_identifier_is_given_to_the_vault_as_an_empty_one()
    {
        using var scene = new Scene("""["fiches:lecture"]""");

        await scene.Requests.AnswerAsync("essai", "fiches.lire", JsonDocument.Parse("[1]").RootElement);
        Assert.AreEqual(string.Empty, scene.Data.AskedSheet);
        await scene.Requests.AnswerAsync("essai", "fiches.lire", JsonDocument.Parse("{}").RootElement);
        Assert.AreEqual(string.Empty, scene.Data.AskedSheet);
    }

    [TestMethod]
    public void The_requests_need_their_library_and_their_data()
    {
        using var scene = new Scene("[]");
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionRequests(null!, scene.Data));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionRequests(scene.Library, null!));
    }

    private sealed class Scene : IDisposable
    {
        private readonly string _root;

        public Scene(string permissions)
        {
            _root = Path.Combine(Path.GetTempPath(), "kyfrev-extensions-" + Guid.NewGuid().ToString("N"));
            var source = Path.Combine(_root, "source");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, ExtensionManifest.FileName), $$"""{"id":"essai","nom":"Essai","version":"1","blocs":["essai"],"permissions":{{permissions}}}""");
            File.WriteAllText(Path.Combine(source, "extension.js"), string.Empty);
            Library = new ExtensionLibrary(Path.Combine(_root, "bibliotheque"), TimeProvider.System);
            Library.Install(source);
            Requests = new ExtensionRequests(Library, Data);
        }

        public ExtensionLibrary Library { get; }

        public FakeData Data { get; } = new();

        public ExtensionRequests Requests { get; }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeData : IExtensionData
    {
        public string? Refusal { get; set; }

        public int Calls { get; private set; }

        public string AskedSheet { get; private set; } = string.Empty;

        public string? Stored { get; private set; }

        public Task<Outcome<IReadOnlyList<ExtensionSheet>>> SheetsAsync()
        {
            Calls++;
            return Task.FromResult(Refusal is { } refusal
                ? Outcome.Refused<IReadOnlyList<ExtensionSheet>>(refusal)
                : Outcome.Of<IReadOnlyList<ExtensionSheet>>([new ExtensionSheet("f_1", "Une fiche", "Cours", ["a"], false)]));
        }

        public Task<Outcome<ExtensionSheetText>> SheetAsync(string id)
        {
            Calls++;
            AskedSheet = id;
            return Task.FromResult(Refusal is { } refusal
                ? Outcome.Refused<ExtensionSheetText>(refusal)
                : Outcome.Of(new ExtensionSheetText(id, "Une fiche", "Son texte")));
        }

        public Task<Outcome<string?>> StoredAsync(string extension)
        {
            Calls++;
            return Task.FromResult(Refusal is { } refusal ? Outcome.Refused<string?>(refusal) : Outcome.Of(Stored));
        }

        public Task<Outcome<Done>> StoreAsync(string extension, string json)
        {
            Calls++;
            if (Refusal is { } refusal)
            {
                return Task.FromResult(Outcome.Refused<Done>(refusal));
            }
            Stored = json == "null" ? null : json;
            return Task.FromResult(Outcome.Done());
        }
    }
}
