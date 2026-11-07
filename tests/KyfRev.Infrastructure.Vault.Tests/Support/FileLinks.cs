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

    /// <summary>Le chemin écrit en noms courts (8.3), tel que Windows le donne ; le chemin tel quel si le volume n'en a pas.</summary>
    public static string ShortPath(string path)
    {
        using var cmd = Process.Start(new ProcessStartInfo("cmd.exe", $"/c for %I in (\"{path}\") do @echo %~sI")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        var output = cmd.StandardOutput.ReadToEnd().Trim();
        cmd.WaitForExit();
        Assert.AreEqual(0, cmd.ExitCode, $"nom court de « {path} » introuvable");
        return output;
    }
}
