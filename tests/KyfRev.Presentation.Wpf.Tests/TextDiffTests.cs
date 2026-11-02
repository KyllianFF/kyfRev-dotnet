using KyfRev.Presentation.Wpf.Editing;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La comparaison de deux textes.</summary>
[TestClass]
public sealed class TextDiffTests
{
    private static string Shape(string left, string right) =>
        string.Join(' ', TextDiff.Rows(left, right).Select(row => row.Kind switch
        {
            DiffKind.Same => "=",
            DiffKind.Changed => "~",
            DiffKind.Removed => "-",
            _ => "+",
        }));

    [TestMethod]
    [DataRow("a\nb", "a\nb", "= =")]
    [DataRow("a\nb\nc", "a\nx\nc", "= ~ =")]
    [DataRow("a\nc", "a\nb\nc", "= + =")]
    [DataRow("a\nb\nc", "a\nc", "= - =")]
    [DataRow("a\nb\nc\nd", "a\nx\ny\nz\nd", "= ~ ~ + =")]
    [DataRow("a\nb\nc\nd", "a\nx\nd", "= ~ - =")]
    [DataRow("", "", "=")]
    [DataRow("a", "", "~")]
    [DataRow("x\na\ny\nb\nz", "a\nb", "- = - = -")]
    public void The_lines_are_set_face_to_face(string left, string right, string expected) =>
        Assert.AreEqual(expected, Shape(left, right));

    [TestMethod]
    public void A_row_says_which_side_differs_with_a_sign()
    {
        var rows = TextDiff.Rows("a\nb\nc", "a\nB\nc\nd");

        Assert.AreEqual(new DiffRow("a", "a", DiffKind.Same), rows[0]);
        Assert.AreEqual(string.Empty, rows[0].LeftSign);
        Assert.IsFalse(rows[0].IsLeftMarked);
        Assert.AreEqual("b", rows[1].Left);
        Assert.AreEqual("B", rows[1].Right);
        Assert.AreEqual("−", rows[1].LeftSign);
        Assert.AreEqual("+", rows[1].RightSign);
        Assert.IsNull(rows[3].Left);
        Assert.IsTrue(rows[3].IsRightMarked);
        Assert.IsFalse(rows[3].IsLeftMarked);
    }

    [TestMethod]
    public void Very_different_long_texts_are_shown_as_one_block_rather_than_blocking()
    {
        var left = string.Join('\n', Enumerable.Range(0, 3000).Select(rank => $"gauche {rank}"));
        var right = string.Join('\n', Enumerable.Range(0, 3000).Select(rank => $"droite {rank}"));

        var rows = TextDiff.Rows(left, right);

        Assert.HasCount(3000, rows);
        Assert.IsTrue(rows.All(row => row.Kind == DiffKind.Changed));
    }

    [TestMethod]
    public void A_long_text_with_a_small_change_stays_precise()
    {
        var lines = Enumerable.Range(0, 20000).Select(rank => $"ligne {rank}").ToList();
        var left = string.Join('\n', lines);
        lines[12345] = "modifiée";

        var rows = TextDiff.Rows(left, string.Join('\n', lines));

        Assert.HasCount(20000, rows);
        Assert.AreEqual(1, rows.Count(row => row.Kind != DiffKind.Same));
    }
}
