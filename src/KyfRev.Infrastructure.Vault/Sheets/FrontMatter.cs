using System.Collections.Immutable;
using System.Text.Json.Nodes;
using KyfRev.Application.Text;
using KyfRev.Infrastructure.Vault.Json;
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
    /// En-tête d'une nouvelle fiche. Les textes sont écrits entre guillemets avec les échappements
    /// JSON, qui sont aussi du YAML valide.
    /// </summary>
    public static string Compose(string id, string title, DateOnly created) =>
        $"---\nid: {id}\ntitre: {TextValue(title)}\ncreee: {created:yyyy-MM-dd}\n---\n\n";

    /// <summary>
    /// Ajoute <c>id</c> en tête de l'en-tête existant, ou crée un en-tête minimal, sans toucher au
    /// reste du fichier. Respecte le style de fin de ligne du fichier.
    /// </summary>
    public static string AddId(string text, string id)
    {
        ArgumentNullException.ThrowIfNull(text);
        var newline = NewlineOf(text);
        var split = Split(text);
        if (split.Yaml is not null)
        {
            return $"{text[..split.YamlStart]}id: {id}{newline}{text[split.YamlStart..]}";
        }
        var (bom, body) = WithoutByteOrderMark(text);
        return $"{bom}---{newline}id: {id}{newline}---{newline}{newline}{body}";
    }

    /// <summary>Valeur YAML d'un texte : entre guillemets, avec les échappements JSON.</summary>
    public static string TextValue(string text) => SerdeJson.Compact(JsonValue.Create(text));

    /// <summary>Valeur YAML d'une liste de textes : <c>["a", "b"]</c>.</summary>
    public static string ListValue(IEnumerable<string> texts) =>
        SerdeJson.Compact(Fields.StringArray(texts)).Replace("\",\"", "\", \"", StringComparison.Ordinal);

    /// <summary>
    /// Remplace la valeur d'un champ de premier niveau de l'en-tête (y compris une liste écrite
    /// sur plusieurs lignes), ou l'ajoute à la fin de l'en-tête. Le reste du fichier n'est pas
    /// touché. <paramref name="value"/> est déjà du YAML valide.
    /// </summary>
    public static string ReplaceField(string text, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(text);
        var newline = NewlineOf(text);
        var split = Split(text);
        if (split.Yaml is not { } yaml)
        {
            var (bom, body) = WithoutByteOrderMark(text);
            return $"{bom}---{newline}{key}: {value}{newline}---{newline}{newline}{body}";
        }

        var yamlEnd = split.YamlStart + yaml.Length;
        var position = split.YamlStart;
        var fieldStart = -1;
        var fieldEnd = -1;
        foreach (var line in LinesWithEndings(yaml))
        {
            var content = line.TrimEnd('\r', '\n');
            if (fieldStart < 0)
            {
                if (content.StartsWith(key, StringComparison.Ordinal) && content.Length > key.Length && content[key.Length] == ':')
                {
                    (fieldStart, fieldEnd) = (position, position + line.Length);
                }
            }
            else if (content.StartsWith(' ') || content.StartsWith('\t') || content.StartsWith('-'))
            {
                // Suite d'une valeur sur plusieurs lignes : ligne indentée ou élément « - ».
                fieldEnd = position + line.Length;
            }
            else
            {
                break;
            }
            position += line.Length;
        }

        if (fieldStart >= 0)
        {
            return $"{text[..fieldStart]}{key}: {value}{newline}{text[fieldEnd..]}";
        }
        var separator = yaml.Length == 0 || yaml.EndsWith('\n') ? string.Empty : newline;
        return $"{text[..yamlEnd]}{separator}{key}: {value}{newline}{text[yamlEnd..]}";
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
            var clean = RustText.Take(RustText.CollapseWhitespace(tag.Trim().TrimStart('#')), MaxTagLength);
            if (clean.Length > 0 && !result.Any(existing => RustText.EqualsIgnoringCase(existing, clean)))
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
        if (stream.Documents.Count > 1)
        {
            // Une ligne « --- » suivie d'une espace ouvre un second document : refusé, comme en 0.x.
            throw new FormatException("l'en-tête contient plusieurs documents YAML");
        }
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

    private static string NewlineOf(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    /// <summary>Un éventuel BOM UTF-8 reste en tout début de fichier.</summary>
    private static (string Bom, string Body) WithoutByteOrderMark(string text) =>
        text.StartsWith(ByteOrderMark) ? (ByteOrderMark.ToString(), text[1..]) : (string.Empty, text);

    /// <summary>Lignes avec leur fin de ligne (<c>split_inclusive('\n')</c>).</summary>
    private static IEnumerable<string> LinesWithEndings(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            var next = end < 0 ? text.Length : end + 1;
            yield return text[start..next];
            start = next;
        }
    }
}
