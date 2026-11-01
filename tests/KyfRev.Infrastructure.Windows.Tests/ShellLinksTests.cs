using System.ComponentModel;
using System.Diagnostics;

namespace KyfRev.Infrastructure.Windows.Tests;

[TestClass]
public sealed class ShellLinksTests
{
    [TestMethod]
    [DataRow("https://exemple.org/a")]
    [DataRow("HTTP://exemple.org")]
    [DataRow("mailto:a@exemple.org")]
    public void A_web_or_mail_address_goes_to_the_system(string address)
    {
        ProcessStartInfo? started = null;
        var links = new ShellLinks(information =>
        {
            started = information;
            return true;
        });

        Assert.IsTrue(links.Open(address));
        Assert.AreEqual(address, started!.FileName);
        Assert.IsTrue(started.UseShellExecute);
    }

    [TestMethod]
    [DataRow("file:///C:/Windows/notepad.exe")]
    [DataRow("C:\\Windows\\notepad.exe")]
    [DataRow("javascript:alert(1)")]
    [DataRow("ms-settings:")]
    [DataRow("")]
    public void Nothing_else_is_ever_started(string address)
    {
        var links = new ShellLinks(_ => throw new AssertFailedException("Rien ne doit démarrer."));

        Assert.IsFalse(links.Open(address));
    }

    [TestMethod]
    public void A_system_that_refuses_is_reported_without_failing()
    {
        var links = new ShellLinks(_ => throw new Win32Exception());

        Assert.IsFalse(links.Open("https://exemple.org"));
    }
}
