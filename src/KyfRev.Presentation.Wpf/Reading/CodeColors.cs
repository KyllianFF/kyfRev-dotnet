using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>Un morceau d'un bloc de code, et la couleur du thème qu'il prend.</summary>
/// <param name="Start">Sa place dans le code.</param>
/// <param name="Length">Sa longueur.</param>
/// <param name="Brush">Sa couleur ; rien pour celle du texte.</param>
internal readonly record struct CodePiece(int Start, int Length, string? Brush);

/// <summary>
/// Colore un bloc de code selon son langage, avec les règles d'AvalonEdit, déjà présent pour écrire. Leurs couleurs
/// sont faites pour un fond clair : seul le nom de chaque règle sert, et il donne une couleur du thème.
/// </summary>
internal static class CodeColors
{
    /// <summary>Au-delà, le code reste d'une couleur : il s'affiche sans attendre.</summary>
    private const int LongestColored = 100_000;

    private const string Keyword = "Kyf.Brush.CodeKeyword";
    private const string Text = "Kyf.Brush.CodeString";
    private const string Number = "Kyf.Brush.CodeNumber";
    private const string Title = "Kyf.Brush.CodeTitle";
    private const string Comment = "Kyf.Brush.TextSecondary";

    /// <summary>Le nom d'un langage dans une fiche, et celui de ses règles.</summary>
    private static readonly Dictionary<string, string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c#"] = "C#",
        ["cs"] = "C#",
        ["csharp"] = "C#",
        ["c"] = "C++",
        ["cpp"] = "C++",
        ["c++"] = "C++",
        ["h"] = "C++",
        ["hpp"] = "C++",
        ["css"] = "CSS",
        ["scss"] = "CSS",
        ["html"] = "HTML",
        ["java"] = "Java",
        ["js"] = "JavaScript",
        ["javascript"] = "JavaScript",
        ["jsx"] = "JavaScript",
        ["ts"] = "JavaScript",
        ["typescript"] = "JavaScript",
        ["tsx"] = "JavaScript",
        ["json"] = "Json",
        ["diff"] = "Patch",
        ["patch"] = "Patch",
        ["php"] = "PHP",
        ["powershell"] = "PowerShell",
        ["ps"] = "PowerShell",
        ["ps1"] = "PowerShell",
        ["pwsh"] = "PowerShell",
        ["py"] = "Python",
        ["python"] = "Python",
        ["sql"] = "TSQL",
        ["tsql"] = "TSQL",
        ["latex"] = "TeX",
        ["tex"] = "TeX",
        ["vb"] = "VB",
        ["vbnet"] = "VB",
        ["svg"] = "XML",
        ["xaml"] = "XML",
        ["xml"] = "XML",
    };

    /// <summary>Le code en morceaux qui se suivent et le couvrent tout entier.</summary>
    public static IReadOnlyList<CodePiece> Pieces(string code, string? language)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code.Length == 0)
        {
            return [];
        }
        var definition = language is not null && code.Length <= LongestColored && Languages.TryGetValue(language, out var name)
            ? HighlightingManager.Instance.GetDefinition(name)
            : null;
        if (definition is null)
        {
            return [new CodePiece(0, code.Length, null)];
        }
        var brushes = new string?[code.Length];
        var document = new TextDocument(code);
        using var highlighter = new DocumentHighlighter(document, definition);
        for (var number = 1; number <= document.LineCount; number++)
        {
            // Les parties sont rangées de la plus large à la plus étroite : la plus précise l'emporte.
            foreach (var section in highlighter.HighlightLine(number).Sections)
            {
                var brush = BrushOf(section.Color?.Name);
                Array.Fill(brushes, brush, section.Offset, section.Length);
            }
        }
        var pieces = new List<CodePiece>();
        var start = 0;
        for (var index = 1; index <= brushes.Length; index++)
        {
            if (index == brushes.Length || !string.Equals(brushes[index], brushes[start], StringComparison.Ordinal))
            {
                pieces.Add(new CodePiece(start, index - start, brushes[start]));
                start = index;
            }
        }
        return pieces;
    }

    /// <summary>Les règles se nomment différemment d'un langage à l'autre : leurs noms se ressemblent assez pour suffire.</summary>
    private static string? BrushOf(string? rule)
    {
        if (rule is null or "Punctuation")
        {
            return null;
        }
        bool Has(string word) => rule.Contains(word, StringComparison.OrdinalIgnoreCase);
        if (Has("Comment"))
        {
            return Comment;
        }
        if (Has("String") || Has("Char") || Has("AttributeValue") || Has("CData"))
        {
            return Text;
        }
        if (Has("Number") || Has("Digit") || Has("Bool") || Has("TrueFalse") || Has("Null"))
        {
            return Number;
        }
        return Has("Method") || Has("Function") || Has("FieldName") || Has("XmlTag") ? Title : Keyword;
    }
}
