using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KyfRev.Rendering.Tests;

/// <summary>Les destinations des liens et des images, confrontées à ce que la 0.x en a fait dans le corpus.</summary>
[TestClass]
public sealed partial class ContentTargetsTests
{
    private const string Folder = "fiches/Corpus";

    private static readonly Lazy<JsonDocument> Projection = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "ancrage", "projection.json"))));

    [TestMethod]
    public void Every_link_of_the_corpus_leads_where_it_led()
    {
        var checkedLinks = 0;
        foreach (var document in Projection.Value.RootElement.GetProperty("cas").EnumerateArray())
        {
            var expected = LinkTag().Matches(document.GetProperty("html").GetString()!)
                .Select(tag => tag.Groups[1].Value)
                .Where(attributes => !attributes.Contains("fnref", StringComparison.Ordinal) && !attributes.Contains("href=\"#fn", StringComparison.Ordinal))
                .Select(ExpectedLink)
                .ToList();
            var actual = All(SheetRenderer.Render(document.GetProperty("corps").GetString()!, document.GetProperty("titre").GetString()!), "a")
                .Where(link => link.Identifier is null && link.Target?.StartsWith("#fn", StringComparison.Ordinal) != true)
                .Select(link => ContentTargets.OfLink(link.Target!, Folder))
                .ToList();
            CollectionAssert.AreEqual(expected, actual, document.GetProperty("nom").GetString());
            checkedLinks += actual.Count;
        }
        Assert.IsGreaterThan(100, checkedLinks);
    }

    [TestMethod]
    public void Every_image_of_the_corpus_is_shown_or_blocked_as_it_was()
    {
        var checkedImages = 0;
        foreach (var document in Projection.Value.RootElement.GetProperty("cas").EnumerateArray())
        {
            var expected = ImageTag().Matches(document.GetProperty("html").GetString()!).Select(ExpectedImage).ToList();
            var actual = All(SheetRenderer.Render(document.GetProperty("corps").GetString()!, document.GetProperty("titre").GetString()!), "img")
                .Select(image => ContentTargets.OfImage(image.Target!, Folder))
                .ToList();
            CollectionAssert.AreEqual(expected, actual, document.GetProperty("nom").GetString());
            checkedImages += actual.Count;
        }
        Assert.IsGreaterThan(10, checkedImages);
    }

    [TestMethod]
    [DataRow("fiches/Histoire/Révolution.md", "fiches/Histoire")]
    [DataRow("Révolution.md", "")]
    public void The_folder_of_a_sheet_is_read_from_its_path(string path, string folder)
    {
        Assert.AreEqual(folder, ContentTargets.FolderOf(path));
    }

    [TestMethod]
    [DataRow("Autre.md", "fiches/Histoire/Autre.md")]
    [DataRow("./a/../Autre.md", "fiches/Histoire/Autre.md")]
    [DataRow("../../Autre.md", "Autre.md")]
    [DataRow("Autre%20fiche.md#partie", "fiches/Histoire/Autre fiche.md")]
    [DataRow("%C3%89t%C3%A9.MD", "fiches/Histoire/Été.MD")]
    [DataRow("Cent%%25.md", "fiches/Histoire/Cent%%.md")]
    [DataRow("a\\_b.md", "fiches/Histoire/a_b.md")]
    [DataRow("%FF.md", "fiches/Histoire/%FF.md")]
    public void A_link_to_a_sheet_is_resolved_from_its_folder(string link, string path)
    {
        Assert.AreEqual(new SheetLink(path), ContentTargets.OfLink(link, "fiches/Histoire"));
    }

    [TestMethod]
    [DataRow("../../../hors.md")]
    [DataRow("/absolu.md")]
    [DataRow("\\absolu.md")]
    [DataRow("C:\\fiche.md")]
    [DataRow("ftp://exemple.org/a.md")]
    [DataRow("image.png")]
    [DataRow("")]
    [DataRow("?q=1")]
    public void A_link_that_leads_nowhere_opens_nothing(string link)
    {
        Assert.AreEqual(new BrokenLink(), ContentTargets.OfLink(link, "fiches/Histoire"));
    }

    [TestMethod]
    [DataRow("HTTPS://exemple.org/a")]
    [DataRow("http://exemple.org")]
    [DataRow("MailTo:a@exemple.org")]
    public void A_web_or_mail_address_goes_to_the_system_browser(string link)
    {
        Assert.AreEqual(new ExternalLink(link), ContentTargets.OfLink(link, "fiches/Histoire"));
    }

    [TestMethod]
    public void An_anchor_stays_in_the_page()
    {
        Assert.AreEqual(new PageAnchor("fn1"), ContentTargets.OfLink("#fn1", "fiches/Histoire"));
    }

    [TestMethod]
    public void Only_the_vault_and_embedded_images_are_loaded()
    {
        Assert.AreEqual(new VaultImage("fiches/Histoire/images/a b.png"), ContentTargets.OfImage("images/a%20b.png?v=2", "fiches/Histoire"));
        Assert.AreEqual(new EmbeddedImage("data:image/png;base64,AA"), ContentTargets.OfImage("data:image/png;base64,AA", "fiches/Histoire"));
        Assert.AreEqual(new BlockedImage(ImageBlock.OutsideVault, "../../../a.png"), ContentTargets.OfImage("../../../a.png", "fiches/Histoire"));
        Assert.AreEqual(new BlockedImage(ImageBlock.Remote, "https://exemple.org/a.png"), ContentTargets.OfImage("https://exemple.org/a.png", "fiches/Histoire"));
        Assert.AreEqual(new BlockedImage(ImageBlock.NotInVault, "file:///C:/a b.png"), ContentTargets.OfImage("file:///C:/a%20b.png", "fiches/Histoire"));
    }

    private static LinkTarget ExpectedLink(string attributes)
    {
        var sheet = SheetAttribute().Match(attributes);
        if (sheet.Success)
        {
            return new SheetLink(WebUtility.HtmlDecode(sheet.Groups[1].Value));
        }
        var external = ExternalAttribute().Match(attributes);
        if (external.Success)
        {
            return new ExternalLink(WebUtility.HtmlDecode(external.Groups[1].Value));
        }
        var anchor = AnchorAttribute().Match(attributes);
        return anchor.Success ? new PageAnchor(WebUtility.HtmlDecode(anchor.Groups[1].Value)) : new BrokenLink();
    }

    private static ImageTarget ExpectedImage(Match tag)
    {
        if (tag.Groups["data"].Success)
        {
            return new EmbeddedImage(WebUtility.HtmlDecode(tag.Groups["data"].Value));
        }
        var detail = WebUtility.HtmlDecode(tag.Groups["detail"].Value);
        return tag.Groups["message"].Value switch
        {
            // Le corpus est calculé hors de l'application : un chemin du coffre y est annoncé sans être chargé.
            "Image du coffre (visible dans kyfRev)" => new VaultImage($"{Folder}/{detail}"),
            "Image hors du coffre" => new BlockedImage(ImageBlock.OutsideVault, detail),
            "Image distante bloquée" => new BlockedImage(ImageBlock.Remote, detail),
            _ => new BlockedImage(ImageBlock.NotInVault, detail),
        };
    }

    private static IEnumerable<RenderElement> All(RenderElement root, string tag)
    {
        foreach (var element in root.Children.OfType<RenderElement>())
        {
            if (element.Tag == tag)
            {
                yield return element;
            }
            foreach (var descendant in All(element, tag))
            {
                yield return descendant;
            }
        }
    }

    [GeneratedRegex("<a ([^>]*)>")]
    private static partial Regex LinkTag();

    [GeneratedRegex("data-fiche=\"([^\"]*)\"")]
    private static partial Regex SheetAttribute();

    [GeneratedRegex("data-externe=\"([^\"]*)\"")]
    private static partial Regex ExternalAttribute();

    [GeneratedRegex("^href=\"#([^\"]+)\"")]
    private static partial Regex AnchorAttribute();

    [GeneratedRegex("<img [^>]*src=\"(?<data>[^\"]*)\"[^>]*>|<span class=\"image-bloquee\" title=\"(?<detail>[^\"]*)\">(?<message>.*?) : ")]
    private static partial Regex ImageTag();
}
