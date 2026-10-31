namespace KyfRev.Infrastructure.Vault.Tests.Reference;

/// <summary>
/// Jetons des entrées tirées au hasard, recopiés de <c>tools/reference-vectors/src/fonctions_coffre.rs</c>
/// par un script : les deux générateurs doivent assembler exactement les mêmes textes.
/// </summary>
internal static class CoffreTokens
{
    public static readonly string[] NameTokens =
    [
        "Chapitre", " ", "3", ":", "la", "mitose", "é", "É", "ß", "Σ", "ς", "İ", "ǅ", "\t", "\n", "\u00A0", "\u2028",
        "\u3000", "\u0001", "\u007F", "\u0085", "<", ">", "?", "*", "\"", "/", "\\", "|", ".", "..", "#", "##", "CON",
        "con", "LPT1", "nul", "😀", "🇫🇷", "e\u0301", "ﬁ", "Ⅻ", "-", "_", ",", "(", ")", "[", "]", "%", ".PNG", ".png",
        ".Exe", ".html", ".tar.gz", "~", "'", "a", "Z", "日本", "\u200B", "\uFEFF",
        "abcdefghijklmnopqrstuvwxyz0123456789", "éééééééééééééééééééé",
    ];

    public static readonly string[] HeaderLines =
    [
        "---", "...", "--- ", "id: f_01JA8Z3K5Q6WXYZABCDEFGHJKM", "titre: Mitose",
        "titre: \"Révolution : les causes\"", "titre:", "titre: 'l''Ancien Régime'", "creee: 2026-10-09",
        "etiquettes: [bio, examen]", "etiquettes:", "  - bio", "  - \"examen\"", "- liste", "tags: examen, bac",
        "tags: [a]", "tags:", "auteur: X", "titre : espace", "titre:collé", "  titre: indenté", "\tclé: tabulation",
        "# commentaire", "", "Corps du texte.", "id: f_court", "etiquettes: [\"a\", \"b\"]", "titre: [liste]",
        "titre: |", "  bloc", "clé: valeur: deux", "titrex: y", "url: http://x.org", "id: 42", "titre: 12",
        "titre: ~",
    ];

    public static readonly string[] Keys =
    [
        "id", "titre", "etiquettes", "tags", "creee", "nouveau",
    ];

    public static readonly string[] LinkTokens =
    [
        "[", "]", "(", ")", "![", "](", "]: ", "<", ">", "\\", "`", "```", "~~~", "\n", "\r\n", " ", "\t", "    ",
        "a.md", "../b.md", "./c.md", "Mitose.md", "Mitose.ressources/img.png", "Mitose.ressources/", "%20", "%2",
        "%C3%A9", "%FF", "#section", "?q=1", "https://x.org", "mailto:a@b", "C:/x.md", "/abs.md", "é",
        "Révolution française.md", "C#.md", "Bio/sub/x.md", "..", ".", "/", "[ref]: ", "[ref]", "\"titre\"", "texte",
        "Bio/", "../Bio/Mitose.md", "\\)", "\\>", "x y.md", "<a b.md>", "\uFEFF",
    ];

    public static readonly string[] Folders =
    [
        "fiches", "fiches/Bio", "fiches/Bio/Cellule", "fiches/Histoire", "",
    ];

    public static readonly string[] PathParts =
    [
        "a", "b", "..", ".", "", "fiches", "Bio", "é", "x.md",
    ];
}
