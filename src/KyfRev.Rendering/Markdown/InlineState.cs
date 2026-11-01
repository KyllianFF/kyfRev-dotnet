using System.Text;

namespace KyfRev.Rendering.Markdown;

/// <summary>Une marque d'emphase ou de barré, en attente de sa marque fermante.</summary>
internal sealed class Delimiter
{
    public required int Marker { get; init; }

    public required int Length { get; init; }

    /// <summary>Indice du jeton de cette marque.</summary>
    public required int Token { get; init; }

    /// <summary>Indice de la marque fermante, ou -1.</summary>
    public int End { get; set; } = -1;

    public required bool Open { get; set; }

    public required bool Close { get; set; }
}

/// <summary>L'état de l'analyse d'un texte en ligne.</summary>
internal sealed class InlineState(string source, ParseEnvironment environment, List<Token> tokens)
{
    private readonly Stack<List<Delimiter>> _previousDelimiters = new();

    public string Source { get; } = source;

    public ParseEnvironment Environment { get; } = environment;

    public List<Token> Tokens { get; } = tokens;

    public int Position { get; set; }

    public int PositionMax { get; set; } = source.Length;

    public int Level { get; set; }

    public StringBuilder Pending { get; } = new();

    public int PendingLevel { get; set; }

    /// <summary>Où mène chaque position déjà parcourue pour lire le texte d'un lien.</summary>
    public Dictionary<int, int> SkipCache { get; } = [];

    /// <summary>Dernière suite d'accents graves de chaque longueur ; rien tant que le texte n'a pas été lu.</summary>
    public Dictionary<int, int>? LastBacktickRuns { get; set; }

    public int LinkLevel { get; set; }

    public List<Delimiter> Delimiters { get; private set; } = [];

    /// <summary>Les marques de chaque niveau ouvert, pour les apparier ensuite niveau par niveau.</summary>
    public List<List<Delimiter>> NestedDelimiters { get; } = [];

    /// <summary>Nombre de balises <c>u</c>, <c>sup</c> et <c>sub</c> ouvertes.</summary>
    public Dictionary<string, int> OpenTags { get; } = new(StringComparer.Ordinal);

    /// <summary>Le caractère à une position, ou -1 hors du texte (le <c>NaN</c> de <c>charCodeAt</c>).</summary>
    public int At(int position) => position >= 0 && position < Source.Length ? Source[position] : -1;

    public void PushPending()
    {
        Tokens.Add(new Token(TokenKind.Text, string.Empty, 0) { Content = Pending.ToString(), Level = PendingLevel });
        Pending.Clear();
    }

    public Token Push(TokenKind kind, string tag, int nesting)
    {
        if (Pending.Length > 0)
        {
            PushPending();
        }
        var token = new Token(kind, tag, nesting);
        if (nesting < 0)
        {
            Level--;
            // Une balise fermée hors du niveau qui l'a ouverte (<u> dans un lien) ne casse rien.
            Delimiters = _previousDelimiters.TryPop(out var previous) ? previous : [];
        }
        token.Level = Level;
        if (nesting > 0)
        {
            Level++;
            _previousDelimiters.Push(Delimiters);
            Delimiters = [];
            NestedDelimiters.Add(Delimiters);
        }
        PendingLevel = Level;
        Tokens.Add(token);
        return token;
    }

    /// <summary>Ce qu'une suite de marques peut faire : ouvrir, fermer, et sa longueur.</summary>
    public (bool CanOpen, bool CanClose, int Length) ScanDelimiters(int start, bool canSplitWord)
    {
        var marker = At(start);
        var pos = start;
        while (pos < PositionMax && At(pos) == marker)
        {
            pos++;
        }
        var count = pos - start;
        var lastChar = CodePointBefore(start);
        var nextChar = pos < PositionMax ? CodePointAt(pos) : ' ';
        var isLastPunctuation = IsPunctuation(lastChar);
        var isNextPunctuation = IsPunctuation(nextChar);
        var isLastWhiteSpace = IsWhiteSpace(lastChar);
        var isNextWhiteSpace = IsWhiteSpace(nextChar);
        var leftFlanking = !isNextWhiteSpace && (!isNextPunctuation || isLastWhiteSpace || isLastPunctuation);
        var rightFlanking = !isLastWhiteSpace && (!isLastPunctuation || isNextWhiteSpace || isNextPunctuation);
        return (
            leftFlanking && (canSplitWord || !rightFlanking || isLastPunctuation),
            rightFlanking && (canSplitWord || !leftFlanking || isNextPunctuation),
            count);
    }

    private static bool IsWhiteSpace(int code) => code is >= 0x2000 and <= 0x200A
        or '\t' or '\n' or '\v' or '\f' or '\r' or ' ' or 0xA0 or 0x1680 or 0x202F or 0x205F or 0x3000;

    private static bool IsPunctuation(int code) => UnicodeClasses.IsPunctuationOrSymbol(code);

    private int CodePointBefore(int start)
    {
        if (start == 0)
        {
            return ' ';
        }
        int last = Source[start - 1];
        if (char.IsLowSurrogate((char)last))
        {
            return start >= 2 && char.IsHighSurrogate(Source[start - 2]) ? char.ConvertToUtf32(Source[start - 2], (char)last) : 0xFFFD;
        }
        return char.IsHighSurrogate((char)last) ? 0xFFFD : last;
    }

    private int CodePointAt(int position)
    {
        int next = Source[position];
        if (char.IsHighSurrogate((char)next))
        {
            return position + 1 < Source.Length && char.IsLowSurrogate(Source[position + 1]) ? char.ConvertToUtf32((char)next, Source[position + 1]) : 0xFFFD;
        }
        return char.IsLowSurrogate((char)next) ? 0xFFFD : next;
    }
}
