using System.Text;

namespace KyfRev.Rendering;

/// <summary>
/// Écrit les masques d'une image dans le Markdown d'une fiche, en changeant le seul titre de cette image. L'image est
/// désignée par son rang parmi les images affichées. Le résultat est relu avant d'être rendu : si la fiche qui en sort
/// n'est pas la même à ce titre près, rien n'est rendu, et la fiche reste comme elle était.
/// </summary>
public static class ImageMaskWriter
{
    /// <summary>
    /// Le Markdown où l'image de rang <paramref name="imageIndex"/> porte ces masques (aucun : son titre de masques est
    /// retiré) ; rien si l'image n'a pas pu être retrouvée sûrement dans le texte, par exemple si elle est écrite par
    /// référence (<c>![alt][ref]</c>).
    /// </summary>
    /// <param name="body">Le corps de la fiche.</param>
    /// <param name="title">Le titre de la fiche, pour la relire comme elle s'affiche.</param>
    /// <param name="imageIndex">Rang de l'image parmi celles de la fiche, à partir de 0.</param>
    /// <param name="masks">Ses masques.</param>
    public static string? Write(string body, string title, int imageIndex, IReadOnlyList<ImageMask> masks)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(masks);
        var before = Images(SheetRenderer.Render(body, title));
        if (imageIndex < 0 || imageIndex >= before.Count)
        {
            return null;
        }
        // Les autres réglages de l'image (largeur, place, cadre) restent : seuls ses masques changent.
        var newTitle = ImageLooks.ToTitle(before[imageIndex].ImageLook with { Masks = [.. masks] });
        var wanted = ImageMasks.FromTitle(newTitle);
        if (Rewrite(body, imageIndex, newTitle) is not { } written)
        {
            return null;
        }
        // La preuve : la même fiche, les mêmes images, et les masques voulus sur celle-là seulement.
        var after = Images(SheetRenderer.Render(written, title));
        if (after.Count != before.Count || TextMap.Of(SheetRenderer.Render(written, title)).Text != TextMap.Of(SheetRenderer.Render(body, title)).Text)
        {
            return null;
        }
        for (var i = 0; i < after.Count; i++)
        {
            var expected = i == imageIndex ? wanted : before[i].Masks;
            if (after[i].Target != before[i].Target || !after[i].Masks.SequenceEqual(expected))
            {
                return null;
            }
        }
        return written;
    }

    /// <summary>Les images d'une fiche, dans l'ordre où elles s'affichent.</summary>
    public static IReadOnlyList<RenderElement> Images(RenderElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var images = new List<RenderElement>();
        void Walk(RenderElement element)
        {
            foreach (var child in element.Children.OfType<RenderElement>())
            {
                if (child.Tag == "img")
                {
                    images.Add(child);
                }
                Walk(child);
            }
        }
        Walk(root);
        return images;
    }

    /// <summary>
    /// Change le titre de la <paramref name="imageIndex"/>-ième image écrite <c>![…](…)</c> hors du code ; rien si elle
    /// n'existe pas. La lecture du résultat décide si c'était la bonne.
    /// </summary>
    private static string? Rewrite(string body, int imageIndex, string? newTitle)
    {
        var result = new StringBuilder(body.Length + 64);
        var seen = 0;
        var done = false;
        (char Mark, int Length)? fence = null;
        var start = 0;
        while (start < body.Length)
        {
            var newline = body.IndexOf('\n', start);
            var next = newline < 0 ? body.Length : newline + 1;
            var line = body.AsSpan(start, next - start);
            start = next;

            var rest = line.TrimStart(' ');
            var indent = line.Length - rest.Length;
            char? mark = rest.Length > 0 && rest[0] is '`' or '~' ? rest[0] : null;
            var run = 0;
            while (mark is { } m && run < rest.Length && rest[run] == m)
            {
                run++;
            }
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
            var i = 0;
            while (i < line.Length)
            {
                if (line[i] == '`')
                {
                    // Le code en ligne se recopie tel quel, jusqu'à la même série d'accents graves.
                    var ticks = 1;
                    while (i + ticks < line.Length && line[i + ticks] == '`')
                    {
                        ticks++;
                    }
                    var close = line[(i + ticks)..].IndexOf(line.Slice(i, ticks), StringComparison.Ordinal);
                    var length = close >= 0 ? ticks + close + ticks : ticks;
                    result.Append(line.Slice(i, length));
                    i += length;
                    continue;
                }
                if (!done && line[i] == '!' && i + 1 < line.Length && line[i + 1] == '[' && (i == 0 || line[i - 1] != '\\') && Image(line[i..]) is var (length2, head) && length2 > 0)
                {
                    if (seen++ == imageIndex)
                    {
                        result.Append(head);
                        if (newTitle is not null)
                        {
                            result.Append(" \"").Append(newTitle).Append('"');
                        }
                        result.Append(')');
                        done = true;
                    }
                    else
                    {
                        result.Append(line.Slice(i, length2));
                    }
                    i += length2;
                    continue;
                }
                result.Append(line[i]);
                i++;
            }
        }
        return done ? result.ToString() : null;
    }

    /// <summary>
    /// Une image écrite en ligne au début de <paramref name="text"/> : sa longueur, et son début jusqu'à la destination
    /// comprise (<c>![alt](destination</c>). Un titre qui n'est pas celui des masques est gardé dans ce début seulement
    /// s'il n'y a pas de masques à écrire : un titre de l'utilisateur ne se remplace pas, et l'image est alors refusée.
    /// </summary>
    private static (int Length, string Head) Image(ReadOnlySpan<char> text)
    {
        // Le libellé : jusqu'au crochet fermant de même niveau.
        var depth = 0;
        var labelEnd = -1;
        for (var i = 1; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
            }
            else if (text[i] == '[')
            {
                depth++;
            }
            else if (text[i] == ']' && --depth == 0)
            {
                labelEnd = i;
                break;
            }
        }
        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
        {
            return (0, string.Empty);
        }
        var pos = labelEnd + 2;
        while (pos < text.Length && text[pos] == ' ')
        {
            pos++;
        }
        // La destination : entre chevrons, ou jusqu'au premier blanc, parenthèses équilibrées.
        int destinationEnd;
        if (pos < text.Length && text[pos] == '<')
        {
            var close = text[pos..].IndexOf('>');
            if (close < 0)
            {
                return (0, string.Empty);
            }
            destinationEnd = pos + close + 1;
        }
        else
        {
            var parentheses = 0;
            destinationEnd = pos;
            while (destinationEnd < text.Length && !char.IsWhiteSpace(text[destinationEnd]))
            {
                if (text[destinationEnd] == '\\')
                {
                    destinationEnd++;
                }
                else if (text[destinationEnd] == '(')
                {
                    parentheses++;
                }
                else if (text[destinationEnd] == ')' && parentheses-- == 0)
                {
                    break;
                }
                destinationEnd++;
            }
        }
        if (destinationEnd > text.Length || destinationEnd == pos)
        {
            return (0, string.Empty);
        }
        var head = text[..destinationEnd].ToString();
        pos = destinationEnd;
        while (pos < text.Length && text[pos] == ' ')
        {
            pos++;
        }
        if (pos < text.Length && text[pos] is '"' or '\'')
        {
            var quote = text[pos];
            var titleStart = pos + 1;
            var close = text[titleStart..].IndexOf(quote);
            if (close < 0)
            {
                return (0, string.Empty);
            }
            // Un titre que l'utilisateur a écrit lui-même n'est pas à nous : l'image est laissée telle quelle.
            if (!ImageLooks.IsOurs(text.Slice(titleStart, close).ToString()))
            {
                return (0, string.Empty);
            }
            pos = titleStart + close + 1;
            while (pos < text.Length && text[pos] == ' ')
            {
                pos++;
            }
        }
        return pos < text.Length && text[pos] == ')' ? (pos + 1, head) : (0, string.Empty);
    }
}
