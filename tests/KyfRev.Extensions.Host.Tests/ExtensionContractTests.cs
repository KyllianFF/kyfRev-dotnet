using System.Text;
using System.Text.Json;
using KyfRev.Application;

namespace KyfRev.Extensions.Host.Tests;

/// <summary>Le contrat 2 : ce qu'une extension déclare ajouter à l'interface, et des permissions graduées qui se retirent.</summary>
[TestClass]
public sealed class ExtensionContractTests
{
    private const string Full = """
        {
          "api": 2,
          "id": "quiz",
          "nom": "Quiz",
          "version": "2.0.0",
          "icone": "icones\\quiz.svg",
          "permissions": ["fiches:lecture", "stockage", "selection", "fiches:creation", "fiches:ecriture"],
          "vue": { "titre": "Quiz", "compteur": true },
          "commandes": [{ "id": "nouveau", "titre": "Nouveau quiz" }, { "id": "ajouter", "titre": "Ajouter au quiz" }],
          "menus": { "fiche": [{ "commande": "nouveau", "titre": "Créer un quiz" }], "dossier": [{ "commande": "nouveau", "titre": "Quiz du dossier" }] },
          "encart": { "titre": "À revoir" }
        }
        """;

    /// <summary>Ce que deviennent une ligne, une polyligne, un polygone, un rectangle et un cercle.</summary>
    private static readonly string[] ShapePaths = ["M4 4h16", "M1 2L3 4", "M1 2L3 4L5 6", "M1 2L3 4L5 6Z", "M2 3h4v5h-4Z", "M10 12a2 2 0 1 0 4 0a2 2 0 1 0 -4 0"];

    [TestMethod]
    public void A_manifest_of_the_second_contract_reads_what_it_adds()
    {
        var manifest = Parse(Full).Value;

        Assert.AreEqual(2, manifest.Api);
        Assert.AreEqual("icones/quiz.svg", manifest.Icon);
        Assert.IsTrue(manifest.HasCounter);
        Assert.AreEqual("À revoir", manifest.CardTitle);
        CollectionAssert.AreEqual(new[] { new ExtensionCommand("nouveau", "Nouveau quiz"), new ExtensionCommand("ajouter", "Ajouter au quiz") }, manifest.Commands.ToArray());
        CollectionAssert.AreEqual(
            new[] { new ExtensionMenuEntry(ExtensionMenuZone.Sheet, "nouveau", "Créer un quiz"), new ExtensionMenuEntry(ExtensionMenuZone.Folder, "nouveau", "Quiz du dossier") },
            manifest.Menus.ToArray());
        CollectionAssert.AreEqual(
            new[] { ExtensionRight.ReadSheets, ExtensionRight.Storage, ExtensionRight.Selection, ExtensionRight.CreateSheets, ExtensionRight.WriteSheets },
            manifest.Permissions.ToArray());
    }

    [TestMethod]
    public void The_first_contract_is_read_as_before()
    {
        var manifest = Parse("""{"id":"quiz","nom":"Q","version":"1","vue":{"titre":"Q","compteur":true}}""").Value;

        Assert.AreEqual(1, manifest.Api);
        Assert.IsFalse(manifest.HasCounter);
        Assert.IsNull(manifest.Icon);
        Assert.IsNull(manifest.CardTitle);
        Assert.IsTrue(manifest.Commands.IsEmpty);
        Assert.IsTrue(manifest.Menus.IsEmpty);
    }

    [TestMethod]
    [DataRow("""{"api":1,"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"icone":"q.svg"}""", "« icone » inconnu")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"commandes":[]}""", "« commandes » inconnu")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"permissions":["selection"]}""", "« selection » inconnue")]
    [DataRow("""{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"permissions":["fiches:ecriture"]}""", "« fiches:ecriture » inconnue")]
    [DataRow("""{"api":"2","id":"quiz","nom":"Q","version":"1","blocs":["quiz"]}""", "« api » vaut 1 ou 2")]
    [DataRow("""{"api":3,"id":"quiz","nom":"Q","version":"1","blocs":["quiz"]}""", "« api » vaut 1 ou 2")]
    [DataRow("""{"api":0,"id":"quiz","nom":"Q","version":"1","blocs":["quiz"]}""", "« api » vaut 1 ou 2")]
    [DataRow("""{"api":1.5,"id":"quiz","nom":"Q","version":"1","blocs":["quiz"]}""", "« api » vaut 1 ou 2")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"permissions":["reseau"]}""", "« reseau » inconnue")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"icone":1}""", "sont des textes")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"icone":"q.png"}""", "fichier .svg")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"icone":".cache/q.svg"}""", "fichier .svg")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"icone":"../q.svg"}""", "fichier .svg")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","vue":{"titre":"Q","compteur":"oui"}}""", "« compteur » vaut true ou false")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":{}}""", "« commandes » est une liste")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[1]}""", "« commandes » est une liste")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"a"}]}""", "« commandes » est une liste")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"Nouveau","titre":"N"}]}""", "commande « Nouveau » invalide")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"","titre":"N"}]}""", "commande «  » invalide")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"n","titre":" "}]}""", "commande « n » invalide")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"n","titre":"A"},{"id":"n","titre":"B"}]}""", "commande « n » en double")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"n","titre":"N"}],"menus":[]}""", "« menus » est un objet")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"n","titre":"N"}],"menus":{"vue":[]}}""", "« menus » est un objet")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"n","titre":"N"}],"menus":{"fiche":{}}}""", "« menus » est un objet")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"n","titre":"N"}],"menus":{"fiche":[{"titre":"T"}]}}""", "« menus » est un objet")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"n","titre":"N"}],"menus":{"fiche":[1]}}""", "« menus » est un objet")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"n","titre":"N"}],"menus":{"fiche":[{"commande":"autre","titre":"T"}]}}""", "entrée de menu « T »")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"n","titre":"N"}],"menus":{"dossier":[{"commande":"n","titre":" "}]}}""", "entrée de menu")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"n","titre":"N"}],"menus":{"selection":[{"commande":"n","titre":"Ajouter au quiz"}]}}""", "demande la permission « selection »")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","encart":"À revoir"}""", "« encart » est un objet")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","encart":{}}""", "« encart » est un objet")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1","encart":{"titre":" "}}""", "titre d'encart")]
    [DataRow("""{"api":2,"id":"quiz","nom":"Q","version":"1"}""", "ni bloc, ni vue, ni commande, ni encart")]
    public void A_bad_manifest_of_the_second_contract_is_refused(string json, string reason)
    {
        var outcome = Parse(json);

        Assert.IsFalse(outcome.Succeeded, json);
        StringAssert.Contains(outcome.Error, reason, StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_selected_text_can_have_its_entry_with_the_permission()
    {
        var manifest = Parse("""{"api":2,"id":"quiz","nom":"Q","version":"1","permissions":["selection"],"commandes":[{"id":"n","titre":"N"}],"menus":{"selection":[{"commande":"n","titre":"Ajouter au quiz"}]}}""").Value;

        Assert.AreEqual(new ExtensionMenuEntry(ExtensionMenuZone.Selection, "n", "Ajouter au quiz"), manifest.Menus.Single());
    }

    [TestMethod]
    public void A_command_or_a_card_is_enough_and_titles_are_counted_in_letters()
    {
        Assert.IsTrue(Parse("""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"n","titre":"N"}]}""").Succeeded);
        Assert.IsTrue(Parse("""{"api":2,"id":"quiz","nom":"Q","version":"1","encart":{"titre":"E"}}""").Succeeded);
        Assert.IsTrue(Parse(Commands(1, new string('é', 60))).Succeeded);
        Assert.IsFalse(Parse(Commands(1, new string('a', 61))).Succeeded);
        Assert.IsTrue(Parse(Commands(20, "N")).Succeeded);
        StringAssert.Contains(Parse(Commands(21, "N")).Error, "20 commandes", StringComparison.Ordinal);
        var menus = string.Join(',', Enumerable.Range(0, 11).Select(_ => """{"commande":"c0","titre":"M"}"""));
        StringAssert.Contains(Parse($$$"""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"c0","titre":"N"}],"menus":{"fiche":[{{{menus}}}]}}""").Error, "10 entrées de menu", StringComparison.Ordinal);
        Assert.IsTrue(Parse($$"""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"{{new string('a', 40)}}","titre":"N"}]}""").Succeeded);
        Assert.IsFalse(Parse($$"""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{"id":"{{new string('a', 41)}}","titre":"N"}]}""").Succeeded);
    }

    [TestMethod]
    [DataRow(ExtensionRight.ReadSheets, "fiches:lecture", ExtensionRisk.Notable)]
    [DataRow(ExtensionRight.Storage, "stockage", ExtensionRisk.Low)]
    [DataRow(ExtensionRight.Selection, "selection", ExtensionRisk.Notable)]
    [DataRow(ExtensionRight.CreateSheets, "fiches:creation", ExtensionRisk.Notable)]
    [DataRow(ExtensionRight.WriteSheets, "fiches:ecriture", ExtensionRisk.High)]
    public void Each_right_has_its_name_and_its_weight(ExtensionRight right, string name, ExtensionRisk risk)
    {
        Assert.AreEqual(name, ExtensionRights.NameOf(right));
        Assert.AreEqual(risk, ExtensionRights.RiskOf(right));
        Assert.AreEqual(right, ExtensionRights.Parse(name, ExtensionManifest.LatestApi));
        Assert.AreEqual(right is ExtensionRight.ReadSheets or ExtensionRight.Storage ? right : null, ExtensionRights.Parse(name, 1));
    }

    [TestMethod]
    public void An_unknown_right_is_nothing()
    {
        Assert.IsNull(ExtensionRights.Parse("reseau", ExtensionManifest.LatestApi));
        Assert.IsNull(ExtensionRights.Parse(null, ExtensionManifest.LatestApi));
    }

    [TestMethod]
    public void An_icon_keeps_only_its_shapes()
    {
        var icon = Icon("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="red" style="x">
              <title>Quiz</title>
              <path d="M4 4h16" />
              <path />
              <line x1="1" y1="2" x2="3" y2="4" />
              <polyline points="1,2 3,4 5,6" />
              <polygon points="1 2 3 4 5 6" />
              <rect x="2" y="3" width="4" height="5" />
              <circle cx="12" cy="12" r="2" />
              <text x="0" y="0">texte</text>
              <image href="https://fuite.invalid/x.png" />
              <a href="https://fuite.invalid"><rect width="0" height="1" /></a>
              <circle r="0" />
              <polyline points="1 2 3" />
              <polyline points="1 2 a 4" />
            </svg>
            """).Value;

        Assert.AreEqual(new ExtensionIcon(0, 0, 24, 24, icon.Paths, true), icon);
        CollectionAssert.AreEqual(ShapePaths, icon.Paths.ToArray());
    }

    [TestMethod]
    public void A_filled_icon_is_drawn_full()
    {
        var icon = Icon("""<svg viewBox="-1,-2,16,8"><path d="M0 0L16 8Z"/></svg>""").Value;

        Assert.IsFalse(icon.Stroked);
        Assert.AreEqual((-1d, -2d, 16d, 8d), (icon.Left, icon.Top, icon.Width, icon.Height));
    }

    [TestMethod]
    [DataRow("pas du svg", "illisible")]
    [DataRow("""<!DOCTYPE svg [<!ENTITY x "y">]><svg viewBox="0 0 1 1"><path d="M0 0"/></svg>""", "illisible")]
    [DataRow("""<g><path d="M0 0"/></g>""", "« viewBox »")]
    [DataRow("""<svg><path d="M0 0"/></svg>""", "« viewBox »")]
    [DataRow("""<svg viewBox="0 0 0 24"><path d="M0 0"/></svg>""", "« viewBox »")]
    [DataRow("""<svg viewBox="0 0 24"><path d="M0 0"/></svg>""", "« viewBox »")]
    [DataRow("""<svg viewBox="a b c d"><path d="M0 0"/></svg>""", "« viewBox »")]
    [DataRow("""<svg viewBox="0 0 1 1"><text>x</text></svg>""", "aucun tracé")]
    [DataRow("""<svg viewBox="0 0 1 1"><path d="M0 0 url(x)"/></svg>""", "invalide")]
    public void A_bad_icon_is_refused(string svg, string reason)
    {
        var outcome = Icon(svg);

        Assert.IsFalse(outcome.Succeeded, svg);
        StringAssert.Contains(outcome.Error, reason, StringComparison.Ordinal);
    }

    [TestMethod]
    public void An_icon_is_small()
    {
        Assert.IsTrue(Icon($"""<svg viewBox="0 0 1 1"><path d="M0 0{new string('1', 4091)}"/></svg>""").Succeeded);
        Assert.IsFalse(Icon($"""<svg viewBox="0 0 1 1"><path d="M0 0{new string('1', 4093)}"/></svg>""").Succeeded);
        Assert.IsTrue(Icon("""<svg viewBox="0 0 1 1">""" + string.Concat(Enumerable.Repeat("""<path d="M0 0"/>""", 32)) + "</svg>").Succeeded);
        Assert.IsFalse(Icon("""<svg viewBox="0 0 1 1">""" + string.Concat(Enumerable.Repeat("""<path d="M0 0"/>""", 33)) + "</svg>").Succeeded);
        StringAssert.Contains(Icon($"""<svg viewBox="0 0 1 1"><path d="M0 0"/><!--{new string('x', 32 * 1024)}--></svg>""").Error, "32 Ko", StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_library_gives_the_icon_of_an_extension()
    {
        using var scene = new Scene();
        var source = scene.Write("quiz", """{"api":2,"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"icone":"icones/quiz.svg"}""");
        Directory.CreateDirectory(Path.Combine(source, "icones"));
        File.WriteAllText(Path.Combine(source, "icones", "quiz.svg"), """<svg viewBox="0 0 24 24"><path d="M0 0L24 24"/></svg>""");
        scene.Library.Install(source);
        scene.Library.Install(scene.Write("sans", """{"id":"sans","nom":"S","version":"1","blocs":["sans"]}"""));
        scene.Library.Install(scene.Write("perdue", """{"api":2,"id":"perdue","nom":"P","version":"1","blocs":["perdue"],"icone":"absente.svg"}"""));
        var broken = scene.Write("cassee", """{"api":2,"id":"cassee","nom":"C","version":"1","blocs":["cassee"],"icone":"c.svg"}""");
        File.WriteAllText(Path.Combine(broken, "c.svg"), "<svg/>");
        scene.Library.Install(broken);

        Assert.AreEqual("M0 0L24 24", scene.Library.Icon("quiz")!.Paths.Single());
        Assert.IsNull(scene.Library.Icon("sans"));
        Assert.IsNull(scene.Library.Icon("perdue"));
        Assert.IsNull(scene.Library.Icon("cassee"));
        Assert.IsNull(scene.Library.Icon("absente"));
    }

    [TestMethod]
    public void A_right_is_withdrawn_and_granted_again()
    {
        using var scene = new Scene();
        scene.Library.Install(scene.Write("quiz", """{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"permissions":["fiches:lecture","stockage"]}"""));

        Assert.IsTrue(scene.Library.SetGranted("quiz", ExtensionRight.ReadSheets, false).Succeeded);
        CollectionAssert.AreEqual(new[] { ExtensionRight.Storage }, scene.Library.Granted("quiz").ToArray());
        CollectionAssert.AreEqual(new[] { ExtensionRight.ReadSheets }, scene.Library.Withdrawn("quiz").ToArray());

        scene.Library.SetActive("quiz", false);
        Assert.IsEmpty(scene.Library.Granted("quiz"));
        CollectionAssert.AreEqual(new[] { ExtensionRight.ReadSheets }, scene.Library.Withdrawn("quiz").ToArray());

        Assert.IsTrue(scene.Library.SetGranted("quiz", ExtensionRight.ReadSheets, true).Succeeded);
        Assert.IsTrue(scene.Library.SetGranted("quiz", ExtensionRight.ReadSheets, true).Succeeded);
        Assert.IsEmpty(scene.Library.Withdrawn("quiz"));
        Assert.DoesNotContain("retirees", File.ReadAllText(Path.Combine(scene.Folder, "etat.json")));
        Assert.IsEmpty(scene.Library.Withdrawn("absente"));
    }

    [TestMethod]
    public void Only_a_right_that_is_asked_is_withdrawn()
    {
        using var scene = new Scene();
        scene.Library.Install(scene.Write("quiz", """{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"permissions":["stockage"]}"""));

        StringAssert.Contains(scene.Library.SetGranted("quiz", ExtensionRight.ReadSheets, false).Error, "ne demande pas", StringComparison.Ordinal);
        StringAssert.Contains(scene.Library.SetGranted("absente", ExtensionRight.Storage, false).Error, "introuvable", StringComparison.Ordinal);
        Assert.ThrowsExactly<ArgumentNullException>(() => scene.Library.SetGranted(null!, ExtensionRight.Storage, false));
        Assert.ThrowsExactly<ArgumentNullException>(() => scene.Library.Withdrawn(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => scene.Library.Icon(null!));
    }

    [TestMethod]
    public void An_update_keeps_what_was_withdrawn_and_shows_what_is_new()
    {
        using var scene = new Scene();
        scene.Library.Install(scene.Write("quiz", """{"api":2,"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"permissions":["fiches:lecture","stockage"]}"""));
        scene.Library.Install(scene.Write("autre", """{"id":"autre","nom":"A","version":"1","blocs":["autre"],"permissions":["stockage"]}"""));
        scene.Library.SetGranted("quiz", ExtensionRight.ReadSheets, false);
        scene.Library.SetGranted("autre", ExtensionRight.Storage, false);
        var second = scene.Write("quiz-2", """{"api":2,"id":"quiz","nom":"Q","version":"2","blocs":["quiz"],"permissions":["fiches:lecture","fiches:ecriture"]}""");

        var preview = scene.Library.Read(second).Value;
        Assert.AreEqual("1", preview.InstalledVersion);
        CollectionAssert.AreEqual(new[] { ExtensionRight.WriteSheets }, preview.NewRights.ToArray());
        Assert.IsTrue(scene.Library.Install(second).Succeeded);
        CollectionAssert.AreEqual(new[] { ExtensionRight.ReadSheets }, scene.Library.Withdrawn("quiz").ToArray());
        CollectionAssert.AreEqual(new[] { ExtensionRight.WriteSheets }, scene.Library.Granted("quiz").ToArray());

        Assert.IsTrue(scene.Library.Install(scene.Write("quiz-3", """{"api":2,"id":"quiz","nom":"Q","version":"3","blocs":["quiz"],"permissions":["fiches:ecriture"]}""")).Succeeded);
        Assert.IsEmpty(scene.Library.Withdrawn("quiz"));
        CollectionAssert.AreEqual(new[] { ExtensionRight.Storage }, scene.Library.Withdrawn("autre").ToArray());
        Assert.IsEmpty(scene.Library.Read(scene.Write("neuve", """{"id":"neuve","nom":"N","version":"1","blocs":["n"],"permissions":["stockage"]}""")).Value.NewRights);
    }

    [TestMethod]
    public async Task A_withdrawn_right_gets_a_refusal_that_says_so()
    {
        using var scene = new Scene();
        scene.Library.Install(scene.Write("quiz", """{"id":"quiz","nom":"Q","version":"1","blocs":["quiz"],"permissions":["fiches:lecture"]}"""));
        scene.Library.SetGranted("quiz", ExtensionRight.ReadSheets, false);
        var requests = new ExtensionRequests(scene.Library, new NoData());

        var answer = await requests.AnswerAsync("quiz", "fiches.lister", JsonDocument.Parse("[]").RootElement);

        Assert.AreEqual("permission « fiches:lecture » retirée dans les Paramètres", answer.Error);
    }

    private static Outcome<ExtensionManifest> Parse(string json) => ExtensionManifest.Parse(Encoding.UTF8.GetBytes(json));

    private static Outcome<ExtensionIcon> Icon(string svg) => ExtensionIcon.Read(Encoding.UTF8.GetBytes(svg));

    private static string Commands(int count, string title) =>
        $$"""{"api":2,"id":"quiz","nom":"Q","version":"1","commandes":[{{string.Join(',', Enumerable.Range(0, count).Select(i => $$"""{"id":"c{{i}}","titre":"{{title}}"}"""))}}]}""";

    /// <summary>Une bibliothèque dans un dossier d'essai, avec des dossiers d'extensions à installer.</summary>
    private sealed class Scene : IDisposable
    {
        public Scene()
        {
            Root = Path.Combine(Path.GetTempPath(), "kyfrev-contrat-" + Guid.NewGuid().ToString("N"));
            Folder = Path.Combine(Root, "bibliotheque");
            Library = new ExtensionLibrary(Folder, TimeProvider.System);
        }

        public string Root { get; }

        public string Folder { get; }

        public ExtensionLibrary Library { get; }

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

    private sealed class NoData : IExtensionData
    {
        public Task<Outcome<IReadOnlyList<ExtensionSheet>>> SheetsAsync() => throw new InvalidOperationException("rien ne doit être demandé");

        public Task<Outcome<ExtensionSheetText>> SheetAsync(string id) => throw new InvalidOperationException("rien ne doit être demandé");

        public Task<Outcome<string?>> StoredAsync(string extension) => throw new InvalidOperationException("rien ne doit être demandé");

        public Task<Outcome<Done>> StoreAsync(string extension, string json) => throw new InvalidOperationException("rien ne doit être demandé");
    }
}
