using System.Collections.Immutable;
using KyfRev.Application.Model;

namespace KyfRev.Rendering;

/// <summary>Une position dans le texte affiché.</summary>
/// <param name="Start">Début, en unités UTF-16.</param>
/// <param name="End">Fin, en unités UTF-16.</param>
public readonly record struct TextPosition(int Start, int End);

/// <summary>Où un passage a été retrouvé.</summary>
/// <param name="State">Ancré, à vérifier ou introuvable.</param>
/// <param name="Position">Sa position, ou la position candidate ; rien s'il est introuvable.</param>
public readonly record struct AnchorResult(AnchorState State, TextPosition? Position);

/// <summary>
/// L'ancrage des passages sur le texte affiché (citation exacte, contexte et position). Le ré-ancrage ne
/// rattache jamais un passage à un texte différent : une position approchée n'est qu'un candidat, montré
/// à l'utilisateur. Reprend <c>src/lib/ancrage.ts</c> de la 0.x.
/// </summary>
public static class Anchoring
{
    /// <summary>Longueur du contexte enregistré avant et après le passage.</summary>
    public const int Context = 32;

    /// <summary>Retire les espaces en bordure d'une sélection ; rien si elle est vide.</summary>
    public static TextPosition? Trim(string text, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(text);
        var from = Math.Max(0, start);
        var to = Math.Min(text.Length, end);
        while (from < to && JsText.IsSpace(text[from]))
        {
            from++;
        }
        while (to > from && JsText.IsSpace(text[to - 1]))
        {
            to--;
        }
        return to > from ? new TextPosition(from, to) : null;
    }

    /// <summary>L'ancre d'un passage du texte, avec son contexte (sans couper de caractère).</summary>
    public static Anchor AnchorOf(string text, TextPosition position, ImmutableArray<string> section, int? sourceLine, string sheetFingerprint)
    {
        ArgumentNullException.ThrowIfNull(text);
        var before = Math.Max(0, position.Start - Context);
        if (before > 0 && char.IsLowSurrogate(text[before]))
        {
            before++;
        }
        var after = Math.Min(text.Length, position.End + Context);
        if (after < text.Length && char.IsHighSurrogate(text[after - 1]))
        {
            after--;
        }
        return new Anchor(
            text[position.Start..position.End],
            text[before..position.Start],
            text[position.End..after],
            position.Start,
            position.End,
            section,
            sourceLine,
            sheetFingerprint);
    }

    /// <summary>Deux ancres identiques, le repère de ligne compris.</summary>
    public static bool SameAnchor(Anchor first, Anchor second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return first.Exact == second.Exact
            && first.Before == second.Before
            && first.After == second.After
            && first.Start == second.Start
            && first.End == second.End
            && first.SourceLine == second.SourceLine
            && first.SheetFingerprint == second.SheetFingerprint
            && first.Section.SequenceEqual(second.Section);
    }

    /// <summary>
    /// Retrouve un passage : la position enregistrée intacte, contexte compris, est ancrée ; sinon, une
    /// occurrence exacte unique, ou une seule dont le contexte correspond le mieux ; une égalité est à
    /// vérifier ; sinon, la recherche approchée peut proposer un candidat, à vérifier. Un passage déjà
    /// introuvable ne reçoit plus de proposition : seule une occurrence exacte et unique le rattache.
    /// </summary>
    public static AnchorResult Locate(string text, Anchor anchor, AnchorState current)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(anchor);
        var exact = anchor.Exact;
        if (exact.Length == 0)
        {
            return new AnchorResult(AnchorState.NotFound, null);
        }
        if (ContextIntact(text, anchor))
        {
            return new AnchorResult(AnchorState.Anchored, new TextPosition((int)anchor.Start, (int)anchor.End));
        }
        var occurrences = new List<TextPosition>();
        for (var i = text.IndexOf(exact, StringComparison.Ordinal); i != -1; i = i + 1 <= text.Length ? text.IndexOf(exact, i + 1, StringComparison.Ordinal) : -1)
        {
            occurrences.Add(new TextPosition(i, i + exact.Length));
        }
        if (occurrences.Count == 1)
        {
            return new AnchorResult(AnchorState.Anchored, occurrences[0]);
        }
        if (occurrences.Count > 1)
        {
            var (position, unique) = Decide(text, occurrences, anchor);
            return unique ? new AnchorResult(AnchorState.Anchored, position)
                : current == AnchorState.NotFound ? new AnchorResult(AnchorState.NotFound, null)
                : new AnchorResult(AnchorState.NeedsReview, position);
        }
        if (current == AnchorState.NotFound)
        {
            return new AnchorResult(AnchorState.NotFound, null);
        }
        return ApproximateSearch(text, anchor) is { } candidate
            ? new AnchorResult(AnchorState.NeedsReview, candidate)
            : new AnchorResult(AnchorState.NotFound, null);
    }

    private static bool ContextIntact(string text, Anchor anchor)
    {
        var (start, end) = ((int)Math.Min(anchor.Start, int.MaxValue), (int)Math.Min(anchor.End, int.MaxValue));
        return JsText.Slice(text, start, end) == anchor.Exact
            && JsText.Slice(text, Math.Max(0, start - anchor.Before.Length), start) == anchor.Before
            && JsText.Slice(text, end, end + anchor.After.Length) == anchor.After;
    }

    private static int CommonSuffix(string a, string b)
    {
        var n = 0;
        while (n < a.Length && n < b.Length && a[a.Length - 1 - n] == b[b.Length - 1 - n])
        {
            n++;
        }
        return n;
    }

    private static int CommonPrefix(string a, string b)
    {
        var n = 0;
        while (n < a.Length && n < b.Length && a[n] == b[n])
        {
            n++;
        }
        return n;
    }

    /// <summary>Nombre de caractères du contexte enregistré retrouvés autour d'une position.</summary>
    private static int ContextScore(string text, TextPosition position, Anchor anchor) =>
        CommonSuffix(JsText.Slice(text, Math.Max(0, position.Start - anchor.Before.Length), position.Start), anchor.Before)
        + CommonPrefix(JsText.Slice(text, position.End, position.End + anchor.After.Length), anchor.After);

    /// <summary>La meilleure position : le contexte le plus proche, puis la plus près de l'ancienne.</summary>
    private static (TextPosition Position, bool Unique) Decide(string text, List<TextPosition> positions, Anchor anchor)
    {
        var scored = positions.Select(position => (Position: position, Score: ContextScore(text, position, anchor))).ToList();
        var best = scored.Max(entry => entry.Score);
        var tied = scored.Where(entry => entry.Score == best).OrderBy(entry => Math.Abs(entry.Position.Start - anchor.Start)).ToList();
        return (tied[0].Position, tied.Count == 1 && best > 0);
    }

    /// <summary>La recherche tolérante aux fautes (un tiers d'écart au plus), pour proposer un candidat.</summary>
    private static TextPosition? ApproximateSearch(string text, Anchor anchor)
    {
        if (JsText.Trim(anchor.Exact).Length < 8)
        {
            return null;
        }
        var matches = ApproximateMatch.Search(text, anchor.Exact, anchor.Exact.Length / 3);
        if (matches.Count == 0)
        {
            return null;
        }
        var fewest = matches.Min(match => match.Errors);
        var best = matches.Where(match => match.Errors == fewest).Select(match => Trim(text, match.Start, match.End)).OfType<TextPosition>().ToList();
        return best.Count > 0 ? RefineBounds(text, Decide(text, best, anchor).Position, anchor) : null;
    }

    /// <summary>
    /// La recherche approchée retient l'alignement le moins coûteux, souvent trop court quand des mots ont
    /// été ajoutés. Les bornes du candidat sont recalées sur le contexte enregistré, s'il se retrouve tout près.
    /// </summary>
    private static TextPosition RefineBounds(string text, TextPosition candidate, Anchor anchor)
    {
        var window = ((anchor.Exact.Length + 1) / 2) + Context;
        var (start, end) = candidate;
        var next = anchor.After[..Math.Min(16, anchor.After.Length)];
        if (JsText.Trim(next).Length >= 3)
        {
            var found = -1;
            for (var i = IndexFrom(text, next, start + 1); i != -1 && i <= end + window; i = IndexFrom(text, next, i + 1))
            {
                if (i >= end - window && (found == -1 || Math.Abs(i - end) < Math.Abs(found - end)))
                {
                    found = i;
                }
            }
            if (found > start)
            {
                end = found;
            }
        }
        var previous = anchor.Before[Math.Max(0, anchor.Before.Length - 16)..];
        if (JsText.Trim(previous).Length >= 3)
        {
            var found = -1;
            for (var i = IndexFrom(text, previous, Math.Max(0, start - window - previous.Length));
                i != -1 && i + previous.Length <= Math.Min(end - 1, start + window);
                i = IndexFrom(text, previous, i + 1))
            {
                var edge = i + previous.Length;
                if (found == -1 || Math.Abs(edge - start) < Math.Abs(found - start))
                {
                    found = edge;
                }
            }
            if (found != -1 && found < end)
            {
                start = found;
            }
        }
        return Trim(text, start, end) ?? candidate;
    }

    private static int IndexFrom(string text, string value, int from) =>
        from > text.Length ? -1 : text.IndexOf(value, from, StringComparison.Ordinal);
}
