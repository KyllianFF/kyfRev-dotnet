namespace KyfRev.Rendering.Markdown;

/// <summary>
/// L'analyse du texte en ligne : les règles de markdown-it, dans son ordre, avec les notes, les formules
/// en <c>$…$</c> et les balises <c>u</c>, <c>sup</c> et <c>sub</c> de kyfRev. Le HTML brut y reste du texte.
/// </summary>
internal static partial class InlineParser
{
    private const int MaxNesting = 100;

    private static readonly InlineRule[] Rules =
    [
        Text, Linkify, Newline, Escape, MathInline, Backticks, Strikethrough, Emphasis, Link, Image, FootnoteInline, FootnoteReference, AllowedTag, Autolink, Entity,
    ];

    private delegate bool InlineRule(InlineState state, bool silent);

    public static void Parse(string source, ParseEnvironment environment, List<Token> tokens)
    {
        var state = new InlineState(source, environment, tokens);
        Tokenize(state);
        PairDelimiters(state.Delimiters);
        state.NestedDelimiters.ForEach(PairDelimiters);
        ApplyStrikethrough(state, state.Delimiters);
        state.NestedDelimiters.ForEach(delimiters => ApplyStrikethrough(state, delimiters));
        ApplyEmphasis(state, state.Delimiters);
        state.NestedDelimiters.ForEach(delimiters => ApplyEmphasis(state, delimiters));
        JoinFragments(state);
    }

    /// <summary>Avance d'un élément sans rien produire, pour trouver la fin du texte d'un lien.</summary>
    public static void SkipToken(InlineState state)
    {
        var pos = state.Position;
        if (state.SkipCache.TryGetValue(pos, out var known))
        {
            state.Position = known;
            return;
        }
        var ok = false;
        if (state.Level < MaxNesting)
        {
            foreach (var rule in Rules)
            {
                state.Level++;
                ok = rule(state, true);
                state.Level--;
                if (ok)
                {
                    break;
                }
            }
        }
        else
        {
            state.Position = state.PositionMax;
        }
        if (!ok)
        {
            state.Position++;
        }
        state.SkipCache[pos] = state.Position;
    }

    private static void Tokenize(InlineState state)
    {
        var end = state.PositionMax;
        while (state.Position < end)
        {
            var ok = false;
            if (state.Level < MaxNesting)
            {
                foreach (var rule in Rules)
                {
                    ok = rule(state, false);
                    if (ok)
                    {
                        break;
                    }
                }
            }
            if (ok)
            {
                if (state.Position >= end)
                {
                    break;
                }
                continue;
            }
            state.Pending.Append(state.Source[state.Position++]);
        }
        if (state.Pending.Length > 0)
        {
            state.PushPending();
        }
    }

    /// <summary>Apparie les marques d'un même niveau, selon les règles de CommonMark.</summary>
    private static void PairDelimiters(List<Delimiter> delimiters)
    {
        if (delimiters.Count == 0)
        {
            return;
        }
        var openersBottom = new Dictionary<int, int[]>();
        var headerIndex = 0;
        var lastTokenIndex = -2;
        var jumps = new List<int>(delimiters.Count);
        for (var closerIndex = 0; closerIndex < delimiters.Count; closerIndex++)
        {
            var closer = delimiters[closerIndex];
            jumps.Add(0);
            if (delimiters[headerIndex].Marker != closer.Marker || lastTokenIndex != closer.Token - 1)
            {
                headerIndex = closerIndex;
            }
            lastTokenIndex = closer.Token;
            if (!closer.Close)
            {
                continue;
            }
            if (!openersBottom.TryGetValue(closer.Marker, out var bottoms))
            {
                bottoms = [-1, -1, -1, -1, -1, -1];
                openersBottom[closer.Marker] = bottoms;
            }
            var minimumOpener = bottoms[(closer.Open ? 3 : 0) + (closer.Length % 3)];
            var openerIndex = headerIndex - jumps[headerIndex] - 1;
            var newMinimumOpener = openerIndex;
            for (; openerIndex > minimumOpener; openerIndex -= jumps[openerIndex] + 1)
            {
                var opener = delimiters[openerIndex];
                if (opener.Marker != closer.Marker || !opener.Open || opener.End >= 0)
                {
                    continue;
                }
                var isOddMatch = (opener.Close || closer.Open)
                    && (opener.Length + closer.Length) % 3 == 0
                    && (opener.Length % 3 != 0 || closer.Length % 3 != 0);
                if (!isOddMatch)
                {
                    var lastJump = openerIndex > 0 && !delimiters[openerIndex - 1].Open ? jumps[openerIndex - 1] + 1 : 0;
                    jumps[closerIndex] = closerIndex - openerIndex + lastJump;
                    jumps[openerIndex] = lastJump;
                    closer.Open = false;
                    opener.End = closerIndex;
                    opener.Close = false;
                    newMinimumOpener = -1;
                    lastTokenIndex = -2;
                    break;
                }
            }
            if (newMinimumOpener != -1)
            {
                bottoms[(closer.Open ? 3 : 0) + (closer.Length % 3)] = newMinimumOpener;
            }
        }
    }

    private static void JoinFragments(InlineState state)
    {
        var level = 0;
        var tokens = state.Tokens;
        var last = 0;
        for (var current = 0; current < tokens.Count; current++)
        {
            if (tokens[current].Nesting < 0)
            {
                level--;
            }
            tokens[current].Level = level;
            if (tokens[current].Nesting > 0)
            {
                level++;
            }
            if (tokens[current].Kind == TokenKind.Text && current + 1 < tokens.Count && tokens[current + 1].Kind == TokenKind.Text)
            {
                tokens[current + 1].Content = tokens[current].Content + tokens[current + 1].Content;
            }
            else
            {
                tokens[last] = tokens[current];
                last++;
            }
        }
        tokens.RemoveRange(last, tokens.Count - last);
    }
}
