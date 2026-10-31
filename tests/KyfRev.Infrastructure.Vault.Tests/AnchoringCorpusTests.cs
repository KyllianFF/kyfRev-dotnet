using System.Text.Json;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// Corpus d'ancrage « Markdown → texte affiché → positions » (<c>tests/fixtures/ancrage</c>),
/// produit par kyfRev 0.x (<c>tools/anchoring-corpus</c>). La projection C# du jalon J7 devra le
/// reproduire à l'identique ; d'ici là, ces tests garantissent qu'il reste intact et cohérent.
/// </summary>
[TestClass]
public sealed class AnchoringCorpusTests
{
    /// <summary>Contexte enregistré de chaque côté d'un passage (<c>CONTEXTE</c> de la 0.x).</summary>
    private const int Context = 32;

    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "fixtures", "ancrage");

    [TestMethod]
    public void The_corpus_comes_from_version_0x_and_covers_its_demo_sheets()
    {
        using var projection = Read("projection.json");
        using var reanchoring = Read("reancrage.json");
        foreach (var document in new[] { projection, reanchoring })
        {
            var source = document.RootElement.GetProperty("source");
            Assert.AreEqual("KyllianFF/kyfRev", source.GetProperty("depot").GetString());
            Assert.MatchesRegex("^[0-9a-f]{40}$", source.GetProperty("commit").GetString());
            Assert.StartsWith("Microsoft Edge ", source.GetProperty("moteur").GetString());
        }
        var names = projection.RootElement.GetProperty("cas").EnumerateArray().Select(c => c.GetProperty("nom").GetString()).ToList();
        Assert.IsGreaterThanOrEqualTo(200, names.Count);
        CollectionAssert.IsSubsetOf(DemoSheets, names);
        Assert.IsGreaterThanOrEqualTo(100, reanchoring.RootElement.GetProperty("cas").GetArrayLength());
    }

    [TestMethod]
    public void Each_rendered_text_is_made_of_its_pieces_and_block_separators()
    {
        using var corpus = Read("projection.json");
        foreach (var document in corpus.RootElement.GetProperty("cas").EnumerateArray())
        {
            var name = document.GetProperty("nom").GetString();
            var text = document.GetProperty("texte").GetString()!;
            var covered = new bool[text.Length];
            var end = 0;
            foreach (var piece in document.GetProperty("morceaux").EnumerateArray())
            {
                var (start, stop) = (piece.GetProperty("debut").GetInt32(), piece.GetProperty("fin").GetInt32());
                Assert.IsTrue(start >= end && stop > start && stop <= text.Length, $"{name} : morceau {start}-{stop}");
                if (piece.GetProperty("genre").GetString() == "formule")
                {
                    Assert.IsTrue(text[start] == '$' && text[stop - 1] == '$', $"{name} : formule {start}-{stop}");
                }
                Array.Fill(covered, true, start, stop - start);
                end = stop;
            }
            // Hors des morceaux : seulement les sauts de ligne entre les blocs et ceux des <br>.
            Assert.IsTrue(text.Where((c, i) => !covered[i]).All(c => c == '\n'), name);
        }
    }

    [TestMethod]
    public void Each_passage_is_the_trimmed_selection_with_its_context()
    {
        using var corpus = Read("projection.json");
        var passages = 0;
        foreach (var document in corpus.RootElement.GetProperty("cas").EnumerateArray())
        {
            var name = document.GetProperty("nom").GetString();
            var text = document.GetProperty("texte").GetString()!;
            var firstBodyLine = document.GetProperty("premiereLigneCorps").GetInt32();
            foreach (var passage in document.GetProperty("passages").EnumerateArray())
            {
                var selection = passage.GetProperty("selection");
                var trimmed = Trim(text, selection[0].GetInt32(), selection[1].GetInt32());
                var anchor = passage.GetProperty("ancre");
                if (trimmed is not var (start, end))
                {
                    Assert.AreEqual(JsonValueKind.Null, anchor.ValueKind, name);
                    continue;
                }
                passages++;
                Assert.AreEqual(start, anchor.GetProperty("debut").GetInt32(), name);
                Assert.AreEqual(end, anchor.GetProperty("fin").GetInt32(), name);
                CheckAnchor(text, anchor, name);
                Assert.AreEqual("sha256:corpus", anchor.GetProperty("empreinteFiche").GetString());
                Assert.IsTrue(anchor.GetProperty("section").EnumerateArray().All(title => title.ValueKind == JsonValueKind.String), name);
                var line = anchor.GetProperty("ligneSource");
                Assert.IsTrue(line.ValueKind == JsonValueKind.Null || line.GetInt32() >= firstBodyLine, name);
            }
        }
        Assert.IsGreaterThanOrEqualTo(2000, passages);
    }

    [TestMethod]
    public void Reanchoring_never_attaches_a_passage_to_another_text()
    {
        using var corpus = Read("reancrage.json");
        var states = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var modification in corpus.RootElement.GetProperty("cas").EnumerateArray())
        {
            var name = modification.GetProperty("nom").GetString();
            var before = modification.GetProperty("texteAvant").GetString()!;
            var after = modification.GetProperty("texteApres").GetString()!;
            foreach (var passage in modification.GetProperty("passages").EnumerateArray())
            {
                var anchor = passage.GetProperty("ancre");
                CheckAnchor(before, anchor, name);
                var exact = anchor.GetProperty("exact").GetString()!;
                var fromAnchored = passage.GetProperty("depuisAncree");
                var fromNotFound = passage.GetProperty("depuisIntrouvable");
                var state = fromAnchored.GetProperty("etat").GetString()!;
                states[state] = states.GetValueOrDefault(state) + 1;
                switch (state)
                {
                    case "ancree":
                        Assert.AreEqual(exact, Slice(after, fromAnchored), name);
                        Assert.AreEqual(fromAnchored.GetRawText(), fromNotFound.GetRawText(), name);
                        break;
                    case "a_verifier":
                        var candidate = fromAnchored.GetProperty("candidat");
                        Assert.IsGreaterThan(0, Slice(after, candidate).Length, name);
                        Assert.AreEqual("introuvable", fromNotFound.GetProperty("etat").GetString(), name);
                        break;
                    default:
                        Assert.AreEqual("introuvable", state, name);
                        Assert.AreEqual("introuvable", fromNotFound.GetProperty("etat").GetString(), name);
                        break;
                }
            }
        }
        // Les trois issues sont représentées.
        Assert.HasCount(3, states);
    }

    private static readonly string[] DemoSheets = ["demo-revolution", "demo-thales", "demo-mitose", "demo-newton"];

    private static JsonDocument Read(string name) => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Folder, name)));

    /// <summary>Texte, contexte avant et après d'une ancre, tels que <c>ancreDe</c> les découpe.</summary>
    private static void CheckAnchor(string text, JsonElement anchor, string? name)
    {
        var (start, end) = (anchor.GetProperty("debut").GetInt32(), anchor.GetProperty("fin").GetInt32());
        var before = Math.Max(0, start - Context);
        if (before > 0 && char.IsLowSurrogate(text[before]))
        {
            before++;
        }
        var after = Math.Min(text.Length, end + Context);
        if (after < text.Length && char.IsHighSurrogate(text[after - 1]))
        {
            after--;
        }
        Assert.AreEqual(text[start..end], anchor.GetProperty("exact").GetString(), name);
        Assert.AreEqual(text[before..start], anchor.GetProperty("avant").GetString(), name);
        Assert.AreEqual(text[end..after], anchor.GetProperty("apres").GetString(), name);
    }

    private static string Slice(string text, JsonElement position) =>
        text[position.GetProperty("debut").GetInt32()..position.GetProperty("fin").GetInt32()];

    /// <summary>Sélection sans les espaces de ses bords (<c>rogner</c>) ; rien si elle est vide.</summary>
    private static (int Start, int End)? Trim(string text, int start, int end)
    {
        var (d, f) = (Math.Max(0, start), Math.Min(text.Length, end));
        while (d < f && IsJavaScriptSpace(text[d]))
        {
            d++;
        }
        while (f > d && IsJavaScriptSpace(text[f - 1]))
        {
            f--;
        }
        return f > d ? (d, f) : null;
    }

    /// <summary><c>\s</c> de JavaScript : les espaces Unicode et l'indicateur d'ordre des octets, sans U+0085.</summary>
    private static bool IsJavaScriptSpace(char c) => (char.IsWhiteSpace(c) && c != (char)0x85) || c == (char)0xFEFF;
}
