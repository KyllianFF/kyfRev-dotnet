using System.Text;
using KyfRev.Application.Ports;

namespace KyfRev.Application.Tests;

/// <summary>
/// Ce que le port du stockage d'un coffre fait de lui-même, pour tout stockage : l'existence, les lectures par défaut, une
/// lecture qui échoue, une sauvegarde lue telle quelle, et les éléments d'un dossier.
/// </summary>
[TestClass]
public sealed class VaultStorageTests
{
    [TestMethod]
    public void A_storage_reads_text_like_the_disk_and_tolerates_what_it_cannot_read()
    {
        var storage = new OneFileStorage([.. Encoding.Unicode.Preamble, .. Encoding.Unicode.GetBytes("é")]);

        Assert.AreEqual("é", storage.ReadAllText(OneFileStorage.File));
        CollectionAssert.AreEqual(storage.ReadAllBytes(OneFileStorage.File), storage.ReadShared(OneFileStorage.File));
        Assert.IsNull(storage.TryReadAllBytes("absent"));
        Assert.IsTrue(storage.Exists(OneFileStorage.File));
        Assert.IsTrue(storage.Exists(OneFileStorage.Folder));
        Assert.IsFalse(storage.Exists("absent"));
        Assert.IsNull(storage.Root);
        Assert.AreSame(storage, storage.Raw);
        StoredFile[] entries = [new("a.md", 1, () => [1])];
        Assert.AreSame(entries, storage.FilesOfBackup(entries, "sauvegarde.zip"));
        string[] raw = [OneFileStorage.File, "dossier/autre"];
        Assert.AreSame(raw, storage.FilesToBackUp(raw), "tout ce qui est posé sur le disque se sauvegarde");

        storage.Reload();
        Assert.IsTrue(storage.Exists(OneFileStorage.File), "rien à relire");
    }

    [TestMethod]
    public void What_stays_on_the_disk_after_a_purge_is_an_input_output_error_of_its_own()
    {
        Assert.IsInstanceOfType<IOException>(new LeftOnDiskException());
        Assert.Contains("restée sur le disque", new LeftOnDiskException().Message);
        Assert.AreEqual("objets/A.kyf", new LeftOnDiskException("objets/A.kyf").Message);
        var cause = new IOException("tenu ouvert");
        Assert.AreSame(cause, new LeftOnDiskException("objets/A.kyf", cause).InnerException);
    }

    [TestMethod]
    public void An_entry_reads_its_size_and_date_once_and_only_when_asked()
    {
        var reads = 0;
        var date = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        var file = new StorageEntry(Path.Combine("coffre", "a.md"), isDirectory: false, () =>
        {
            reads++;
            return (3, date);
        });

        Assert.AreEqual(0, reads);
        Assert.AreEqual("a.md", file.Name);
        Assert.AreEqual(3, file.Length);
        Assert.AreEqual(date, file.LastWriteTimeUtc);
        Assert.AreEqual(1, reads);
        Assert.IsFalse(file.IsLink);
        Assert.IsFalse(file.IsReparsePoint);

        var folder = new StorageEntry(Path.Combine("coffre", "fiches"), isDirectory: true, () => throw new InvalidOperationException());
        Assert.AreEqual(0, folder.Length);
        Assert.IsTrue(folder.IsDirectory);

        var found = new StorageEntry(Path.Combine("coffre", "b.md"), "b.md", isDirectory: false, 5, date, isReparsePoint: true);
        Assert.AreEqual((5L, date, true), (found.Length, found.LastWriteTimeUtc, found.IsReparsePoint));
    }

    [TestMethod]
    public void An_entry_of_the_disk_keeps_what_the_listing_gave()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kyfrev-tests", Guid.NewGuid().ToString("N")));
        try
        {
            File.WriteAllBytes(Path.Combine(folder.FullName, "a.md"), [1, 2]);
            var entries = folder.EnumerateFileSystemInfos().Select(info => new StorageEntry(info)).ToList();

            var file = Assert.ContainsSingle(entries);
            Assert.AreEqual(("a.md", 2L, false, false), (file.Name, file.Length, file.IsDirectory, file.IsLink));
            Assert.AreEqual(new StorageEntry(folder).Name, folder.Name);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>Un stockage d'un dossier et d'un fichier, qui ne fait que lire.</summary>
    private sealed class OneFileStorage(byte[] content) : VaultStorage
    {
        public const string Folder = "dossier";
        public const string File = "dossier/fichier";

        public override bool FileExists(string path) => path == File;

        public override bool DirectoryExists(string path) => path == Folder;

        public override byte[] ReadAllBytes(string path) => path == File ? content : throw new FileNotFoundException(path);

        public override long LengthOf(string path) => content.Length;

        public override DateTime LastWriteTimeUtc(string path) => DateTime.UnixEpoch;

        public override bool TryCreateNew(string path, ReadOnlySpan<byte> content) => throw new NotSupportedException();

        public override void Replace(string path, ReadOnlySpan<byte> content) => throw new NotSupportedException();

        public override void CreateDirectory(string path) => throw new NotSupportedException();

        public override void MoveFile(string source, string destination) => throw new NotSupportedException();

        public override void MoveDirectory(string source, string destination) => throw new NotSupportedException();

        public override void Purge(string path) => throw new NotSupportedException();

        public override IReadOnlyList<StorageEntry> Entries(string folder) => [new(File, isDirectory: false)];

        public override IReadOnlyList<string> Files(string folder, string pattern) => [File];

        public override IReadOnlyList<StorageEntry> AllEntries(string folder) => Entries(folder);

        public override string RealPath(string path) => path;

        public override string RealPathBelow(string realBase, string relative) => Path.Combine(realBase, relative);

        public override bool SameEntry(string first, string second) => first == second;
    }
}
