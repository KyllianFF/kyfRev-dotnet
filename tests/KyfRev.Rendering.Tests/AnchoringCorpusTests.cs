using System.Text.Json;
using KyfRev.Application.Model;

namespace KyfRev.Rendering.Tests;

/// <summary>
/// Le corpus d'ancrage (T12), produit par kyfRev 0.x dans un navigateur : le moteur natif doit en reproduire
/// le texte affiché, les passages et leur ré-ancrage à l'identique. Aucun écart n'est toléré.
/// </summary>
[TestClass]
public sealed class AnchoringCorpusTests
{
    private const string Fingerprint = "sha256:corpus";

    /// <summary>
    /// La 0.x lisait les titres dans la page : le rendu de KaTeX, ou le message d'une image que ce navigateur
    /// ne pouvait pas charger, entraient dans ce repère lisible. Le moteur natif écrit la source de la formule,
    /// et rien pour une image. Ni la position ni le ré-ancrage n'en dépendent.
    /// </summary>
    private static readonly Dictionary<string, string> SectionsWrittenDifferently = new(StringComparer.Ordinal)
    {
        ["Formule x9+y2x_9 + y^2x9\u200B+y2 dans la ligne."] = "Formule x_9 + y^2 dans la ligne.",
        ["Image du coffre (visible dans kyfRev) : intégrée86.png\nTitre setext 30"] = "Titre setext 30",
    };

    private static readonly Lazy<JsonDocument> Projection = new(() => Load("projection.json"));

    private static readonly Lazy<JsonDocument> Analysis = new(() => Load("analyse.json"));

    private static readonly Lazy<JsonDocument> Reanchoring = new(() => Load("reancrage.json"));

    [TestMethod]
    public void The_corpus_is_the_one_expected()
    {
        Assert.AreEqual(230, Cases(Projection.Value).Count());
        Assert.AreEqual(116, Cases(Reanchoring.Value).Count());
        Assert.AreEqual(1149, Cases(Analysis.Value).Count());
    }

    [TestMethod]
    [DataRow("projection")]
    [DataRow("analyse")]
    public void Every_document_gives_the_same_displayed_text(string corpus)
    {
        var differences = new List<string>();
        foreach (var document in Cases(corpus == "analyse" ? Analysis.Value : Projection.Value))
        {
            var text = MapOf(document.GetProperty("corps").GetString()!, document.GetProperty("titre").GetString()!).Text;
            if (text != document.GetProperty("texte").GetString())
            {
                differences.Add(FirstDifference(document.GetProperty("nom").GetString()!, document.GetProperty("texte").GetString()!, text));
            }
        }
        AssertNone(differences);
    }

    [TestMethod]
    [DataRow("projection")]
    [DataRow("analyse")]
    public void Every_document_gives_the_same_pieces_outside_highlighted_code(string corpus)
    {
        var differences = new List<string>();
        foreach (var document in Cases(corpus == "analyse" ? Analysis.Value : Projection.Value))
        {
            var map = MapOf(document.GetProperty("corps").GetString()!, document.GetProperty("titre").GetString()!);
            var highlighted = map.Pieces.Where(piece => IsInCodeBlock(piece.Node)).Select(piece => (piece.Start, piece.End)).ToList();
            var expected = document.GetProperty("morceaux").EnumerateArray()
                .Select(piece => (Formula: piece.GetProperty("genre").GetString() == "formule", Start: piece.GetProperty("debut").GetInt32(), End: piece.GetProperty("fin").GetInt32()))
                .Where(piece => !highlighted.Exists(code => piece.Start >= code.Start && piece.End <= code.End))
                .Select(piece => $"{piece.Formula}:{piece.Start}-{piece.End}");
            var actual = map.Pieces.Where(piece => !IsInCodeBlock(piece.Node)).Select(piece => $"{piece.IsFormula}:{piece.Start}-{piece.End}");
            if (!expected.SequenceEqual(actual, StringComparer.OrdinalIgnoreCase))
            {
                differences.Add($"{document.GetProperty("nom").GetString()}: attendu {string.Join(' ', expected.Take(40))} | obtenu {string.Join(' ', actual.Take(40))}");
            }
        }
        AssertNone(differences);
    }

    [TestMethod]
    [DataRow("projection")]
    [DataRow("analyse")]
    public void Every_selection_gives_the_same_anchor(string corpus)
    {
        var differences = new List<string>();
        foreach (var document in Cases(corpus == "analyse" ? Analysis.Value : Projection.Value))
        {
            var map = MapOf(document.GetProperty("corps").GetString()!, document.GetProperty("titre").GetString()!);
            var firstLine = document.GetProperty("premiereLigneCorps").GetInt32();
            foreach (var passage in document.GetProperty("passages").EnumerateArray())
            {
                var selection = passage.GetProperty("selection");
                var position = Anchoring.Trim(map.Text, selection[0].GetInt32(), selection[1].GetInt32());
                var expected = passage.GetProperty("ancre");
                if (position is null)
                {
                    if (expected.ValueKind != JsonValueKind.Null)
                    {
                        differences.Add($"{document.GetProperty("nom").GetString()}: sélection {selection} vide");
                    }
                    continue;
                }
                var anchor = Anchoring.AnchorOf(map.Text, position.Value, map.SectionAt(position.Value.Start), map.SourceLineAt(position.Value.Start, firstLine), Fingerprint);
                if (expected.ValueKind == JsonValueKind.Null || !Anchoring.SameAnchor(anchor, ToAnchor(expected)))
                {
                    differences.Add($"{document.GetProperty("nom").GetString()}: attendu {expected} | obtenu {JsonSerializer.Serialize(anchor)}");
                }
            }
        }
        AssertNone(differences);
    }

    [TestMethod]
    public void Every_edit_gives_the_same_displayed_text()
    {
        var differences = new List<string>();
        foreach (var edit in Cases(Reanchoring.Value))
        {
            var title = edit.GetProperty("titre").GetString()!;
            var before = MapOf(edit.GetProperty("avant").GetString()!, title).Text;
            var after = MapOf(edit.GetProperty("apres").GetString()!, title).Text;
            if (before != edit.GetProperty("texteAvant").GetString())
            {
                differences.Add(FirstDifference(edit.GetProperty("nom").GetString()! + " (avant)", edit.GetProperty("texteAvant").GetString()!, before));
            }
            if (after != edit.GetProperty("texteApres").GetString())
            {
                differences.Add(FirstDifference(edit.GetProperty("nom").GetString()! + " (après)", edit.GetProperty("texteApres").GetString()!, after));
            }
        }
        AssertNone(differences);
    }

    [TestMethod]
    public void Every_passage_is_found_again_the_same_way()
    {
        var differences = new List<string>();
        foreach (var edit in Cases(Reanchoring.Value))
        {
            var text = edit.GetProperty("texteApres").GetString()!;
            foreach (var passage in edit.GetProperty("passages").EnumerateArray())
            {
                var anchor = ToAnchor(passage.GetProperty("ancre"));
                CompareResult(differences, edit, text, anchor, AnchorState.Anchored, passage.GetProperty("depuisAncree"));
                CompareResult(differences, edit, text, anchor, AnchorState.NotFound, passage.GetProperty("depuisIntrouvable"));
            }
        }
        AssertNone(differences);
    }

    private static void CompareResult(List<string> differences, JsonElement edit, string text, Anchor anchor, AnchorState current, JsonElement expected)
    {
        var result = Anchoring.Locate(text, anchor, current);
        var expectedState = expected.GetProperty("etat").GetString() switch
        {
            "ancree" => AnchorState.Anchored,
            "a_verifier" => AnchorState.NeedsReview,
            _ => AnchorState.NotFound,
        };
        TextPosition? expectedPosition = expectedState switch
        {
            AnchorState.Anchored => new TextPosition(expected.GetProperty("debut").GetInt32(), expected.GetProperty("fin").GetInt32()),
            AnchorState.NeedsReview => new TextPosition(expected.GetProperty("candidat").GetProperty("debut").GetInt32(), expected.GetProperty("candidat").GetProperty("fin").GetInt32()),
            _ => null,
        };
        if (result.State != expectedState || result.Position != expectedPosition)
        {
            differences.Add($"{edit.GetProperty("nom").GetString()} « {anchor.Exact} » depuis {current}: attendu {expected} | obtenu {result}");
        }
    }

    private static TextMap MapOf(string body, string title) => TextMap.Of(SheetRenderer.Render(body, title));

    /// <summary>La 0.x colore le code : ses morceaux y sont plus petits, mais le texte reste le même.</summary>
    private static bool IsInCodeBlock(RenderNode node)
    {
        for (var element = node as RenderElement ?? node.Parent; element is not null; element = element.Parent)
        {
            if (element.Tag == "pre")
            {
                return true;
            }
        }
        return false;
    }

    private static Anchor ToAnchor(JsonElement anchor) => new(
        anchor.GetProperty("exact").GetString()!,
        anchor.GetProperty("avant").GetString()!,
        anchor.GetProperty("apres").GetString()!,
        anchor.GetProperty("debut").GetInt64(),
        anchor.GetProperty("fin").GetInt64(),
        [.. anchor.GetProperty("section").EnumerateArray().Select(title => SectionsWrittenDifferently.GetValueOrDefault(title.GetString()!, title.GetString()!))],
        anchor.GetProperty("ligneSource").ValueKind == JsonValueKind.Null ? null : anchor.GetProperty("ligneSource").GetInt64(),
        anchor.GetProperty("empreinteFiche").GetString()!);

    private static JsonDocument Load(string file) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "ancrage", file)));

    private static JsonElement.ArrayEnumerator Cases(JsonDocument corpus) => corpus.RootElement.GetProperty("cas").EnumerateArray();

    private static string FirstDifference(string name, string expected, string actual)
    {
        var at = 0;
        while (at < expected.Length && at < actual.Length && expected[at] == actual[at])
        {
            at++;
        }
        return $"{name} @{at}: attendu {Excerpt(expected, at)} | obtenu {Excerpt(actual, at)}";
    }

    private static string Excerpt(string text, int at) =>
        JsonSerializer.Serialize(text[Math.Max(0, at - 24)..Math.Min(text.Length, at + 40)]);

    private static void AssertNone(List<string> differences)
    {
        Assert.IsEmpty(differences, $"{differences.Count} écarts :\n{string.Join('\n', differences.Take(25))}");
    }
}
