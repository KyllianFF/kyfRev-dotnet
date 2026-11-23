namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>Un seul kyfRev par session et par installation : relancé, il réveille celui qui tourne déjà.</summary>
[TestClass]
public sealed class SingleInstanceTests
{
    [TestMethod]
    public void A_second_launch_wakes_the_first_and_steps_aside()
    {
        var name = Unique();
        using var first = new SingleInstance(name);
        Assert.IsTrue(first.IsFirst);
        using var woken = new ManualResetEventSlim();
        first.Listen(woken.Set);

        using (var second = new SingleInstance(name))
        {
            Assert.IsFalse(second.IsFirst, "kyfRev tourne déjà");
            second.WakeFirst();
        }

        Assert.IsTrue(woken.Wait(TimeSpan.FromSeconds(5)), "celui qui tourne est réveillé");

        // Une autre relance le réveille encore.
        woken.Reset();
        using (var third = new SingleInstance(name))
        {
            Assert.IsFalse(third.IsFirst);
            third.WakeFirst();
        }
        Assert.IsTrue(woken.Wait(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void Once_closed_the_next_launch_is_the_first()
    {
        var name = Unique();
        using (var first = new SingleInstance(name))
        {
            Assert.IsTrue(first.IsFirst);
        }

        using var next = new SingleInstance(name);
        Assert.IsTrue(next.IsFirst, "le nom a été rendu");
        next.Dispose();
        next.Dispose();
    }

    [TestMethod]
    public void The_name_follows_the_executable_and_not_its_spelling()
    {
        var installed = SingleInstance.NameOf(@"C:\Program Files\kyfRev\kyfRev.exe");
        Assert.AreEqual(installed, SingleInstance.NameOf(@"c:\program files\KYFREV\kyfrev.EXE"), "la casse ne compte pas");
        Assert.AreNotEqual(installed, SingleInstance.NameOf(@"C:\Projet\kyfRev\src\KyfRev.App\bin\Debug\kyfRev.exe"), "une version de développement est à part");
        StringAssert.StartsWith(installed, "kyfRev.instance.", StringComparison.Ordinal);
        Assert.ThrowsExactly<ArgumentException>(() => SingleInstance.NameOf(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new SingleInstance(string.Empty));
    }

    private static string Unique() => "kyfRev.essai." + Guid.NewGuid().ToString("N");
}
