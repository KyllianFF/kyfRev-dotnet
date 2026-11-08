using System.Text;

namespace KyfRev.Acceptance.Tests;

/// <summary>L'outil de la preuve lui-même : un scan qui ne verrait rien prouverait tout. Ces essais ne lancent pas l'application.</summary>
[TestClass]
public sealed class DiskScanTests
{
    private const string Marker = "kyfmarque0123456789abcdef0123456789";

    private static readonly byte[] Noise = [0xC3, 0x28, 0x00, 0xFF, 0x7A];

    private static DiskScan Scan => new([Marker]);

    [TestMethod]
    [DataRow("utf-8")]
    [DataRow("utf-16")]
    [DataRow("utf-16BE")]
    public void A_marker_is_read_in_each_encoding_among_binary_bytes(string encoding)
    {
        using var folder = new Disposable();
        File.WriteAllBytes(folder.File("donnees.bin"), [.. Noise, .. Encoding.GetEncoding(encoding).GetBytes(Marker), .. Noise]);

        Assert.IsNotEmpty(Scan.Of(folder.Path).Findings);
    }

    [TestMethod]
    public void A_marker_changed_to_capitals_is_read_too()
    {
        using var folder = new Disposable();
        File.WriteAllText(folder.File("texte.txt"), Marker.ToUpperInvariant());

        Assert.IsNotEmpty(Scan.Of(folder.Path).Findings);
    }

    [TestMethod]
    [DataRow(0, "utf-8")]
    [DataRow(1, "utf-8")]
    [DataRow(2, "utf-8")]
    [DataRow(0, "utf-16")]
    [DataRow(1, "utf-16")]
    [DataRow(2, "utf-16")]
    public void A_marker_inside_base64_is_read_at_every_alignment(int before, string encoding)
    {
        using var folder = new Disposable();
        File.WriteAllText(folder.File("base64.txt"), Convert.ToBase64String([.. new byte[before], .. Encoding.GetEncoding(encoding).GetBytes(Marker), 1, 2]));

        Assert.IsNotEmpty(Scan.Of(folder.Path).Findings);
    }

    [TestMethod]
    public void The_name_of_a_file_or_of_a_folder_counts()
    {
        using var folder = new Disposable();
        File.WriteAllText(folder.File($"{Marker}.md"), "rien");
        Directory.CreateDirectory(Path.Combine(folder.Path, "dossier", Marker.ToUpperInvariant()));

        Assert.HasCount(2, Scan.Of(folder.Path).Findings);
    }

    [TestMethod]
    public void A_hidden_file_deep_in_the_tree_is_read()
    {
        using var folder = new Disposable();
        var deep = Path.Combine(folder.Path, "a", "b", "c");
        Directory.CreateDirectory(deep);
        var file = Path.Combine(deep, "cache");
        File.WriteAllText(file, Marker);
        File.SetAttributes(file, FileAttributes.Hidden | FileAttributes.System);

        var result = Scan.Of(folder.Path);

        Assert.IsNotEmpty(result.Findings);
        File.SetAttributes(file, FileAttributes.Normal);
    }

    [TestMethod]
    public void A_clean_folder_is_walked_and_finds_nothing()
    {
        using var folder = new Disposable();
        File.WriteAllText(folder.File("un.txt"), "kyfmarque sans la suite");
        File.WriteAllBytes(folder.File("deux.bin"), Noise);

        var result = Scan.Of(folder.Path);

        Assert.HasCount(2, result.Files);
        Assert.IsEmpty(result.Skipped);
        Assert.IsEmpty(result.Findings);
    }

    [TestMethod]
    public void A_file_held_by_another_program_is_named_not_ignored()
    {
        using var folder = new Disposable();
        var held = folder.File("tenu.lock");
        using var stream = new FileStream(held, FileMode.Create, FileAccess.Write, FileShare.None);

        var result = Scan.Of(folder.Path);

        Assert.AreEqual("tenu.lock", result.Skipped.Single());
    }

    private sealed class Disposable : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("kyfrev-scan-").FullName;

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
