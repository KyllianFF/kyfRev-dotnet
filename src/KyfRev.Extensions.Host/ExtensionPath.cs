using System.Buffers;
using System.Text;

namespace KyfRev.Extensions.Host;

/// <summary>
/// Les chemins et les types de fichiers acceptés dans une extension : relatifs à son dossier, sans « .. » ni lecteur, sans caractère
/// ni nom interdit par Windows, et d'un type connu seulement (scripts, styles, images, textes).
/// </summary>
public static class ExtensionPath
{
    private const int MaxComponentBytes = 255;
    private static readonly SearchValues<char> Forbidden = SearchValues.Create("<>:\"\\|?*");
    private static readonly string[] Reserved =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>Ce qui fait refuser ce chemin, écrit avec des « / » ; rien s'il est bon.</summary>
    /// <param name="path">Le chemin à vérifier.</param>
    public static string? Refusal(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.StartsWith('/'))
        {
            return "chemin absolu";
        }
        foreach (var component in path.Split('/'))
        {
            if (ComponentRefusal(component) is { } refusal)
            {
                return refusal;
            }
        }
        return null;
    }

    /// <summary>Le type MIME d'un fichier accepté ; rien pour un type refusé.</summary>
    /// <param name="name">Le nom ou le chemin du fichier.</param>
    public static string? ContentTypeOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var dot = name.LastIndexOf('.');
        return dot < 0 ? null : name[(dot + 1)..].ToUpperInvariant() switch
        {
            "JS" or "MJS" => "text/javascript; charset=utf-8",
            "CSS" => "text/css; charset=utf-8",
            "JSON" => "application/json",
            "MD" or "TXT" => "text/plain; charset=utf-8",
            "SVG" => "image/svg+xml",
            "PNG" => "image/png",
            "JPG" or "JPEG" => "image/jpeg",
            "GIF" => "image/gif",
            "WEBP" => "image/webp",
            _ => null,
        };
    }

    private static string? ComponentRefusal(string component)
    {
        if (component.Length == 0)
        {
            return "composant vide";
        }
        if (component is "." or "..")
        {
            return "« . » ou « .. »";
        }
        if (Encoding.UTF8.GetByteCount(component) > MaxComponentBytes)
        {
            return "nom trop long";
        }
        if (component.AsSpan().ContainsAny(Forbidden) || component.Any(char.IsControl))
        {
            return "caractère interdit";
        }
        if (component.EndsWith('.') || component.EndsWith(' '))
        {
            return "point ou espace final";
        }
        var stem = component.Split('.')[0];
        return Reserved.Contains(stem, StringComparer.OrdinalIgnoreCase) ? "nom réservé de Windows" : null;
    }
}
