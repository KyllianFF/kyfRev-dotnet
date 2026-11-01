using System.Text;
using System.Text.RegularExpressions;

namespace KyfRev.Rendering;

/// <summary>Où mène un lien d'une fiche.</summary>
public abstract record LinkTarget;

/// <summary>Une adresse web ou de courriel, à ouvrir dans le navigateur du système.</summary>
/// <param name="Address">L'adresse.</param>
public sealed record ExternalLink(string Address) : LinkTarget;

/// <summary>Un endroit de la fiche, comme une note.</summary>
/// <param name="Identifier">Son identifiant.</param>
public sealed record PageAnchor(string Identifier) : LinkTarget;

/// <summary>Une autre fiche du coffre.</summary>
/// <param name="Path">Son chemin dans le coffre.</param>
public sealed record SheetLink(string Path) : LinkTarget;

/// <summary>Un lien qui ne mène nulle part : rien ne s'ouvre.</summary>
public sealed record BrokenLink : LinkTarget;

/// <summary>Ce que montre une image d'une fiche.</summary>
public abstract record ImageTarget;

/// <summary>Un fichier du coffre.</summary>
/// <param name="Path">Son chemin dans le coffre.</param>
public sealed record VaultImage(string Path) : ImageTarget;

/// <summary>Une image écrite dans la fiche (<c>data:image/…</c>).</summary>
/// <param name="Data">L'adresse complète.</param>
public sealed record EmbeddedImage(string Data) : ImageTarget;

/// <summary>Pourquoi une image n'est pas chargée.</summary>
public enum ImageBlock
{
    /// <summary>Son chemin relatif sort du coffre.</summary>
    OutsideVault,

    /// <summary>Elle est sur le web : rien n'est jamais téléchargé.</summary>
    Remote,

    /// <summary>Elle est ailleurs sur le poste : seul le contenu du coffre s'affiche.</summary>
    NotInVault,
}

/// <summary>Une image qui ne sera pas chargée.</summary>
/// <param name="Reason">La raison.</param>
/// <param name="Detail">Son adresse, à montrer à la place.</param>
public sealed record BlockedImage(ImageBlock Reason, string Detail) : ImageTarget;

/// <summary>
/// Les destinations d'une fiche, comme les résout la 0.x (<c>src/lib/rendu.ts</c> et <c>chemins.ts</c>) : une
/// fiche ne charge jamais rien hors du coffre, et un lien ne navigue jamais de lui-même.
/// </summary>
public static partial class ContentTargets
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Le dossier d'une fiche, relatif au coffre.</summary>
    public static string FolderOf(string sheetPath)
    {
        ArgumentNullException.ThrowIfNull(sheetPath);
        var slash = sheetPath.LastIndexOf('/');
        return slash < 0 ? string.Empty : sheetPath[..slash];
    }

    /// <summary>Où mène un lien écrit dans une fiche du dossier <paramref name="folder"/>.</summary>
    public static LinkTarget OfLink(string address, string folder)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(folder);
        var (path, suffix) = SplitSuffix(address);
        if (WebOrMail().IsMatch(address))
        {
            return new ExternalLink(address);
        }
        if (path.Length == 0 && suffix.StartsWith('#'))
        {
            return new PageAnchor(suffix[1..]);
        }
        if (!IsExternal(path) && path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && Resolve(folder, Decode(path)) is { } target)
        {
            return new SheetLink(target);
        }
        return new BrokenLink();
    }

    /// <summary>Ce que montre une image écrite dans une fiche du dossier <paramref name="folder"/>.</summary>
    public static ImageTarget OfImage(string source, string folder)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(folder);
        var (path, _) = SplitSuffix(source);
        if (!IsExternal(path))
        {
            var decoded = Decode(path);
            return Resolve(folder, decoded) is { } target ? new VaultImage(target) : new BlockedImage(ImageBlock.OutsideVault, decoded);
        }
        if (Web().IsMatch(source))
        {
            return new BlockedImage(ImageBlock.Remote, source);
        }
        return DataImage().IsMatch(source) ? new EmbeddedImage(source) : new BlockedImage(ImageBlock.NotInVault, Decode(source));
    }

    private static (string Path, string Suffix) SplitSuffix(string address)
    {
        var cut = address.AsSpan().IndexOfAny('#', '?');
        return cut < 0 ? (address, string.Empty) : (address[..cut], address[cut..]);
    }

    /// <summary>Une adresse à protocole, un chemin absolu ou rien du tout.</summary>
    private static bool IsExternal(string target)
    {
        if (target.Length == 0 || target[0] is '/' or '\\')
        {
            return true;
        }
        var colon = target.IndexOf(':', StringComparison.Ordinal);
        var slash = target.IndexOf('/', StringComparison.Ordinal);
        return colon >= 0 && (slash < 0 || colon < slash);
    }

    /// <summary>Le chemin désigné depuis un dossier du coffre ; rien s'il sort du coffre.</summary>
    private static string? Resolve(string folder, string relative)
    {
        var parts = folder.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var part in relative.Split('/'))
        {
            if (part is "" or ".")
            {
                continue;
            }
            if (part == "..")
            {
                if (parts.Count == 0)
                {
                    return null;
                }
                parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                parts.Add(part);
            }
        }
        return string.Join('/', parts);
    }

    /// <summary>Retire les échappements et décode les <c>%XX</c> ; une suite invalide laisse le texte tel quel.</summary>
    private static string Decode(string raw)
    {
        var plain = Escape().Replace(raw, "$1");
        try
        {
            return EscapedBytes().Replace(plain, match =>
            {
                var bytes = new byte[match.Length / 3];
                for (var i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(match.ValueSpan.Slice((i * 3) + 1, 2).ToString(), 16);
                }
                return StrictUtf8.GetString(bytes);
            });
        }
        catch (DecoderFallbackException)
        {
            return plain;
        }
    }

    [GeneratedRegex(@"\\([!-/:-@\[-`{-~])", RegexOptions.CultureInvariant)]
    private static partial Regex Escape();

    [GeneratedRegex("(?:%[0-9a-fA-F]{2})+", RegexOptions.CultureInvariant)]
    private static partial Regex EscapedBytes();

    [GeneratedRegex(@"\A(?:https?|mailto):", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WebOrMail();

    [GeneratedRegex(@"\Ahttps?:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Web();

    [GeneratedRegex(@"\Adata:image/", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DataImage();
}
