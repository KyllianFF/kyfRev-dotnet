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
