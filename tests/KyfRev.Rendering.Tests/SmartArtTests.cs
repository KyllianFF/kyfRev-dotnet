using CsCheck;
using KyfRev.Rendering.Diagrams;

namespace KyfRev.Rendering.Tests;

/// <summary>Les SmartArt : kyfRev les dessine lui-même, quel que soit le texte.</summary>
[TestClass]
public sealed class SmartArtTests
{
    public static IEnumerable<object[]> Layouts => SmartArt.Layouts.Select(layout => new object[] { layout.Id });

    [TestMethod]
    public void There_are_the_seventeen_models_of_the_gallery_with_distinct_names()
    {
        Assert.HasCount(17, SmartArt.Layouts);
        Assert.HasCount(17, SmartArt.Layouts.Select(layout => layout.Id).Distinct());
        Assert.HasCount(17, SmartArt.Layouts.Select(layout => layout.Name).Distinct());
    }

    [TestMethod]
    [DynamicData(nameof(Layouts))]
    public void A_model_draws_its_sample_and_shows_all_its_text(string id)
    {
        var layout = SmartArt.Layouts.Single(candidate => candidate.Id == id);

        var scene = SmartArt.Draw(id, layout.Sample)!;

        var shown = string.Concat(scene.Shapes.OfType<DiagramText>().SelectMany(text => text.Lines)).Replace(" ", string.Empty, StringComparison.Ordinal);
        foreach (var item in SmartArt.Items(layout.Sample))
        {
            Assert.Contains(item.Replace(" ", string.Empty, StringComparison.Ordinal), shown);
            Assert.Contains(item, scene.Description);
        }
        AssertInside(scene);
    }

    [TestMethod]
    [DynamicData(nameof(Layouts))]
    public void A_model_stands_any_number_of_items(string id)
    {
        foreach (var count in new[] { 0, 1, 2, 3, 5, 8, 12, 40 })
        {
            var scene = SmartArt.Draw(id, string.Join('\n', Enumerable.Range(1, count).Select(rank => $"Élément {rank}")))!;

            AssertInside(scene);
            if (count == 0)
            {
                Assert.IsEmpty(scene.Shapes);
            }
        }
    }

    [TestMethod]
    public void No_text_at_all_breaks_a_model_or_leaves_the_drawing() =>
        Gen.String[0, 400].Sample(text =>
        {
            foreach (var layout in SmartArt.Layouts)
            {
                AssertInside(SmartArt.Draw(layout.Id, text)!);
            }
        }, iter: 200);

    [TestMethod]
    public void A_text_too_long_for_its_shape_ends_with_an_ellipsis()
    {
        var scene = SmartArt.Draw("cycle", new string('m', 300) + "\nb\nc\nd")!;

        var text = scene.Shapes.OfType<DiagramText>().First();
        Assert.EndsWith("…", text.Lines[^1]);
        Assert.AreEqual(10, text.Size);
    }

    [TestMethod]
    public void A_short_text_takes_the_largest_size_and_a_long_one_wraps()
    {
        var scene = SmartArt.Draw("liste", "Court\nUn texte plus long qui doit passer à la ligne pour tenir dans sa boîte, sans sortir ni être coupé du tout, même s'il continue encore un peu")!;

        var texts = scene.Shapes.OfType<DiagramText>().ToList();
        Assert.AreEqual(17, texts[0].Size);
        Assert.HasCount(1, texts[0].Lines);
        Assert.IsGreaterThan(1, texts[1].Lines.Length);
    }

    [TestMethod]
    public void A_model_shows_no_more_than_it_can()
    {
        var scene = SmartArt.Draw("matrice", "a\nb\nc\nd\ne\nf")!;

        Assert.HasCount(4, scene.Shapes.OfType<DiagramText>());
        Assert.AreEqual("Matrice 2 × 2 : a, b, c, d", scene.Description);
    }

    [TestMethod]
    public void An_unknown_model_is_only_a_code_block()
    {
        Assert.IsNull(SmartArt.Draw("inconnu", "a"));
        Assert.IsNull(SmartArt.Draw(null, "a"));
    }

    [TestMethod]
    public void The_lines_are_the_items_without_the_empty_ones()
    {
        var items = SmartArt.Items("  a  \n\n\tb c\n");
        Assert.HasCount(2, items);
        Assert.AreEqual("a", items[0]);
        Assert.AreEqual("b c", items[1]);
        Assert.IsTrue(SmartArt.Items(" \n ").IsEmpty);
    }

    [TestMethod]
    public void The_source_is_a_code_block_readable_anywhere()
    {
        var (info, code) = SmartArt.Source("cycle", " G1 \n\nS\n");

        Assert.AreEqual("smartart cycle", info);
        Assert.AreEqual("G1\nS", code);
    }

    [TestMethod]
    public void The_block_says_its_model_in_the_tree()
    {
        var root = SheetRenderer.Render("```smartart cycle\nG1\nS\n```\n\n```python\nx\n```\n\n```\ny\n```", string.Empty);

        var codes = Elements(root).Where(element => element.Tag == "code").ToList();
        Assert.AreEqual("smartart", codes[0].Language);
        Assert.AreEqual("cycle", codes[0].CodeArguments);
        Assert.AreEqual("python", codes[1].Language);
        Assert.IsNull(codes[1].CodeArguments);
        Assert.IsNull(codes[2].Language);
        Assert.IsNull(codes[2].CodeArguments);
    }

    [TestMethod]
    public void The_svg_escapes_all_the_text_and_follows_the_theme()
    {
        var svg = DiagramSvg.Write(SmartArt.Draw("processus", "<script>alert(1)</script>\n\"a\" & b\n<img src=x onerror=y>")!);

        Assert.DoesNotContain("<script", svg);
        Assert.DoesNotContain("<img", svg);
        Assert.Contains("&lt;", svg);
        Assert.Contains("&amp;", svg);
        Assert.Contains("var(--actif-fond)", svg);
        Assert.DoesNotContain("#", svg, "Aucune couleur écrite en dur.");
    }

    [TestMethod]
    [DynamicData(nameof(Layouts))]
    public void The_svg_writes_all_the_shapes(string id)
    {
        var layout = SmartArt.Layouts.Single(candidate => candidate.Id == id);

        var svg = DiagramSvg.Write(SmartArt.Draw(id, layout.Sample)!);

        Assert.StartsWith("<svg ", svg);
        Assert.EndsWith("</svg>", svg);
        Assert.Contains("<tspan", svg);
    }

    private static void AssertInside(DiagramScene scene)
    {
        const double margin = 1;
        void Point(double x, double y)
        {
            Assert.IsTrue(x >= -margin && x <= scene.Width + margin, $"x = {x} dans {scene.Description}");
            Assert.IsTrue(y >= -margin && y <= scene.Height + margin, $"y = {y} dans {scene.Description}");
        }
        foreach (var shape in scene.Shapes)
        {
            switch (shape)
            {
                case DiagramRectangle box:
                    Point(box.X, box.Y);
                    Point(box.X + box.Width, box.Y + box.Height);
                    break;
                case DiagramEllipse ellipse:
                    Point(ellipse.CenterX - ellipse.RadiusX, ellipse.CenterY - ellipse.RadiusY);
                    Point(ellipse.CenterX + ellipse.RadiusX, ellipse.CenterY + ellipse.RadiusY);
                    break;
                case DiagramPolygon polygon:
                    foreach (var point in polygon.Points)
                    {
                        Point(point.X, point.Y);
                    }
                    break;
                case DiagramLine line:
                    Point(line.From.X, line.From.Y);
                    Point(line.To.X, line.To.Y);
                    break;
                case DiagramText text:
                    Point(text.CenterX, text.CenterY);
                    break;
            }
        }
    }

    private static IEnumerable<RenderElement> Elements(RenderElement root)
    {
        yield return root;
        foreach (var child in root.Children.OfType<RenderElement>().SelectMany(Elements))
        {
            yield return child;
        }
    }
}
