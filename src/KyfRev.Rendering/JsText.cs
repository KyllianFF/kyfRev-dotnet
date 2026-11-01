namespace KyfRev.Rendering;

/// <summary>Le texte selon JavaScript : la 0.x comptait ainsi les espaces, et les positions en dépendent.</summary>
internal static class JsText
{
    /// <summary>Comme <c>\s</c> : l'espace de largeur nulle y compte, pas le saut de ligne U+0085.</summary>
    public static bool IsSpace(char c) => c == '\uFEFF' || (c != '\u0085' && char.IsWhiteSpace(c));

    /// <summary>Comme <c>String.prototype.trim</c>.</summary>
    public static string Trim(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && IsSpace(text[start]))
        {
            start++;
        }
        while (end > start && IsSpace(text[end - 1]))
        {
            end--;
        }
        return text[start..end];
    }

    /// <summary>Comme <c>String.prototype.slice</c>, avec des bornes positives : elles sont ramenées dans le texte.</summary>
    public static string Slice(string text, int start, int end)
    {
        var from = Math.Min(start, text.Length);
        var to = Math.Min(end, text.Length);
        return to > from ? text[from..to] : string.Empty;
    }
}
