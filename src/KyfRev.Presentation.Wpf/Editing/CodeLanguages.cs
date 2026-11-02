namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Un langage proposé pour un bloc de code.</summary>
/// <param name="Id">Ce qui s'écrit dans le bloc.</param>
/// <param name="Name">Son nom lisible.</param>
public sealed record CodeLanguage(string Id, string Name);

/// <summary>Les langages de la coloration de la 0.x, par nom lisible ; le texte brut d'abord.</summary>
public static class CodeLanguages
{
    /// <summary>Les langages proposés.</summary>
    public static IReadOnlyList<CodeLanguage> All { get; } =
    [
        new(string.Empty, EditingText.PlainCode),
        new("bash", "Bash"),
        new("c", "C"),
        new("csharp", "C#"),
        new("cpp", "C++"),
        new("shell", "Console"),
        new("css", "CSS"),
        new("diff", "Diff"),
        new("go", "Go"),
        new("graphql", "GraphQL"),
        new("xml", "HTML / XML"),
        new("ini", "INI / TOML"),
        new("java", "Java"),
        new("javascript", "JavaScript"),
        new("json", "JSON"),
        new("kotlin", "Kotlin"),
        new("less", "Less"),
        new("lua", "Lua"),
        new("makefile", "Makefile"),
        new("markdown", "Markdown"),
        new("objectivec", "Objective-C"),
        new("perl", "Perl"),
        new("php", "PHP"),
        new("php-template", "PHP (gabarit)"),
        new("python", "Python"),
        new("python-repl", "Python (console)"),
        new("r", "R"),
        new("ruby", "Ruby"),
        new("rust", "Rust"),
        new("scss", "SCSS"),
        new("sql", "SQL"),
        new("swift", "Swift"),
        new("typescript", "TypeScript"),
        new("vbnet", "VB.NET"),
        new("wasm", "WebAssembly"),
        new("yaml", "YAML"),
    ];
}
