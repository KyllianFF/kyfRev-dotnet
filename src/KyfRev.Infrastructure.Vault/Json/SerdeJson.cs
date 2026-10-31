using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KyfRev.Infrastructure.Vault.Json;

/// <summary>
/// Écrit du JSON exactement comme <c>serde_json</c> (version 0.x) : deux espaces d'indentation,
/// « ": " », un élément par ligne, <c>[]</c> et <c>{}</c> pour les conteneurs vides, caractères
/// non ASCII écrits tels quels et échappements minimaux. L'ordre des propriétés est celui des
/// objets donnés.
/// </summary>
internal static class SerdeJson
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>JSON indenté, suivi d'un saut de ligne final (fichiers du coffre).</summary>
    public static byte[] PrettyFile(JsonNode node) => Utf8.GetBytes(Pretty(node) + "\n");

    /// <summary>JSON indenté, sans saut de ligne final.</summary>
    public static string Pretty(JsonNode? node)
    {
        var builder = new StringBuilder();
        WritePretty(builder, node, depth: 0);
        return builder.ToString();
    }

    /// <summary>JSON compact, sans espace.</summary>
    public static string Compact(JsonNode? node)
    {
        var builder = new StringBuilder();
        WriteCompact(builder, node);
        return builder.ToString();
    }

    /// <summary>
    /// Copie d'un JSON telle que <c>serde_json::Value</c> la réécrit : clés des objets triées par
    /// octets UTF-8 (à tous les niveaux), nombres réels au format de Rust.
    /// </summary>
    public static JsonNode? Canonical(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj
            .OrderBy(pair => pair.Key, Text.Utf8Order.Instance)
            .Select(pair => KeyValuePair.Create(pair.Key, Canonical(pair.Value)))),
        JsonArray array => new JsonArray([.. array.Select(Canonical)]),
        JsonValue value when value.GetValueKind() == JsonValueKind.Number && value.ToJsonString() is var raw && raw.AsSpan().IndexOfAny(".eE") >= 0 =>
            JsonValue.Create(JsonDocument.Parse(Float(double.Parse(raw, CultureInfo.InvariantCulture))).RootElement.Clone()),
        null => null,
        _ => node.DeepClone(),
    };

    /// <summary>
    /// Un <c>f64</c> écrit comme <c>serde_json</c> (algorithme Ryū) : le plus court qui se relit à
    /// l'identique, « .0 » pour une valeur entière, exposant au-delà de 16 chiffres ou en deçà de 10⁻⁵.
    /// </summary>
    public static string Float(double value)
    {
        if (value == 0)
        {
            return double.IsNegative(value) ? "-0.0" : "0.0";
        }
        var roundTrip = Math.Abs(value).ToString("R", CultureInfo.InvariantCulture);
        // Chiffres significatifs (sans point ni zéros de tête) et exposant de la représentation la plus courte.
        var mantissa = roundTrip;
        var exponent = 0;
        if (roundTrip.IndexOf('E', StringComparison.Ordinal) is var e and >= 0)
        {
            mantissa = roundTrip[..e];
            exponent = int.Parse(roundTrip[(e + 1)..], CultureInfo.InvariantCulture);
        }
        var dot = mantissa.IndexOf('.', StringComparison.Ordinal);
        var digits = mantissa.Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('0');
        var integerDigits = dot < 0 ? mantissa.Length : dot;
        var leadingZeros = mantissa.Replace(".", string.Empty, StringComparison.Ordinal).Length - digits.Length;
        digits = digits.TrimEnd('0');
        if (digits.Length == 0)
        {
            digits = "0";
        }
        // Position du point décimal après le premier chiffre significatif.
        var point = integerDigits - leadingZeros + exponent;
        var length = digits.Length;
        var k = point - length;
        var text = k switch
        {
            _ when k >= 0 && point <= 16 => digits + new string('0', k) + ".0",
            _ when point is > 0 and <= 16 => $"{digits[..point]}.{digits[point..]}",
            _ when point is > -5 and <= 0 => "0." + new string('0', -point) + digits,
            _ when length == 1 => $"{digits}e{point - 1}",
            _ => $"{digits[..1]}.{digits[1..]}e{point - 1}",
        };
        return value < 0 ? "-" + text : text;
    }

    private static void WritePretty(StringBuilder builder, JsonNode? node, int depth)
    {
        switch (node)
        {
            case JsonObject obj when obj.Count == 0:
                builder.Append("{}");
                break;
            case JsonObject obj:
                builder.Append("{\n");
                var firstProperty = true;
                foreach (var (key, value) in obj)
                {
                    if (!firstProperty)
                    {
                        builder.Append(",\n");
                    }
                    firstProperty = false;
                    Indent(builder, depth + 1);
                    WriteString(builder, key);
                    builder.Append(": ");
                    WritePretty(builder, value, depth + 1);
                }
                builder.Append('\n');
                Indent(builder, depth);
                builder.Append('}');
                break;
            case JsonArray array when array.Count == 0:
                builder.Append("[]");
                break;
            case JsonArray array:
                builder.Append("[\n");
                for (var i = 0; i < array.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(",\n");
                    }
                    Indent(builder, depth + 1);
                    WritePretty(builder, array[i], depth + 1);
                }
                builder.Append('\n');
                Indent(builder, depth);
                builder.Append(']');
                break;
            default:
                WriteScalar(builder, node);
                break;
        }
    }

    private static void WriteCompact(StringBuilder builder, JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                builder.Append('{');
                var firstProperty = true;
                foreach (var (key, value) in obj)
                {
                    if (!firstProperty)
                    {
                        builder.Append(',');
                    }
                    firstProperty = false;
                    WriteString(builder, key);
                    builder.Append(':');
                    WriteCompact(builder, value);
                }
                builder.Append('}');
                break;
            case JsonArray array:
                builder.Append('[');
                for (var i = 0; i < array.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }
                    WriteCompact(builder, array[i]);
                }
                builder.Append(']');
                break;
            default:
                WriteScalar(builder, node);
                break;
        }
    }

    private static void WriteScalar(StringBuilder builder, JsonNode? node)
    {
        if (node is null)
        {
            builder.Append("null");
            return;
        }
        var value = node.AsValue();
        switch (value.GetValueKind())
        {
            case JsonValueKind.String:
                WriteString(builder, value.GetValue<string>());
                break;
            case JsonValueKind.True:
                builder.Append("true");
                break;
            case JsonValueKind.False:
                builder.Append("false");
                break;
            default:
                // Nombre : tel qu'il a été lu, ou écrit par .NET sans exposant pour un entier.
                builder.Append(value.ToJsonString());
                break;
        }
    }

    private static void WriteString(StringBuilder builder, string text)
    {
        builder.Append('"');
        foreach (var c in text)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case < ' ':
                    builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }
        builder.Append('"');
    }

    private static void Indent(StringBuilder builder, int depth) => builder.Append(' ', depth * 2);
}
