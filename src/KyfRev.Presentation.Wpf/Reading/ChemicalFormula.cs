using System.Text;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// L'écriture chimique <c>\ce{…}</c> (celle de mhchem), traduite en LaTeX ordinaire pour le moteur de formules : les
/// symboles en romain, les indices, les charges, les flèches de réaction. C'est le cœur de mhchem, pas tout mhchem : ce
/// qu'elle ne comprend pas passe tel quel, et la formule reste lisible.
/// </summary>
internal static class ChemicalFormula
{
    private const string Command = "\\ce";

    /// <summary>Remplace chaque <c>\ce{…}</c> d'une formule par son LaTeX ; une formule sans chimie ne change pas.</summary>
    public static string Expand(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var at = source.IndexOf(Command, StringComparison.Ordinal);
        if (at < 0)
        {
            return source;
        }
        var result = new StringBuilder(source.Length + 32);
        var from = 0;
        while (at >= 0)
        {
            var open = at + Command.Length;
            while (open < source.Length && source[open] == ' ')
            {
                open++;
            }
            var close = open < source.Length && source[open] == '{' ? Closing(source, open) : -1;
            if (close < 0)
            {
                // « \cellule » ou une accolade jamais fermée : ce n'est pas de la chimie.
                at = source.IndexOf(Command, at + Command.Length, StringComparison.Ordinal);
                continue;
            }
            result.Append(source, from, at - from).Append('{').Append(Translate(source.AsSpan(open + 1, close - open - 1))).Append('}');
            from = close + 1;
            at = source.IndexOf(Command, from, StringComparison.Ordinal);
        }
        return result.Append(source, from, source.Length - from).ToString();
    }

    /// <summary>L'accolade qui ferme celle ouverte en <paramref name="open"/> ; -1 si elle manque.</summary>
    private static int Closing(string source, int open)
    {
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '\\')
            {
                i++;
            }
            else if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}' && --depth == 0)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Traduit le contenu d'un <c>\ce{…}</c>. Un nombre en tête d'un terme est un coefficient ; après un symbole ou une
    /// parenthèse, un indice. Un « + » ou un « - » collé à ce qui précède et suivi d'un blanc est une charge.
    /// </summary>
    private static string Translate(ReadOnlySpan<char> text)
    {
        var result = new StringBuilder(text.Length * 2);
        // En tête de terme : rien n'a encore été écrit depuis le début, un blanc, un signe ou une flèche.
        var termStart = true;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var rest = text[i..];
            if (Arrow(rest) is var (arrow, length) && length > 0)
            {
                result.Append(' ').Append(arrow).Append(' ');
                termStart = true;
                i += length;
            }
            else if (c == '\\')
            {
                // Une commande LaTeX passe telle quelle, avec son nom.
                var end = 1;
                while (end < rest.Length && char.IsAsciiLetter(rest[end]))
                {
                    end++;
                }
                end = Math.Max(end, Math.Min(2, rest.Length));
                result.Append(rest[..end]).Append(' ');
                termStart = false;
                i += end;
            }
            else if (char.IsWhiteSpace(c))
            {
                result.Append("\\;");
                termStart = true;
                i++;
            }
            else if (char.IsAsciiLetter(c))
            {
                var end = 1;
                while (end < rest.Length && char.IsAsciiLetter(rest[end]))
                {
                    end++;
                }
                result.Append("\\mathrm{").Append(rest[..end]).Append('}');
                termStart = false;
                i += end;
            }
            else if (char.IsAsciiDigit(c))
            {
                var end = 1;
                while (end < rest.Length && char.IsAsciiDigit(rest[end]))
                {
                    end++;
                }
                if (termStart)
                {
                    result.Append(rest[..end]);
                }
                else
                {
                    result.Append("_{").Append(rest[..end]).Append('}');
                }
                // Après un coefficient vient le symbole, dont les chiffres seront des indices.
                termStart = false;
                i += end;
            }
            else if (c == '^')
            {
                i += Charge(rest, result);
                termStart = false;
            }
            else if (c is '+' or '-' && !termStart && (rest.Length == 1 || char.IsWhiteSpace(rest[1]) || rest[1] is ')' or ']'))
            {
                // « Na+ », « OH- » : collé au symbole et suivi d'un blanc ou de la fin, c'est une charge.
                result.Append("^{").Append(c).Append('}');
                i++;
            }
            else if (c is '.' or '*' && !termStart)
            {
                // Un hydrate : « CuSO4.5H2O ». Le nombre qui suit est un coefficient.
                result.Append(" \\cdot ");
                termStart = true;
                i++;
            }
            else
            {
                result.Append(c);
                termStart = c is '+' or '=' or '(' or '[' or '{';
                i++;
            }
        }
        return result.ToString();
    }

    /// <summary>La flèche qui commence ici, et sa longueur dans le texte ; une longueur nulle sans flèche.</summary>
    private static (string Arrow, int Length) Arrow(ReadOnlySpan<char> rest) =>
        rest.StartsWith("<=>", StringComparison.Ordinal) ? ("\\rightleftharpoons", 3)
        : rest.StartsWith("<->", StringComparison.Ordinal) ? ("\\leftrightarrow", 3)
        : rest.StartsWith("->", StringComparison.Ordinal) ? ("\\rightarrow", 2)
        : rest.StartsWith("<-", StringComparison.Ordinal) ? ("\\leftarrow", 2)
        : (string.Empty, 0);

    /// <summary>« ^2+ », « ^{2-} », « ^+ » : un exposant, avec son signe ; rend ce qui a été lu.</summary>
    private static int Charge(ReadOnlySpan<char> rest, StringBuilder result)
    {
        if (rest.Length > 1 && rest[1] == '{')
        {
            var close = rest.IndexOf('}');
            if (close > 0)
            {
                result.Append("^{").Append(rest[2..close]).Append('}');
                return close + 1;
            }
        }
        var end = 1;
        while (end < rest.Length && char.IsAsciiDigit(rest[end]))
        {
            end++;
        }
        if (end < rest.Length && rest[end] is '+' or '-')
        {
            end++;
        }
        result.Append("^{").Append(rest[1..end]).Append('}');
        return end;
    }
}
