using System.IO;
using System.Security.Cryptography;
using KyfRev.Application;
using KyfRev.Application.Ports;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le catalogue dans la carte « Extensions » : lu à la demande, montré vérifié, installé après confirmation.</summary>
[TestClass]
public sealed class CatalogueSettingsTests
{
    [STATestMethod]
    public void A_verified_catalogue_lists_its_extensions_and_installs_one_after_confirmation()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        Assert.IsTrue(scene.Page.CanReadCatalogue);
        Assert.IsFalse(scene.Page.HasCatalogue, "rien n'est lu tant qu'on ne le demande pas");
        Assert.IsEmpty(scene.Online.Asked);

        Run(scene.Page.ReadCatalogueCommand.ExecuteAsync(null));

        Assert.AreEqual("Catalogue n° 4 du 6 novembre 2026, signé (clé « essai »).", scene.Page.CatalogueStatus);
        var quiz = scene.Page.CatalogueRows.Single(row => row.Entry.Id == "quiz");
        Assert.HasCount(4, scene.Page.CatalogueRows);
        Assert.AreEqual(("Quiz", "version 2.0.0", ModulesSettingsText.CatalogueInstall, "Installer Quiz"), (quiz.Name, quiz.Version, quiz.Action, quiz.Spoken));
        Assert.IsTrue(quiz.HasSummary);
        StringAssert.StartsWith(quiz.Permissions, "Demande : ", StringComparison.Ordinal);
        Assert.IsTrue(quiz.CanInstall);

        Run(quiz.InstallCommand.ExecuteAsync(null));
        Assert.IsTrue(scene.Page.IsConfirmingInstall, "rien ne s'installe sans montrer ce qu'elle demande");
        Assert.AreEqual(ModulesSettingsText.ExtensionInstallTitle, scene.Page.InstallTitle);
        Run(scene.Page.AnswerInstallCommand.ExecuteAsync(true));

        Assert.AreEqual("2.0.0", scene.Library.Installed().Single().Manifest.Version);
        Assert.IsNull(scene.Library.Source("quiz"));
        Assert.IsTrue(quiz.IsUpToDate);
        Assert.IsFalse(quiz.CanInstall);
        Assert.IsFalse(Directory.Exists(scene.Downloads) && Directory.EnumerateFileSystemEntries(scene.Downloads).Any(), "le dossier téléchargé est rangé");
        Assert.AreEqual(4, scene.Library.CatalogueNumber);
    }

    [STATestMethod]
    public void A_refused_download_or_answer_installs_nothing_and_keeps_nothing()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        Run(scene.Page.ReadCatalogueCommand.ExecuteAsync(null));
        var bilan = scene.Page.CatalogueRows.Single(row => row.Entry.Id == "bilan");

        scene.Online.Altered = "extension.js";
        Run(bilan.InstallCommand.ExecuteAsync(null));
        StringAssert.Contains(scene.Page.Error, "ne correspond pas au catalogue", StringComparison.Ordinal);
        Assert.IsFalse(scene.Page.IsConfirmingInstall);

        scene.Online.Altered = null;
        Run(bilan.InstallCommand.ExecuteAsync(null));
        Assert.IsTrue(scene.Page.IsConfirmingInstall);
        Run(scene.Page.AnswerInstallCommand.ExecuteAsync(false));

        Assert.IsEmpty(scene.Library.Installed());
        Assert.IsFalse(Directory.Exists(scene.Downloads) && Directory.EnumerateFileSystemEntries(scene.Downloads).Any());
    }

    [STATestMethod]
    public void An_older_or_unreachable_catalogue_says_why_and_shows_nothing()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.Library.SetCatalogueNumber(9);

        Run(scene.Page.ReadCatalogueCommand.ExecuteAsync(null));
        StringAssert.Contains(scene.Page.Error, "plus ancien", StringComparison.Ordinal);
        Assert.IsFalse(scene.Page.HasCatalogue);
        Assert.IsEmpty(scene.Page.CatalogueRows);

        scene.Online.Fails = true;
        Run(scene.Page.ReadCatalogueCommand.ExecuteAsync(null));
        StringAssert.Contains(scene.Page.Error, "injoignable", StringComparison.Ordinal);
        Assert.IsFalse(new ExtensionSettingsViewModel(scene.Library, new FakePicker()).CanReadCatalogue);
    }

    private static void Run(Task task) => UiThread.Until(() => task.IsCompleted);

    private sealed class Scene : IDisposable
    {
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly string _root = Path.Combine(Path.GetTempPath(), "kyfrev-catalogue-" + Guid.NewGuid().ToString("N"));

        public Scene()
        {
            Library = new ExtensionLibrary(Path.Combine(_root, "bibliotheque"), TimeProvider.System);
            var catalogue = ExtensionCatalogue.Write(4, new DateOnly(2026, 11, 6), Directory.EnumerateDirectories(Path.Combine(Repository.Root, "extensions")).Select(folder => ExtensionCatalogue.EntryOf(folder).Value));
            Online.Catalogue = catalogue;
            Online.Signature = ExtensionCatalogue.Sign(catalogue, _key, "essai");
            Page = new ExtensionSettingsViewModel(Library, new FakePicker())
            {
                Catalogue = new CatalogueReader(Online, Library, [new CatalogueKey("essai", _key.ExportSubjectPublicKeyInfoPem())], Downloads),
            };
        }

        public string Downloads => Path.Combine(_root, "telechargements");

        public ExtensionLibrary Library { get; }

        public Online Online { get; } = new();

        public ExtensionSettingsViewModel Page { get; }

        public void Dispose()
        {
            _key.Dispose();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }

    /// <summary>Le dépôt en ligne simulé : le catalogue signé, et les extensions publiées.</summary>
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
                throw new Unreachable("raw.githubusercontent.com injoignable : délai dépassé.");
            }
            var path = address.AbsolutePath["/KyllianFF/kyfRev-dotnet/main/".Length..];
            if (path.StartsWith("catalogue/", StringComparison.Ordinal))
            {
                return path.EndsWith(".sig", StringComparison.Ordinal) ? Signature : Catalogue;
            }
            var content = await File.ReadAllBytesAsync(Path.Combine(Repository.Root, path.Replace('/', Path.DirectorySeparatorChar)), cancellationToken);
            return path.EndsWith("/" + Altered, StringComparison.Ordinal) ? [.. content, 0x20] : content;
        }
    }

    private sealed class Unreachable : OperationException
    {
        public Unreachable()
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
