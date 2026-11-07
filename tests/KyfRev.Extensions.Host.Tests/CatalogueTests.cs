using System.Security.Cryptography;
using System.Text;
using KyfRev.Application;
using KyfRev.Application.Ports;

namespace KyfRev.Extensions.Host.Tests;

/// <summary>Le catalogue signé : sa signature, ses refus, ses empreintes, son lecteur en ligne.</summary>
[TestClass]
public sealed class CatalogueTests
{
    private static readonly DateOnly Day = new(2026, 11, 6);

    private static string Root
    {
        get
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            {
                if (File.Exists(Path.Combine(folder.FullName, "KyfRev.slnx")))
                {
                    return folder.FullName;
                }
            }
            throw new InvalidOperationException("Dépôt introuvable.");
        }
    }

    private static string Official => Path.Combine(Root, "extensions");

    [TestMethod]
    public void A_signed_catalogue_is_taken_with_its_extensions()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var entries = OfficialEntries();
        var catalogue = ExtensionCatalogue.Write(3, Day, entries);

        var verified = ExtensionCatalogue.Verify(catalogue, ExtensionCatalogue.Sign(catalogue, key, "essai"), Keys(key), lastSeen: 3);

        Assert.IsTrue(verified.Succeeded, verified.Error);
        Assert.AreEqual((3, Day, "essai"), (verified.Value.Number, verified.Value.PublishedOn, verified.Value.KeyId));
        CollectionAssert.AreEqual(entries.Select(entry => entry.Id).Order(StringComparer.Ordinal).ToArray(), verified.Value.Extensions.Select(entry => entry.Id).ToArray());
        var quiz = verified.Value.Extensions.Single(entry => entry.Id == "quiz");
        Assert.AreEqual("Quiz", quiz.Name);
        Assert.Contains("fiches:lecture", quiz.Permissions);
        Assert.IsTrue(quiz.Files.Any(file => file.Path == "extension.json"));
        Assert.IsEmpty(ExtensionCatalogue.Differences(verified.Value, Official), "le catalogue décrit exactement les extensions publiées");
        CollectionAssert.AreEqual(catalogue, ExtensionCatalogue.Write(3, Day, entries.AsEnumerable().Reverse()), "les mêmes extensions s'écrivent toujours de la même façon");
    }

    [TestMethod]
    public void A_changed_byte_an_unknown_key_or_an_older_number_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var catalogue = ExtensionCatalogue.Write(5, Day, OfficialEntries());
        var signature = ExtensionCatalogue.Sign(catalogue, key, "essai");
        var changed = (byte[])catalogue.Clone();
        changed[^3] ^= 1;

        StringAssert.Contains(ExtensionCatalogue.Verify(changed, signature, Keys(key), null).Error, "ne correspond pas", StringComparison.Ordinal);
        StringAssert.Contains(ExtensionCatalogue.Verify(catalogue, signature, Keys(other), null).Error, "ne correspond pas", StringComparison.Ordinal);
        StringAssert.Contains(ExtensionCatalogue.Verify(catalogue, signature, [new CatalogueKey("autre", Public(key))], null).Error, "clé inconnue", StringComparison.Ordinal);
        StringAssert.Contains(ExtensionCatalogue.Verify(catalogue, signature, Keys(key), 6).Error, "plus ancien", StringComparison.Ordinal);
        StringAssert.Contains(ExtensionCatalogue.Verify(catalogue, "pas du json"u8.ToArray(), Keys(key), null).Error, "illisible", StringComparison.Ordinal);
        StringAssert.Contains(ExtensionCatalogue.Verify(catalogue, """{"cle":"essai","signature":"%%"}"""u8.ToArray(), Keys(key), null).Error, "illisible", StringComparison.Ordinal);
        StringAssert.Contains(ExtensionCatalogue.Verify(new byte[ExtensionCatalogue.MaxCatalogueBytes + 1], signature, Keys(key), null).Error, "trop gros", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("""{"format":2,"numero":1,"publieLe":"2026-11-06","extensions":[]}""")]
    [DataRow("""{"format":1,"numero":1,"publieLe":"6 novembre","extensions":[]}""")]
    [DataRow("""{"format":1,"numero":1,"publieLe":"2026-11-06"}""")]
    [DataRow("""{"format":1,"numero":1,"publieLe":"2026-11-06","extensions":[{"id":"Mauvais Id","nom":"x","version":"1","permissions":[],"fichiers":[{"chemin":"a.js","taille":1,"sha256":"0000000000000000000000000000000000000000000000000000000000000000"}]}]}""")]
    [DataRow("""{"format":1,"numero":1,"publieLe":"2026-11-06","extensions":[{"id":"bon","nom":"x","version":"1","permissions":[],"fichiers":[{"chemin":"../a.js","taille":1,"sha256":"0000000000000000000000000000000000000000000000000000000000000000"}]}]}""")]
    [DataRow("""{"format":1,"numero":1,"publieLe":"2026-11-06","extensions":[{"id":"bon","nom":"x","version":"1","permissions":[],"fichiers":[]}]}""")]
    [DataRow("""{"format":1,"numero":1,"publieLe":"2026-11-06","extensions":[{"id":"bon","nom":3,"version":"1","permissions":[],"fichiers":[]}]}""")]
    [DataRow("""[1, 2]""")]
    public void A_signed_but_malformed_catalogue_is_refused(string json)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var catalogue = Encoding.UTF8.GetBytes(json);

        StringAssert.Contains(ExtensionCatalogue.Verify(catalogue, ExtensionCatalogue.Sign(catalogue, key, "essai"), Keys(key), null).Error, "illisible", StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_file_matches_only_its_size_and_fingerprint()
    {
        var content = "kyfrev.vue(() => '');"u8.ToArray();
        var file = new CatalogueFile("extension.js", content.Length, ExtensionCatalogue.Hash(content));

        Assert.IsTrue(ExtensionCatalogue.Matches(file, content));
        Assert.IsFalse(ExtensionCatalogue.Matches(file, [.. content, 0x20]));
        Assert.IsFalse(ExtensionCatalogue.Matches(file with { Size = content.Length }, [.. content[..^1], 0x20]));
        Assert.AreEqual(64, file.Sha256.Length);
    }

    [TestMethod]
    public void A_changed_missing_or_extra_file_is_a_difference()
    {
        using var copy = new Copy();
        var catalogue = new Catalogue(1, Day, "essai", [.. OfficialEntries()]);
        File.AppendAllText(Path.Combine(copy.Path, "quiz", "extension.js"), "// changé");
        File.Delete(Path.Combine(copy.Path, "bilan", "extension.json"));
        File.WriteAllText(Path.Combine(copy.Path, "vocabulaire", "en-trop.js"), "1");
        Directory.Delete(Path.Combine(copy.Path, "chronologie"), recursive: true);

        var differences = ExtensionCatalogue.Differences(catalogue, copy.Path);

        Assert.Contains("quiz/extension.js : différent du catalogue", differences);
        Assert.Contains("bilan/extension.json : différent du catalogue", differences);
        Assert.Contains("vocabulaire : des fichiers ne sont pas dans le catalogue", differences);
        Assert.Contains("chronologie : dossier absent", differences);
    }

    [TestMethod]
    public void The_published_catalogue_if_any_is_signed_by_a_known_key_and_matches_the_extensions()
    {
        foreach (var known in ExtensionCatalogue.KnownKeys)
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(known.PublicKey);
            Assert.AreEqual(256, key.KeySize, known.Id);
        }
        var folder = Path.Combine(Root, "catalogue");
        if (!File.Exists(Path.Combine(folder, "catalogue.json")))
        {
            Assert.IsFalse(Directory.Exists(folder), "un dossier catalogue sans catalogue");
            return;
        }
        var verified = ExtensionCatalogue.Verify(File.ReadAllBytes(Path.Combine(folder, "catalogue.json")), File.ReadAllBytes(Path.Combine(folder, "catalogue.sig")), ExtensionCatalogue.KnownKeys, null);
        Assert.IsTrue(verified.Succeeded, verified.Error);
        Assert.IsEmpty(ExtensionCatalogue.Differences(verified.Value, Official), "republiez le catalogue : tools/catalogue/catalogue.cs publier");
    }

    [TestMethod]
    public async Task The_reader_takes_a_verified_catalogue_and_never_goes_back()
    {
        using var scene = new Scene();
        var first = await scene.Reader.ReadAsync(CancellationToken.None);
        Assert.IsTrue(first.Succeeded, first.Error);
        Assert.AreEqual(4, scene.Library.CatalogueNumber);
        Assert.AreEqual("https://raw.githubusercontent.com/KyllianFF/kyfRev-dotnet/main/catalogue/catalogue.json", scene.Online.Asked[0]);

        scene.Publish(3);
        StringAssert.Contains((await scene.Reader.ReadAsync(CancellationToken.None)).Error, "plus ancien", StringComparison.Ordinal);
        Assert.AreEqual(4, scene.Library.CatalogueNumber, "un catalogue refusé ne change rien");

        scene.Online.Fails = true;
        StringAssert.Contains((await scene.Reader.ReadAsync(CancellationToken.None)).Error, "injoignable", StringComparison.Ordinal);
        var without = new CatalogueReader(scene.Online, scene.Library, [], scene.Downloads);
        Assert.IsFalse(without.HasKeys);
        StringAssert.Contains((await without.ReadAsync(CancellationToken.None)).Error, "pas encore de clé", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task An_extension_downloads_file_by_file_and_installs_without_keeping_its_folder()
    {
        using var scene = new Scene();
        var quiz = (await scene.Reader.ReadAsync(CancellationToken.None)).Value.Extensions.Single(entry => entry.Id == "quiz");

        var downloaded = await scene.Reader.DownloadAsync(quiz, CancellationToken.None);

        Assert.IsTrue(downloaded.Succeeded, downloaded.Error);
        Assert.IsTrue(File.Exists(Path.Combine(downloaded.Value, "extension.json")));
        Assert.IsTrue(scene.Library.Read(downloaded.Value).Succeeded);
        var installed = scene.Library.InstallDownloaded(downloaded.Value);
        Assert.IsTrue(installed.Succeeded, installed.Error);
        Assert.IsNull(scene.Library.Source("quiz"), "un dossier téléchargé n'est pas retenu");
        scene.Reader.Discard(downloaded.Value);
        Assert.IsFalse(Directory.Exists(downloaded.Value));
        scene.Reader.Discard(downloaded.Value);
    }

    [TestMethod]
    public async Task A_file_that_differs_from_the_catalogue_stops_the_download_and_leaves_nothing()
    {
        using var scene = new Scene();
        var quiz = (await scene.Reader.ReadAsync(CancellationToken.None)).Value.Extensions.Single(entry => entry.Id == "quiz");
        scene.Online.Altered = "extension.js";

        var downloaded = await scene.Reader.DownloadAsync(quiz, CancellationToken.None);

        StringAssert.Contains(downloaded.Error, "ne correspond pas au catalogue", StringComparison.Ordinal);
        Assert.IsTrue(Empty(scene.Downloads), "rien n'est gardé");
        scene.Online.Altered = null;
        scene.Online.Fails = true;
        StringAssert.Contains((await scene.Reader.DownloadAsync(quiz, CancellationToken.None)).Error, "injoignable", StringComparison.Ordinal);
        Assert.IsTrue(Empty(scene.Downloads));
    }

    [TestMethod]
    public void Only_a_downloaded_folder_is_discarded()
    {
        using var scene = new Scene();
        var outside = Path.Combine(Path.GetTempPath(), "kyfrev-garde-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            scene.Reader.Discard(outside);
            Assert.IsTrue(Directory.Exists(outside), "rien ne s'efface hors du dossier des téléchargements");
        }
        finally
        {
            Directory.Delete(outside);
        }
    }

    [TestMethod]
    public void The_last_catalogue_number_is_kept_and_never_goes_back()
    {
        using var scene = new Scene();
        Assert.IsNull(scene.Library.CatalogueNumber);
        Assert.IsTrue(scene.Library.SetCatalogueNumber(7).Succeeded);
        Assert.IsTrue(scene.Library.SetCatalogueNumber(2).Succeeded);
        Assert.AreEqual(7, new ExtensionLibrary(scene.LibraryFolder, TimeProvider.System).CatalogueNumber);
    }

    private static bool Empty(string folder) => !Directory.Exists(folder) || !Directory.EnumerateFileSystemEntries(folder).Any();

    private static List<CatalogueEntry> OfficialEntries() =>
        [.. Directory.EnumerateDirectories(Official).Select(folder => ExtensionCatalogue.EntryOf(folder).Value)];

    private static CatalogueKey[] Keys(ECDsa key) => [new("essai", Public(key))];

    private static string Public(ECDsa key) => key.ExportSubjectPublicKeyInfoPem();

    /// <summary>Une copie des extensions publiées, à abîmer.</summary>
    private sealed class Copy : IDisposable
    {
        public Copy()
        {
            foreach (var file in Directory.EnumerateFiles(Official, "*", SearchOption.AllDirectories))
            {
                var target = System.IO.Path.Combine(Path, System.IO.Path.GetRelativePath(Official, file));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }

        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kyfrev-catalogue-" + Guid.NewGuid().ToString("N"));

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    /// <summary>Un dépôt en ligne simulé : le catalogue signé et les extensions publiées.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly string _root = Path.Combine(Path.GetTempPath(), "kyfrev-lecteur-" + Guid.NewGuid().ToString("N"));

        public Scene()
        {
            Library = new ExtensionLibrary(LibraryFolder, TimeProvider.System);
            Publish(4);
            Reader = new CatalogueReader(Online, Library, Keys(_key), Downloads);
        }

        public string LibraryFolder => Path.Combine(_root, "bibliotheque");

        public string Downloads => Path.Combine(_root, "telechargements");

        public ExtensionLibrary Library { get; }

        public Online Online { get; } = new();

        public CatalogueReader Reader { get; }

        public void Publish(int number)
        {
            var catalogue = ExtensionCatalogue.Write(number, Day, OfficialEntries());
            Online.Catalogue = catalogue;
            Online.Signature = ExtensionCatalogue.Sign(catalogue, _key, "essai");
        }

        public void Dispose()
        {
            _key.Dispose();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }

    /// <summary>Le lecteur en ligne, servi depuis le dossier des extensions publiées.</summary>
    private sealed class Online : IOnlineReader
    {
        public byte[] Catalogue { get; set; } = [];

        public byte[] Signature { get; set; } = [];

        public bool Fails { get; set; }

        public string? Altered { get; set; }

        public List<string> Asked { get; } = [];

        public Task<string> ReadTextAsync(Uri address, CancellationToken cancellationToken) => throw new NotSupportedException("le catalogue se lit en octets");

        public async Task<byte[]> ReadBytesAsync(Uri address, CancellationToken cancellationToken)
        {
            Asked.Add(address.ToString());
            if (Fails)
            {
                throw new Unreachable();
            }
            var path = address.AbsolutePath["/KyllianFF/kyfRev-dotnet/main/".Length..];
            if (path == "catalogue/catalogue.json")
            {
                return Catalogue;
            }
            if (path == "catalogue/catalogue.sig")
            {
                return Signature;
            }
            var content = await File.ReadAllBytesAsync(Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar)), cancellationToken);
            return path.EndsWith("/" + Altered, StringComparison.Ordinal) ? [.. content, 0x20] : content;
        }
    }

    private sealed class Unreachable : OperationException
    {
        public Unreachable()
            : base("raw.githubusercontent.com injoignable : délai dépassé.")
        {
        }

        public Unreachable(string message)
            : base(message)
        {
        }

        public Unreachable(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
