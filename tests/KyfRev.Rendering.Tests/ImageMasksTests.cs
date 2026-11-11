namespace KyfRev.Rendering.Tests;

/// <summary>Les masques d'une image : leur écriture dans son titre, et leur pose dans le Markdown d'une fiche.</summary>
[TestClass]
public sealed class ImageMasksTests
{
    private static readonly ImageMask[] Two = [new(0.1, 0.2, 0.3, 0.15), new(0.5, 0.5, 0.2, 0.1)];

    private static string Text(IEnumerable<ImageMask> masks) =>
        string.Join(" | ", masks.Select(mask => FormattableString.Invariant($"{mask.X:0.###},{mask.Y:0.###},{mask.Width:0.###},{mask.Height:0.###}")));

    [TestMethod]
    public void Masks_are_written_in_percent_and_read_back_the_same()
    {
        var title = ImageMasks.ToTitle(Two);

        Assert.AreEqual("kyfrev-masques:10,20,30,15;50,50,20,10", title);
        Assert.AreEqual(Text(Two), Text(ImageMasks.FromTitle(title)));
        Assert.IsNull(ImageMasks.ToTitle([]), "sans masque, l'image n'a pas de titre");
        Assert.AreEqual("kyfrev-masques:12.3,0,50.1,99.9", ImageMasks.ToTitle([new(0.1234, 0, 0.5005, 0.9994)]), "au dixième de pourcent");
    }

    [TestMethod]
    public void A_title_that_is_not_ours_or_a_broken_mask_gives_no_mask()
    {
        Assert.IsEmpty(ImageMasks.FromTitle(null));
        Assert.IsEmpty(ImageMasks.FromTitle("Une légende ordinaire"));
        Assert.IsEmpty(ImageMasks.FromTitle("kyfrev-masques:"));
        // Le masque mal écrit, vide ou hors de l'image est laissé de côté ; les autres restent.
        Assert.AreEqual("0.1,0.2,0.3,0.15", Text(ImageMasks.FromTitle("kyfrev-masques:a,b,c,d;10,20,30;10,20,30,15;10,10,0,0;150,150,10,10;1e2,0,1,1")));
        Assert.HasCount(ImageMasks.Maximum, ImageMasks.FromTitle("kyfrev-masques:" + string.Join(';', Enumerable.Repeat("10,10,10,10", ImageMasks.Maximum + 5))));
    }

    [TestMethod]
    public void A_mask_is_brought_back_inside_the_image()
    {
        // Tracé de droite à gauche et de bas en haut : remis à l'endroit.
        Assert.AreEqual("0.2,0.3,0.4,0.2", Text([ImageMasks.Normalize(new ImageMask(0.6, 0.5, -0.4, -0.2))!.Value]));
        // Ce qui dépasse est coupé.
        Assert.AreEqual("0.9,0,0.1,0.5", Text([ImageMasks.Normalize(new ImageMask(0.9, -0.2, 0.5, 0.7))!.Value]));
        Assert.IsNull(ImageMasks.Normalize(new ImageMask(0.5, 0.5, 0.005, 0.5)), "trop fin pour être vu");
        Assert.IsNull(ImageMasks.Normalize(new ImageMask(1.2, 0.5, 0.5, 0.5)), "hors de l'image");
    }

    [TestMethod]
    public void The_renderer_gives_each_image_its_masks_without_changing_the_text()
    {
        var plain = SheetRenderer.Render("Avant ![Cellule](cellule.png) après.\n", "Fiche");
        var masked = SheetRenderer.Render("Avant ![Cellule](cellule.png \"kyfrev-masques:10,20,30,15;50,50,20,10\") après.\n", "Fiche");

        Assert.IsEmpty(ImageMaskWriter.Images(plain).Single().Masks);
        Assert.AreEqual(Text(Two), Text(ImageMaskWriter.Images(masked).Single().Masks));
        Assert.AreEqual(TextMap.Of(plain).Text, TextMap.Of(masked).Text, "un titre ne s'affiche pas : les positions des passages ne bougent pas");
        Assert.IsEmpty(ImageMaskWriter.Images(SheetRenderer.Render("![Cellule](cellule.png \"Une légende\")\n", "Fiche")).Single().Masks);
    }

    [TestMethod]
    [DataRow("![A](a.png) et ![B](b.png)\n", 1, "![A](a.png) et ![B](b.png \"kyfrev-masques:10,20,30,15;50,50,20,10\")\n")]
    [DataRow("![A](a.png \"kyfrev-masques:1,1,5,5\")\n", 0, "![A](a.png \"kyfrev-masques:10,20,30,15;50,50,20,10\")\n")]
    [DataRow("![A](<mon image.png>)\n", 0, "![A](<mon image.png> \"kyfrev-masques:10,20,30,15;50,50,20,10\")\n")]
    [DataRow("`![X](x.png)` puis ![A](a.png)\n```\n![Y](y.png)\n```\n", 0, "`![X](x.png)` puis ![A](a.png \"kyfrev-masques:10,20,30,15;50,50,20,10\")\n```\n![Y](y.png)\n```\n")]
    [DataRow("![Un [crochet] dedans](a_(1).png)\n", 0, "![Un [crochet] dedans](a_(1).png \"kyfrev-masques:10,20,30,15;50,50,20,10\")\n")]
    public void Masks_are_written_on_the_right_image_and_nothing_else_changes(string body, int index, string expected) =>
        Assert.AreEqual(expected, ImageMaskWriter.Write(body, "Fiche", index, Two));

    [TestMethod]
    public void Removing_the_masks_removes_the_title()
    {
        Assert.AreEqual("![A](a.png) et ![B](b.png)\n", ImageMaskWriter.Write("![A](a.png \"kyfrev-masques:1,1,5,5\") et ![B](b.png)\n", "Fiche", 0, []));
    }

    [TestMethod]
    [DataRow("Pas d'image.\n", 0)]
    [DataRow("![A](a.png)\n", 1)]
    [DataRow("![A](a.png)\n", -1)]
    [DataRow("![A](a.png \"Ma légende\")\n", 0)]
    [DataRow("![A][ref]\n\n[ref]: a.png\n", 0)]
    [DataRow("    ![Code](code.png)\n\n![A][ref]\n\n[ref]: a.png\n", 0)]
    public void An_image_that_cannot_be_found_for_sure_is_left_alone(string body, int index) =>
        Assert.IsNull(ImageMaskWriter.Write(body, "Fiche", index, Two));
}
