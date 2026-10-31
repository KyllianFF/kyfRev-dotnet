using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;
using KyfRev.Infrastructure.Vault.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace KyfRev.Infrastructure.Vault.Quotes;

/// <summary>Nature de la source d'une citation.</summary>
public enum SourceKind
{
    /// <summary>Livre.</summary>
    Book,

    /// <summary>Journal.</summary>
    Newspaper,

    /// <summary>Article.</summary>
    Article,

    /// <summary>Discours.</summary>
    Speech,

    /// <summary>Site.</summary>
    Website,

    /// <summary>Film.</summary>
    Film,

    /// <summary>Chanson.</summary>
    Song,

    /// <summary>Émission.</summary>
    Broadcast,

    /// <summary>Autre.</summary>
    Other,

    /// <summary>Source inconnue, dite explicitement.</summary>
    Unknown,
}

/// <summary>Source d'une citation.</summary>
/// <param name="Kind">Nature de la source.</param>
/// <param name="Title">Titre de l'œuvre.</param>
/// <param name="Page">Page, telle qu'écrite (« 12 », « 12-14 »).</param>
/// <param name="Address">Adresse en ligne, telle que saisie (pas forcément une URL valide).</param>
public sealed record QuoteSource(SourceKind? Kind, string? Title, string? Page, string? Address)
{
    /// <summary>Aucune source.</summary>
    public static QuoteSource None { get; } = new(null, null, null, null);
}

/// <summary>Ce que l'utilisateur saisit ou modifie ; seul le texte est obligatoire.</summary>
/// <param name="Text">Texte de la citation.</param>
/// <param name="Author">Auteur.</param>
/// <param name="Date">Date libre : « 1670 », « mai 1968 », « vers 1650 », « XVIIe siècle ».</param>
/// <param name="Source">Source.</param>
/// <param name="Context">Contexte.</param>
/// <param name="Tags">Étiquettes.</param>
public sealed record QuoteContent(string Text, string? Author, string? Date, QuoteSource Source, string? Context, ImmutableArray<string> Tags);

/// <summary>Une citation du coffre : un fichier <c>.md</c> de <c>citations/</c>.</summary>
/// <param name="Path">Fichier de la citation, relatif au coffre : c'est ce qui la désigne.</param>
/// <param name="Id">Identifiant, absent pour une citation ajoutée à la main.</param>
/// <param name="Content">Contenu.</param>
/// <param name="Year">Année déduite de la date, pour le tri (négative avant notre ère).</param>
/// <param name="AddedOn">Jour d'ajout.</param>
/// <param name="InTrash">Dans <c>citations/.supprimees/</c>.</param>
/// <param name="Origin">D'où vient la citation si kyfRev l'a trouvée en ligne : « wikiquote ».</param>
public sealed record Quote(string Path, string? Id, QuoteContent Content, int? Year, DateOnly? AddedOn, bool InTrash, string? Origin);

/// <summary>Toutes les citations, celles de la corbeille à part, et les fichiers illisibles.</summary>
public sealed record QuoteList(IReadOnlyList<Quote> Quotes, IReadOnlyList<Quote> Trash, IReadOnlyList<UnreadableFile> Unreadable);

/// <summary>Fichier d'une citation, lu et écrit comme dans la version 0.x.</summary>
public static partial class QuoteFile
{
    /// <summary>Longueur maximale du texte, en caractères.</summary>
    public const int MaxTextChars = 20_000;

    /// <summary>Longueur maximale des autres champs, en caractères.</summary>
    public const int MaxFieldChars = 2_000;

    /// <summary>Clés de l'en-tête écrites par kyfRev ; les autres lignes sont conservées.</summary>
    private static readonly string[] KyfRevKeys = ["id", "auteur", "date", "dateTri", "source", "contexte", "etiquettes", "tags", "ajoutee"];

    /// <summary>Vérifie le contenu saisi ; refusé avec le message de la version 0.x.</summary>
    /// <exception cref="VaultException">Texte vide, ou champ trop long.</exception>
    public static void Verify(QuoteContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Text.Trim().Length == 0)
        {
            throw VaultException.InvalidQuote("le texte de la citation est obligatoire");
        }
        if (RustText.CharCount(content.Text) > MaxTextChars)
        {
            throw VaultException.InvalidQuote("texte trop long");
        }
        string?[] fields = [content.Author, content.Date, content.Context, content.Source.Title, content.Source.Page, content.Source.Address];
        if (fields.Any(field => field is not null && RustText.CharCount(field) > MaxFieldChars))
        {
            throw VaultException.InvalidQuote("un champ dépasse 2 000 caractères");
        }
    }

    /// <summary>Texte complet du fichier d'une citation ; <paramref name="foreignLines"/> sont les lignes d'en-tête d'autres outils, conservées.</summary>
    public static string Compose(string id, QuoteContent content, DateOnly addedOn, IEnumerable<string> foreignLines)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(foreignLines);
        var yaml = new StringBuilder($"---\nid: {id}\n");
        if (Value(content.Author) is { } author)
        {
            yaml.Append(CultureInfo.InvariantCulture, $"auteur: {author}\n");
        }
        if (Value(content.Date) is { } date)
        {
            yaml.Append(CultureInfo.InvariantCulture, $"date: {date}\n");
            if (Year(content.Date!) is { } year)
            {
                yaml.Append(CultureInfo.InvariantCulture, $"dateTri: {year}\n");
            }
        }
        var source = content.Source;
        string?[] sourceLines =
        [
            source.Kind is { } kind ? $"  type: {KindName(kind)}" : null,
            Value(source.Title) is { } title ? $"  titre: {title}" : null,
            Value(source.Page) is { } page ? $"  page: {page}" : null,
            Value(source.Address) is { } url ? $"  url: {url}" : null,
        ];
        if (sourceLines.Any(line => line is not null))
        {
            yaml.Append("source:\n");
            foreach (var line in sourceLines.OfType<string>())
            {
                yaml.Append(line).Append('\n');
            }
        }
        if (Value(content.Context) is { } context)
        {
            yaml.Append(CultureInfo.InvariantCulture, $"contexte: {context}\n");
        }
        var tags = FrontMatter.NormalizeTags(content.Tags);
        if (!tags.IsEmpty)
        {
            yaml.Append(CultureInfo.InvariantCulture, $"etiquettes: {FrontMatter.ListValue(tags)}\n");
        }
        yaml.Append(CultureInfo.InvariantCulture, $"ajoutee: {addedOn:yyyy-MM-dd}\n");
        foreach (var line in foreignLines)
        {
            yaml.Append(line).Append('\n');
        }
        return $"{yaml}---\n\n{content.Text.Trim()}\n";
    }

    /// <summary>Lignes de l'en-tête que kyfRev n'écrit pas : clés inconnues et leur suite.</summary>
    public static IReadOnlyList<string> ForeignLines(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        var kept = new List<string>();
        var keep = false;
        foreach (var line in RustLines(yaml))
        {
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && line[0] != '-' && line[0] != '#')
            {
                var key = line.Split(':')[0].Trim();
                keep = !KyfRevKeys.Contains(key, StringComparer.Ordinal);
            }
            if (keep && line.Trim().Length > 0)
            {
                kept.Add(line);
            }
        }
        return kept;
    }

    /// <summary>
    /// Année d'une date libre, pour le tri : « mai 1968 » → 1968, « vers 1650 » → 1650,
    /// « XVIIe siècle » → 1650, « IVe siècle av. J.-C. » → -350.
    /// </summary>
    public static int? Year(string date)
    {
        ArgumentNullException.ThrowIfNull(date);
        var lower = RustText.ToLowercase(date);
        var sign = lower.Contains("av. j", StringComparison.Ordinal) || lower.Contains("avant j", StringComparison.Ordinal) ? -1 : 1;
        var numbers = AsciiDigits().Matches(date).Select(m => m.Value).Where(n => n.Length <= 4).ToList();
        // Un nombre de 3 ou 4 chiffres est l'année ; sinon, le premier nombre.
        if ((numbers.FirstOrDefault(n => n.Length >= 3) ?? numbers.FirstOrDefault()) is { } digits)
        {
            return sign * int.Parse(digits, CultureInfo.InvariantCulture);
        }
        // Siècle en chiffres romains, suivi de « e » : « XVIIe ».
        var roman = date.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => TrimEndRepeated(word.TrimEnd('e', 'è'), "er"))
            .FirstOrDefault(word => word.Length > 0 && word.All(c => "IVXLC".Contains(c, StringComparison.Ordinal)));
        if (roman is null)
        {
            return null;
        }
        var total = 0;
        var previous = 0;
        foreach (var c in roman.Reverse())
        {
            var value = c switch
            {
                'I' => 1,
                'V' => 5,
                'X' => 10,
                'L' => 50,
                _ => 100,
            };
            if (value < previous)
            {
                total -= value;
            }
            else
            {
                total += value;
                previous = value;
            }
        }
        return total > 0 ? sign * (((total - 1) * 100) + 50) : null;
    }

    /// <summary>Nom de fichier lisible : « Blaise Pascal - Le cœur a ses raisons ».</summary>
    public static string FileName(QuoteContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var author = NonEmpty(content.Author) ?? "Auteur inconnu";
        var start = string.Join(' ', content.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(6));
        return FileNames.ForTitle($"{author} - {RustText.Take(start, 60)}");
    }

    /// <summary>Texte réduit à ses lettres et chiffres, en minuscules : deux citations identiques à la ponctuation près se reconnaissent.</summary>
    public static string TextFingerprint(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsLetter(rune) || Rune.GetUnicodeCategory(rune) is UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber)
            {
                builder.Append(RustText.ToLowercase(rune.ToString()));
            }
        }
        return builder.ToString();
    }

    /// <summary>Lit une citation ; refusée (fichier illisible) avec la raison.</summary>
    /// <exception cref="FormatException">En-tête illisible, ou citation sans texte.</exception>
    public static Quote Read(string text, string path, bool inTrash)
    {
        ArgumentNullException.ThrowIfNull(text);
        var split = FrontMatter.Split(text);
        var header = split.Yaml is { } yaml && yaml.Trim().Length > 0 ? ParseHeader(yaml) : new Header();
        var body = text[split.BodyStart..].Trim();
        if (body.Length == 0)
        {
            throw new FormatException("citation sans texte");
        }
        var date = NonEmpty(header.Date);
        var content = new QuoteContent(
            body,
            NonEmpty(header.Author),
            date,
            new QuoteSource(
                header.SourceKind is { } kind ? ReadKind(kind) : null,
                NonEmpty(header.SourceTitle),
                NonEmpty(header.SourcePage),
                NonEmpty(header.SourceUrl)),
            NonEmpty(header.Context),
            FrontMatter.NormalizeTags(header.Tags ?? header.TagsFallback ?? []));
        var addedOn = header.AddedOn is { } added && DateOnly.TryParseExact(added.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : (DateOnly?)null;
        return new Quote(path, NonEmpty(header.Id), content, date is null ? null : Year(date), addedOn, inTrash, NonEmpty(header.Origin));
    }

    internal static string KindName(SourceKind kind) => kind switch
    {
        SourceKind.Book => "livre",
        SourceKind.Newspaper => "journal",
        SourceKind.Article => "article",
        SourceKind.Speech => "discours",
        SourceKind.Website => "site",
        SourceKind.Film => "film",
        SourceKind.Song => "chanson",
        SourceKind.Broadcast => "émission",
        SourceKind.Other => "autre",
        _ => "inconnue",
    };

    private static SourceKind ReadKind(string name) => RustText.ToLowercase(name.Trim()) switch
    {
        "livre" => SourceKind.Book,
        "journal" => SourceKind.Newspaper,
        "article" => SourceKind.Article,
        "discours" => SourceKind.Speech,
        "site" => SourceKind.Website,
        "film" => SourceKind.Film,
        "chanson" => SourceKind.Song,
        "émission" or "emission" => SourceKind.Broadcast,
        "inconnue" or "inconnu" => SourceKind.Unknown,
        _ => SourceKind.Other,
    };

    private static string? NonEmpty(string? text) => text?.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    /// <summary>Valeur YAML d'un champ texte non vide.</summary>
    private static string? Value(string? text) => NonEmpty(text) is { } clean ? FrontMatter.TextValue(clean) : null;

    private static string TrimEndRepeated(string text, string suffix)
    {
        while (text.EndsWith(suffix, StringComparison.Ordinal))
        {
            text = text[..^suffix.Length];
        }
        return text;
    }

    /// <summary>Lignes comme <c>str::lines</c> : séparées par « \n », « \r » final retiré, pas de ligne vide finale.</summary>
    private static IEnumerable<string> RustLines(string text)
    {
        var lines = text.Split('\n');
        var count = text.EndsWith('\n') ? lines.Length - 1 : lines.Length;
        return lines.Take(count).Select(line => line.EndsWith('\r') ? line[..^1] : line);
    }

    private static Header ParseHeader(string yaml)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException exception)
        {
            throw new FormatException(exception.Message, exception);
        }
        if (stream.Documents.Count > 1)
        {
            throw new FormatException("l'en-tête contient plusieurs documents YAML");
        }
        var header = new Header();
        if (stream.Documents.Count == 0 || IsNull(stream.Documents[0].RootNode))
        {
            return header;
        }
        if (stream.Documents[0].RootNode is not YamlMappingNode mapping)
        {
            throw new FormatException("l'en-tête doit être une suite de champs « clé: valeur »");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (keyNode, value) in mapping.Children)
        {
            var key = (keyNode as YamlScalarNode)?.Value;
            if (key is not null && Array.IndexOf(HeaderKeys, key) >= 0 && !seen.Add(key))
            {
                throw new FormatException($"champ « {key} » en double");
            }
            switch (key)
            {
                case "id":
                    header.Id = Text(value, key);
                    break;
                case "auteur":
                    header.Author = Number(value, key);
                    break;
                case "date":
                    header.Date = Number(value, key);
                    break;
                case "contexte":
                    header.Context = Text(value, key);
                    break;
                case "etiquettes":
                    header.Tags = TextList(value, key);
                    break;
                case "tags":
                    header.TagsFallback = TextList(value, key);
                    break;
                case "ajoutee":
                    header.AddedOn = Text(value, key);
                    break;
                case "origine":
                    header.Origin = Text(value, key);
                    break;
                case "source" when !IsNull(value):
                    ReadSource(header, value as YamlMappingNode ?? throw new FormatException("« source » doit être une suite de champs"));
                    break;
                default:
                    break;
            }
        }
        return header;
    }

    private static readonly string[] HeaderKeys = ["id", "auteur", "date", "source", "contexte", "etiquettes", "tags", "ajoutee", "origine"];

    private static void ReadSource(Header header, YamlMappingNode source)
    {
        foreach (var (keyNode, value) in source.Children)
        {
            switch ((keyNode as YamlScalarNode)?.Value)
            {
                case "type":
                    header.SourceKind = Text(value, "type");
                    break;
                case "titre":
                    header.SourceTitle = Number(value, "titre");
                    break;
                case "page":
                    header.SourcePage = Number(value, "page");
                    break;
                case "url":
                    header.SourceUrl = Text(value, "url");
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Un texte : tout scalaire, tel qu'écrit.</summary>
    private static string? Text(YamlNode node, string key) => node switch
    {
        _ when IsNull(node) => null,
        YamlScalarNode scalar => scalar.Value ?? string.Empty,
        _ => throw new FormatException($"« {key} » doit être un texte"),
    };

    /// <summary>
    /// Un texte ou un nombre (« page: 12 » comme « page: "12-14" ») : un nombre YAML est réécrit
    /// comme Rust l'affiche (« 007 » → « 7 », « 12.50 » → « 12.5 ») ; un booléen est refusé.
    /// </summary>
    private static string? Number(YamlNode node, string key)
    {
        if (IsNull(node))
        {
            return null;
        }
        if (node is not YamlScalarNode scalar)
        {
            throw new FormatException($"« {key} » doit être un texte ou un nombre");
        }
        var value = scalar.Value ?? string.Empty;
        if (scalar.Style is not (ScalarStyle.Plain or ScalarStyle.Any))
        {
            return value;
        }
        if (value is "true" or "True" or "TRUE" or "false" or "False" or "FALSE")
        {
            throw new FormatException($"« {key} » doit être un texte ou un nombre");
        }
        if (YamlInteger().IsMatch(value) && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
        {
            return integer.ToString(CultureInfo.InvariantCulture);
        }
        if (YamlFloat().IsMatch(value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
        {
            return RustFloat(real);
        }
        return value;
    }

    /// <summary>Un nombre réel affiché comme <c>f64</c> en Rust : sans exposant, le plus court qui se relit à l'identique.</summary>
    private static string RustFloat(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }
        if (double.IsInfinity(value))
        {
            return value > 0 ? "inf" : "-inf";
        }
        return value.ToString("R", CultureInfo.InvariantCulture) is var shortest && shortest.Contains('E', StringComparison.Ordinal)
            ? decimal.TryParse(shortest, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact) ? exact.ToString(CultureInfo.InvariantCulture) : shortest
            : shortest;
    }

    private static List<string>? TextList(YamlNode node, string key) => node switch
    {
        _ when IsNull(node) => null,
        YamlSequenceNode sequence => [.. sequence.Children.Select(item => item is YamlScalarNode { Value: { } text } && !IsNull(item) ? text : throw new FormatException($"« {key} » doit être une liste de textes"))],
        _ => throw new FormatException($"« {key} » doit être une liste de textes"),
    };

    private static bool IsNull(YamlNode node) =>
        node is YamlScalarNode { Style: ScalarStyle.Plain or ScalarStyle.Any, Value: null or "" or "~" or "null" or "Null" or "NULL" };

    [GeneratedRegex("[0-9]+")]
    private static partial Regex AsciiDigits();

    [GeneratedRegex("^[-+]?[0-9]+$")]
    private static partial Regex YamlInteger();

    [GeneratedRegex(@"^[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?$")]
    private static partial Regex YamlFloat();

    private sealed class Header
    {
        public string? Id { get; set; }

        public string? Author { get; set; }

        public string? Date { get; set; }

        public string? Context { get; set; }

        public List<string>? Tags { get; set; }

        public List<string>? TagsFallback { get; set; }

        public string? AddedOn { get; set; }

        public string? Origin { get; set; }

        public string? SourceKind { get; set; }

        public string? SourceTitle { get; set; }

        public string? SourcePage { get; set; }

        public string? SourceUrl { get; set; }
    }
}
