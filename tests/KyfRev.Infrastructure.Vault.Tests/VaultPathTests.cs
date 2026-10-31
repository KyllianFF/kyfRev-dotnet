using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>Règles des chemins du coffre, reprises de <c>CheminRelatif</c> (version 0.x).</summary>
[TestClass]
public sealed class VaultPathTests
{
    [TestMethod]
    public void Valid_paths_use_forward_slashes()
    {
        Assert.IsTrue(VaultPath.TryParse(@"fiches\Histoire\Révolution.md", out var path, out _));
        Assert.AreEqual("fiches/Histoire/Révolution.md", path.Value);
        Assert.AreEqual("Révolution.md", path.Name);
        Assert.AreEqual("fiches/Histoire", path.Parent!.Value);
        Assert.IsNull(VaultPath.Parse("fiches").Parent);
        Assert.AreEqual(".suivi", VaultPath.Parse(".suivi/coffre.json").Parent!.Value);
    }

    [TestMethod]
    public void A_folder_contains_its_children_but_not_its_namesakes()
    {
        var sheet = VaultPath.Parse("fiches/Bio/Mitose.md");
        Assert.IsTrue(sheet.IsWithin(VaultPath.Parse("fiches")));
        Assert.IsTrue(sheet.IsWithin(VaultPath.Parse("fiches/Bio")));
        Assert.IsTrue(sheet.IsWithin(sheet));
        Assert.IsFalse(sheet.IsWithin(VaultPath.Parse("fich")));
        Assert.IsFalse(VaultPath.Parse("fichesX/a.md").IsWithin(VaultPath.Parse("fiches")));
    }

    [TestMethod]
    [DataRow("", "composant vide (« // » ou « / » en fin de chemin)")]
    [DataRow("/etc/passwd", "chemin absolu")]
    [DataRow(@"\\serveur\partage", "chemin absolu")]
    [DataRow("C:/Windows", "caractère interdit par Windows")]
    [DataRow("C:fiche.md", "caractère interdit par Windows")]
    [DataRow("fiches/../secret.md", "« . » et « .. » sont interdits")]
    [DataRow("fiches/./a.md", "« . » et « .. » sont interdits")]
    [DataRow("fiches//a.md", "composant vide (« // » ou « / » en fin de chemin)")]
    [DataRow("fiches/", "composant vide (« // » ou « / » en fin de chemin)")]
    [DataRow("fiches/CON", "nom réservé par Windows")]
    [DataRow("fiches/nul.md", "nom réservé par Windows")]
    [DataRow("fiches/com1.txt", "nom réservé par Windows")]
    [DataRow("fiches/LPT9 .md", "nom réservé par Windows")]
    [DataRow("fiches/fin.", "un nom ne peut pas finir par un point ou une espace")]
    [DataRow("fiches/fin ", "un nom ne peut pas finir par un point ou une espace")]
    [DataRow("fiches/quoi?.md", "caractère interdit par Windows")]
    [DataRow("fiches/a\u0000b.md", "caractère interdit par Windows")]
    [DataRow("fiches/a\u0007b.md", "caractère interdit par Windows")]
    public void Dangerous_paths_are_refused(string text, string reason)
    {
        Assert.IsFalse(VaultPath.TryParse(text, out var path, out var refusal));
        Assert.IsNull(path);
        Assert.AreEqual(reason, refusal);
        Assert.ThrowsExactly<ArgumentException>(() => VaultPath.Parse(text));
    }

    [TestMethod]
    public void Names_are_limited_to_255_utf8_bytes()
    {
        Assert.IsTrue(VaultPath.TryParse("fiches/" + new string('a', 255), out _, out _));
        Assert.IsFalse(VaultPath.TryParse("fiches/" + new string('a', 256), out _, out var reason));
        Assert.AreEqual("nom trop long", reason);
        // 128 « é » font 256 octets UTF-8 : refusé, bien que 128 caractères seulement.
        Assert.IsFalse(VaultPath.TryParse("fiches/" + new string('é', 128), out _, out _));
        Assert.IsTrue(VaultPath.TryParse("fiches/COM10.md", out _, out _), "COM10 n'est pas réservé");
        Assert.IsTrue(VaultPath.TryParse(".suivi/.cache", out _, out _), "un nom peut commencer par un point");
    }

    [TestMethod]
    public void Absolute_paths_inside_the_vault_become_relative()
    {
        var root = Path.Combine(Path.GetTempPath(), "coffre");
        Assert.IsTrue(VaultPath.TryFromAbsolute(root, Path.Combine(root, "fiches", "a.md"), out var path, out _));
        Assert.AreEqual("fiches/a.md", path.Value);
        Assert.AreEqual(Path.Combine(root, "fiches", "a.md"), path.ToAbsolute(root));
        Assert.IsFalse(VaultPath.TryFromAbsolute(root, Path.Combine(Path.GetTempPath(), "ailleurs", "a.md"), out _, out var reason));
        Assert.AreEqual("hors du coffre", reason);
    }

    [TestMethod]
    public void Combining_validates_the_new_name()
    {
        var folder = VaultPath.Parse("fiches/Bio");
        Assert.IsTrue(folder.TryCombine("Mitose.md", out var sheet, out _));
        Assert.AreEqual("fiches/Bio/Mitose.md", sheet.Value);
        Assert.IsFalse(folder.TryCombine("../x.md", out _, out _));
    }

    private static VaultPath? Nothing() => null;

    [TestMethod]
    public void Paths_are_ordered_by_utf8_bytes_and_compared_by_value()
    {
        // « É » (C3 89 en UTF-8) vient après « Z » (5A), comme dans la version 0.x.
        VaultPath[] paths = [VaultPath.Parse("fiches/Été.md"), VaultPath.Parse("fiches/Zèbre.md"), VaultPath.Parse("fiches/Arbre.md")];
        Array.Sort(paths);
        string[] expected = ["fiches/Arbre.md", "fiches/Zèbre.md", "fiches/Été.md"];
        Assert.AreSequenceEqual(expected, paths.Select(p => p.Value));

        var a = VaultPath.Parse("fiches/a.md");
        var same = VaultPath.Parse(@"fiches\a.md");
        var b = VaultPath.Parse("fiches/b.md");
        var none = Nothing();
        Assert.IsTrue(a == same);
        Assert.IsTrue(a.Equals((object)same));
        Assert.AreEqual(a.GetHashCode(), same.GetHashCode());
        Assert.IsTrue(a != b);
        Assert.IsTrue(a < b);
        Assert.IsTrue(a <= same);
        Assert.IsTrue(b > a);
        Assert.IsTrue(a >= same);
        Assert.IsFalse(a.Equals(none));
        Assert.IsFalse(a == none);
        Assert.IsTrue(none < a);
        Assert.IsTrue(none <= a);
        Assert.IsTrue(a > none);
        Assert.IsTrue(a >= none);
        Assert.IsFalse(none > a);
        Assert.AreEqual(1, a.CompareTo(none));
        Assert.AreEqual("fiches/a.md", a.ToString());
    }
}
