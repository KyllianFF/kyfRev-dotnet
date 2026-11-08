using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>La 0.x vue dans le registre, sur une clé d'essai.</summary>
[TestClass]
public sealed class LegacyInstallationTests
{
    private static readonly string Base = $@"Software\kyfRev-essai-{Guid.NewGuid():N}";

    [ClassCleanup]
    public static void Cleanup() => Registry.CurrentUser.DeleteSubKeyTree(Base, throwOnMissingSubKey: false);

    [TestMethod]
    public void Nothing_is_found_without_the_key()
    {
        Assert.IsNull(Legacy($@"{Base}\absente").Find());
    }

    [TestMethod]
    public void The_0x_is_found_with_its_folder_without_quotes()
    {
        var name = Written("0.7.0", "\"C:\\Program Files\\kyfRev\"");

        var found = Legacy(name).Find();

        Assert.IsNotNull(found);
        Assert.AreEqual(("0.7.0", @"C:\Program Files\kyfRev"), (found.Version, found.Folder));
    }

    [TestMethod]
    public void A_key_without_folder_gives_an_empty_folder()
    {
        Assert.AreEqual(string.Empty, Legacy(Written("0.8.0", null)).Find()!.Folder);
    }

    [TestMethod]
    [DataRow("1.0.0")]
    [DataRow("")]
    public void Another_version_is_not_the_0x(string version)
    {
        Assert.IsNull(Legacy(Written(version, @"C:\kyfRev")).Find());
    }

    [TestMethod]
    public void The_page_of_windows_opens_and_a_failure_is_said()
    {
        ProcessStartInfo? started = null;
        var links = new LegacyInstallation([Registry.CurrentUser], Base, information =>
        {
            started = information;
            return true;
        });

        Assert.IsTrue(links.OpenInstalledApps());
        Assert.AreEqual("ms-settings:appsfeatures", started!.FileName);
        Assert.IsTrue(started.UseShellExecute);

        Assert.IsFalse(new LegacyInstallation([Registry.CurrentUser], Base, _ => throw new Win32Exception()).OpenInstalledApps());
    }

    [TestMethod]
    public void The_default_reads_the_real_registry_without_failing() =>
        Assert.IsTrue(new LegacyInstallation().Find() is null or { Version.Length: > 0 });

    private static LegacyInstallation Legacy(string key) => new([Registry.CurrentUser], key, _ => true);

    private static string Written(string version, string? folder)
    {
        var name = $@"{Base}\{Guid.NewGuid():N}";
        using var key = Registry.CurrentUser.CreateSubKey(name);
        key.SetValue("DisplayVersion", version);
        if (folder is not null)
        {
            key.SetValue("InstallLocation", folder);
        }
        return name;
    }
}
