using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Le texte du diagnostic, comme dans la 0.x : version, système, moteur d'affichage et fin du journal, à copier pour un
/// signalement. Le journal y masque déjà le dossier du profil.
/// </summary>
public static class DiagnosticReport
{
    /// <summary>Lignes du journal, au plus.</summary>
    public const int LogLines = 300;

    /// <summary>Rédige le diagnostic.</summary>
    /// <param name="version">Version de l'application.</param>
    /// <param name="mode">« installée », « portable »…</param>
    /// <param name="webView">Version du moteur d'affichage, ou ce qui l'empêche.</param>
    /// <param name="vaultOpen">Un coffre est ouvert.</param>
    /// <param name="now">Le moment de la copie.</param>
    /// <param name="logTail">La fin du journal.</param>
    public static string Write(string version, string mode, string webView, bool vaultOpen, DateTimeOffset now, string logTail)
    {
        ArgumentNullException.ThrowIfNull(logTail);
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        text.Append(culture, $"kyfRev {version}\n");
        text.Append(culture, $"Version : {mode}\n");
        text.Append(culture, $"Système : {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}\n");
        text.Append(culture, $"WebView2 : {webView}\n");
        text.Append(culture, $"Coffre ouvert : {(vaultOpen ? "oui" : "non")}\n");
        text.Append(culture, $"Copié le : {now.ToString("yyyy-MM-dd HH:mm:ss", culture)}\n");
        text.Append(culture, $"\n--- Fin du journal ({LogLines} lignes au plus) ---\n");
        text.Append(logTail);
        return text.ToString();
    }
}
