using System.Text;
using KyfRev.Application.Ports;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Tests.Support;

namespace KyfRev.Infrastructure.Crypto.Tests;

/// <summary>
/// Le stockage d'un coffre chiffré se comporte comme le disque : mêmes résultats, mêmes erreurs, mêmes fichiers. Une
/// écriture coupée à n'importe quelle étape ne laisse jamais l'index en mémoire différent de celui du disque, et une
/// sauvegarde chiffrée se relit par son index.
/// </summary>
[TestClass]
public sealed class EncryptedStorageTests
{
    private const string VaultId = "v_01M69JFV2A3WD8KH55EHWC1G7N";

    /// <summary>Un en-tête et sa clé : des réglages faibles, que seuls les essais acceptent ; la clé de données ne dépend pas d'eux.</summary>
    private static readonly Lazy<(VaultHeader Header, byte[] Key)> Shared = new(() =>
    {
        var parameters = new PasswordParameters(PasswordKeys.Pbkdf2, 0, 1, new ImmutableBytes(new byte[PasswordKeys.SaltBytes]));
        var (header, key, _) = VaultHeader.Create(VaultId, "un mot de passe d'essai", parameters, enforcesStrength: false);
        return (header, key);
    });

    private static readonly byte[] One = "un"u8.ToArray();
    private static readonly byte[] Two = "deux"u8.ToArray();

    [TestMethod]
    public void Every_operation_gives_what_the_disk_gives()
    {
        using var pair = new Pair();

        pair.Same("un dossier", (s, p) => s.CreateDirectory(p("fiches/Cours")));
        pair.Same("un dossier qui existe", (s, p) => s.CreateDirectory(p("fiches/Cours")));
        pair.Same("la racine", (s, p) => s.CreateDirectory(p(string.Empty)));
        pair.Same("une création", (s, p) => s.TryCreateNew(p("fiches/Cours/a.md"), One));
        pair.Same("une création par-dessus un fichier", (s, p) => s.TryCreateNew(p("fiches/Cours/a.md"), Two));
        pair.Same("une création par-dessus un dossier", (s, p) => s.TryCreateNew(p("fiches/Cours"), Two));
        pair.Same("une création sans dossier", (s, p) => s.TryCreateNew(p("absent/x.md"), One));
        pair.Same("un remplacement", (s, p) => s.Replace(p("fiches/Cours/a.md"), Two));
        pair.Same("un remplacement qui crée", (s, p) => s.Replace(p("fiches/Cours/c.md"), One));
        pair.Same("un remplacement sans dossier", (s, p) => s.Replace(p("absent/x.md"), One));
        pair.Same("un dossier sous un fichier", (s, p) => s.CreateDirectory(p("fiches/Cours/a.md/sous")));
        pair.Same("une lecture", (s, p) => s.ReadAllBytes(p("fiches/Cours/a.md")));
        pair.Same("une lecture partagée", (s, p) => s.ReadShared(p("fiches/Cours/a.md")));
        pair.Same("une lecture sans changer la casse", (s, p) => s.ReadAllBytes(p("FICHES/cours/A.MD")));
        pair.Same("une lecture d'un absent", (s, p) => s.ReadAllBytes(p("fiches/absent.md")));
        pair.Same("une lecture sans dossier", (s, p) => s.ReadAllBytes(p("absent/x.md")));
        pair.Same("une lecture d'un dossier", (s, p) => s.ReadAllBytes(p("fiches/Cours")));
        pair.Same("une taille", (s, p) => s.LengthOf(p("fiches/Cours/a.md")));
        pair.Same("la taille d'un absent", (s, p) => s.LengthOf(p("fiches/absent.md")));
        pair.Same("la taille d'un dossier", (s, p) => s.LengthOf(p("fiches/Cours")));
        pair.Same("la date d'un absent", (s, p) => s.LastWriteTimeUtc(p("fiches/absent.md")));
        pair.Same("des existences", (s, p) => string.Join(' ',
            s.FileExists(p("fiches/Cours/a.md")), s.FileExists(p("fiches/Cours")), s.FileExists(p("absent")), s.FileExists(p(string.Empty)),
            s.DirectoryExists(p("fiches/Cours")), s.DirectoryExists(p("fiches/Cours/a.md")), s.DirectoryExists(p(string.Empty)),
            s.Exists(p("fiches")), s.Exists(p("absent"))));
        pair.Same("les mêmes éléments", (s, p) => string.Join(' ',
            s.SameEntry(p("fiches/Cours/a.md"), p("FICHES/cours/A.md")), s.SameEntry(p("fiches/Cours/a.md"), p("fiches/Cours/c.md")),
            s.SameEntry(p("absent"), p("absent")), s.SameEntry(p("fiches/Cours"), p("fiches/cours"))));
        pair.Same("un déplacement", (s, p) => s.MoveFile(p("fiches/Cours/c.md"), p("fiches/b.md")));
        pair.Same("un déplacement par-dessus un fichier", (s, p) => s.MoveFile(p("fiches/b.md"), p("fiches/Cours/a.md")));
        pair.Same("un déplacement par-dessus un dossier", (s, p) => s.MoveFile(p("fiches/b.md"), p("fiches/Cours")));
        pair.Same("un déplacement d'un absent", (s, p) => s.MoveFile(p("fiches/absent.md"), p("fiches/x.md")));
        pair.Same("un déplacement sans dossier", (s, p) => s.MoveFile(p("fiches/b.md"), p("absent/x.md")));
        pair.Same("un changement de casse", (s, p) => s.MoveFile(p("fiches/b.md"), p("fiches/B.md")));
        pair.Same("un dossier déplacé", (s, p) => s.MoveDirectory(p("fiches/Cours"), p("fiches/Leçons")));
        pair.Same("un dossier dans lui-même", (s, p) => s.MoveDirectory(p("fiches/Leçons"), p("fiches/Leçons/Sous")));
        pair.Same("un dossier par-dessus un dossier", (s, p) => s.MoveDirectory(p("fiches/Leçons"), p("fiches")));
        pair.Same("un dossier par-dessus un fichier", (s, p) => s.MoveDirectory(p("fiches/Leçons"), p("fiches/B.md")));
        pair.Same("un dossier absent", (s, p) => s.MoveDirectory(p("absent"), p("ailleurs")));
        pair.Same("un dossier vers un dossier absent", (s, p) => s.MoveDirectory(p("fiches/Leçons"), p("absent/Leçons")));
        pair.Same("la casse d'un dossier", (s, p) => s.MoveDirectory(p("fiches/Leçons"), p("fiches/leçons")));
        pair.Same("un fichier de plus", (s, p) => s.Replace(p("fiches/leçons/Données.json"), "{}"u8));
        pair.Same("les fichiers d'un motif", (s, p) => s.Files(p("fiches/leçons"), "*.json").Select(Path.GetFileName));
        pair.Same("tous les fichiers", (s, p) => s.Files(p("fiches/leçons"), "*").Select(Path.GetFileName).Order(StringComparer.Ordinal));
        pair.Same("le contenu d'un dossier", (s, p) => Described(s.Entries(p("fiches"))));
        pair.Same("le contenu d'un dossier absent", (s, p) => s.Entries(p("absent")));
        pair.Same("tout le contenu", (s, p) => Described(s.AllEntries(p(string.Empty)), p(string.Empty)));
        pair.Same("l'effacement d'un absent", (s, p) => s.Purge(p("fiches/absent.md")));
        pair.Same("l'effacement sans dossier", (s, p) => s.Purge(p("absent/x.md")));
        pair.Same("un effacement", (s, p) => s.Purge(p("fiches/B.md")));
        pair.Same("un dossier effacé", (s, p) => s.Purge(p("fiches/leçons")));
        pair.Same("un dossier absent effacé", (s, p) => s.Purge(p("fiches/leçons")));
        pair.Same("un texte annoncé par sa marque", (s, p) =>
        {
            s.Replace(p("fiches/utf16.txt"), [.. Encoding.Unicode.Preamble, .. Encoding.Unicode.GetBytes("é")]);
            s.Replace(p("fiches/utf8.txt"), [.. Encoding.UTF8.Preamble, .. "è"u8]);
            return s.ReadAllText(p("fiches/utf16.txt")) + s.ReadAllText(p("fiches/utf8.txt"));
        });
        pair.SameTree();
    }

    [TestMethod]
    public void Replacing_a_folder_is_refused_like_on_the_disk()
    {
        using var pair = new Pair();
        pair.Same("un dossier", (s, p) => s.CreateDirectory(p("fiches")));
        pair.Same("un dossier remplacé", (s, p) => s.Replace(p("fiches"), One));
    }

    [TestMethod]
    public void A_path_outside_the_vault_does_not_exist_and_cannot_be_written()
    {
        using var folder = new TemporaryFolder();
        using var storage = NewStorage(folder.Root);
        var outside = Path.Combine(Path.GetDirectoryName(folder.Root)!, "ailleurs.md");

        Assert.IsFalse(storage.FileExists(outside));
        Assert.IsFalse(storage.DirectoryExists(outside));
        Assert.IsFalse(storage.FileExists(Path.Combine(folder.Root, "a.md:flux")));
        Assert.AreEqual(EncryptedVault.Missing, storage.LastWriteTimeUtc(outside));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => storage.Replace(outside, One));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => storage.Replace(folder.Root, One));
        Assert.ThrowsExactly<IOException>(() => storage.Purge(folder.Root));
        storage.CreateDirectory(Path.Combine(folder.Root, "fiches"));
        Assert.ThrowsExactly<IOException>(() => storage.MoveDirectory(Path.Combine(folder.Root, "fiches"), folder.Root));
        Assert.AreEqual(folder.Root, storage.Root);
        Assert.AreEqual(VaultId, storage.VaultId);
        Assert.AreSame(DiskStorage.Instance, storage.Raw);
        Assert.AreEqual(Path.Combine(folder.Root, "x"), storage.RealPath(Path.Combine(folder.Root, "x")));
        Assert.AreEqual(Path.Combine(folder.Root, "a", "b"), storage.RealPathBelow(folder.Root, Path.Combine("a", "b")));
    }

    [TestMethod]
    public void Nothing_on_the_disk_names_a_file_and_an_altered_object_is_an_error()
    {
        using var folder = new TemporaryFolder();
        using (var storage = NewStorage(folder.Root))
        {
            storage.CreateDirectory(Path.Combine(folder.Root, "fiches"));
            storage.Replace(Path.Combine(folder.Root, "fiches", "Révolution.md"), "Révolution"u8);
        }
        var names = Directory.EnumerateFileSystemEntries(folder.Root, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(folder.Root, path)).ToList();
        Assert.IsTrue(names.All(name => name is EncryptedVault.HeaderFile or EncryptedVault.IndexFile or EncryptedVault.Objects || name.StartsWith(EncryptedVault.Objects + Path.DirectorySeparatorChar, StringComparison.Ordinal)), string.Join(", ", names));

        var objet = Directory.GetFiles(Path.Combine(folder.Root, EncryptedVault.Objects)).Single();
        var bytes = File.ReadAllBytes(objet);
        bytes[^1] ^= 1;
        File.WriteAllBytes(objet, bytes);
        using var reopened = EncryptedStorage.Open(folder.Root, Shared.Value.Key, DiskStorage.Instance);
        var sheet = Path.Combine(folder.Root, "fiches", "Révolution.md");
        Assert.AreEqual(11, reopened.LengthOf(sheet), "la taille en clair, en octets");
        Assert.Contains("ne s'ouvre pas", Assert.ThrowsExactly<IOException>(() => reopened.ReadAllBytes(sheet)).Message);
        File.Delete(objet);
        Assert.AreEqual(EncryptedVault.Missing, reopened.AllEntries(folder.Root).Single(entry => !entry.IsDirectory).LastWriteTimeUtc, "un objet effacé n'a plus de date");
    }

    [TestMethod]
    public void A_write_cut_at_any_step_leaves_the_index_as_the_disk_holds_it()
    {
        var writes = Cut(failAt: null, cut: false);
        Assert.IsGreaterThan(10, writes, "chaque sorte d'écriture compte");
        for (var failAt = 1; failAt <= writes; failAt++)
        {
            Cut(failAt, cut: false);
            Cut(failAt, cut: true);
        }
    }

    [TestMethod]
    public void A_backup_reads_through_its_index_and_refuses_another_vault()
    {
        using var folder = new TemporaryFolder();
        using var storage = NewStorage(folder.Root);
        storage.CreateDirectory(Path.Combine(folder.Root, "fiches"));
        storage.Replace(Path.Combine(folder.Root, "fiches", "a.md"), One);
        storage.Replace(Path.Combine(folder.Root, "b.md"), Two);
        var raw = RawFiles(folder.Root);

        var files = storage.FilesOfBackup(raw, "sauvegarde.zip");
        Assert.AreEqual("b.md:4 fiches/a.md:2", string.Join(' ', files.Select(file => $"{file.Path}:{file.Length}")));
        CollectionAssert.AreEqual(One, files.Single(file => file.Path == "fiches/a.md").Read());

        // Une sauvegarde faite avant le chiffrement se lit telle quelle.
        StoredFile[] plain = [new("fiches/clair.md", 1, () => [1])];
        Assert.AreSame(plain, storage.FilesOfBackup(plain, "ancienne.zip"));

        Assert.ThrowsExactly<IOException>(() => storage.FilesOfBackup([.. raw.Where(file => file.Path != EncryptedVault.IndexFile)], "sans-index.zip"));
        Assert.ThrowsExactly<IOException>(() => storage.FilesOfBackup([.. raw.Where(file => !file.Path.StartsWith(EncryptedVault.Objects, StringComparison.Ordinal) || file == raw[^1])], "incomplete.zip"));
        var altered = raw.Select(file => file.Path.StartsWith(EncryptedVault.Objects, StringComparison.Ordinal) ? file with { Read = () => [.. file.Read()[..^1], 0] } : file).ToList();
        Assert.ThrowsExactly<IOException>(() => storage.FilesOfBackup(altered, "alteree.zip")[0].Read());

        using var other = new TemporaryFolder();
        using var otherStorage = EncryptedStorage.Create(other.Root, Shared.Value.Header, new byte[32], DiskStorage.Instance);
        Assert.Contains("autre coffre", Assert.ThrowsExactly<IOException>(() => otherStorage.FilesOfBackup(raw, "autre.zip")).Message);
    }

    /// <summary>
    /// Joue une suite d'écritures sur un coffre chiffré dont l'écriture <paramref name="failAt"/> échoue (et, coupée,
    /// toutes les suivantes) ; vérifie après chacune que l'index en mémoire est celui du disque. Rend le nombre d'écritures.
    /// </summary>
    private static int Cut(int? failAt, bool cut)
    {
        using var folder = new TemporaryFolder();
        var disk = new FailingDisk();
        using var storage = EncryptedStorage.Create(folder.Root, Shared.Value.Header, Shared.Value.Key, disk);
        var start = disk.Writes;
        disk.FailAt = start + failAt;
        disk.Cut = cut;
        string P(string relative) => Path.Combine([folder.Root, .. relative.Split('/')]);
        Action[] steps =
        [
            () => storage.CreateDirectory(P("fiches/Cours")),
            () => storage.Replace(P("fiches/Cours/a.md"), One),
            () => storage.Replace(P("fiches/Cours/a.md"), Two),
            () => storage.TryCreateNew(P("fiches/b.md"), One),
            () => storage.MoveFile(P("fiches/b.md"), P("fiches/Cours/b.md")),
            () => storage.CreateDirectory(P("fiches/Vide/Sous")),
            () => storage.MoveDirectory(P("fiches/Cours"), P("fiches/Leçons")),
            () => storage.Purge(P("fiches/Leçons/a.md")),
            () => storage.Replace(P("fiches/Vide/c.md"), One),
            () => storage.Purge(P("fiches/Vide")),
            () => storage.Replace(P("racine.md"), Two),
        ];
        foreach (var step in steps)
        {
            try
            {
                step();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Assert.IsGreaterThan(0, disk.Failures, $"seule la coupure fait échouer ({exception.Message})");
            }
            Assert.AreEqual(Tree(storage, folder.Root), Reopened(folder.Root), $"écriture {failAt} coupée{(cut ? " net" : string.Empty)}");
        }
        return disk.Writes - start;
    }

    private static string Reopened(string root)
    {
        using var reopened = EncryptedStorage.Open(root, Shared.Value.Key, DiskStorage.Instance);
        return Tree(reopened, root);
    }

    /// <summary>Chaque dossier et chaque fichier du coffre, avec son contenu.</summary>
    private static string Tree(EncryptedStorage storage, string root) => string.Join('\n', storage.AllEntries(root)
        .Select(entry => Path.GetRelativePath(root, entry.FullName) + (entry.IsDirectory ? "/" : "=" + Convert.ToHexString(storage.ReadAllBytes(entry.FullName))))
        .Order(StringComparer.Ordinal));

    private static List<StoredFile> RawFiles(string root) =>
    [
        .. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => new StoredFile(Path.GetRelativePath(root, file).Replace('\\', '/'), new FileInfo(file).Length, () => File.ReadAllBytes(file)))
            .OrderBy(file => file.Path, StringComparer.Ordinal),
    ];

    private static string Described(IEnumerable<StorageEntry> entries, string? root = null) => string.Join(' ', entries
        .Select(entry => $"{(root is null ? entry.Name : Path.GetRelativePath(root, entry.FullName))}{(entry.IsDirectory ? "/" : $":{entry.Length}")}")
        .Order(StringComparer.Ordinal));

    private static EncryptedStorage NewStorage(string root) => EncryptedStorage.Create(root, Shared.Value.Header, Shared.Value.Key, DiskStorage.Instance);

    /// <summary>Un dossier sur le disque et un coffre chiffré, où chaque opération est jouée deux fois.</summary>
    private sealed class Pair : IDisposable
    {
        private readonly TemporaryFolder _disk = new();
        private readonly TemporaryFolder _encrypted = new();
        private readonly EncryptedStorage _storage;

        public Pair()
        {
            _storage = NewStorage(_encrypted.Root);
        }

        public void Same(string what, Action<VaultStorage, Func<string, string>> action) =>
            Same(what, (storage, path) =>
            {
                action(storage, path);
                return "fait";
            });

        public void Same(string what, Func<VaultStorage, Func<string, string>, object?> action)
        {
            var onDisk = Outcome(DiskStorage.Instance, _disk.Root, action);
            var encrypted = Outcome(_storage, _encrypted.Root, action);
            Assert.AreEqual(onDisk, encrypted, what);
        }

        /// <summary>Les mêmes dossiers et les mêmes fichiers, octet pour octet.</summary>
        public void SameTree()
        {
            var onDisk = string.Join('\n', Directory.EnumerateFileSystemEntries(_disk.Root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(_disk.Root, path) + (Directory.Exists(path) ? "/" : "=" + Convert.ToHexString(File.ReadAllBytes(path))))
                .Order(StringComparer.Ordinal));
            Assert.AreEqual(onDisk, Tree(_storage, _encrypted.Root));
            Assert.AreEqual(onDisk, Reopened(_encrypted.Root), "relu depuis le disque");
        }

        public void Dispose()
        {
            _storage.Dispose();
            _disk.Dispose();
            _encrypted.Dispose();
        }

        private static string Outcome(VaultStorage storage, string root, Func<VaultStorage, Func<string, string>, object?> action)
        {
            try
            {
                return action(storage, relative => Path.Combine([root, .. relative.Split('/', StringSplitOptions.RemoveEmptyEntries)])) switch
                {
                    byte[] bytes => Convert.ToHexString(bytes),
                    IEnumerable<string> names => string.Join(' ', names),
                    DateTime date => date.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    var value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "rien",
                };
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return exception.GetType().Name;
            }
        }
    }

    private sealed class TemporaryFolder : IDisposable
    {
        public string Root { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kyfrev-tests", Guid.NewGuid().ToString("N"))).FullName;

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
