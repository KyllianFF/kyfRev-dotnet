using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace KyfRev.Rendering.Markdown;

/// <summary>Les entités HTML : les noms de la norme, écrits avec leur point-virgule, et les codes numériques.</summary>
internal static partial class Entities
{
    private static readonly Lazy<Dictionary<string, string>> Named = new(LoadNamed);

    /// <summary>Le texte d'une entité nommée ; rien si ce nom n'existe pas.</summary>
    public static string? Decode(string name) => Named.Value.GetValueOrDefault(name);

    /// <summary>Un code qui peut figurer dans un document : pas de surrogate, de non-caractère ni de contrôle.</summary>
    public static bool IsValidCode(int code) => code switch
    {
        >= 0xD800 and <= 0xDFFF or >= 0xFDD0 and <= 0xFDEF => false,
        _ when (code & 0xFFFF) is 0xFFFF or 0xFFFE => false,
        >= 0 and <= 8 or 11 or >= 14 and <= 31 or >= 127 and <= 159 => false,
        > 0x10FFFF => false,
        _ => true,
    };

    /// <summary>Le texte d'une entité trouvée dans une destination ; elle reste écrite telle quelle si elle n'existe pas.</summary>
    public static string DecodeReference(string match, string name)
    {
        var digital = DigitalReference().Match(name);
        if (digital.Success)
        {
            var value = digital.Groups[1].Value;
            var code = value[0] is 'x' or 'X'
                ? long.Parse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : long.Parse(value, CultureInfo.InvariantCulture);
            return code <= 0x10FFFF && IsValidCode((int)code) ? char.ConvertFromUtf32((int)code) : match;
        }
        return Decode(name) ?? match;
    }

    private static Dictionary<string, string> LoadNamed()
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("KyfRev.Rendering.Markdown.html5-entities.txt")!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split(' ');
            table[parts[0]] = string.Concat(parts.Skip(1).Select(code => char.ConvertFromUtf32(int.Parse(code, NumberStyles.HexNumber, CultureInfo.InvariantCulture))));
        }
        return table;
    }

    [GeneratedRegex(@"\A#((?:x[a-f0-9]{1,8}|[0-9]{1,8}))\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DigitalReference();
}
