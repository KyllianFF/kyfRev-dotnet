using System.Text;

namespace KyfRev.Rendering.Markdown;

/// <summary>
/// Les noms de domaine internationaux, comme <c>punycode.js</c> les traite : sans les contrôles d'un vrai
/// nom de domaine, car ce texte entre dans les positions. Un nom illisible reste écrit tel quel.
/// </summary>
internal static class Punycode
{
    private const int Base = 36;
    private const int TMin = 1;
    private const int TMax = 26;
    private const int Skew = 38;
    private const int Damp = 700;
    private const int InitialBias = 72;
    private const int InitialN = 128;
    private const string Prefix = "xn--";

    public static string ToUnicode(string domain)
    {
        var labels = Labels(domain);
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i].StartsWith(Prefix, StringComparison.Ordinal))
            {
                if (Decode(System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToLower(labels[i][Prefix.Length..])) is not { } decoded)
                {
                    return domain;
                }
                labels[i] = decoded;
            }
        }
        return string.Join('.', labels);
    }

    public static string ToAscii(string domain)
    {
        var labels = Labels(domain);
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i].Any(c => c > 0x7F))
            {
                labels[i] = Prefix + Encode(labels[i]);
            }
        }
        return string.Join('.', labels);
    }

    private static string[] Labels(string domain) =>
        domain.Replace('\u3002', '.').Replace('\uFF0E', '.').Replace('\uFF61', '.').Split('.');

    private static int Adapt(int delta, int points, bool firstTime)
    {
        var k = 0;
        delta = firstTime ? delta / Damp : delta >> 1;
        delta += delta / points;
        for (; delta > (Base - TMin) * TMax >> 1; k += Base)
        {
            delta /= Base - TMin;
        }
        return k + (((Base - TMin + 1) * delta) / (delta + Skew));
    }

    private static int Threshold(int k, int bias) => k <= bias ? TMin : k >= bias + TMax ? TMax : k - bias;

    private static int DigitOf(char c) => c switch
    {
        >= '0' and <= '9' => 26 + (c - '0'),
        >= 'A' and <= 'Z' => c - 'A',
        >= 'a' and <= 'z' => c - 'a',
        _ => Base,
    };

    private static char BasicOf(int digit) => (char)(digit + 22 + (digit < 26 ? 75 : 0));

    private static string? Decode(string input)
    {
        var output = new List<int>();
        var i = 0;
        var n = InitialN;
        var bias = InitialBias;
        var basic = Math.Max(input.LastIndexOf('-'), 0);
        for (var j = 0; j < basic; j++)
        {
            if (input[j] >= 0x80)
            {
                return null;
            }
            output.Add(input[j]);
        }
        for (var index = basic > 0 ? basic + 1 : 0; index < input.Length;)
        {
            var oldI = i;
            var w = 1;
            for (var k = Base; ; k += Base)
            {
                if (index >= input.Length)
                {
                    return null;
                }
                var digit = DigitOf(input[index++]);
                if (digit >= Base || digit > (int.MaxValue - i) / w)
                {
                    return null;
                }
                i += digit * w;
                var t = Threshold(k, bias);
                if (digit < t)
                {
                    break;
                }
                if (w > int.MaxValue / (Base - t))
                {
                    return null;
                }
                w *= Base - t;
            }
            var count = output.Count + 1;
            bias = Adapt(i - oldI, count, oldI == 0);
            if (i / count > int.MaxValue - n)
            {
                return null;
            }
            n += i / count;
            i %= count;
            output.Insert(i++, n);
        }
        return output.Exists(code => code > 0x10FFFF) ? null : FromCodePoints(output);
    }

    private static string FromCodePoints(List<int> codes)
    {
        var text = new StringBuilder();
        foreach (var code in codes)
        {
            // Comme String.fromCodePoint, qui écrit aussi un surrogate seul.
            if (code < 0x10000)
            {
                text.Append((char)code);
            }
            else
            {
                text.Append(char.ConvertFromUtf32(code));
            }
        }
        return text.ToString();
    }

    private static List<int> CodePoints(string text)
    {
        var codes = new List<int>();
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codes.Add(char.ConvertToUtf32(text[i], text[i + 1]));
                i++;
            }
            else
            {
                codes.Add(text[i]);
            }
        }
        return codes;
    }

    private static string Encode(string text)
    {
        var input = CodePoints(text);
        var output = new StringBuilder();
        foreach (var code in input.Where(code => code < 0x80))
        {
            output.Append((char)code);
        }
        var basicLength = output.Length;
        var handled = basicLength;
        if (basicLength > 0)
        {
            output.Append('-');
        }
        var n = InitialN;
        var delta = 0;
        var bias = InitialBias;
        while (handled < input.Count)
        {
            var m = input.Where(code => code >= n).Min();
            delta += (m - n) * (handled + 1);
            n = m;
            foreach (var code in input)
            {
                if (code < n)
                {
                    delta++;
                }
                if (code == n)
                {
                    var q = delta;
                    for (var k = Base; ; k += Base)
                    {
                        var t = Threshold(k, bias);
                        if (q < t)
                        {
                            break;
                        }
                        output.Append(BasicOf(t + ((q - t) % (Base - t))));
                        q = (q - t) / (Base - t);
                    }
                    output.Append(BasicOf(q));
                    bias = Adapt(delta, handled + 1, handled == basicLength);
                    delta = 0;
                    handled++;
                }
            }
            delta++;
            n++;
        }
        return output.ToString();
    }
}
