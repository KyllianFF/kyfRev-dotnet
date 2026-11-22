using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using KyfRev.Application.Ports;
using Microsoft.Win32;

namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>
/// L'installateur lancé en silence pour une mise à jour : le mode de l'installation en place lu sur des clés d'essai, les
/// options passées, et rien de lancé pour de vrai.
/// </summary>
[TestClass]
public sealed class SilentInstallerTests
{
    private const string Application = @"C:\Users\Personne\AppData\Local\Programs\kyfRev";

    private static readonly string Base = $@"Software\kyfRev-essai-{Guid.NewGuid():N}";

    [ClassCleanup]
    public static void Cleanup() => Registry.CurrentUser.DeleteSubKeyTree(Base, throwOnMissingSubKey: false);

    [TestMethod]
    [DataRow(Application + @"\", null, InstallScope.CurrentUser)]
    [DataRow("\"" + Application + "\"", null, InstallScope.CurrentUser)]
    [DataRow(null, @"c:\users\personne\appdata\local\programs\KYFREV\", InstallScope.AllUsers)]
    [DataRow(@"C:\Ailleurs\kyfRev\", Application, InstallScope.AllUsers)]
    public void The_scope_is_that_of_the_key_which_names_this_folder(string? user, string? machine, InstallScope scope)
    {
        var (installer, _) = Installer(user, machine);

        Assert.AreEqual(scope, installer.Scope);
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow(@"C:\Ailleurs\kyfRev\", "")]
    public void A_copy_the_installer_did_not_put_here_cannot_be_updated(string? user, string? machine)
    {
        var (installer, started) = Installer(user, machine);

        Assert.IsNull(installer.Scope);
        Assert.IsFalse(installer.Start(@"C:\maj\kyfRev-1.2.0-installation.exe", reopen: true, @"C:\maj\installation.log"));
        Assert.IsEmpty(started);
    }

    [TestMethod]
    [DataRow(InstallScope.CurrentUser, true, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /LOG=\"C:\\Mes données\\installation.log\" /RELANCER")]
    [DataRow(InstallScope.AllUsers, false, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /ALLUSERS /LOG=\"C:\\Mes données\\installation.log\"")]
    public void The_installer_runs_without_any_window_in_the_mode_in_place(InstallScope scope, bool reopen, string arguments) =>
        Assert.AreEqual(arguments, SilentInstaller.Arguments(scope, reopen, @"C:\Mes données\installation.log"));

    [TestMethod]
    public void The_installer_starts_through_windows_from_its_folder()
    {
        var (installer, started) = Installer(Application, null);

        Assert.IsTrue(installer.Start(@"C:\maj\kyfRev-1.2.0-installation.exe", reopen: false, @"C:\maj\installation.log"));

        var information = started.Single();
        Assert.AreEqual(@"C:\maj\kyfRev-1.2.0-installation.exe", information.FileName);
        Assert.AreEqual(SilentInstaller.Arguments(InstallScope.CurrentUser, false, @"C:\maj\installation.log"), information.Arguments);
        Assert.AreEqual(@"C:\maj", information.WorkingDirectory);
        Assert.IsTrue(information.UseShellExecute);
    }

    [TestMethod]
    public void An_installer_windows_refuses_to_start_is_said()
    {
        var key = Written(Application);
        var installer = new SilentInstaller((Registry.CurrentUser, key), (Registry.CurrentUser, key), _ => throw new Win32Exception(1223, "Opération annulée par l'utilisateur."), Application);

        Assert.IsFalse(installer.Start(@"C:\maj\kyfRev-1.2.0-installation.exe", reopen: true, @"C:\maj\installation.log"));
    }

    [TestMethod]
    public void An_installation_running_is_seen_through_its_mutex()
    {
        var name = $"kyfRevInstallation-essai-{Guid.NewGuid():N}";
        Assert.IsFalse(SilentInstaller.IsInstalling(name));

        using (new Mutex(false, name))
        {
            Assert.IsTrue(SilentInstaller.IsInstalling(name));
        }

        Assert.IsFalse(SilentInstaller.IsInstalling(name));
    }

    [TestMethod]
    public void Only_the_kyfrev_the_installer_opens_itself_stays_while_it_runs()
    {
        var name = $"kyfRevInstallation-essai-{Guid.NewGuid():N}";
        string[] opened = [SilentInstaller.AfterInstallation];
        Assert.IsFalse(SilentInstaller.StepsAside([], name));
        Assert.IsFalse(SilentInstaller.StepsAside(opened, name));

        using (new Mutex(false, name))
        {
            Assert.IsTrue(SilentInstaller.StepsAside([], name));
            Assert.IsTrue(SilentInstaller.StepsAside(["--galerie"], name));
            Assert.IsFalse(SilentInstaller.StepsAside(opened, name));
        }
        Assert.AreEqual(SilentInstaller.IsInstalling(), SilentInstaller.StepsAside([]));
    }

    [TestMethod]
    public void The_default_reads_the_real_registry_without_failing()
    {
        Assert.IsTrue(new SilentInstaller().Scope is null or InstallScope.CurrentUser or InstallScope.AllUsers);
        Assert.ThrowsExactly<ArgumentException>(() => SilentInstaller.Arguments(InstallScope.CurrentUser, false, string.Empty));
        Assert.ThrowsExactly<ArgumentException>(() => new SilentInstaller().Start(string.Empty, reopen: false, "journal.log"));
    }

    [TestMethod]
    public void The_key_the_mutex_and_the_options_are_those_of_the_installer()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(folder.FullName, "KyfRev.slnx")))
        {
            folder = folder.Parent!;
        }
        var script = File.ReadAllText(Path.Combine(folder.FullName, "installer", "kyfRev.iss"));
        var guid = System.Text.RegularExpressions.Regex.Match(script, "#define AppGuid \"([0-9A-F-]+)\"").Groups[1].Value;

        Assert.AreEqual(SilentInstaller.UninstallKey, $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{{{guid}}}_is1");
        StringAssert.Contains(script, $"SetupMutex={SilentInstaller.InstallingMutex}\r\n", StringComparison.Ordinal);
        StringAssert.Contains(script, "Check: ReopenWanted", StringComparison.Ordinal);
        StringAssert.Contains(script, "HasSwitch('/RELANCER')", StringComparison.Ordinal);
        // Les deux ouvertures de la fin (case « Exécuter kyfRev », /RELANCER) passent alors que l'installateur tient son mutex.
        var opens = script.Split("\r\n").Where(line => line.StartsWith(@"Filename: ""{app}\kyfRev.exe""", StringComparison.Ordinal)).ToList();
        Assert.HasCount(2, opens);
        Assert.IsTrue(opens.All(line => line.Contains($"Parameters: \"{SilentInstaller.AfterInstallation}\"", StringComparison.Ordinal)), string.Join("\n", opens));
    }

    /// <summary>Un installateur sur deux clés d'essai, l'une pour la personne, l'autre pour le poste.</summary>
    private static (SilentInstaller Installer, List<ProcessStartInfo> Started) Installer(string? user, string? machine)
    {
        var started = new List<ProcessStartInfo>();
        var installer = new SilentInstaller((Registry.CurrentUser, Written(user)), (Registry.CurrentUser, Written(machine)), information =>
        {
            started.Add(information);
            return true;
        }, Application);
        return (installer, started);
    }

    /// <summary>Une clé d'essai, avec ce dossier d'installation s'il est donné ; sans lui, une clé qui n'existe pas.</summary>
    private static string Written(string? folder)
    {
        var name = $@"{Base}\{Guid.NewGuid():N}";
        if (folder is not null)
        {
            using var key = Registry.CurrentUser.CreateSubKey(name);
            key.SetValue("InstallLocation", folder);
        }
        return name;
    }
}
