using System.Globalization;
using System.Text;
using KyfRev.Application.Text;

namespace KyfRev.Infrastructure.Vault.Paths;

/// <summary>Noms de fichiers sûrs, tirés d'un titre ou d'un nom importé, comme dans la version 0.x.</summary>
public static class FileNames
{
    /// <summary>Longueur maximale d'un nom de fiche, en caractères.</summary>
    public const int MaxTitleNameChars = 120;

    /// <summary>Nom donné à une fiche sans titre.</summary>
    public const string Untitled = "Sans titre";

    private const int MaxResourceStemChars = 60;
    private const int MaxExtensionLength = 10;

    /// <summary>Extensions refusées pour une ressource : un clic dessus pourrait lancer un programme ou un script.</summary>
    private static readonly string[] DangerousExtensions =
    [
        "exe", "com", "bat", "cmd", "ps1", "psm1", "psd1", "vbs", "vbe", "js", "jse", "wsf", "wsh", "msi", "msp", "scr",
        "pif", "lnk", "url", "hta", "cpl", "reg", "jar", "dll", "sys", "inf", "html", "htm", "xhtml", "mht", "appref-ms",
    ];

    /// <summary>
    /// Nom de fichier (sans extension) tiré d'un titre, valide sous Windows : « Chapitre 3 : la
    /// mitose » devient « Chapitre 3 - la mitose ».
    /// </summary>
    public static string ForTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        var name = new StringBuilder(title.Length);
        foreach (var rune in title.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case ':':
                    name.Append(" -");
                    break;
                case '"':
                    name.Append('\'');
                    break;
                case '/' or '\\' or '|':
                    name.Append('-');
                    break;
                case '<' or '>' or '?' or '*':
                    break;
                default:
                    if (Rune.IsControl(rune) || Rune.IsWhiteSpace(rune))
                    {
                        name.Append(' ');
                    }
                    else
                    {
                        name.Append(rune.ToString());
                    }
                    break;
            }
        }
        var result = RustText.CollapseWhitespace(name.ToString());
        if (RustText.CharCount(result) > MaxTitleNameChars)
        {
            result = RustText.Take(result, MaxTitleNameChars);
        }
        result = result.TrimEnd('.', ' ');
        if (result.Length == 0)
        {
            result = Untitled;
        }
        return VaultPath.IsReserved(result) ? result + "_" : result;
    }

    /// <summary>
    /// Nom de fichier sûr pour une ressource : minuscules, sans accents ni espaces (« Carte de
    /// France 1789.PNG » devient « carte-de-france-1789.png »).
    /// </summary>
    /// <exception cref="VaultException">Nom sans extension, extension invalide ou exécutable.</exception>
    public static string ForResource(string originalName)
    {
        ArgumentNullException.ThrowIfNull(originalName);
        var name = originalName[(originalName.LastIndexOfAny(['/', '\\']) + 1)..];
        var dot = name.LastIndexOf('.');
        if (dot < 0)
        {
            throw VaultException.ResourceRefused(originalName, "extension absente");
        }
        var stem = name[..dot];
        var extension = string.Concat(name[(dot + 1)..].Select(c => char.IsAsciiLetterUpper(c) ? (char)(c | 0x20) : c));
        if (extension.Length == 0
            || Encoding.UTF8.GetByteCount(extension) > MaxExtensionLength
            || !extension.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw VaultException.ResourceRefused(originalName, "extension invalide");
        }
        if (DangerousExtensions.Contains(extension, StringComparer.Ordinal))
        {
            throw VaultException.ResourceRefused(originalName, "type de fichier exécutable ou actif");
        }

        var clean = new StringBuilder();
        foreach (var rune in stem.Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                continue;
            }
            foreach (var lower in RustText.ToLowercase(rune.ToString()))
            {
                if (char.IsAsciiLetterOrDigit(lower))
                {
                    clean.Append(lower);
                }
                else if (clean.Length == 0 || clean[^1] != '-')
                {
                    clean.Append('-');
                }
            }
        }
        var result = RustText.Take(clean.ToString().Trim('-'), MaxResourceStemChars).TrimEnd('-');
        if (result.Length == 0 || VaultPath.IsReserved(result))
        {
            result = "fichier";
        }
        return $"{result}.{extension}";
    }

    /// <summary>Dossier des ressources d'une fiche : « Mitose.md » → « Mitose.ressources ».</summary>
    /// <exception cref="VaultException">Le nom obtenu est refusé (trop long, par exemple).</exception>
    public static VaultPath ResourcesFolder(VaultPath sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var name = sheet.Name;
        var folder = (name.EndsWith(".md", StringComparison.Ordinal) ? name[..^3] : name) + ".ressources";
        return sheet.Parent is { } parent ? parent.Join(folder) : VaultPath.Create(folder);
    }
}
