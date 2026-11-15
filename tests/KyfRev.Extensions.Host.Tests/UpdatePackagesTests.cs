using System.Security.Cryptography;
using KyfRev.Application;
using KyfRev.Application.Ports;

namespace KyfRev.Extensions.Host.Tests;

/// <summary>
/// L'installateur d'une mise à jour : téléchargé, vérifié contre l'annonce signée, vérifié de nouveau au lancement, et ce
/// qu'il est devenu au démarrage suivant. Ni réseau ni installation : un faux téléchargement et un faux installateur.
/// </summary>
[TestClass]
public sealed class UpdatePackagesTests
{
    private static readonly byte[] Content = [10, 20, 30, 40, 50, 60];

    [TestMethod]
    public async Task The_installer_downloads_once_is_checked_then_reused()
    {
        using var folder = new Folder();
        var downloads = new Downloads(Content);
        var packages = new UpdatePackages(downloads, new Installer(), folder.Path);
        var file = Announced("1.2.0", Content);
        var seen = new List<long>();

        var first = await packages.DownloadAsync(file, new Inline(seen.Add), CancellationToken.None);
        var second = await packages.DownloadAsync(file, new Inline(seen.Add), CancellationToken.None);

        Assert.IsTrue(first.Succeeded, first.Error);
        Assert.AreEqual(Path.Combine(folder.Path, "kyfRev-1.2.0-installation.exe"), first.Value);
        Assert.AreEqual(first.Value, second.Value);
        CollectionAssert.AreEqual(Content, await File.ReadAllBytesAsync(first.Value));
        Assert.AreEqual(1, downloads.Calls, "un installateur déjà là et conforme ne se télécharge pas de nouveau");
        Assert.AreEqual((file.Address, file.Size), (downloads.Address, downloads.MaxBytes));
        Assert.AreEqual(Content.Length, seen[^1]);
        Assert.AreEqual(first.Value, packages.Find(file));
    }

    [TestMethod]
    public async Task A_file_that_differs_from_the_announcement_is_deleted_and_never_offered()
    {
        using var folder = new Folder();
        var packages = new UpdatePackages(new Downloads([9, 9, 9, 9, 9, 9]), new Installer(), folder.Path);
        var file = Announced("1.2.0", Content);

        var result = await packages.DownloadAsync(file, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.Contains("ne correspond pas", result.Error!);
        Assert.IsEmpty(Directory.GetFiles(folder.Path));
        Assert.IsNull(packages.Find(file));
    }

    [TestMethod]
    public async Task A_failed_download_says_why()
    {
        using var folder = new Folder();
        var packages = new UpdatePackages(new Downloads(Content) { Fails = true }, new Installer(), folder.Path);

        var result = await packages.DownloadAsync(Announced("1.2.0", Content), null, CancellationToken.None);

        Assert.AreEqual("Le téléchargement n'a pas abouti (réseau coupé).", result.Error);
    }

    [TestMethod]
    public async Task A_folder_that_cannot_be_made_says_so()
    {
        using var folder = new Folder();
        var blocked = Path.Combine(folder.Path, "fichier");
        await File.WriteAllTextAsync(blocked, "pas un dossier");
        var packages = new UpdatePackages(new Downloads(Content), new Installer(), Path.Combine(blocked, "mises-a-jour"));

        var result = await packages.DownloadAsync(Announced("1.2.0", Content), null, CancellationToken.None);

        Assert.Contains("inaccessible", result.Error!);
    }

    [TestMethod]
    public async Task The_installers_of_other_versions_are_deleted()
    {
        using var folder = new Folder();
        var old = Path.Combine(folder.Path, "kyfRev-1.1.0-installation.exe");
        var partial = Path.Combine(folder.Path, "kyfRev-1.1.5-installation.exe.partiel");
        var interrupted = Path.Combine(folder.Path, "kyfRev-1.2.0-installation.exe.partiel");
        var log = Path.Combine(folder.Path, UpdatePackages.LogFile);
        var foreign = Path.Combine(folder.Path, "kyfRev-1.1.0-installation.exe.copie");
        foreach (var path in new[] { old, partial, interrupted, log, foreign })
        {
            await File.WriteAllTextAsync(path, "ancien");
        }
        var packages = new UpdatePackages(new Downloads(Content), new Installer(), folder.Path);

        await packages.DownloadAsync(Announced("1.2.0", Content), null, CancellationToken.None);

        Assert.IsFalse(File.Exists(old) || File.Exists(partial) || File.Exists(interrupted));
        Assert.IsTrue(File.Exists(log), "le journal de la dernière installation reste");
        Assert.IsTrue(File.Exists(foreign), "un fichier que les mises à jour n'ont pas écrit n'est jamais effacé");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Launching_checks_the_file_again_remembers_the_update_and_starts_the_installer(bool reopen)
    {
        using var folder = new Folder();
        var installer = new Installer();
        var packages = new UpdatePackages(new Downloads(Content), installer, folder.Path);
        var file = Announced("1.2.0", Content);
        var path = (await packages.DownloadAsync(file, null, CancellationToken.None)).Value;

        var launched = packages.Launch("1.2.0", file, reopen);

        Assert.IsTrue(launched.Succeeded, launched.Error);
        Assert.AreEqual((path, reopen, Path.Combine(folder.Path, UpdatePackages.LogFile)), installer.Started.Single());
        Assert.IsTrue(File.Exists(Path.Combine(folder.Path, UpdatePackages.PendingFile)));
    }

    [TestMethod]
    public async Task A_file_changed_after_its_download_is_never_launched()
    {
        using var folder = new Folder();
        var installer = new Installer();
        var packages = new UpdatePackages(new Downloads(Content), installer, folder.Path);
        var file = Announced("1.2.0", Content);
        var path = (await packages.DownloadAsync(file, null, CancellationToken.None)).Value;
        await File.WriteAllBytesAsync(path, [6, 6, 6, 6, 6, 6]);

        var launched = packages.Launch("1.2.0", file, reopen: true);

        Assert.Contains("ne correspond plus", launched.Error!);
        Assert.IsEmpty(installer.Started);
        Assert.IsNull(packages.TakeAftermath("1.1.0"), "rien n'a été lancé : rien n'est retenu");
    }

    [TestMethod]
    public void A_missing_installer_is_not_launched()
    {
        using var folder = new Folder();
        var installer = new Installer();
        var packages = new UpdatePackages(new Downloads(Content), installer, folder.Path);

        Assert.Contains("illisible", packages.Launch("1.2.0", Announced("1.2.0", Content), reopen: false).Error!);
        Assert.IsEmpty(installer.Started);
    }

    [TestMethod]
    public async Task An_installer_windows_did_not_start_is_said_now_and_at_the_next_start()
    {
        using var folder = new Folder();
        var packages = new UpdatePackages(new Downloads(Content), new Installer { Starts = false }, folder.Path);
        var file = Announced("1.2.0", Content);
        await packages.DownloadAsync(file, null, CancellationToken.None);

        Assert.AreEqual("Windows n'a pas lancé l'installation.", packages.Launch("1.2.0", file, reopen: true).Error);

        var aftermath = packages.TakeAftermath("1.1.0");
        Assert.AreEqual(new UpdateAftermath("1.2.0", false, file), aftermath);
        Assert.IsNotNull(packages.Find(file), "l'installateur reste, pour réessayer");
        Assert.IsNull(packages.TakeAftermath("1.1.0"), "dit une seule fois");
    }

    [TestMethod]
    [DataRow("1.2.0")]
    [DataRow("1.3.0")]
    public async Task An_update_that_now_runs_is_said_installed_and_its_installer_is_deleted(string running)
    {
        using var folder = new Folder();
        var packages = new UpdatePackages(new Downloads(Content), new Installer(), folder.Path);
        var file = Announced("1.2.0", Content);
        await packages.DownloadAsync(file, null, CancellationToken.None);
        Assert.IsTrue(packages.Launch("1.2.0", file, reopen: true).Succeeded);

        Assert.AreEqual(new UpdateAftermath("1.2.0", true, null), packages.TakeAftermath(running));
        Assert.IsNull(packages.Find(file));
        Assert.IsEmpty(Directory.GetFiles(folder.Path, "*.exe"));
    }

    [TestMethod]
    [DataRow("pas du JSON")]
    [DataRow("{\"version\":\"1.2.0\"}")]
    [DataRow("{\"version\":\"../piege\",\"taille\":6,\"sha256\":\"aa\"}")]
    public void A_remembered_update_that_cannot_be_read_says_nothing(string content)
    {
        using var folder = new Folder();
        File.WriteAllText(Path.Combine(folder.Path, UpdatePackages.PendingFile), content);
        var packages = new UpdatePackages(new Downloads(Content), new Installer(), folder.Path);

        Assert.IsNull(packages.TakeAftermath("1.1.0"));
        Assert.IsFalse(File.Exists(Path.Combine(folder.Path, UpdatePackages.PendingFile)));
    }

    [TestMethod]
    public async Task Files_held_by_another_program_are_neither_trusted_nor_forced()
    {
        using var folder = new Folder();
        var packages = new UpdatePackages(new Downloads(Content), new Installer(), folder.Path);
        var file = Announced("1.2.0", Content);
        var path = (await packages.DownloadAsync(file, null, CancellationToken.None)).Value;
        var old = Path.Combine(folder.Path, "kyfRev-1.1.0-installation.exe");
        await File.WriteAllTextAsync(old, "ancien");

        // Tenus ouverts sans partage : l'installateur ne se vérifie pas, l'ancien ne s'efface pas, rien ne plante.
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        using (new FileStream(old, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.IsNull(packages.Find(file));
            Assert.Contains("illisible", packages.Launch("1.2.0", file, reopen: false).Error!);
            await packages.DownloadAsync(Announced("1.3.0", Content), null, CancellationToken.None);
            Assert.IsTrue(File.Exists(old));
        }
    }

    [TestMethod]
    public async Task An_update_that_cannot_be_remembered_still_starts_and_a_held_record_says_nothing()
    {
        using var folder = new Folder();
        var installer = new Installer();
        var packages = new UpdatePackages(new Downloads(Content), installer, folder.Path);
        var file = Announced("1.2.0", Content);
        await packages.DownloadAsync(file, null, CancellationToken.None);
        var pending = Path.Combine(folder.Path, UpdatePackages.PendingFile);
        Directory.CreateDirectory(pending);

        Assert.IsTrue(packages.Launch("1.2.0", file, reopen: true).Succeeded, "retenir la mise à jour n'est pas une condition pour l'installer");
        Assert.HasCount(1, installer.Started);

        Directory.Delete(pending);
        Assert.IsTrue(packages.Launch("1.2.0", file, reopen: true).Succeeded);
        using (new FileStream(pending, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.IsNull(packages.TakeAftermath("1.1.0"), "illisible pour l'instant, elle n'est pas dite");
        }
        Assert.IsNotNull(packages.TakeAftermath("1.1.0"), "relâchée, elle l'est");
    }

    [TestMethod]
    public void Nothing_is_said_when_no_update_was_launched_or_the_folder_does_not_exist()
    {
        using var folder = new Folder();
        var packages = new UpdatePackages(new Downloads(Content), new Installer { Scope = null }, Path.Combine(folder.Path, "absent"));

        Assert.IsNull(packages.TakeAftermath("1.1.0"));
        Assert.IsNull(packages.Scope);
        Assert.AreEqual(Path.Combine(folder.Path, "absent"), packages.Folder);
    }

    private static InstallerFile Announced(string version, byte[] content) =>
        UpdateChecker.InstallerOf(version, content.Length, Convert.ToHexStringLower(SHA256.HashData(content)));

    private sealed class Folder : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("kyfrev-maj-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    /// <summary>Reçoit la progression tout de suite, sur le fil qui la donne.</summary>
    private sealed class Inline(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }

    private sealed class Downloads(byte[] content) : IFileDownloader
    {
        public int Calls { get; private set; }

        public bool Fails { get; init; }

        public Uri? Address { get; private set; }

        public long MaxBytes { get; private set; }

        public async Task DownloadAsync(Uri address, string destination, long maxBytes, IProgress<long>? progress, CancellationToken cancellationToken)
        {
            Calls++;
            (Address, MaxBytes) = (address, maxBytes);
            if (Fails)
            {
                throw new Unreachable("réseau coupé.");
            }
            await File.WriteAllBytesAsync(destination, content, cancellationToken);
            progress?.Report(content.Length);
        }
    }

    private sealed class Installer : IUpdateInstaller
    {
        public InstallScope? Scope { get; init; } = InstallScope.CurrentUser;

        public bool Starts { get; init; } = true;

        public List<(string Installer, bool Reopen, string Log)> Started { get; } = [];

        public bool Start(string installer, bool reopen, string log)
        {
            if (Starts)
            {
                Started.Add((installer, reopen, log));
            }
            return Starts;
        }
    }

    private sealed class Unreachable : OperationException
    {
        public Unreachable()
            : base("réseau coupé.")
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
