using System.Diagnostics;
using System.Text;

namespace KyfRev.Infrastructure.Vault.Tests.Support;

/// <summary>Liens de dossier pour les essais.</summary>
internal static class FileLinks
{
    /// <summary>Lien symbolique, ou jonction si le poste n'autorise pas les liens symboliques.</summary>
    public static void CreateDirectoryLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            using var mklink = Process.Start(new ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, target])
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
            })!;
            mklink.WaitForExit();
            Assert.AreEqual(0, mklink.ExitCode, "jonction impossible à créer");
        }
    }
}
