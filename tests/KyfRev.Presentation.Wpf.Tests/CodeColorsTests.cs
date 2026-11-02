using System.Windows.Documents;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le code coloré en lecture : les couleurs du thème, sans rien changer au texte ni aux positions.</summary>
[TestClass]
public sealed class CodeColorsTests
{
    private const string CSharp = """
        // Un commentaire
        public void Dire(string texte)
        {
            var nombre = 42;
            Console.WriteLine("bonjour");
        }
        """;

    [TestMethod]
    public void Each_kind_of_word_takes_its_color()
    {
        Assert.AreEqual("Kyf.Brush.TextSecondary", BrushOf(CSharp, "cs", "// Un commentaire"));
        Assert.AreEqual("Kyf.Brush.CodeKeyword", BrushOf(CSharp, "cs", "public"));
        Assert.AreEqual("Kyf.Brush.CodeNumber", BrushOf(CSharp, "cs", "42"));
        Assert.AreEqual("Kyf.Brush.CodeString", BrushOf(CSharp, "cs", "\"bonjour\""));
        Assert.AreEqual("Kyf.Brush.CodeTitle", BrushOf(CSharp, "cs", "WriteLine"));
    }

    [TestMethod]
    [DataRow("csharp")]
    [DataRow("C#")]
    [DataRow("CS")]
    public void A_language_goes_by_several_names(string language) =>
        Assert.AreEqual("Kyf.Brush.CodeKeyword", BrushOf(CSharp, language, "public"));

    [TestMethod]
    [DataRow("py", "def dire():\n    return 42\n", "def")]
    [DataRow("js", "function dire() { return 42; }", "function")]
    [DataRow("json", "{ \"a\": true }", "true")]
    [DataRow("xml", "<a b=\"c\">d</a>", "\"c\"")]
    [DataRow("sql", "SELECT a FROM b", "SELECT")]
    public void The_usual_languages_are_colored(string language, string code, string word) =>
        Assert.IsNotNull(BrushOf(code, language, word));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("cobol")]
    public void An_unknown_language_stays_as_text(string? language)
    {
        var pieces = CodeColors.Pieces(CSharp, language);

        Assert.HasCount(1, pieces);
        Assert.AreEqual(new CodePiece(0, CSharp.Length, null), pieces[0]);
    }

    [TestMethod]
    public void Pieces_follow_each_other_and_cover_all_the_code()
    {
        var pieces = CodeColors.Pieces(CSharp, "cs");

        var next = 0;
        foreach (var piece in pieces)
        {
            Assert.AreEqual(next, piece.Start);
            Assert.IsGreaterThan(0, piece.Length);
            next += piece.Length;
        }
        Assert.AreEqual(CSharp.Length, next);
    }

    [TestMethod]
    public void Empty_code_gives_nothing()
    {
        Assert.IsEmpty(CodeColors.Pieces(string.Empty, "cs"));
        Assert.ThrowsExactly<ArgumentNullException>(() => CodeColors.Pieces(null!, "cs"));
    }

    [TestMethod]
    public void A_very_long_block_stays_as_text()
    {
        var code = string.Concat(Enumerable.Repeat("var a = 1;\n", 10_000));

        Assert.HasCount(1, CodeColors.Pieces(code, "cs"));
    }

    [STATestMethod]
    public void The_text_of_a_colored_block_and_its_positions_stay_the_same()
    {
        var reading = SheetReading.Of(
            new OpenedSheet("f_1", "Code", "fiches/Code.md", string.Empty, [], "```cs\n" + CSharp + "\n```\n", 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []),
            _ => Outcome.Refused<byte[]>("non"));
        var viewer = new SheetDocumentViewer { Reading = reading };
        var block = viewer.Document.Blocks.OfType<Paragraph>().Single(paragraph => paragraph.Inlines.Count > 1);

        var text = string.Concat(block.Inlines.OfType<Run>().Select(run => run.Text));

        Assert.AreEqual(CSharp.Replace("\r\n", "\n", StringComparison.Ordinal), text);
        Assert.IsGreaterThan(5, block.Inlines.Count, "plusieurs couleurs");
        var start = reading.Map.Text.IndexOf("Console", StringComparison.Ordinal);
        viewer.Highlights = [new Highlight("h", start, start + "Console".Length, false, false)];
        Assert.AreEqual("h", viewer.AnnotationsAt(block.Inlines.OfType<Run>().First(run => run.Text == "Console")).Single());
    }

    private static string? BrushOf(string code, string language, string word)
    {
        var start = code.IndexOf(word, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, word);
        return CodeColors.Pieces(code, language).First(piece => piece.Start <= start && start < piece.Start + piece.Length).Brush;
    }
}
