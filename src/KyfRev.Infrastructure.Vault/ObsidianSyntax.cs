using System.Text;

namespace KyfRev.Infrastructure.Vault;

/// <summary>
/// Ce que l'écriture d'Obsidian a de propre, hors des liens <c>[[…]]</c>, et que kyfRev afficherait tel quel : les encadrés
/// <c>&gt; [!note]</c>, le surlignage <c>==…==</c> et les commentaires <c>%%…%%</c>. Rien n'est touché dans le code, en
/// bloc comme en ligne.
/// </summary>
internal static class ObsidianSyntax
{
    /// <summary>Le nom français des encadrés courants ; un type inconnu garde le sien, avec une majuscule.</summary>
    private static readonly Dictionary<string, string> CalloutNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["note"] = "Note",
        ["info"] = "Info",
        ["tip"] = "Astuce",
        ["hint"] = "Astuce",
        ["important"] = "Important",
        ["warning"] = "Attention",
        ["caution"] = "Attention",
        ["attention"] = "Attention",
        ["danger"] = "Danger",
        ["error"] = "Erreur",
        ["bug"] = "Bogue",
        ["question"] = "Question",
        ["help"] = "Question",
        ["faq"] = "Question",
        ["example"] = "Exemple",
        ["quote"] = "Citation",
        ["cite"] = "Citation",
        ["abstract"] = "Résumé",
        ["summary"] = "Résumé",
        ["tldr"] = "Résumé",
        ["todo"] = "À faire",
        ["success"] = "Réussi",
        ["check"] = "Réussi",
        ["done"] = "Réussi",
        ["failure"] = "Échec",
        ["fail"] = "Échec",
        ["missing"] = "Échec",
    };

    /// <summary>Ce qui a été converti dans une note.</summary>
    /// <param name="Text">Le texte converti.</param>
    /// <param name="Callouts">Encadrés devenus des citations titrées.</param>
    /// <param name="Highlights">Surlignages devenus du gras.</param>
    /// <param name="Comments">Commentaires retirés.</param>
    internal readonly record struct Result(string Text, int Callouts, int Highlights, int Comments);

    /// <summary>
    /// Convertit une note. Un encadré devient une citation dont la première ligne dit le type et le titre. Un surlignage
    /// devient du gras, que la révision masque comme un trou ; s'il contient déjà une étoile, il est souligné. Un
    /// commentaire, sur une ligne ou sur plusieurs, est retiré : Obsidian ne le montre pas non plus à la lecture.
    /// </summary>
    public static Result Convert(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new StringBuilder(text.Length);
        var (callouts, highlights, comments) = (0, 0, 0);
        (char Mark, int Length)? fence = null;
        var inComment = false;
        var start = 0;
        while (start < text.Length)
        {
            var newline = text.IndexOf('\n', start);
            var next = newline < 0 ? text.Length : newline + 1;
            var line = text[start..next];
            start = next;

            var rest = line.TrimStart(' ');
            var indent = line.Length - rest.Length;
            char? mark = rest.Length > 0 && rest[0] is '`' or '~' ? rest[0] : null;
            var run = mark is { } m ? rest.TakeWhile(c => c == m).Count() : 0;
            if (!inComment)
            {
                if (fence is { } open)
                {
                    if (indent <= 3 && mark == open.Mark && run >= open.Length && rest[run..].Trim().Length == 0)
                    {
                        fence = null;
                    }
                    result.Append(line);
                    continue;
                }
                if (indent <= 3 && run >= 3)
                {
                    fence = (mark!.Value, run);
                    result.Append(line);
                    continue;
                }
                if (Callout(line) is { } titled)
                {
                    line = titled;
                    callouts++;
                }
            }
            var before = result.Length;
            var commented = inComment;
            var seen = comments;
            var hadText = Inline(line, result, ref inComment, ref highlights, ref comments);
            // Une ligne qui n'était qu'un commentaire disparaît avec lui, sans laisser de ligne vide ; une ligne vide de la
            // note, elle, reste.
            if (!hadText && (commented || comments > seen))
            {
                result.Length = before;
            }
        }
        return new Result(result.ToString(), callouts, highlights, comments);
    }

    /// <summary>« &gt; [!warning]- Titre » devient « &gt; Attention : Titre » ; rien si la ligne n'ouvre pas un encadré.</summary>
    private static string? Callout(string line)
    {
        var quote = 0;
        while (quote < line.Length && line[quote] is ' ' or '>')
        {
            quote++;
        }
        var prefix = line[..quote];
        if (!prefix.Contains('>', StringComparison.Ordinal) || !line.AsSpan(quote).StartsWith("[!", StringComparison.Ordinal))
        {
            return null;
        }
        var close = line.IndexOf(']', quote);
        if (close < 0)
        {
            return null;
        }
        var type = line[(quote + 2)..close].Trim();
        if (type.Length == 0 || !type.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
        {
            return null;
        }
        var after = line[(close + 1)..];
        // « + » et « - » replient ou déplient l'encadré dans Obsidian : kyfRev n'a pas de repli.
        if (after.Length > 0 && after[0] is '+' or '-')
        {
            after = after[1..];
        }
        var ending = after.EndsWith("\r\n", StringComparison.Ordinal) ? "\r\n" : after.EndsWith('\n') ? "\n" : string.Empty;
        var title = after.Trim();
        var name = CalloutNames.TryGetValue(type, out var known) ? known : char.ToUpperInvariant(type[0]) + type[1..];
        return title.Length == 0 ? $"{prefix}{name}{ending}" : $"{prefix}{name} : {title}{ending}";
    }

    /// <summary>Une ligne hors bloc de code : le code en ligne recopié, les commentaires retirés, les surlignages convertis.</summary>
    /// <returns>Vrai si la ligne portait autre chose qu'un commentaire.</returns>
    private static bool Inline(string line, StringBuilder result, ref bool inComment, ref int highlights, ref int comments)
    {
        var hadText = false;
        var i = 0;
        while (i < line.Length)
        {
            var rest = line.AsSpan(i);
            if (inComment)
            {
                var close = rest.IndexOf("%%", StringComparison.Ordinal);
                if (close < 0)
                {
                    // Le commentaire court encore : la fin de ligne part avec lui.
                    return hadText;
                }
                inComment = false;
                i += close + 2;
                continue;
            }
            if (rest[0] == '`')
            {
                var ticks = 0;
                while (ticks < rest.Length && rest[ticks] == '`')
                {
                    ticks++;
                }
                var closing = rest[ticks..].IndexOf(rest[..ticks], StringComparison.Ordinal);
                var length = closing >= 0 ? ticks + closing + ticks : ticks;
                result.Append(rest[..length]);
                hadText = true;
                i += length;
                continue;
            }
            if (rest.StartsWith("%%", StringComparison.Ordinal))
            {
                comments++;
                inComment = true;
                i += 2;
                continue;
            }
            if (rest.StartsWith("==", StringComparison.Ordinal) && rest.Length > 4 && !char.IsWhiteSpace(rest[2]) && rest[2] != '=')
            {
                var close = rest[2..].IndexOf("==", StringComparison.Ordinal);
                if (close > 0 && !char.IsWhiteSpace(rest[1 + close]) && rest[2..(2 + close)].IndexOfAny('`', '\n') < 0)
                {
                    var inside = rest[2..(2 + close)];
                    // Du gras dans du gras ne se lirait plus : un surlignage qui a déjà ses étoiles est souligné.
                    var (opening, ending) = inside.Contains('*') ? ("<u>", "</u>") : ("**", "**");
                    result.Append(opening).Append(inside).Append(ending);
                    highlights++;
                    hadText = true;
                    i += close + 4;
                    continue;
                }
            }
            hadText |= !char.IsWhiteSpace(rest[0]);
            result.Append(rest[0]);
            i++;
        }
        return hadText;
    }
}
