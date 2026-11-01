using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KyfRev.Rendering.Markdown;

/// <summary>
/// Les adresses des liens, comme <c>mdurl</c> et markdown-it les traitent : la destination encodée, et le
/// texte affiché d'un lien automatique, où ce qui est lisible est décodé. Ce texte entre dans les positions.
/// </summary>
internal static partial class Url
{
    private const string DecodeExcluded = ";/?:@&=+$,#%";

    private const string EncodeKept = ";/?:@&=+$,-_.!~*'()#";

    private static readonly SearchValues<char> HostEndings = SearchValues.Create("/?#");

    private static readonly SearchValues<char> NonHostCharacters = SearchValues.Create("%/?;#'{}|\\^`<>\" \r\n\t");

    /// <summary>Refuse les protocoles dangereux ; seules quelques images <c>data:</c> passent.</summary>
    public static bool IsValidLink(string url)
    {
        var text = JsText.Trim(url);
        var dangerous = StartsWith(text, "vbscript:") || StartsWith(text, "javascript:") || StartsWith(text, "file:") || StartsWith(text, "data:");
        return !dangerous
            || StartsWith(text, "data:image/gif;") || StartsWith(text, "data:image/png;") || StartsWith(text, "data:image/jpeg;") || StartsWith(text, "data:image/webp;");
    }

    public static string NormalizeLink(string url)
    {
        var parsed = Parsed.From(url);
        if (parsed.Hostname.Length > 0 && parsed.RecodesHostname)
        {
            parsed.Hostname = Punycode.ToAscii(parsed.Hostname);
        }
        return parsed.Format(Encode);
    }

    public static string NormalizeLinkText(string url)
    {
        var parsed = Parsed.From(url);
        if (parsed.Hostname.Length > 0 && parsed.RecodesHostname)
        {
            parsed.Hostname = Punycode.ToUnicode(parsed.Hostname);
        }
        return Decode(parsed.Format(part => part));
    }

    private static bool StartsWith(string text, string prefix) => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static string Encode(string text)
    {
        var result = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '%' && i + 2 < text.Length && char.IsAsciiHexDigit(text[i + 1]) && char.IsAsciiHexDigit(text[i + 2]))
            {
                result.Append(text, i, 3);
                i += 2;
            }
            else if (ch < 128)
            {
                if (char.IsAsciiLetterOrDigit(ch) || EncodeKept.Contains(ch, StringComparison.Ordinal))
                {
                    result.Append(ch);
                }
                else
                {
                    AppendEscaped(result, [(byte)ch]);
                }
            }
            else if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                AppendEscaped(result, Encoding.UTF8.GetBytes(text.Substring(i, 2)));
                i++;
            }
            else
            {
                // Un surrogate isolé devient le caractère de remplacement.
                AppendEscaped(result, Encoding.UTF8.GetBytes(char.IsSurrogate(ch) ? "\uFFFD" : ch.ToString()));
            }
        }
        return result.ToString();
    }

    private static void AppendEscaped(StringBuilder result, byte[] bytes)
    {
        foreach (var value in bytes)
        {
            result.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
        }
    }

    private static string Decode(string text) => EscapedBytes().Replace(text, match => DecodeBytes(match.Value));

    private static int ByteAt(string sequence, int index) => int.Parse(sequence.AsSpan(index + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>Décode une suite de <c>%XX</c> en UTF-8 ; une suite invalide donne des caractères de remplacement.</summary>
    private static string DecodeBytes(string sequence)
    {
        var result = new StringBuilder();
        var length = sequence.Length;
        for (var i = 0; i < length; i += 3)
        {
            var b1 = ByteAt(sequence, i);
            if (b1 < 0x80)
            {
                var ch = (char)b1;
                if (DecodeExcluded.Contains(ch, StringComparison.Ordinal))
                {
                    result.Append('%').Append(b1.ToString("X2", CultureInfo.InvariantCulture));
                }
                else
                {
                    result.Append(ch);
                }
                continue;
            }
            if ((b1 & 0xE0) == 0xC0 && i + 3 < length)
            {
                var b2 = ByteAt(sequence, i + 3);
                if ((b2 & 0xC0) == 0x80)
                {
                    var code = ((b1 << 6) & 0x7C0) | (b2 & 0x3F);
                    result.Append(code < 0x80 ? "\uFFFD\uFFFD" : ((char)code).ToString());
                    i += 3;
                    continue;
                }
            }
            if ((b1 & 0xF0) == 0xE0 && i + 6 < length)
            {
                var b2 = ByteAt(sequence, i + 3);
                var b3 = ByteAt(sequence, i + 6);
                if ((b2 & 0xC0) == 0x80 && (b3 & 0xC0) == 0x80)
                {
                    var code = ((b1 << 12) & 0xF000) | ((b2 << 6) & 0xFC0) | (b3 & 0x3F);
                    result.Append(code is < 0x800 or (>= 0xD800 and <= 0xDFFF) ? "\uFFFD\uFFFD\uFFFD" : ((char)code).ToString());
                    i += 6;
                    continue;
                }
            }
            if ((b1 & 0xF8) == 0xF0 && i + 9 < length)
            {
                var b2 = ByteAt(sequence, i + 3);
                var b3 = ByteAt(sequence, i + 6);
                var b4 = ByteAt(sequence, i + 9);
                if ((b2 & 0xC0) == 0x80 && (b3 & 0xC0) == 0x80 && (b4 & 0xC0) == 0x80)
                {
                    var code = ((b1 << 18) & 0x1C0000) | ((b2 << 12) & 0x3F000) | ((b3 << 6) & 0xFC0) | (b4 & 0x3F);
                    result.Append(code is < 0x10000 or > 0x10FFFF ? "\uFFFD\uFFFD\uFFFD\uFFFD" : char.ConvertFromUtf32(code));
                    i += 9;
                    continue;
                }
            }
            result.Append('\uFFFD');
        }
        return result.ToString();
    }

    [GeneratedRegex("(?:%[a-f0-9]{2})+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EscapedBytes();

    [GeneratedRegex(@"\A[a-z0-9.+-]+:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Protocol();

    [GeneratedRegex(@"\A[+a-z0-9A-Z_-]{0,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex HostnamePart();

    [GeneratedRegex(@"\A([+a-z0-9A-Z_-]{0,63})([^\n\r\u2028\u2029]*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex HostnamePartStart();

    /// <summary>Une adresse découpée, comme par <c>mdurl.parse(url, true)</c>.</summary>
    private sealed class Parsed
    {
        public string Protocol { get; private set; } = string.Empty;

        public bool Slashes { get; private set; }

        public string Auth { get; private set; } = string.Empty;

        public string Port { get; private set; } = string.Empty;

        public string Hostname { get; set; } = string.Empty;

        public string Hash { get; private set; } = string.Empty;

        public string Search { get; private set; } = string.Empty;

        public string Pathname { get; private set; } = string.Empty;

        public bool RecodesHostname => Protocol.Length == 0 || Protocol is "http:" or "https:" or "mailto:";

        public static Parsed From(string url)
        {
            var parsed = new Parsed();
            parsed.Parse(url);
            return parsed;
        }

        public string Format(Func<string, string> encode)
        {
            var result = new StringBuilder(Protocol);
            result.Append(Slashes ? "//" : string.Empty);
            result.Append(Auth.Length > 0 ? encode(Auth) + "@" : string.Empty);
            var hostname = encode(Hostname);
            result.Append(hostname.Contains(':', StringComparison.Ordinal) ? $"[{hostname}]" : hostname);
            result.Append(Port.Length > 0 ? ":" + Port : string.Empty);
            result.Append(encode(Pathname)).Append(encode(Search)).Append(encode(Hash));
            return result.ToString();
        }

        private static bool IsSlashed(string protocol) => protocol is "http:" or "https:" or "ftp:" or "gopher:" or "file:";

        private void Parse(string url)
        {
            var rest = JsText.Trim(url);
            var protocol = Url.Protocol().Match(rest);
            if (protocol.Success)
            {
                Protocol = protocol.Value;
                rest = rest[Protocol.Length..];
            }
            var slashes = rest.StartsWith("//", StringComparison.Ordinal);
            if (slashes && Protocol != "javascript:")
            {
                rest = rest[2..];
                Slashes = true;
            }
            if (Protocol != "javascript:" && (slashes || (Protocol.Length > 0 && !IsSlashed(Protocol))))
            {
                rest = ParseAuthority(rest);
            }
            var hash = rest.IndexOf('#', StringComparison.Ordinal);
            if (hash != -1)
            {
                Hash = rest[hash..];
                rest = rest[..hash];
            }
            var query = rest.IndexOf('?', StringComparison.Ordinal);
            if (query != -1)
            {
                Search = rest[query..];
                rest = rest[..query];
            }
            Pathname = rest;
        }

        private string ParseAuthority(string rest)
        {
            var hostEnd = rest.AsSpan().IndexOfAny(HostEndings);
            var atSign = hostEnd == -1 ? rest.LastIndexOf('@') : rest.LastIndexOf('@', hostEnd);
            if (atSign != -1)
            {
                Auth = rest[..atSign];
                rest = rest[(atSign + 1)..];
            }
            hostEnd = rest.AsSpan().IndexOfAny(NonHostCharacters);
            if (hostEnd == -1)
            {
                hostEnd = rest.Length;
            }
            if (hostEnd > 0 && rest[hostEnd - 1] == ':')
            {
                hostEnd--;
            }
            ParseHost(rest[..hostEnd]);
            rest = rest[hostEnd..];
            var isIpv6 = Hostname.StartsWith('[') && Hostname.EndsWith(']');
            if (!isIpv6)
            {
                rest = CheckHostnameParts(rest);
            }
            if (Hostname.Length > 255)
            {
                Hostname = string.Empty;
            }
            if (isIpv6)
            {
                Hostname = Hostname[1..^1];
            }
            return rest;
        }

        private void ParseHost(string host)
        {
            var colon = host.LastIndexOf(':');
            if (colon != -1 && host.AsSpan(colon + 1).IndexOfAnyExceptInRange('0', '9') == -1)
            {
                Port = host[(colon + 1)..];
                host = host[..colon];
            }
            Hostname = host;
        }

        /// <summary>Ce qui ne peut pas être un nom d'hôte retourne dans le chemin.</summary>
        private string CheckHostnameParts(string rest)
        {
            var parts = Hostname.Split('.');
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length == 0 || HostnamePart().IsMatch(part))
                {
                    continue;
                }
                var ascii = string.Concat(part.Select(c => c > 127 ? 'x' : c));
                if (HostnamePart().IsMatch(ascii))
                {
                    continue;
                }
                var valid = parts.Take(i).ToList();
                var notHost = parts.Skip(i + 1).ToList();
                var bit = HostnamePartStart().Match(part);
                if (bit.Success)
                {
                    valid.Add(bit.Groups[1].Value);
                    notHost.Insert(0, bit.Groups[2].Value);
                }
                if (notHost.Count > 0)
                {
                    rest = string.Join('.', notHost) + rest;
                }
                Hostname = string.Join('.', valid);
                break;
            }
            return rest;
        }
    }
}
