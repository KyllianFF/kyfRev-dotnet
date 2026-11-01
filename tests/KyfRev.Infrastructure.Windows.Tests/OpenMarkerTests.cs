namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>Le signal « kyfRev est ouvert » que l'installateur cherche avant de remplacer ou de retirer kyfRev.</summary>
[TestClass]
public sealed class OpenMarkerTests
{
    private static readonly string[] InstallerNames = ["kyfRev", @"Global\kyfRev"];

    [TestMethod]
    public void The_installer_sees_kyfrev_open_until_it_closes()
    {
        var names = new[] { Unique(), @"Global\" + Unique() };

        using (var marker = new OpenMarker(names))
        {
            Assert.AreEqual(2, marker.Held);
            Assert.IsTrue(names.All(Exists), "kyfRev ouvert, pour la session et pour le poste");
        }

        Assert.IsFalse(names.Any(Exists), "kyfRev fermé");
    }

    [TestMethod]
    public void The_names_are_those_of_the_installer()
    {
        CollectionAssert.AreEqual(InstallerNames, OpenMarker.Names.ToArray());
        using var marker = new OpenMarker();

        Assert.IsTrue(OpenMarker.Names.All(Exists));
    }

    [TestMethod]
    public void A_name_already_taken_does_not_stop_kyfrev()
    {
        var taken = Unique();
        using var other = new EventWaitHandle(false, EventResetMode.ManualReset, taken);
        var free = Unique();

        using var marker = new OpenMarker([taken, free]);

        Assert.AreEqual(1, marker.Held);
        Assert.IsTrue(Exists(free));
    }

    private static string Unique() => "kyfRev-essai-" + Guid.NewGuid().ToString("N");

    private static bool Exists(string name)
    {
        if (!Mutex.TryOpenExisting(name, out var mutex))
        {
            return false;
        }
        mutex.Dispose();
        return true;
    }
}
