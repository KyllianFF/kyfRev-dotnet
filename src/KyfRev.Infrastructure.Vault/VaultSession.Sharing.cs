using System.IO.Compression;
using System.Text;
using KyfRev.Infrastructure.Vault.Links;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;
using KyfRev.Infrastructure.Vault.Text;

namespace KyfRev.Infrastructure.Vault;

/// <remarks>
/// Partage d'une fiche : un fichier à envoyer, écrit hors du coffre, sans jamais écraser un fichier
/// existant. Le texte et la page web sont préparés par l'interface (qui sait rendre la fiche).
/// </remarks>
public sealed partial class VaultSession
{
    /// <summary>Taille maximale d'un contenu préparé par l'interface et d'une image intégrée : 50 Mo.</summary>
    public const int MaxSharedBytes = 50 * 1024 * 1024;

    /// <summary>Écrit la fiche <paramref name="id"/> au format demandé dans <paramref name="folder"/> (hors du coffre).</summary>
    /// <param name="id">Identifiant de la fiche.</param>
    /// <param name="format">Format du partage.</param>
    /// <param name="folder">Dossier existant, hors du coffre.</param>
    /// <param name="content">Pour le texte et la page web : le contenu préparé par l'interface.</param>
    /// <returns>Le fichier écrit.</returns>
    public string ShareSheet(string id, ShareFormat format, string folder, string? content)
    {
        CheckOutputFolder(folder);
        var stem = FileNames.ForTitle(Sheet(id).Sheet.Title);
        string Prepared() =>
            content is not null && Encoding.UTF8.GetByteCount(content) <= MaxSharedBytes
                ? content
                : throw VaultException.InvalidPath(format == ShareFormat.Text ? "Texte" : "Html", "contenu à partager absent ou trop volumineux");
        return format switch
        {
            ShareFormat.Markdown => CreateWithoutOverwriting(folder, stem, "md", Encoding.UTF8.GetBytes(MarkdownToShare(id))),
            ShareFormat.MarkdownWithImages => CreateWithoutOverwriting(folder, stem, "zip", ArchiveToShare(id, stem)),
            ShareFormat.Text => CreateWithoutOverwriting(folder, stem, "txt", Encoding.UTF8.GetBytes(Prepared())),
            _ => CreateWithoutOverwriting(folder, stem, "html", Encoding.UTF8.GetBytes(EmbedImages(Prepared()))),
        };
    }

    /// <summary>Le corps de la fiche, précédé de son titre s'il n'y est pas déjà.</summary>
    private string MarkdownToShare(string id)
    {
        var title = Sheet(id).Sheet.Title;
        var text = ReadSheet(id).Text;
        var body = text[FrontMatter.Split(text).BodyStart..].TrimStart('\r', '\n');
        var firstLine = body.Split('\n')[0];
        firstLine = firstLine.EndsWith('\r') ? firstLine[..^1] : firstLine;
        var hasTitle = body.Length > 0
            && firstLine.StartsWith("# ", StringComparison.Ordinal)
            && TrimStartRepeated(firstLine, "# ").Trim() == title.Trim();
        return hasTitle ? body : $"# {title}\n\n{body}";
    }

    /// <summary>
    /// Archive <c>.zip</c> : la fiche, et les fichiers du coffre que ses liens visent. Ceux qui sont
    /// hors du dossier de la fiche vont dans « images/ » (le lien est réécrit).
    /// </summary>
    private byte[] ArchiveToShare(string id, string stem)
    {
        var folder = Sheet(id).Sheet.Path.Parent?.Value ?? string.Empty;
        var markdown = MarkdownToShare(id);
        var targets = new SortedDictionary<string, string>(Utf8Order.Instance);
        var prefix = folder + "/";
        var rewritten = MarkdownLinks.Rewrite(markdown, folder, folder, target =>
        {
            if (target.StartsWith(prefix, StringComparison.Ordinal))
            {
                targets[target] = target[prefix.Length..];
                return null;
            }
            var name = target[(target.LastIndexOf('/') + 1)..];
            var inArchive = $"images/{name}";
            for (var number = 2; targets.ContainsValue(inArchive); number++)
            {
                inArchive = $"images/{number}-{name}";
            }
            targets[target] = inArchive;
            return $"{folder}/{inArchive}";
        }) ?? markdown;

        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, $"{stem}.md", Encoding.UTF8.GetBytes(rewritten));
            foreach (var (target, inArchive) in targets)
            {
                if (!VaultPath.TryParse(target, out var path, out _))
                {
                    continue;
                }
                string absolute;
                try
                {
                    absolute = Confine(path);
                }
                catch (VaultException)
                {
                    continue;
                }
                var isSheet = RustText.ToLowercase(target).EndsWith(".md", StringComparison.Ordinal);
                if (isSheet || !File.Exists(absolute) || new FileInfo(absolute).Length > MaxSharedBytes)
                {
                    continue;
                }
                Add(archive, inArchive, ReadAllBytes(absolute));
            }
        }
        return buffer.ToArray();

        static void Add(ZipArchive archive, string name, byte[] bytes)
        {
            using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
            stream.Write(bytes);
        }
    }

    /// <summary>
    /// Remplace chaque <c>data-coffre="chemin"</c> (image du coffre) par <c>src="data:…"</c> ; une
    /// image introuvable reste signalée par son chemin.
    /// </summary>
    private string EmbedImages(string page)
    {
        const string attribute = "data-coffre=\"";
        var output = new StringBuilder(page.Length);
        var rest = page;
        while (rest.IndexOf(attribute, StringComparison.Ordinal) is var start and >= 0)
        {
            output.Append(rest, 0, start);
            var after = rest[(start + attribute.Length)..];
            var end = after.IndexOf('"', StringComparison.Ordinal);
            if (end < 0)
            {
                rest = rest[start..];
                break;
            }
            var raw = after[..end];
            output.Append(ImageAsData(DecodeAttribute(raw)) is { } data ? $"src=\"{data}\"" : $"data-introuvable=\"{raw}\"");
            rest = after[(end + 1)..];
        }
        return output.Append(rest).ToString();
    }

    private string? ImageAsData(string path)
    {
        var mime = RustText.ToLowercase(path[(path.LastIndexOf('.') + 1)..]) switch
        {
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "webp" => "image/webp",
            "svg" => "image/svg+xml",
            "bmp" => "image/bmp",
            "avif" => "image/avif",
            _ => null,
        };
        if (mime is null || !VaultPath.TryParse(path, out var vaultPath, out _))
        {
            return null;
        }
        try
        {
            var absolute = Confine(vaultPath);
            return File.Exists(absolute) && new FileInfo(absolute).Length <= MaxSharedBytes && TryReadAllBytes(absolute) is { } bytes
                ? $"data:{mime};base64,{Convert.ToBase64String(bytes)}"
                : null;
        }
        catch (VaultException)
        {
            return null;
        }
    }

    /// <summary>Valeur d'attribut HTML → texte (entités courantes).</summary>
    private static string DecodeAttribute(string value) => value
        .Replace("&quot;", "\"", StringComparison.Ordinal)
        .Replace("&#39;", "'", StringComparison.Ordinal)
        .Replace("&lt;", "<", StringComparison.Ordinal)
        .Replace("&gt;", ">", StringComparison.Ordinal)
        .Replace("&amp;", "&", StringComparison.Ordinal);

    private static string TrimStartRepeated(string text, string prefix)
    {
        while (text.StartsWith(prefix, StringComparison.Ordinal))
        {
            text = text[prefix.Length..];
        }
        return text;
    }
}
