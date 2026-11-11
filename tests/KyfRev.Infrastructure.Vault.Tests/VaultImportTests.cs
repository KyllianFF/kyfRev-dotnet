using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>Import d'un dossier de notes et réparations de la santé du coffre, hors scénarios.</summary>
public abstract class VaultImportTests(StorageKind storage)
{
    protected static readonly Moment Now = new(new DateOnly(2026, 10, 7), new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void An_import_needs_a_folder_of_notes_outside_the_vault()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var source = TemporaryVault.Empty();
        using var vault = folder.Initialize(Now);
        source.Write("image.png", "x");
        folder.Write("fiches/Déjà/Note.md", "Texte.\n");

        Assert.AreEqual(VaultErrorKind.UnknownFolder, Error(() => vault.ImportFolder(source.PathOf("absent"), "X", track: false, Now)).Kind);
        // Un dossier du coffre sur le disque ; celui d'un coffre chiffré ne contient que ses objets.
        var inside = folder.PathOf(storage == StorageKind.Disk ? "fiches/Déjà" : EncryptedVault.Objects);
        Assert.Contains("déjà dans le coffre", Error(() => vault.ImportFolder(inside, "X", track: false, Now)).Message);
        Assert.Contains("aucune note Markdown", Error(() => vault.ImportFolder(source.Root, "X", track: false, Now)).Message);
        Assert.IsFalse(folder.DirectoryExists("fiches/X"));
    }

    [TestMethod]
    public void A_huge_folder_is_refused()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var source = TemporaryVault.Empty();
        using var vault = folder.Initialize(Now);
        for (var n = 0; n <= VaultSession.MaxImportedNotes; n++)
        {
            File.WriteAllBytes(source.PathOf($"{n}.md"), []);
        }
        Assert.Contains("plus de 5 000 notes", Error(() => vault.ImportFolder(source.Root, "X", track: false, Now)).Message);
    }

    [TestMethod]
    public void Names_are_made_valid_and_never_collide()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var source = TemporaryVault.Empty();
        using var vault = folder.Initialize(Now);
        source.Write("Note  double.md", "Un.\n");
        source.Write("Note double.md", "Deux.\n");
        source.Write("Cours  Bio/Leçon.MD", "Trois.\n");
        var deep = string.Join('/', Enumerable.Repeat("d", 21));
        source.Write($"{deep}/Profonde.md", "Trop loin.\n");
        source.Write("sans-nom", "x");
        source.Write("grande.png", "x");
        using (var big = File.OpenWrite(source.PathOf("grande.png")))
        {
            big.SetLength(VaultSession.MaxResourceBytes + 1L);
        }
        source.Write("Pièces.md", "![[sans-nom]] ![[grande.png]] ![](grande.png)\n");

        var report = vault.ImportFolder(source.Root, "Notes : import", track: false, Now);

        Assert.AreEqual("Notes - import", report.Folder);
        Assert.AreEqual(4, report.Sheets);
        Assert.AreEqual(0, report.Resources);
        Assert.IsTrue(folder.FileExists("fiches/Notes - import/Note double.md"));
        Assert.AreEqual("Deux.\n", folder.ReadText("fiches/Notes - import/Note double (2).md"));
        Assert.IsTrue(folder.FileExists("fiches/Notes - import/Cours Bio/Leçon.MD.md"));
        CollectionAssert.Contains(report.Ignored.ToList(), $"{Path.Combine([.. Enumerable.Repeat("d", 21)])} : trop profond, ignoré");
        CollectionAssert.Contains(report.Ignored.ToList(), "Pièces.md : Ressource « sans-nom » refusée : extension absente.");
        Assert.AreEqual(2, report.Ignored.Count(reason => reason == "Pièces.md : « grande.png » : plus de 50 Mo, non copiée"));
        Assert.HasCount(4, vault.Health.Untracked);
    }

    [TestMethod]
    public void Notes_that_cannot_enter_review_are_reported()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var source = TemporaryVault.Empty();
        using var vault = folder.Initialize(Now);
        source.Write("Bonne.md", "Texte.\n");
        source.Write("Cassée.md", "---\ntitre: [\n---\nTexte.\n");
        source.Write("Suivie.md", "---\nid: f_01JQZ8S7ZK4V3H2N8M6X5W9PQR\n---\nTexte.\n");

        var report = vault.ImportFolder(source.Root, "Lot", track: true, Now);

        Assert.AreEqual(3, report.Sheets);
        Assert.AreEqual(1, report.Tracked);
        Assert.HasCount(2, report.Ignored);
        Assert.IsTrue(report.Ignored.All(reason => reason.Contains(" : copiée, mais pas mise en révision (", StringComparison.Ordinal)));
        Assert.AreEqual(FilePresence.Present, vault.Sheets.Single().Presence);
    }

    [TestMethod]
    public void Repairs_only_touch_what_health_reports()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var id = vault.CreateSheet(null, "Fiche", "Texte [cassé](absent.md).\n", Now);

        Assert.AreEqual(VaultErrorKind.UnknownSheet, Error(() => vault.KeepOriginal(id, VaultPath.Create("fiches/Fiche.md"), Now)).Kind);
        Assert.Contains("n'attend pas de suivi", Error(() => vault.RecreateTracking(VaultPath.Create("fiches/Fiche.md"), Now)).Message);
        Assert.AreEqual("absent.md", vault.BrokenLinks().Single().Target);

        // Une fiche devenue illisible depuis l'analyse n'empêche pas le balayage des liens.
        folder.WriteBytes("fiches/Fiche.md", [0xE9]);
        Assert.IsEmpty(vault.BrokenLinks());
        Assert.AreEqual(0, vault.HealthDetails().Problems);
        Assert.IsEmpty(vault.TrashedAnnotations());
    }

    private static VaultException Error(Action action) => Assert.ThrowsExactly<VaultException>(action);
}

/// <summary>Sur le disque tel quel, et la conversion des liens d'Obsidian, qui ne touche aucun fichier.</summary>
[TestClass]
public sealed class VaultImportOnDiskTests() : VaultImportTests(StorageKind.Disk)
{
    [TestMethod]
    [DataRow("> [!note] À retenir@> Le texte.@", "> Note : À retenir@> Le texte.@", 1, 0, 0)]
    [DataRow("> [!warning]- Piège@", "> Attention : Piège@", 1, 0, 0)]
    [DataRow(">[!tip]+@> > [!perso] Mien@", ">Astuce@> > Perso : Mien@", 2, 0, 0)]
    [DataRow("> [!] vide et [!note] en plein texte@", "> [!] vide et [!note] en plein texte@", 0, 0, 0)]
    [DataRow("Un ==mot clé== et ==deux==.@", "Un **mot clé** et **deux**.@", 0, 2, 0)]
    [DataRow("==déjà **gras**== mais a == b == c et x==y@", "<u>déjà **gras**</u> mais a == b == c et x==y@", 0, 1, 0)]
    [DataRow("Avant %%privé%% après.@%%@sur@plusieurs lignes@%%@Suite.@", "Avant  après.@Suite.@", 0, 0, 2)]
    [DataRow("`==code==` et `%%code%%`@```@==bloc== %%bloc%%@> [!note] bloc@```@", "`==code==` et `%%code%%`@```@==bloc== %%bloc%%@> [!note] bloc@```@", 0, 0, 0)]
    [DataRow("%%seul%%@Texte %%jamais fermé@perdu", "Texte ", 0, 0, 2)]
    [DataRow("# Titre@@Un paragraphe.@  @    @Un autre.@@", "# Titre@@Un paragraphe.@  @    @Un autre.@@", 0, 0, 0)]
    public void Obsidian_callouts_highlights_and_comments_are_converted_outside_code(string text, string expected, int callouts, int highlights, int comments)
    {
        var result = ObsidianSyntax.Convert(text.Replace('@', '\n'));

        Assert.AreEqual((expected.Replace('@', '\n'), callouts, highlights, comments), (result.Text, result.Callouts, result.Highlights, result.Comments));
    }

    [TestMethod]
    [DataRow("Voir [[Mitose]] et [[Biologie/Mitose|la mitose]].\n", "Voir [Mitose](<Biologie/Mitose.md>) et [la mitose](<Biologie/Mitose.md>).\n", 2)]
    [DataRow("[[Mitose#Prophase]]", "[Mitose › Prophase](<Biologie/Mitose.md>)", 1)]
    [DataRow("[[Langage C]]", "[Langage C](<Langages/C%23.md>)", 1)]
    [DataRow("[[Mitose|[x] ]]", "[[Mitose|[x] ]]", 0)]
    [DataRow("[[Mitose|crochets ] fermés]]", "[crochets \\] fermés](<Biologie/Mitose.md>)", 1)]
    [DataRow("Voir [[Inconnue]].", "Voir Inconnue.", 0)]
    [DataRow("Voir [[Inconnue|ceci]].", "Voir ceci.", 0)]
    [DataRow("Voir [[Inconnue#Partie]] et [[#Plus bas]].", "Voir Inconnue › Partie et Plus bas.", 0)]
    [DataRow("![[cellule.png|300]]", "![](<Note.ressources/cellule.png>)", 1)]
    [DataRow("![[cellule.png|640x480]]", "![](<Note.ressources/cellule.png>)", 1)]
    [DataRow("![[cellule.png|Une cellule]]", "![Une cellule](<Note.ressources/cellule.png>)", 1)]
    [DataRow("![[absente.png]] [[non fermé", "![[absente.png]] [[non fermé", 0)]
    [DataRow("`[[Mitose]]` et [[Mitose]]\n```\n[[Mitose]]\n```\n~~~~\n[[Mitose]]\n~~~~\n", "`[[Mitose]]` et [Mitose](<Biologie/Mitose.md>)\n```\n[[Mitose]]\n```\n~~~~\n[[Mitose]]\n~~~~\n", 1)]
    [DataRow("``a`` `[[Mitose]] sans fin\n    ```\n[[Mitose]]\n", "``a`` `[Mitose](<Biologie/Mitose.md>) sans fin\n    ```\n[Mitose](<Biologie/Mitose.md>)\n", 2)]
    [DataRow("Accents : éèà, crochets [simples] et [lien](x.md).\n", "Accents : éèà, crochets [simples] et [lien](x.md).\n", 0)]
    public void Obsidian_links_become_markdown_links_outside_code(string text, string expected, int converted)
    {
        static string? Note(string target) => target.Trim() switch
        {
            "Mitose" or "Biologie/Mitose" => "Biologie/Mitose.md",
            "Langage C" => "Langages/C#.md",
            _ => null,
        };
        static string? Attachment(string target) => target == "cellule.png" ? "Note.ressources/cellule.png" : null;

        Assert.AreEqual((expected, converted), VaultSession.ConvertObsidianLinks(text, Note, Attachment));
    }
}

/// <summary>Sur un coffre chiffré : les notes viennent du disque, les fiches vont dans le coffre chiffré.</summary>
[TestClass]
public sealed class VaultImportEncryptedTests() : VaultImportTests(StorageKind.Encrypted);
