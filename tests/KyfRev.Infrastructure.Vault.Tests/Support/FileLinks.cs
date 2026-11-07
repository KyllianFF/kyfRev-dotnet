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

    /// <summary>
    /// Un lecteur substitué (<c>subst</c>) qui mène à <paramref name="target"/>, retiré à sa libération ; rien si aucune lettre
    /// n'est libre. Il ne demande pas de droits d'administrateur, et n'est pas un point d'analyse : rien sur le disque ne
    /// relie son chemin à celui de sa cible.
    /// </summary>
    public static SubstitutedDrive? Subst(string target)
    {
        foreach (var letter in "ZYXWVUTSRQPONMLKJIHGFED")
        {
            if (!Directory.Exists($"{letter}:\\") && Cmd($"/c subst {letter}: \"{target}\"") == 0)
            {
                return new SubstitutedDrive(letter);
            }
        }
        return null;
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

    private static int Cmd(string arguments)
    {
        using var cmd = Process.Start(new ProcessStartInfo("cmd.exe", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        cmd.StandardOutput.ReadToEnd();
        cmd.StandardError.ReadToEnd();
        cmd.WaitForExit();
        return cmd.ExitCode;
    }

    /// <summary>Un lecteur substitué, retiré à la libération.</summary>
    internal sealed class SubstitutedDrive(char letter) : IDisposable
    {
        /// <summary>La racine du lecteur, <c>X:\</c>.</summary>
        public string Root => $"{letter}:\\";

        public void Dispose() => Cmd($"/c subst {letter}: /d");
    }
}
