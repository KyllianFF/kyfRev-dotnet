namespace KyfRev.Rendering.Markdown;

/// <summary>Le type du bloc parent, que certaines règles consultent.</summary>
internal enum ParentBlock
{
    Root,
    Paragraph,
    Reference,
    Blockquote,
    List,
    Table,
    Footnote,
}

/// <summary>L'état de l'analyse des blocs : le découpage du texte en lignes, et leur indentation.</summary>
internal sealed class BlockState
{
    public BlockState(string source, ParseEnvironment environment, List<Token> tokens)
    {
        Source = source;
        Environment = environment;
        Tokens = tokens;
        var start = 0;
        var indent = 0;
        var offset = 0;
        var indentFound = false;
        for (var pos = 0; pos < source.Length; pos++)
        {
            var ch = source[pos];
            if (!indentFound)
            {
                if (IsSpace(ch))
                {
                    indent++;
                    offset += ch == '\t' ? 4 - (offset % 4) : 1;
                    continue;
                }
                indentFound = true;
            }
            if (ch == '\n' || pos == source.Length - 1)
            {
                if (ch != '\n')
                {
                    pos++;
                }
                AddLine(start, pos, indent, offset);
                indentFound = false;
                indent = 0;
                offset = 0;
                start = pos + 1;
            }
        }
        AddLine(source.Length, source.Length, 0, 0);
        LineMax = LineStarts.Count - 1;
    }

    public string Source { get; }

    public ParseEnvironment Environment { get; }

    public List<Token> Tokens { get; }

    /// <summary>Début de chaque ligne, après les marqueurs de ses conteneurs.</summary>
    public List<int> LineStarts { get; } = [];

    /// <summary>Fin de chaque ligne.</summary>
    public List<int> LineEnds { get; } = [];

    /// <summary>Caractères d'indentation de chaque ligne.</summary>
    public List<int> TextShift { get; } = [];

    /// <summary>Largeur de l'indentation de chaque ligne, tabulations développées.</summary>
    public List<int> SpaceCount { get; } = [];

    /// <summary>Colonne d'où part l'indentation de chaque ligne, pour développer ses tabulations.</summary>
    public List<int> BaseSpaceCount { get; } = [];

    public int BlockIndent { get; set; }

    public int Line { get; set; }

    public int LineMax { get; set; }

    public bool Tight { get; set; }

    public int ListIndent { get; set; } = -1;

    public ParentBlock Parent { get; set; } = ParentBlock.Root;

    public int Level { get; set; }

    public static bool IsSpace(int code) => code is '\t' or ' ';

    /// <summary>Le caractère à une position, ou -1 hors du texte (le <c>NaN</c> de <c>charCodeAt</c>).</summary>
    public int At(int position) => position >= 0 && position < Source.Length ? Source[position] : -1;

    public Token Push(TokenKind kind, string tag, int nesting)
    {
        var token = new Token(kind, tag, nesting) { Block = true };
        if (nesting < 0)
        {
            Level--;
        }
        token.Level = Level;
        if (nesting > 0)
        {
            Level++;
        }
        Tokens.Add(token);
        return token;
    }

    public bool IsEmpty(int line) => LineStarts[line] + TextShift[line] >= LineEnds[line];

    public int SkipEmptyLines(int from)
    {
        for (; from < LineMax; from++)
        {
            if (LineStarts[from] + TextShift[from] < LineEnds[from])
            {
                break;
            }
        }
        return from;
    }

    public int SkipSpaces(int position)
    {
        for (; position < Source.Length; position++)
        {
            if (!IsSpace(Source[position]))
            {
                break;
            }
        }
        return position;
    }

    public int SkipSpacesBack(int position, int minimum)
    {
        if (position <= minimum)
        {
            return position;
        }
        while (position > minimum)
        {
            if (!IsSpace(Source[--position]))
            {
                return position + 1;
            }
        }
        return position;
    }

    public int SkipChars(int position, int code)
    {
        for (; position < Source.Length; position++)
        {
            if (Source[position] != code)
            {
                break;
            }
        }
        return position;
    }

    public int SkipCharsBack(int position, int code, int minimum)
    {
        if (position <= minimum)
        {
            return position;
        }
        while (position > minimum)
        {
            if (code != Source[--position])
            {
                return position + 1;
            }
        }
        return position;
    }

    /// <summary>Le texte de plusieurs lignes, retiré de leur indentation jusqu'à <paramref name="indent"/>.</summary>
    public string GetLines(int begin, int end, int indent, bool keepLastLineFeed)
    {
        if (begin >= end)
        {
            return string.Empty;
        }
        var text = new System.Text.StringBuilder();
        for (var line = begin; line < end; line++)
        {
            var lineIndent = 0;
            var lineStart = LineStarts[line];
            var first = lineStart;
            // La dernière ligne du document n'a pas de saut de ligne à reprendre.
            var last = Math.Min(line + 1 < end || keepLastLineFeed ? LineEnds[line] + 1 : LineEnds[line], Source.Length);
            while (first < last && lineIndent < indent)
            {
                var ch = Source[first];
                if (IsSpace(ch))
                {
                    lineIndent += ch == '\t' ? 4 - ((lineIndent + BaseSpaceCount[line]) % 4) : 1;
                }
                else if (first - lineStart < TextShift[line])
                {
                    lineIndent++;
                }
                else
                {
                    break;
                }
                first++;
            }
            if (lineIndent > indent)
            {
                text.Append(' ', lineIndent - indent);
            }
            text.Append(Source, first, last - first);
        }
        return text.ToString();
    }

    private void AddLine(int start, int end, int indent, int offset)
    {
        LineStarts.Add(start);
        LineEnds.Add(end);
        TextShift.Add(indent);
        SpaceCount.Add(offset);
        BaseSpaceCount.Add(0);
    }
}
