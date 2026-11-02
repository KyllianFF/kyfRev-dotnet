using System.IO;
using KyfRev.Presentation.Wpf.Editing;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Les fichiers de la page de l'éditeur, posés sur le disque par version.</summary>
[TestClass]
public sealed class EditorPageTests
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "kyfrev-page-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [TestMethod]
    public void The_page_its_styles_and_all_its_fonts_are_there()
    {
        var (folder, version) = EditorPage.Prepare(_folder);

        var page = Path.Combine(folder, version);
        Assert.IsTrue(File.Exists(Path.Combine(page, "index.html")));
        Assert.IsTrue(File.Exists(Path.Combine(page, "editor.js")));
        Assert.IsTrue(File.Exists(Path.Combine(page, "polices.css")));
        var fonts = Directory.GetFiles(Path.Combine(page, "polices")).Select(Path.GetFileName).ToList();
        Assert.Contains("AtkinsonHyperlegibleNext-Regular.ttf", fonts);
        Assert.Contains("AtkinsonHyperlegibleMono-SemiBold.ttf", fonts);
        Assert.IsTrue(fonts.Exists(font => font!.StartsWith("KaTeX", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void A_damaged_file_is_put_right()
    {
        var (folder, version) = EditorPage.Prepare(_folder);
        var page = Path.Combine(folder, version, "index.html");
        var whole = File.ReadAllText(page);
        File.WriteAllText(page, "abîmé");
        File.Delete(Path.Combine(folder, version, "editor.css"));

        EditorPage.Prepare(_folder);

        Assert.AreEqual(whole, File.ReadAllText(page));
        Assert.IsTrue(File.Exists(Path.Combine(folder, version, "editor.css")));
    }

    [TestMethod]
    public void An_older_version_goes_away_unless_it_is_in_use()
    {
        var older = Path.Combine(_folder, "page", "ancienne");
        Directory.CreateDirectory(older);
        File.WriteAllText(Path.Combine(older, "index.html"), "ancienne");
        var used = Path.Combine(_folder, "page", "ouverte");
        Directory.CreateDirectory(used);
        var file = Path.Combine(used, "index.html");
        using (new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            EditorPage.Prepare(_folder);

            Assert.IsFalse(Directory.Exists(older));
            Assert.IsTrue(File.Exists(file));
        }

        EditorPage.Prepare(_folder);

        Assert.IsFalse(Directory.Exists(used), "Elle part la fois suivante.");
    }
}
