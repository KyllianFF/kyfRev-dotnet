using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Links;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;

namespace KyfRev.Infrastructure.Vault.Tests.Reference;

/// <summary>
/// Fonctions pures du coffre comparées au crate <c>coffre</c> de la version 0.x sur 9 000 cas
/// tirés au hasard (voir <c>tools/reference-vectors</c>). Pour un écart sur le cas n d'une
/// famille, <c>--fonctions &lt;famille&gt; n</c> affiche le détail côté Rust.
/// </summary>
[TestClass]
public sealed class PureFunctionTests
{
    private const ulong NameBase = 5_000_000;
    private const ulong HeaderBase = 6_000_000;
    private const ulong LinkBase = 7_000_000;
    private const string SheetId = "f_01M69JFV2X5VM53EY3DR1BY61M";

    [TestMethod]
    public void File_names_match_version_0x() => Compare("noms", Names);

    /// <summary>
    /// Les éditions d'en-tête sont identiques. À la lecture, un YAML mal formé que la 0.x tolère
    /// (indentation par tabulation, contenu après la racine) peut être refusé ici : la fiche est
    /// alors signalée illisible et jamais réécrite. L'inverse (lire ce que la 0.x refuse) est un
    /// échec.
    /// </summary>
    [TestMethod]
    public void Header_edits_match_version_0x()
    {
        var stricter = Compare("entetes", Headers);
        Assert.IsLessThan(30, stricter, "en-têtes refusés ici et lus par la 0.x");
    }

    [TestMethod]
    public void Links_match_version_0x() => Compare("liens", Links);

    private static (string Part, string Text)[] Names(int n)
    {
        var random = new SplitMix(NameBase + (ulong)n);
        var title = random.Assemble(CoffreTokens.NameTokens, 13);
        var name = FileNames.ForTitle(title);
        string path;
        try
        {
            var sheet = VaultPath.Create($"fiches/{name}.md");
            try
            {
                path = $"ok {sheet} {FileNames.ResourcesFolder(sheet)}";
            }
            catch (VaultException exception)
            {
                path = $"erreur ressources : {exception.Message}";
            }
        }
        catch (VaultException exception)
        {
            path = $"erreur : {exception.Message}";
        }
        var original = random.Assemble(CoffreTokens.NameTokens, 9);
        string resource;
        try
        {
            resource = $"ok {Json(FileNames.ForResource(original))}";
        }
        catch (VaultException exception)
        {
            resource = $"erreur : {exception.Message}";
        }
        var tags = FrontMatter.NormalizeTags(random.List());
        return [("nom", Json(name)), ("chemin", path), ("ressource", resource), ("etiquettes", Json(tags))];
    }

    private static (string Part, string Text)[] Headers(int n)
    {
        var random = new SplitMix(HeaderBase + (ulong)n);
        var bom = random.Below(5) == 0 ? "\uFEFF" : string.Empty;
        var newline = random.Below(3) == 0 ? "\r\n" : "\n";
        var count = random.Below(9);
        var lines = new List<string>();
        if (random.Below(4) != 0)
        {
            lines.Add("---");
        }
        for (var i = 0; i < count; i++)
        {
            lines.Add(random.Pick(CoffreTokens.HeaderLines));
        }
        var text = bom + string.Join(newline, lines);
        if (random.Below(2) == 0)
        {
            text += newline;
        }
        text += random.Assemble(CoffreTokens.NameTokens, 4);

        var split = FrontMatter.Split(text);
        var reading = FrontMatter.Read(text);
        var read = reading.IsSuccess
            ? $"ok {Json(reading.Header.Id)} {Json(reading.Header.Title)} {Json(reading.Header.Created)} {Json(reading.Header.Tags)} {reading.Header.TagsKey}"
            : "erreur";
        var tagsKey = reading.IsSuccess ? reading.Header.TagsKey : "etiquettes";
        var title = random.Assemble(CoffreTokens.NameTokens, 6);
        var tags = FrontMatter.NormalizeTags(random.List());
        var key = random.Pick(CoffreTokens.Keys);
        return
        [
            ("decoupage", $"{Json(split.Yaml)} {Utf8Offset(text, split.YamlStart)} {Utf8Offset(text, split.BodyStart)}"),
            ("lire", read),
            ("ajouter_id", Json(FrontMatter.AddId(text, SheetId))),
            ("titre", Json(FrontMatter.ReplaceField(text, "titre", FrontMatter.TextValue(title)))),
            ("etiquettes", Json(FrontMatter.ReplaceField(text, tagsKey, FrontMatter.ListValue(tags)))),
            ("champ", Json(FrontMatter.ReplaceField(text, key, "x"))),
            ("composer", Json(FrontMatter.Compose(SheetId, title, new DateOnly(2026, 10, 9)))),
        ];
    }

    /// <summary>Où se retrouvent les cibles après un renommage fictif (même règle côté Rust).</summary>
    private static string? Move(string target)
    {
        const string resources = "fiches/Bio/Mitose.ressources";
        return target switch
        {
            "fiches/Bio/Mitose.md" => "fiches/Histoire/Mitose (2).md",
            "fiches/a.md" => "fiches/Z/a b.md",
            resources => "fiches/Histoire/Mitose (2).ressources",
            _ when target.StartsWith(resources + "/", StringComparison.Ordinal) => "fiches/Histoire/Mitose (2).ressources" + target[resources.Length..],
            _ => null,
        };
    }

    private static string RandomPath(SplitMix random)
    {
        var count = random.Below(5);
        return string.Join('/', Enumerable.Range(0, count).Select(_ => random.Pick(CoffreTokens.PathParts)));
    }

    private static (string Part, string Text)[] Links(int n)
    {
        var random = new SplitMix(LinkBase + (ulong)n);
        var text = random.Assemble(CoffreTokens.LinkTokens, 25);
        var folder = random.Pick(CoffreTokens.Folders);
        var newFolder = random.Pick(CoffreTokens.Folders);
        var found = string.Join(';', MarkdownLinks.Find(text).Select(link =>
            $"{Utf8Offset(text, link.Start)}-{Utf8Offset(text, link.End)}-{(link.AngleBrackets ? "true" : "false")}"));
        var rewritten = MarkdownLinks.Rewrite(text, folder, newFolder, Move);
        var raw = random.Assemble(CoffreTokens.LinkTokens, 6);
        var angleBrackets = random.Below(2) == 0;
        var toEncode = random.Assemble(CoffreTokens.NameTokens, 6);
        var relative = RandomPath(random);
        var target = RandomPath(random);
        return
        [
            ("reperer", found),
            ("reecrire", Json(rewritten)),
            ("decoder", Json(MarkdownLinks.Decode(raw))),
            ("encoder", Json(MarkdownLinks.Encode(toEncode, angleBrackets))),
            ("resoudre", Json(MarkdownLinks.Resolve(folder, relative))),
            ("relatif_depuis", Json(MarkdownLinks.RelativeFrom(folder, target))),
        ];
    }

    /// <summary>Compare chaque fonction de chaque cas ; rend le nombre d'en-têtes refusés ici et lus par la 0.x.</summary>
    private static int Compare(string family, Func<int, (string Part, string Text)[]> transcribe)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "reference", $"{family}.txt"))
            .Where(line => !line.StartsWith('#'))
            .ToList();
        Assert.HasCount(3_000, lines);
        var failures = new List<string>();
        var stricter = 0;
        for (var n = 0; n < lines.Count; n++)
        {
            var expected = lines[n].Split('|');
            var parts = transcribe(n);
            Assert.HasCount(expected.Length, parts);
            var refusedHere = false;
            for (var p = 0; p < parts.Length; p++)
            {
                var (part, text) = parts[p];
                if (Short(text) == expected[p])
                {
                    continue;
                }
                if (part == "lire" && text == "erreur")
                {
                    refusedHere = true;
                    stricter++;
                }
                else if (!(part == "etiquettes" && refusedHere))
                {
                    // La clé des étiquettes dépend de la lecture : sans objet si l'en-tête est refusé ici.
                    failures.Add($"cas {n}, {part} : {text}");
                }
            }
        }
        Assert.IsEmpty(failures, $"{failures.Count} écart(s) avec la 0.x (détail Rust : --fonctions {family} <n>) :\n{string.Join('\n', failures.Take(25))}");
        return stricter;
    }

    private static string Short(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];

    private static int Utf8Offset(string text, int index) => Encoding.UTF8.GetByteCount(text.AsSpan(0, index));

    private static string Json(string? text) => text is null ? "null" : SerdeJson.Compact(JsonValue.Create(text));

    private static string Json(IEnumerable<string> texts) => SerdeJson.Compact(Fields.StringArray(texts));
}
