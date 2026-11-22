using Microsoft.Win32;

namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>Le lancement avec la session, sur une clé d'essai du compte : jamais la vraie clé « Run ».</summary>
[TestClass]
public sealed class SessionStartupTests
{
    private static readonly string Base = $@"Software\kyfRev-essai-{Guid.NewGuid():N}";

    [ClassCleanup]
    public static void Cleanup() => Registry.CurrentUser.DeleteSubKeyTree(Base, throwOnMissingSubKey: false);

    [TestMethod]
    public void It_is_written_in_the_tray_or_open_and_removed()
    {
        var startup = new SessionStartup(Registry.CurrentUser, $@"{Base}\Run", @"C:\Program Files\kyfRev\kyfRev.exe");
        Assert.IsNull(startup.Command);

        Assert.IsTrue(startup.Apply(enabled: true, inTray: true));
        Assert.AreEqual("\"C:\\Program Files\\kyfRev\\kyfRev.exe\" --barre-etat", startup.Command, "le chemin entre guillemets, puis l'option");

        Assert.IsTrue(startup.Apply(enabled: true, inTray: false));
        Assert.AreEqual("\"C:\\Program Files\\kyfRev\\kyfRev.exe\"", startup.Command);

        Assert.IsTrue(startup.Apply(enabled: false, inTray: false));
        Assert.IsNull(startup.Command, "retiré quand on ne le veut plus");
        Assert.IsTrue(startup.Apply(enabled: false, inTray: false), "déjà retiré : rien à faire");
    }

    [TestMethod]
    public void Without_a_program_nothing_is_written()
    {
        var startup = new SessionStartup(Registry.CurrentUser, $@"{Base}\Sans", null);

        Assert.IsFalse(startup.Apply(enabled: true, inTray: true));
        Assert.IsNull(startup.Command);
    }
}
