using KyfRev.Rendering.Diagrams;

namespace KyfRev.Rendering.Tests;

/// <summary>Les hiérarchies : plusieurs niveaux, écrits en texte, et un dessin qui les montre.</summary>
[TestClass]
public sealed class DiagramOutlineTests
{
    private const string Family = """
        Grand-mère
          Enfant 1
          Enfant 2
          Enfant 3
            Petit-enfant 1
            Petit-enfant 2
        """;

    private static readonly OutlineItem[] FamilyItems =
    [
        new(0, "Grand-mère"), new(1, "Enfant 1"), new(1, "Enfant 2"), new(1, "Enfant 3"), new(2, "Petit-enfant 1"), new(2, "Petit-enfant 2"),
    ];

    [TestMethod]
    public void The_levels_come_from_the_indentation()
    {
        CollectionAssert.AreEqual(FamilyItems, DiagramOutline.Parse(Family).ToArray());
    }

    [TestMethod]
    public void A_list_without_indentation_reads_as_before()
    {
        OutlineItem[] expected = [new(0, "Cellule"), new(1, "Noyau"), new(1, "Membrane")];

        CollectionAssert.AreEqual(expected, DiagramOutline.Parse("Cellule\nNoyau\n\nMembrane\n").ToArray());
        CollectionAssert.AreEqual(expected, DiagramOutline.Parse("  Cellule\n  Noyau\n  Membrane").ToArray(), "Le même décalage partout n'en est pas un.");
        Assert.IsEmpty(DiagramOutline.Parse(" \n "));
    }

    [TestMethod]
    public void No_level_is_skipped_whatever_is_written()
    {
        OutlineItem[] expected = [new(0, "a"), new(1, "b"), new(2, "c"), new(1, "d"), new(0, "e")];

        CollectionAssert.AreEqual(expected, DiagramOutline.Parse("a\n      b\n\t\t\t\tc\r\n      d\ne").ToArray());
    }

    [TestMethod]
    public void Writing_leaves_out_what_is_empty_and_reads_back_the_same()
    {
        var written = DiagramOutline.Write([new(0, " Grand-mère "), new(1, "Enfant 1"), new(1, "   "), new(1, "Enfant 2"), new(1, "Enfant 3"), new(2, "Petit-enfant 1"), new(2, "Petit-enfant 2")]);

        Assert.AreEqual(Family.ReplaceLineEndings("\n"), written);
        Assert.AreEqual(written, DiagramOutline.Write(DiagramOutline.Parse(written)));
    }

    [TestMethod]
    public void Only_a_hierarchy_keeps_its_indentation_in_the_file()
    {
        Assert.AreEqual(("smartart organigramme", Family.ReplaceLineEndings("\n")), SmartArt.Source("organigramme", Family));
        Assert.AreEqual(("smartart horizontal", "a\n  b"), SmartArt.Source("horizontal", "a\nb"));
        Assert.AreEqual(("smartart liste", "a\nb"), SmartArt.Source("liste", "a\n  b"));
        Assert.IsTrue(SmartArt.IsHierarchy("organigramme"));
        Assert.IsTrue(SmartArt.IsHierarchy("horizontal"));
        Assert.IsFalse(SmartArt.IsHierarchy("liste"));
        Assert.IsFalse(SmartArt.IsHierarchy(null));
    }

    [TestMethod]
    public void Going_down_only_the_third_child_has_children_under_it()
    {
        var scene = SmartArt.Draw("organigramme", Family)!;

        var boxes = scene.Shapes.OfType<DiagramRectangle>().ToList();
        Assert.HasCount(6, boxes);
        Assert.AreEqual(DiagramTone.Accent, boxes[0].Tone);
        Assert.AreEqual(DiagramTone.Main, boxes[1].Tone);
        Assert.AreEqual(DiagramTone.Quiet, boxes[4].Tone);
        Assert.IsTrue(boxes[0].Y < boxes[1].Y && boxes[1].Y < boxes[4].Y, "Chaque niveau sous le précédent.");
        Assert.AreEqual(boxes[1].Y, boxes[3].Y, "Les enfants sur la même ligne.");
        Assert.AreEqual((boxes[4].X + boxes[5].X) / 2, boxes[3].X, 0.001, "Un parent au milieu de ses enfants.");
        Assert.IsTrue(boxes[1].X < boxes[2].X && boxes[2].X < boxes[3].X);
        foreach (var box in boxes)
        {
            Assert.IsTrue(box.X >= 0 && box.X + box.Width <= scene.Width && box.Y + box.Height <= scene.Height);
        }
        Assert.HasCount(12, scene.Shapes.OfType<DiagramLine>().ToList(), "Pour chaque parent, un trait qui descend ; pour chaque enfant, deux.");
        Assert.AreEqual("Organigramme : Grand-mère, Enfant 1, Enfant 2, Enfant 3, Petit-enfant 1, Petit-enfant 2", scene.Description);
    }

    [TestMethod]
    public void Going_across_each_level_is_a_column()
    {
        var scene = SmartArt.Draw("horizontal", Family)!;

        var boxes = scene.Shapes.OfType<DiagramRectangle>().ToList();
        Assert.HasCount(6, boxes);
        Assert.IsTrue(boxes[0].X < boxes[1].X && boxes[1].X < boxes[4].X);
        Assert.AreEqual((boxes[4].Y + boxes[5].Y) / 2, boxes[3].Y, 0.001);
        Assert.IsTrue(boxes[5].Y + boxes[5].Height <= scene.Height);
    }

    [TestMethod]
    [DataRow("organigramme")]
    [DataRow("horizontal")]
    public void The_drawing_grows_with_what_it_shows_instead_of_cutting_it(string layout)
    {
        var small = SmartArt.Draw(layout, "a\nb\nc")!;
        var large = SmartArt.Draw(layout, string.Join('\n', Enumerable.Range(1, 30).Select(rank => $"Élément {rank}")))!;
        var deep = SmartArt.Draw(layout, string.Join('\n', Enumerable.Range(0, 8).Select(level => new string(' ', level * 2) + $"Niveau {level}")))!;

        Assert.IsGreaterThan(small.Width * small.Height, large.Width * large.Height);
        Assert.HasCount(30, large.Shapes.OfType<DiagramRectangle>().ToList());
        Assert.HasCount(8, deep.Shapes.OfType<DiagramRectangle>().ToList());
        Assert.IsGreaterThan(small.Width * small.Height, deep.Width * deep.Height);
        foreach (var text in deep.Shapes.OfType<DiagramText>())
        {
            Assert.DoesNotEndWith("…", text.Lines[^1]);
        }
    }

    [TestMethod]
    public void Several_tops_written_by_hand_stand_side_by_side()
    {
        var scene = SmartArt.Draw("organigramme", "a\n  b\nc\n  d")!;

        var boxes = scene.Shapes.OfType<DiagramRectangle>().ToList();
        Assert.AreEqual(boxes[0].Y, boxes[2].Y);
        Assert.IsTrue(boxes[0].X < boxes[2].X);
    }

    [TestMethod]
    public void Nothing_written_draws_nothing()
    {
        var scene = SmartArt.Draw("organigramme", " ")!;

        Assert.IsEmpty(scene.Shapes);
        Assert.AreEqual(480, scene.Width);
    }
}
