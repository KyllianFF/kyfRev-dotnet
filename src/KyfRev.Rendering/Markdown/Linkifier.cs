using System.Text.RegularExpressions;

namespace KyfRev.Rendering.Markdown;

/// <summary>Un lien trouvé dans le texte.</summary>
/// <param name="Schema">Son protocole, en minuscules (<c>https:</c>, <c>mailto:</c>, <c>//</c>…).</param>
/// <param name="Index">Son début.</param>
/// <param name="LastIndex">Sa fin.</param>
/// <param name="Text">Son texte.</param>
/// <param name="Url">Son adresse.</param>
internal sealed record LinkMatch(string Schema, int Index, int LastIndex, string Text, string Url);

/// <summary>
/// Les liens écrits dans le texte sans syntaxe, comme les trouve <c>linkify-it</c> avec les réglages de
/// markdown-it : les adresses à protocole et les courriels, pas les noms de domaine seuls. Les expressions
/// sont les siennes, à trois adaptations près : <c>$</c> s'écrit <c>\z</c>, <c>\d</c> s'écrit <c>[0-9]</c>
/// et l'accroche à une position s'écrit <c>\G</c>.
/// </summary>
internal static class Linkifier
{
    private const int MaxLength = 10000;

    private const RegexOptions Insensitive = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private const string ZPCc = UnicodeClasses.Z + "|" + UnicodeClasses.P + "|" + UnicodeClasses.Cc;

    private const string ZCc = UnicodeClasses.Z + "|" + UnicodeClasses.Cc;

    private const string TextSeparators = "[><\uFF5C]";

    private const string PseudoLetter = "(?:(?!" + TextSeparators + "|" + ZPCc + ")" + UnicodeClasses.Any + ")";

    private const string Ipv4 = "(?:(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9][0-9]|[0-9])[.]){3}(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9][0-9]|[0-9])";

    private const string H16 = "[0-9A-Fa-f]{1,4}";

    private const string Ls32 = "(?:(?:" + H16 + ":" + H16 + ")|" + Ipv4 + ")";

    private const string Ipv6 = "(?:(?:" + H16 + ":){6}" + Ls32
        + "|::(?:" + H16 + ":){5}" + Ls32
        + "|(?:" + H16 + ")?::(?:" + H16 + ":){4}" + Ls32
        + "|(?:(?:" + H16 + ":){0,1}" + H16 + ")?::(?:" + H16 + ":){3}" + Ls32
        + "|(?:(?:" + H16 + ":){0,2}" + H16 + ")?::(?:" + H16 + ":){2}" + Ls32
        + "|(?:(?:" + H16 + ":){0,3}" + H16 + ")?::" + H16 + ":" + Ls32
        + "|(?:(?:" + H16 + ":){0,4}" + H16 + ")?::" + Ls32
        + "|(?:(?:" + H16 + ":){0,5}" + H16 + ")?::" + H16
        + "|(?:(?:" + H16 + ":){0,6}" + H16 + ")?::)";

    private const string Ipv6UrlHost = @"\[" + Ipv6 + @"\]";

    private const string Ipv6MailHost = @"\[IPv6:" + Ipv6 + @"\]";

    private const string Port = "(?::(?:6(?:[0-4][0-9]{3}|5(?:[0-4][0-9]{2}|5(?:[0-2][0-9]|3[0-5])))|[1-5]?[0-9]{1,4}))?";

    private const string HostTerminator = @"(?=\z|" + TextSeparators + "|" + ZPCc + @")(?!-|_|:[0-9]|\.-|\.(?!\z|" + ZPCc + "))";

    private const string PathTerminator = ZPCc + "|" + TextSeparators;

    private const string MailName = "[-!#$%&'*+/=?^_`{|}~a-zA-Z0-9](?:[-!#$%&'*+/=?^_`{|}~a-zA-Z0-9]|[.](?=[-!#$%&'*+/=?^_`{|}~a-zA-Z0-9])){0,63}";

    private const string Xn = @"xn--[a-z0-9\-]{1,59}";

    private const string DomainRoot = "(?:" + Xn + "|" + PseudoLetter + "{1,63})";

    private const string Domain = "(?:" + Xn + "|(?:" + PseudoLetter + ")|(?:" + PseudoLetter + "(?:-|" + PseudoLetter + "){0,61}" + PseudoLetter + "))";

    private const string UrlHostPort = "(?:" + Ipv6UrlHost + "|(?:(?:(?:" + Domain + @")\.){0,10}" + Domain + "))" + Port + HostTerminator;

    private const string MailHost = "(?:" + Ipv6MailHost + "|(?:(?:(?:" + Domain + @")\.){0,4}" + Domain + "))" + HostTerminator;

    private const string FuzzyMailHost = "(?:" + Ipv6MailHost + "|(?:(?:(?:" + Domain + ")[.]){1,4}" + DomainRoot + "))" + HostTerminator;

    private const string SchemaSearch = "(^|(?!_)(?:[><\uFF5C]|" + ZPCc + "))(http:|https:|ftp:|//|mailto:)";

    private static readonly Lazy<string> Path = new(BuildPath);

    private static readonly Lazy<Regex> SchemaSearchExpression = new(() => new Regex(SchemaSearch, Insensitive));

    private static readonly Lazy<Regex> SchemaAtStartExpression = new(() => new Regex(@"\A" + SchemaSearch, Insensitive));

    private static readonly Lazy<Regex> HttpValidator = new(() => new Regex(@"\G//" + UrlHostPort + Path.Value, Insensitive));

    private static readonly Lazy<Regex> RelativeProtocolValidator = new(() => new Regex(
        @"\G(?:localhost|" + Ipv6UrlHost + "|(?:(?:" + Domain + ")[.]){1,10}" + DomainRoot + ")" + Port + HostTerminator + Path.Value,
        Insensitive));

    private static readonly Lazy<Regex> MailtoValidator = new(() => new Regex(@"\G" + MailName + "@" + MailHost, Insensitive));

    private static readonly Lazy<Regex> MailNameValidator = new(() => new Regex(
        "(?:^|" + TextSeparators + "|\"|\\(|" + ZCc + ")(" + MailName + @")\z",
        RegexOptions.CultureInvariant));

    private static readonly Lazy<Regex> FuzzyMailHostSearch = new(() => new Regex("@" + FuzzyMailHost, Insensitive));

    /// <summary>Le texte contient-il un lien ?</summary>
    public static bool Test(string text)
    {
        if (MayHaveSchema(text))
        {
            for (var match = SchemaSearchExpression.Value.Match(text); match.Success; match = match.NextMatch())
            {
                if (TestSchemaAt(text, match.Groups[2].Value, match.Index + match.Length) != 0)
                {
                    return true;
                }
            }
        }
        if (text.Contains('@', StringComparison.Ordinal))
        {
            for (var match = FuzzyMailHostSearch.Value.Match(text); match.Success; match = match.NextMatch())
            {
                if (MailNameValidator.Value.IsMatch(text[Math.Max(0, match.Index - 65)..match.Index]))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Tous les liens du texte, dans l'ordre.</summary>
    public static List<LinkMatch> Match(string text) => new Scan(text).All();

    /// <summary>Le lien qui commence le texte ; rien s'il n'y en a pas.</summary>
    public static LinkMatch? MatchAtStart(string text)
    {
        var match = SchemaAtStartExpression.Value.Match(text);
        if (!match.Success)
        {
            return null;
        }
        var length = TestSchemaAt(text, match.Groups[2].Value, match.Length);
        return length == 0 ? null : ToLink(text, match.Groups[2].Value, match.Groups[1].Length, match.Length + length);
    }

    private static bool MayHaveSchema(string text) => text.Contains(':', StringComparison.Ordinal) || text.Contains("//", StringComparison.Ordinal);

    private static LinkMatch ToLink(string text, string schema, int index, int lastIndex)
    {
        var raw = text[index..lastIndex];
        var lowered = schema.ToUpperInvariant() switch
        {
            "HTTP:" => "http:",
            "HTTPS:" => "https:",
            "FTP:" => "ftp:",
            "MAILTO:" => "mailto:",
            _ => "//",
        };
        var url = lowered == "mailto:" && !raw.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? "mailto:" + raw : raw;
        return new LinkMatch(lowered, index, lastIndex, raw, url);
    }

    /// <summary>La longueur de ce qui suit un protocole, s'il mène bien à une adresse ; sinon 0.</summary>
    private static int TestSchemaAt(string text, string schema, int position)
    {
        var limited = text.Length > position + MaxLength ? text[..(position + MaxLength)] : text;
        var kind = schema.ToUpperInvariant();
        if (kind == "//")
        {
            var relative = RelativeProtocolValidator.Value.Match(limited, position);
            return relative.Success && !(position >= 3 && limited[position - 3] is ':' or '/') ? relative.Length : 0;
        }
        var validator = kind == "MAILTO:" ? MailtoValidator.Value : HttpValidator.Value;
        var match = validator.Match(limited, position);
        return match.Success ? match.Length : 0;
    }

    private static string NestedPair(string open, string close)
    {
        var openExpression = Regex.Escape(open);
        var closeExpression = Regex.Escape(close);
        var atom = "(?:(?!" + ZCc + "|" + openExpression + "|" + closeExpression + ").)";
        var pair = openExpression + atom + "{0,1000}" + closeExpression;
        for (var level = 2; level <= 4; level++)
        {
            pair = openExpression + "(?:" + atom + "|" + pair + "){0,1000}" + closeExpression;
        }
        return pair;
    }

    private static string BuildPath() =>
        "(?:[/?#](?:" + NestedPair("[", "]") + "|" + NestedPair("(", ")") + "|" + NestedPair("{", "}")
        + "|\\\"(?:(?!" + ZCc + "|[\"]).){1,100}\\\""
        + "|\\'(?:(?!" + ZCc + "|[']).){1,100}\\'"
        + "|\\'(?=" + PseudoLetter + "|[-])"
        + "|\\.{2,20}[:]?[a-zA-Z0-9%/&]"
        + "|\\.(?!" + ZCc + "|[.]|\\z)"
        + "|\\-{1,20}"
        + "|,(?!" + ZCc + "|\\z)"
        + "|;(?!" + ZCc + "|\\z)"
        + "|\\!{1,20}(?!" + ZCc + "|[!]|\\z)"
        + "|\\?(?!" + ZCc + "|[?]|\\z)"
        + "|[\\\\/:%@#&=_~*]"
        + "|(?!" + PathTerminator + ").){1," + MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}|\\/)?";

    /// <summary>Le parcours d'un texte : à chaque pas, l'adresse ou le courriel qui vient le premier.</summary>
    private sealed class Scan(string text)
    {
        private readonly List<LinkMatch> _result = [];
        private int _position;
        private int _schemaPosition;
        private int _mailPosition;
        private bool _schemaDone = !MayHaveSchema(text);
        private bool _mailDone = !text.Contains('@', StringComparison.Ordinal);
        private Candidate? _mail;
        private Candidate? _prefix;

        public List<LinkMatch> All()
        {
            while (true)
            {
                var scanFrom = Math.Max(_position - 1, 0);
                FindMail(scanFrom);
                var schema = FindSchema(scanFrom);
                var useMail = _mail is not null
                    && (schema is null || _mail.Index < schema.Index || (_mail.Index == schema.Index && _mail.LastIndex > schema.LastIndex));
                var candidate = useMail ? _mail : schema;
                if (candidate is null)
                {
                    return _result;
                }
                if (useMail)
                {
                    _mail = null;
                }
                _result.Add(ToLink(text, candidate.Schema, candidate.Index, candidate.LastIndex));
                _position = candidate.LastIndex;
            }
        }

        private void FindMail(int scanFrom)
        {
            if (_mailDone || (_mail is not null && _mail.Index >= _position))
            {
                return;
            }
            _mailPosition = Math.Max(_mailPosition, scanFrom);
            while (true)
            {
                var host = FuzzyMailHostSearch.Value.Match(text, _mailPosition);
                if (!host.Success)
                {
                    _mailDone = true;
                    _mail = null;
                    return;
                }
                _mailPosition = host.Index + host.Length;
                var name = MailNameValidator.Value.Match(text[Math.Max(0, host.Index - 65)..host.Index]);
                if (!name.Success)
                {
                    continue;
                }
                _mail = new Candidate("mailto:", host.Index - name.Groups[1].Length, host.Index + host.Length);
                if (_mail.Index >= _position)
                {
                    return;
                }
                _mailPosition = Math.Max(_mailPosition, scanFrom);
            }
        }

        private Candidate? FindSchema(int scanFrom)
        {
            while (!_schemaDone)
            {
                if (_prefix is null)
                {
                    _schemaPosition = Math.Max(_schemaPosition, scanFrom);
                    var found = SchemaSearchExpression.Value.Match(text, _schemaPosition);
                    if (!found.Success)
                    {
                        _schemaDone = true;
                        return null;
                    }
                    _schemaPosition = found.Index + found.Length;
                    _prefix = new Candidate(found.Groups[2].Value, found.Index + found.Groups[1].Length, found.Index + found.Length);
                }
                if (_prefix.Index < _position)
                {
                    _prefix = null;
                    continue;
                }
                if (_mail is not null && _prefix.Index > _mail.Index)
                {
                    return null;
                }
                var current = _prefix;
                _prefix = null;
                var length = TestSchemaAt(text, current.Schema, current.LastIndex);
                if (length != 0)
                {
                    return current with { LastIndex = current.LastIndex + length };
                }
            }
            return null;
        }

        private sealed record Candidate(string Schema, int Index, int LastIndex);
    }
}
