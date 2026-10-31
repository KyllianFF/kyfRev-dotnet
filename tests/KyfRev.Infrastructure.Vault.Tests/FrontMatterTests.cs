using KyfRev.Infrastructure.Vault.Sheets;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>En-tête YAML des fiches, comme dans la version 0.x (<c>entete.rs</c>).</summary>
[TestClass]
public sealed class FrontMatterTests
{
    [TestMethod]
    public void A_header_written_by_kyfrev_is_read_back()
    {
        const string text = "---\nid: f_01JA8Z3K5Q6WXYZABCDEFGHJKM\ntitre: \"Révolution : les causes\"\ncreee: 2026-10-09\n---\n\n# Corps\n";
        var header = Read(text);
        Assert.AreEqual("f_01JA8Z3K5Q6WXYZABCDEFGHJKM", header.Id);
        Assert.AreEqual("Révolution : les causes", header.Title);
        Assert.AreEqual("2026-10-09", header.Created);
        Assert.IsEmpty(header.Tags);
        Assert.AreEqual("etiquettes", header.TagsKey);

        var split = FrontMatter.Split(text);
        Assert.AreEqual("\n# Corps\n", text[split.BodyStart..], "le corps commence juste après la ligne de fermeture");
        Assert.AreEqual(4, split.YamlStart);
    }

    [TestMethod]
    public void Unknown_fields_are_ignored_and_a_missing_header_is_empty()
    {
        var header = Read("---\nid: f_01JA8Z3K5Q6WXYZABCDEFGHJKM\nauteur: Moi\naliases: [a, b]\nnote:\n  sous: champ\n---\nTexte");
        Assert.AreEqual("f_01JA8Z3K5Q6WXYZABCDEFGHJKM", header.Id);

        Assert.AreEqual(SheetHeader.Empty, Read("# Pas d'en-tête\n"));
        Assert.AreEqual(SheetHeader.Empty, Read("---\nid: f_X\npas de fermeture\n"), "un en-tête non fermé n'est pas un en-tête");
        Assert.AreEqual(SheetHeader.Empty, Read("--- \nid: f_X\n---\n"), "la première ligne doit être exactement ---");
        Assert.AreEqual(SheetHeader.Empty, Read("---\n\n---\n"));
        Assert.AreEqual(SheetHeader.Empty, Read("---\nnull\n---\n"));
    }

    [TestMethod]
    public void Windows_line_endings_byte_order_mark_and_dot_closing_are_accepted()
    {
        const string text = "\uFEFF---\r\nid: f_X\r\ntitre: Titre\r\n...\r\nCorps";
        var header = Read(text);
        Assert.AreEqual("f_X", header.Id);
        Assert.AreEqual("Titre", header.Title);
        Assert.AreEqual("Corps", text[FrontMatter.Split(text).BodyStart..]);
    }

    [TestMethod]
    public void Tags_from_both_keys_are_normalized()
    {
        var header = Read("---\netiquettes: [\"  #Bio  \", \"bio\", \"examen   final\", \"\"]\ntags: Histoire, bio\n---\n");
        string[] expected = ["Bio", "examen final", "Histoire"];
        Assert.AreSequenceEqual(expected, header.Tags);
        Assert.AreEqual("etiquettes", header.TagsKey);

        Assert.AreEqual("tags", Read("---\ntags: [a]\n---\n").TagsKey, "un fichier qui n'a que « tags » le garde");
        string[] numbers = ["2024", "bio"];
        Assert.AreSequenceEqual(numbers, Read("---\ntags: [2024, bio]\n---\n").Tags, "un nombre dans la liste est lu comme un texte");
    }

    [TestMethod]
    public void Tags_are_limited_in_length_and_number()
    {
        var tags = FrontMatter.NormalizeTags([new string('é', 60), .. Enumerable.Range(0, 40).Select(i => $"t{i}")]);
        Assert.HasCount(FrontMatter.MaxTags, tags);
        Assert.AreEqual(new string('é', FrontMatter.MaxTagLength), tags[0]);
        Assert.AreEqual("😀😀", FrontMatter.NormalizeTags(["😀😀"])[0], "une limite en caractères, pas en unités UTF-16");
    }

    [TestMethod]
    [DataRow("---\nid: f_X\nid: f_Y\n---\n")]
    [DataRow("---\nid: [f_X]\n---\n")]
    [DataRow("---\netiquettes: { a: 1 }\n---\n")]
    [DataRow("---\netiquettes: [[a]]\n---\n")]
    [DataRow("---\n- une liste\n---\n")]
    [DataRow("---\nid: \"non fermé\n---\n")]
    public void Unreadable_headers_are_reported(string text)
    {
        var reading = FrontMatter.Read(text);
        Assert.IsFalse(reading.IsSuccess);
        Assert.IsFalse(string.IsNullOrWhiteSpace(reading.Error));
    }

    [TestMethod]
    public void Null_values_mean_absent()
    {
        var header = Read("---\nid: ~\ntitre: null\ncreee:\netiquettes: NULL\n---\n");
        Assert.AreEqual(SheetHeader.Empty with { }, header);
        Assert.AreEqual("null", Read("---\ntitre: \"null\"\n---\n").Title, "un « null » entre guillemets est un texte");
    }

    private static SheetHeader Read(string text)
    {
        var reading = FrontMatter.Read(text);
        return reading.IsSuccess ? reading.Header : throw new AssertFailedException(reading.Error);
    }
}
