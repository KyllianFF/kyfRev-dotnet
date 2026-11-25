using System.IO;
using System.Text;

namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>
/// Les fichiers locaux (coffre retenu, préférences du poste, séance) s'écrivent d'un coup : un échec laisse l'ancien
/// contenu entier, jamais un fichier tronqué.
/// </summary>
[TestClass]
public sealed class LocalFileTests
{
    [TestMethod]
    public void A_file_is_replaced_whole_and_its_neighbour_does_not_stay()
    {
        using var folder = new Folder();
        var file = Path.Combine(folder.Path, "config-locale.json");

        LocalFile.Replace(file, "premier"u8);
        LocalFile.Replace(file, "second"u8);

        Assert.AreEqual("second", File.ReadAllText(file, Encoding.UTF8));
        Assert.IsFalse(File.Exists(file + LocalFile.PendingSuffix));
    }

    [TestMethod]
    public void A_write_that_fails_keeps_the_previous_content()
    {
        using var folder = new Folder();
        var file = Path.Combine(folder.Path, "seance.json");
        LocalFile.Replace(file, "la séance d'avant"u8);
        File.SetAttributes(file, FileAttributes.ReadOnly);
        try
        {
            Assert.ThrowsExactly<UnauthorizedAccessException>(() => LocalFile.Replace(file, "une autre séance, plus longue que la première"u8));
            Assert.AreEqual("la séance d'avant", File.ReadAllText(file, Encoding.UTF8), "l'ancien contenu, entier");
        }
        finally
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        // Le voisin laissé par l'échec, plus long, est repris par l'écriture suivante.
        LocalFile.Replace(file, "court"u8);
        Assert.AreEqual("court", File.ReadAllText(file, Encoding.UTF8));
        Assert.IsFalse(File.Exists(file + LocalFile.PendingSuffix));
    }

    private sealed class Folder : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kyfrev-fichier-local", Guid.NewGuid().ToString("N"))).FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
