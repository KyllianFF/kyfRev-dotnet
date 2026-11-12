namespace KyfRev.Rendering.Tests;

/// <summary>L'apparence d'une image, lue et écrite dans son titre Markdown.</summary>
[TestClass]
public sealed class ImageLookTests
{
    [TestMethod]
    public void A_title_gives_width_place_frame_and_masks_and_is_written_back_the_same()
    {
        const string title = "kyfrev-largeur:60 kyfrev-position:centre kyfrev-cadre:10,5,80,90 kyfrev-masques:10,20,30,15;50,50,20,10";

        var look = ImageLooks.FromTitle(title);

        Assert.IsNotNull(look);
        Assert.AreEqual(60, look.Width);
        Assert.AreEqual(ImageAlign.Center, look.Align);
        Assert.AreEqual(new ImageMask(0.1, 0.05, 0.8, 0.9), look.Crop);
        Assert.HasCount(2, look.Masks);
        Assert.AreEqual(title, ImageLooks.ToTitle(look));
        Assert.IsFalse(look.IsPlain);
    }

    [TestMethod]
    public void Only_what_is_set_is_written_always_in_the_same_order()
    {
        Assert.IsNull(ImageLooks.ToTitle(ImageLook.Plain), "une image ordinaire n'a pas de titre");
        Assert.AreEqual("kyfrev-largeur:40", ImageLooks.ToTitle(ImageLook.Plain with { Width = 40 }));
        Assert.AreEqual("kyfrev-position:droite", ImageLooks.ToTitle(ImageLook.Plain with { Align = ImageAlign.Right }));
        Assert.AreEqual("kyfrev-position:gauche", ImageLooks.ToTitle(ImageLook.Plain with { Align = ImageAlign.Left }));
        Assert.AreEqual(
            "kyfrev-largeur:100 kyfrev-masques:10,10,20,20",
            ImageLooks.ToTitle(ImageLooks.FromTitle("kyfrev-masques:10,10,20,20 kyfrev-largeur:250")!),
            "écrit dans le désordre et trop large, il est remis d'aplomb");
        Assert.IsNull(ImageLooks.ToTitle(ImageLook.Plain with { Crop = new ImageMask(0, 0, 1, 1) }), "un cadre qui garde tout n'en est pas un");
        Assert.AreEqual("kyfrev-largeur:5", ImageLooks.ToTitle(ImageLook.Plain with { Width = 1 }));
    }

    [TestMethod]
    public void A_caption_is_not_ours_and_a_broken_word_does_not_lose_the_others()
    {
        Assert.AreEqual(ImageLook.Plain, ImageLooks.FromTitle(null));
        Assert.AreEqual(ImageLook.Plain, ImageLooks.FromTitle(string.Empty));
        Assert.IsNull(ImageLooks.FromTitle("Ma légende"));
        Assert.IsNull(ImageLooks.FromTitle("kyfrev-largeur:60 et une légende"), "un mot étranger suffit : ce titre a été écrit à la main");
        Assert.IsNull(ImageLooks.FromTitle("kyfrev-inconnu:1"));
        Assert.IsFalse(ImageLooks.IsOurs("Ma légende"));
        Assert.IsTrue(ImageLooks.IsOurs(null));

        var look = ImageLooks.FromTitle("kyfrev-largeur:abc kyfrev-position:ailleurs kyfrev-cadre:1,2 kyfrev-masques:10,10,20,20");
        Assert.IsNotNull(look);
        Assert.IsNull(look.Width);
        Assert.AreEqual(ImageAlign.Inline, look.Align);
        Assert.IsNull(look.Crop);
        Assert.AreEqual(new ImageMask(0.1, 0.1, 0.2, 0.2), look.Masks.Single());
    }

    [TestMethod]
    public void Changing_one_setting_keeps_the_others_and_masks_alone_still_read_as_before()
    {
        var (ours, title) = ImageLooks.Change("kyfrev-largeur:60 kyfrev-masques:10,10,20,20", look => look with { Align = ImageAlign.Center });
        Assert.IsTrue(ours);
        Assert.AreEqual("kyfrev-largeur:60 kyfrev-position:centre kyfrev-masques:10,10,20,20", title);

        Assert.AreEqual((false, "Ma légende"), ImageLooks.Change("Ma légende", look => look with { Width = 50 }), "une légende n'est pas remplacée");
        Assert.AreEqual((true, null), ImageLooks.Change("kyfrev-largeur:60", _ => ImageLook.Plain));

        // Ce que la version d'avant écrivait se lit toujours, et s'écrit pareil.
        Assert.AreEqual(new ImageMask(0.1, 0.2, 0.3, 0.15), ImageMasks.FromTitle("kyfrev-masques:10,20,30,15").Single());
        Assert.AreEqual("kyfrev-masques:10,20,30,15", ImageMasks.ToTitle([new ImageMask(0.1, 0.2, 0.3, 0.15)]));
        Assert.IsEmpty(ImageMasks.FromTitle("Ma légende"));
    }

    [TestMethod]
    public void Masks_written_on_an_image_keep_its_width_place_and_frame()
    {
        const string body = "![Cellule](cellule.png \"kyfrev-largeur:60 kyfrev-position:centre\")\n";

        var written = ImageMaskWriter.Write(body, "Fiche", 0, [new ImageMask(0.1, 0.1, 0.2, 0.2)]);

        Assert.AreEqual("![Cellule](cellule.png \"kyfrev-largeur:60 kyfrev-position:centre kyfrev-masques:10,10,20,20\")\n", written);
        Assert.AreEqual("![Cellule](cellule.png \"kyfrev-largeur:60 kyfrev-position:centre\")\n", ImageMaskWriter.Write(written!, "Fiche", 0, []));
        var image = ImageMaskWriter.Images(SheetRenderer.Render(written!, "Fiche")).Single();
        Assert.AreEqual(60, image.ImageLook.Width);
        Assert.AreEqual(ImageAlign.Center, image.ImageLook.Align);
    }
}
