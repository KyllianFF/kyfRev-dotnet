using System.Collections.Immutable;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace KyfRev.Infrastructure.Vault.Sheets;

/// <summary>Position de l'en-tête YAML dans le texte d'une fiche (indices en caractères).</summary>
/// <param name="Yaml">Texte YAML entre les deux lignes « --- », s'il y a un en-tête.</param>
/// <param name="YamlStart">Début du YAML, juste après la ligne d'ouverture.</param>
/// <param name="BodyStart">Début du corps Markdown.</param>
public sealed record FrontMatterSplit(string? Yaml, int YamlStart, int BodyStart);

/// <summary>Champs de l'en-tête utiles à kyfRev ; les autres sont ignorés.</summary>
/// <param name="Id">Identifiant de la fiche suivie.</param>
/// <param name="Title">Titre.</param>
/// <param name="Created">Date de création, telle qu'écrite.</param>
/// <param name="Tags">Étiquettes de <c>etiquettes</c> puis de <c>tags</c>, normalisées.</param>
/// <param name="TagsKey">Clé à écrire pour les étiquettes : <c>tags</c> si le fichier n'utilise que celle-ci.</param>
public sealed record SheetHeader(string? Id, string? Title, string? Created, ImmutableArray<string> Tags, string TagsKey)
{
    /// <summary>En-tête vide (fiche sans en-tête).</summary>
    public static SheetHeader Empty { get; } = new(null, null, null, [], "etiquettes");
}

/// <summary>Résultat de la lecture d'un en-tête.</summary>
public sealed record HeaderReading(SheetHeader? Header, string? Error)
{
    /// <summary>Vrai si l'en-tête est lisible.</summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Header))]
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Header is not null;
}

/// <summary>
/// En-tête YAML des fiches (« front matter ») : <c>id</c>, <c>titre</c>, <c>creee</c>,
/// <c>etiquettes</c> (ou <c>tags</c>, lu aussi pour les fichiers d'autres outils). Reprend le
/// découpage et la normalisation de la version 0.x.
/// </summary>
public static class FrontMatter
{
    /// <summary>Longueur maximale d'une étiquette, en caractères.</summary>
    public const int MaxTagLength = 50;

    /// <summary>Nombre maximal d'étiquettes par fiche.</summary>
    public const int MaxTags = 30;

    private const char ByteOrderMark = '\uFEFF';

    /// <summary>
    /// Repère l'en-tête : une première ligne exactement « --- » (après un éventuel BOM) et une ligne
    /// de fermeture « --- » ou « ... ». Sans fermeture, il n'y a pas d'en-tête.
    /// </summary>
    public static FrontMatterSplit Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var none = new FrontMatterSplit(null, 0, 0);
        var start = text.StartsWith(ByteOrderMark) ? 1 : 0;
        int yamlStart;
        if (string.CompareOrdinal(text, start, "---\r\n", 0, 5) == 0)
        {
            yamlStart = start + 5;
        }
        else if (string.CompareOrdinal(text, start, "---\n", 0, 4) == 0)
        {
            yamlStart = start + 4;
        }
        else
        {
            return none;
        }

        var position = yamlStart;
        while (position < text.Length)
        {
            var end = text.IndexOf('\n', position);
            var lineEnd = end < 0 ? text.Length : end + 1;
            var content = text[position..lineEnd].TrimEnd('\r', '\n');
            if (content is "---" or "...")
            {
                return new FrontMatterSplit(text[yamlStart..position], yamlStart, lineEnd);
            }
            position = lineEnd;
        }
        return none;
    }

    /// <summary>Lit l'en-tête d'une fiche. Sans en-tête, tous les champs sont absents.</summary>
    public static HeaderReading Read(string text)
    {
        var yaml = Split(text).Yaml;
        if (yaml is null || string.IsNullOrWhiteSpace(yaml))
        {
            return new HeaderReading(SheetHeader.Empty, null);
        }
        try
        {
            return new HeaderReading(Parse(yaml), null);
        }
        catch (YamlException exception)
        {
            return new HeaderReading(null, exception.Message);
        }
        catch (FormatException exception)
        {
            return new HeaderReading(null, exception.Message);
        }
    }

    /// <summary>
    /// Espaces réduits, « # » de tête retirés, vides retirés, 50 caractères au plus, doublons
    /// retirés sans tenir compte de la casse, 30 étiquettes au plus ; l'ordre est conservé.
    /// </summary>
    public static ImmutableArray<string> NormalizeTags(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var result = ImmutableArray.CreateBuilder<string>();
        foreach (var tag in tags)
        {
            var words = tag.Trim().TrimStart('#').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var clean = Truncate(string.Join(' ', words), MaxTagLength);
            if (clean.Length > 0 && !result.Contains(clean, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(clean);
            }
            if (result.Count == MaxTags)
            {
                break;
            }
        }
        return result.ToImmutable();
    }

    private static SheetHeader Parse(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0 || IsNull(stream.Documents[0].RootNode))
        {
            return SheetHeader.Empty;
        }
        if (stream.Documents[0].RootNode is not YamlMappingNode mapping)
        {
            throw new FormatException("l'en-tête doit être une suite de champs « clé: valeur »");
        }

        string? id = null, title = null, created = null;
        List<string>? etiquettes = null, tags = null;
        foreach (var (keyNode, value) in mapping.Children)
        {
            switch ((keyNode as YamlScalarNode)?.Value)
            {
                case "id":
                    id = Scalar(value, "id");
                    break;
                case "titre":
                    title = Scalar(value, "titre");
                    break;
                case "creee":
                    created = Scalar(value, "creee");
                    break;
                case "etiquettes":
                    etiquettes = TagList(value, "etiquettes");
                    break;
                case "tags":
                    tags = TagList(value, "tags");
                    break;
                default:
                    break;
            }
        }
        var all = (etiquettes ?? []).Concat(tags ?? []);
        var key = tags is not null && etiquettes is null ? "tags" : "etiquettes";
        return new SheetHeader(id, title, created, NormalizeTags(all), key);
    }

    private static string? Scalar(YamlNode node, string key) => node switch
    {
        _ when IsNull(node) => null,
        YamlScalarNode scalar => scalar.Value ?? string.Empty,
        _ => throw new FormatException($"« {key} » doit être un texte"),
    };

    /// <summary>Une liste YAML, ou un texte seul découpé sur les virgules (« tags: examen, bac »).</summary>
    private static List<string>? TagList(YamlNode node, string key) => node switch
    {
        _ when IsNull(node) => null,
        YamlScalarNode scalar => [.. (scalar.Value ?? string.Empty).Split(',')],
        YamlSequenceNode sequence => [.. sequence.Children.Select(item => item is YamlScalarNode { Value: { } text } ? text : throw new FormatException($"« {key} » doit être une liste de textes"))],
        _ => throw new FormatException($"« {key} » doit être une liste ou un texte"),
    };

    private static bool IsNull(YamlNode node) =>
        node is YamlScalarNode { Style: ScalarStyle.Plain or ScalarStyle.Any, Value: null or "" or "~" or "null" or "Null" or "NULL" };

    /// <summary>Les <paramref name="max"/> premiers caractères (scalaires Unicode, comme en Rust).</summary>
    private static string Truncate(string text, int max)
    {
        var builder = new StringBuilder();
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (count++ == max)
            {
                break;
            }
            builder.Append(rune.ToString());
        }
        return builder.ToString();
    }
}
