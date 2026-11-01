namespace KyfRev.Rendering.Markdown;

/// <summary>
/// L'analyse des blocs : les règles de markdown-it, dans son ordre, avec les notes de
/// <c>markdown-it-footnote</c> et les formules de <c>@mdit/plugin-tex</c>. Le HTML brut y est désactivé.
/// </summary>
internal static partial class BlockParser
{
    private const int MaxNesting = 100;

    private static readonly BlockRule[] AllRules =
    [
        Table, Code, Fence, Blockquote, MathBlock, HorizontalRule, List, FootnoteDefinition, Reference, Heading, SetextHeading, Paragraph,
    ];

    /// <summary>Ce qui peut interrompre un paragraphe ou une définition de lien.</summary>
    private static readonly BlockRule[] ParagraphTerminators =
    [
        Table, Fence, Blockquote, MathBlock, HorizontalRule, List, FootnoteDefinition, Heading,
    ];

    private static readonly BlockRule[] BlockquoteTerminators = [Fence, Blockquote, MathBlock, HorizontalRule, List, Heading];

    private static readonly BlockRule[] ListTerminators = [Fence, Blockquote, MathBlock, HorizontalRule];

    private delegate bool BlockRule(BlockState state, int startLine, int endLine, bool silent);

    public static void Parse(string source, ParseEnvironment environment, List<Token> tokens)
    {
        if (source.Length == 0)
        {
            return;
        }
        var state = new BlockState(source, environment, tokens);
        Tokenize(state, state.Line, state.LineMax);
    }

    private static void Tokenize(BlockState state, int startLine, int endLine)
    {
        var line = startLine;
        var hasEmptyLines = false;
        while (line < endLine)
        {
            state.Line = line = state.SkipEmptyLines(line);
            if (line >= endLine || state.SpaceCount[line] < state.BlockIndent)
            {
                break;
            }
            if (state.Level >= MaxNesting)
            {
                state.Line = endLine;
                break;
            }
            // Le paragraphe, dernière règle, accepte toujours : aucune ligne ne reste sans bloc.
            Array.Find(AllRules, rule => rule(state, line, endLine, false));
            state.Tight = !hasEmptyLines;
            if (state.IsEmpty(state.Line - 1))
            {
                hasEmptyLines = true;
            }
            line = state.Line;
            if (line < endLine && state.IsEmpty(line))
            {
                hasEmptyLines = true;
                line++;
                state.Line = line;
            }
        }
    }

    private static bool Terminates(BlockRule[] terminators, BlockState state, int line, int endLine)
    {
        foreach (var rule in terminators)
        {
            if (rule(state, line, endLine, true))
            {
                return true;
            }
        }
        return false;
    }
}
