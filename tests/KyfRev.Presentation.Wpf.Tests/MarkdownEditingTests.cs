using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Editing;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Les gestes du ruban sur le Markdown, et les cas de test de la 0.x pour la casse et les liens.</summary>
[TestClass]
public sealed class MarkdownEditingTests
{
    [TestMethod]
    [DataRow("un «mot» ici", "color", "#ff0000", "un <span style=\"color:#ff0000\">«mot»</span> ici")]
    [DataRow("un « mot » ici", "color", "#ff0000", "un  <span style=\"color:#ff0000\">«mot»</span>  ici")]
    [DataRow("<span style=\"color:#ff0000\">«mot»</span>", "color", "#0000ff", "<span style=\"color:#0000ff\">«mot»</span>")]
    [DataRow("<span style=\"color:#ff0000\">«mot»</span>", "color", null, "«mot»")]
    [DataRow("«mot»", "font-size", "1.25em", "<span style=\"font-size:1.25em\">«mot»</span>")]
    [DataRow("<span style=\"font-size:1.25em\">«mot»</span>", "font-size", "2em", "<span style=\"font-size:2em\">«mot»</span>")]
    [DataRow("<span style=\"font-size:1.25em\">«mot»</span>", "color", "#ff0000", "<span style=\"font-size:1.25em\"><span style=\"color:#ff0000\">«mot»</span></span>")]
    [DataRow("|", "color", "#ff0000", "<span style=\"color:#ff0000\">|</span>")]
    public void A_color_or_a_size_wraps_the_selection_or_changes_what_wraps_it(string model, string property, string? value, string expected) =>
        Assert.AreEqual(expected, Apply(model, (text, start, end) => MarkdownEdits.Style(text, start, end, property, value)!.Value));

    [TestMethod]
    [DataRow("«mot»", "color")]
    [DataRow("<span style=\"font-size:2em\">«mot»</span>", "color")]
    [DataRow("<b>«mot»</span>", "color")]
    public void Removing_a_style_that_is_not_there_changes_nothing(string model, string property)
    {
        var (text, start, end) = Parse(model);

        Assert.IsNull(MarkdownEdits.Style(text, start, end, property, null));
    }

    [TestMethod]
    [DataRow("<span style=\"color:#ff0000\">«mot»</span>", "color", "#ff0000")]
    [DataRow("<span style=\"font-size:1.25em\">«mot»</span>", "font-size", "1.25em")]
    [DataRow("<span style=\"color:#ff0000\">«mot»</span>", "font-size", null)]
    [DataRow("«mot»", "color", null)]
    [DataRow("|", "color", null)]
    public void The_style_around_the_selection_is_read(string model, string property, string? expected)
    {
        var (text, start, end) = Parse(model);

        Assert.AreEqual(expected, MarkdownEdits.StyleAround(text, start, end, property));
    }

    [TestMethod]
    [DataRow("un|", TextAlign.Center, "<div align=\"center\">\n\nun|\n\n</div>")]
    [DataRow("avant\n\nun|\n\naprès", TextAlign.Right, "avant\n\n<div align=\"right\">\n\nun|\n\n</div>\n\naprès")]
    [DataRow("avant\nun|\naprès", TextAlign.Justify, "avant\n\n<div align=\"justify\">\n\nun|\n\n</div>\n\naprès")]
    [DataRow("«un\ndeux»", TextAlign.Center, "<div align=\"center\">\n\n«un\ndeux»\n\n</div>")]
    [DataRow("<div align=\"center\">\n\nun|\n\n</div>", TextAlign.Right, "<div align=\"right\">\n\nun|\n\n</div>")]
    [DataRow("<div align=\"justify\">\n\nun|\n\n</div>", TextAlign.Center, "<div align=\"center\">\n\nun|\n\n</div>")]
    [DataRow("<div align=\"center\">\n\nun|\n\n</div>", TextAlign.Left, "un|")]
    [DataRow("a\n\n<div align=\"center\">\n\nun|\n\n</div>\n\nb", TextAlign.Left, "a\n\nun|\n\nb")]
    [DataRow("<div align=\"center\">\n\n<div align=\"right\">\n\nun|\n\n</div>\n\n</div>", TextAlign.Left, "<div align=\"center\">\n\nun|\n\n</div>")]
    public void Lines_are_aligned_and_a_passage_changes_or_goes_away(string model, TextAlign align, string expected) =>
        Assert.AreEqual(expected, Apply(model, (text, start, end) => MarkdownEdits.Align(text, start, end, align)!.Value));

    [TestMethod]
    public void Aligning_to_the_left_outside_a_passage_changes_nothing()
    {
        Assert.IsNull(MarkdownEdits.Align("un", 1, 1, TextAlign.Left));
        Assert.IsNull(MarkdownEdits.Align("<div align=\"center\">\n\nun\n\n</div>\n\ndeux", 33, 33, TextAlign.Left));
    }

    [TestMethod]
    public void A_closing_line_in_a_code_block_closes_nothing()
    {
        const string text = "<div align=\"center\">\n\n```\n</div>\n```\n\nun\n\n</div>";

        Assert.AreEqual(TextAlign.Center, MarkdownEdits.AlignAt(text, text.IndexOf("un", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("un", 1, TextAlign.Left)]
    [DataRow("<div align=\"center\">\n\nun\n\n</div>", 25, TextAlign.Center)]
    [DataRow("<div align=\"right\">\n\nun\n\n</div>", 24, TextAlign.Right)]
    [DataRow("<div align=\"justify\">\n\nun\n\n</div>", 26, TextAlign.Justify)]
    [DataRow("<div align=\"center\">\n\nun\n\n</div>\n\ndeux", 37, TextAlign.Left)]
    [DataRow("<div align=\"center\">\n\nun sans fin", 25, TextAlign.Left)]
    public void The_alignment_at_a_position_is_read(string text, int position, TextAlign expected) =>
        Assert.AreEqual(expected, MarkdownEdits.AlignAt(text, position));

    [TestMethod]
    public void Font_sizes_are_written_relative_to_the_normal_text()
    {
        Assert.IsNull(FontSizes.Style(FontSizes.Normal));
        foreach (var pixels in FontSizes.Steps)
        {
            Assert.AreEqual(pixels, FontSizes.Pixels(FontSizes.Style(pixels)), "La taille se relit à l'identique.");
        }
        Assert.AreEqual("1.18em", FontSizes.Style(20));
        Assert.AreEqual(20, FontSizes.Step(null, 1));
        Assert.AreEqual(14, FontSizes.Step(null, -1));
        Assert.AreEqual(40, FontSizes.Step("2.35em", 1), "Au bout de la liste, on y reste.");
        Assert.AreEqual(10, FontSizes.Step("0.59em", -1));
        Assert.AreEqual(24, FontSizes.Step("1.25em", 1), "Une taille hors de la liste va à la suivante.");
    }

    /// <summary>Applique un remplacement au texte où « | » marque le curseur et « [ ] » la sélection ; rend le résultat marqué de même.</summary>
    [TestMethod]
    [DataRow("«un\n\ndeux»", "«> un\n>\n> deux»")]
    [DataRow("«> un\n>\n> deux»", "«un\n\ndeux»")]
    [DataRow("a|", "> a|")]
    [DataRow("|", ">|")]
    public void A_quote_takes_whole_lines_and_comes_off_again(string model, string expected) =>
        Assert.AreEqual(expected, Apply(model, MarkdownEdits.Quote));

    [TestMethod]
    public void A_line_says_if_it_is_quoted()
    {
        Assert.IsTrue(MarkdownEdits.IsQuoted("> un"));
        Assert.IsFalse(MarkdownEdits.IsQuoted("un > deux"));
    }

    private static string Apply(string model, Func<string, int, int, TextChange> edit)
    {
        var (text, start, end) = Parse(model);
        var change = edit(text, start, end);
        var result = text.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return Mark(result, change.SelectionStart, change.SelectionLength);
    }

    private static (string Text, int Start, int End) Parse(string model)
    {
        var open = model.IndexOf('«', StringComparison.Ordinal);
        if (open >= 0)
        {
            var close = model.IndexOf('»', StringComparison.Ordinal);
            return (model.Remove(close, 1).Remove(open, 1), open, close - 1);
        }
        var caret = model.IndexOf('|', StringComparison.Ordinal);
        return (model.Remove(caret, 1), caret, caret);
    }

    private static string Mark(string text, int start, int length) =>
        length == 0 ? text.Insert(start, "|") : text.Insert(start + length, "»").Insert(start, "«");

    [TestMethod]
    [DataRow("un «mot» ici", InlineMark.Bold, "un **«mot»** ici")]
    [DataRow("un **«mot»** ici", InlineMark.Bold, "un «mot» ici")]
    [DataRow("un «mot» ici", InlineMark.Italic, "un *«mot»* ici")]
    [DataRow("un *«mot»* ici", InlineMark.Italic, "un «mot» ici")]
    [DataRow("un **«mot»** ici", InlineMark.Italic, "un ***«mot»*** ici")]
    [DataRow("un ***«mot»*** ici", InlineMark.Italic, "un **«mot»** ici")]
    [DataRow("un ***«mot»*** ici", InlineMark.Bold, "un *«mot»* ici")]
    [DataRow("un *«mot»* ici", InlineMark.Bold, "un ***«mot»*** ici")]
    [DataRow("un «mot» ici", InlineMark.Underline, "un <u>«mot»</u> ici")]
    [DataRow("un <u>«mot»</u> ici", InlineMark.Underline, "un «mot» ici")]
    [DataRow("un «mot» ici", InlineMark.Strike, "un ~~«mot»~~ ici")]
    [DataRow("x«2»", InlineMark.Superscript, "x<sup>«2»</sup>")]
    [DataRow("H«2»O", InlineMark.Subscript, "H<sub>«2»</sub>O")]
    [DataRow("un «mot» ici", InlineMark.Code, "un `«mot»` ici")]
    [DataRow("un `«mot»` ici", InlineMark.Code, "un «mot» ici")]
    [DataRow("«x»", InlineMark.Formula, "$«x»$")]
    [DataRow("$«x»$", InlineMark.Formula, "«x»")]
    [DataRow("un |mot", InlineMark.Bold, "un **|**mot")]
    [DataRow("un **|** mot", InlineMark.Bold, "un | mot")]
    public void A_mark_wraps_the_selection_and_comes_off_again(string model, InlineMark mark, string expected) =>
        Assert.AreEqual(expected, Apply(model, (text, start, end) => MarkdownEdits.Toggle(text, start, end, mark)));

    [TestMethod]
    public void Edge_spaces_stay_out_of_a_mark_since_it_would_not_be_one() =>
        Assert.AreEqual("un **«mot»** ici", Apply("un« mot »ici", (text, start, end) => MarkdownEdits.Toggle(text, start, end, InlineMark.Bold)));

    [TestMethod]
    [DataRow("Un |titre", 2, "## Un |titre")]
    [DataRow("# Un |titre", 3, "### Un |titre")]
    [DataRow("## Un |titre", 0, "Un |titre")]
    [DataRow("|", 1, "# |")]
    [DataRow("«Un\nDeux»", 2, "«## Un\n## Deux»")]
    [DataRow("avant\n|Un\naprès", 1, "avant\n# |Un\naprès")]
    public void A_heading_level_goes_on_the_lines_of_the_selection(string model, int level, string expected) =>
        Assert.AreEqual(expected, Apply(model, (text, start, end) => MarkdownEdits.Heading(text, start, end, level)));

    [TestMethod]
    [DataRow("texte", 0, 0)]
    [DataRow("# un", 1, 1)]
    [DataRow("avant\n### un", 7, 3)]
    [DataRow("#### ", 5, 4)]
    [DataRow("#pas un titre", 0, 0)]
    public void The_heading_level_of_a_line_is_read_back(string text, int position, int level) =>
        Assert.AreEqual(level, MarkdownEdits.HeadingLevel(text, position));

    [TestMethod]
    [DataRow("«un\ndeux»", ListKind.Bullets, "«- un\n- deux»")]
    [DataRow("«- un\n- deux»", ListKind.Bullets, "«un\ndeux»")]
    [DataRow("«un\n\ndeux»", ListKind.Numbers, "«1. un\n\n2. deux»")]
    [DataRow("«1. un\n2. deux»", ListKind.Numbers, "«un\ndeux»")]
    [DataRow("«- un\n- deux»", ListKind.Numbers, "«1. un\n2. deux»")]
    [DataRow("«1. un\n2. deux»", ListKind.Bullets, "«- un\n- deux»")]
    [DataRow("  |retrait", ListKind.Bullets, "  - |retrait")]
    [DataRow("«un\ndeux»", ListKind.Tasks, "«- [ ] un\n- [ ] deux»")]
    [DataRow("«- [ ] un\n- [x] deux»", ListKind.Tasks, "«- un\n- deux»")]
    [DataRow("«- un»", ListKind.Tasks, "«- [ ] un»")]
    [DataRow("«- [ ] un»", ListKind.Bullets, "«- un»")]
    [DataRow("«- [x] un»", ListKind.Numbers, "«1. un»")]
    [DataRow("«1. un»", ListKind.Tasks, "«- [ ] un»")]
    public void Lines_go_in_and_out_of_a_list(string model, ListKind kind, string expected) =>
        Assert.AreEqual(expected, Apply(model, (text, start, end) => MarkdownEdits.List(text, start, end, kind)));

    [TestMethod]
    public void A_selection_stopping_at_a_line_start_leaves_that_line_out() =>
        Assert.AreEqual("«- un»\ndeux", Apply("«un\n»deux", (text, start, end) => MarkdownEdits.List(text, start, end, ListKind.Bullets)));

    [TestMethod]
    [DataRow("|", "BLOC\n|")]
    [DataRow("   |", "BLOC\n|")]
    [DataRow("avant|", "avant\n\nBLOC\n|")]
    [DataRow("|après", "BLOC\n\n|après")]
    [DataRow("av|ant", "av\n\nBLOC\n\n|ant")]
    public void A_block_stands_on_its_own_lines(string model, string expected) =>
        Assert.AreEqual(expected, Apply(model, (text, start, _) => MarkdownEdits.Block(text, start, "BLOC")));

    [TestMethod]
    public void An_empty_table_has_its_header_then_its_rows()
    {
        Assert.AreEqual("| Colonne 1 | Colonne 2 |\n| --- | --- |\n|   |   |\n|   |   |", MarkdownEdits.Table(3, 2));
        Assert.AreEqual("| Colonne 1 |\n| --- |\n|   |", MarkdownEdits.Table(1, 1));
    }

    [TestMethod]
    [DataRow("Premier paragraphe.|", "Premier paragraphe.\n\n---\n|")]
    [DataRow("Premier.\n|Second.", "Premier.\n\n---\n\n|Second.")]
    [DataRow("Pre|mier.", "Pre\n\n---\n\n|mier.")]
    [DataRow("|", "---\n|")]
    [DataRow("Premier.\n\n|Second.", "Premier.\n\n---\n\n|Second.")]
    [DataRow("Premier.\n\n|\n\nSecond.", "Premier.\n\n---\n\n|Second.")]
    public void A_rule_is_kept_apart_or_it_would_make_a_heading(string model, string expected) =>
        Assert.AreEqual(expected, Apply(model, (text, start, _) => MarkdownEdits.Rule(text, start)));

    [TestMethod]
    [DataRow("Premier.|", false, "Premier.\n\n<!-- kyfrev saut-de-page -->\n|")]
    [DataRow("Premier.\n\n|Second.", false, "Premier.\n\n<!-- kyfrev saut-de-page -->\n\n|Second.")]
    [DataRow("Premier.\n\n|Second.", true, "Premier.\n\n<!-- kyfrev page-blanche -->\n\n|Second.")]
    public void A_page_break_is_written_alone_on_its_line(string model, bool blank, string expected) =>
        Assert.AreEqual(expected, Apply(model, (text, start, _) => MarkdownEdits.PageBreak(text, start, blank)));

    [TestMethod]
    [DataRow("|", "python", "```python\n|\n```\n")]
    [DataRow("avant|", "sql", "avant\n\n```sql\n|\n```\n")]
    [DataRow("avant\n«du code»\naprès", "bash", "avant\n```bash\ndu code\n```\n|\naprès")]
    [DataRow("```python\nx|\n```", "sql", "```sql\nx|\n```")]
    [DataRow("|```python\nx\n```", "", "|```\nx\n```")]
    [DataRow("~~~\nx|\n~~~", "bash", "~~~bash\nx|\n~~~")]
    public void A_code_block_is_created_or_changes_language(string model, string language, string expected) =>
        Assert.AreEqual(expected, Apply(model, (text, start, end) => MarkdownEdits.Code(text, start, end, language)));

    [TestMethod]
    public void A_link_replaces_the_selection() =>
        Assert.AreEqual("voir [la page](https://exemple.org)| ici", Apply("voir «page» ici", (_, start, end) => MarkdownEdits.Link(start, end, "la page", "https://exemple.org")));

    [TestMethod]
    public void A_case_change_applies_to_the_selection_or_the_word_under_the_caret()
    {
        Assert.AreEqual("un «MOT» ici", Apply("un «mot» ici", (text, start, end) => MarkdownEdits.Case(text, start, end, CaseStyle.Upper)!.Value));
        Assert.AreEqual("un MO|T ici", Apply("un mo|t ici", (text, start, end) => MarkdownEdits.Case(text, start, end, CaseStyle.Upper)!.Value));
        Assert.AreEqual("avant\nMOT|", Apply("avant\nmot|", (text, start, end) => MarkdownEdits.Case(text, start, end, null)!.Value));
        Assert.IsNull(MarkdownEdits.Case("   ", 1, 1, CaseStyle.Upper));
    }

    [TestMethod]
    public void Maj_F3_moves_on_to_the_next_case_on_the_selection() =>
        Assert.AreEqual("«MON TITRE»", Apply("«mon titre»", (text, start, end) => MarkdownEdits.Case(text, start, end, null)!.Value));

    [TestMethod]
    public void Lower_and_upper_case_keep_accents()
    {
        Assert.AreEqual("été à l'école", TextCase.Apply("Été À L'ÉCOLE", CaseStyle.Lower));
        Assert.AreEqual("ÉTÉ À L'ÉCOLE, ŒUVRE", TextCase.Apply("été à l'école, œuvre", CaseStyle.Upper));
    }

    [TestMethod]
    public void The_length_never_changes()
    {
        Assert.AreEqual("STRAßE", TextCase.Apply("straße", CaseStyle.Upper));
        var text = "Une phrase. Puis « l'autre » ! 3.14 kg\nstraße";
        foreach (var style in Enum.GetValues<CaseStyle>())
        {
            Assert.AreEqual(text.Length, TextCase.Apply(text, style).Length);
        }
    }

    [TestMethod]
    [DataRow("bONJOUR. comment ÇA va ? très BIEN… merci", "Bonjour. Comment ça va ? Très bien… Merci")]
    [DataRow("premier paragraphe\nSECOND", "Premier paragraphe\nSecond")]
    [DataRow("il a dit « non. » puis il est parti", "Il a dit « non. » Puis il est parti")]
    [DataRow("le fichier notes.md fait 3.14 ko", "Le fichier notes.md fait 3.14 ko")]
    public void A_sentence_starts_after_its_end_and_a_space_and_at_each_paragraph(string text, string expected) =>
        Assert.AreEqual(expected, TextCase.Apply(text, CaseStyle.Sentence));

    [TestMethod]
    public void A_word_starts_after_a_space_or_hyphen_but_not_after_an_apostrophe() =>
        Assert.AreEqual("Jean-Pierre Va À L'école Aujourd'hui", TextCase.Apply("JEAN-PIERRE va à l'école aujourd'hui", CaseStyle.Words));

    [TestMethod]
    public void One_letter_out_of_two_ignores_spaces()
    {
        Assert.AreEqual("UnE lEtTrE sUr DeUx", TextCase.Apply("une lettre sur deux", CaseStyle.Alternate));
        Assert.AreEqual("UnE lEtTrE sUr DeUx En MaJuScUlE eT eN mInUsCuLe", TextCase.Apply("Une lettre sur deux en majuscule et en minuscule", CaseStyle.Alternate));
    }

    [TestMethod]
    public void Inverting_swaps_each_letter() =>
        Assert.AreEqual("iNVERSER lA cASSE 3.14 É", TextCase.Apply("Inverser La Casse 3.14 é", CaseStyle.Invert));

    [TestMethod]
    public void Maj_F3_cycles_through_the_four_cases()
    {
        var text = "mon titre de fiche";
        var seen = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            text = TextCase.Apply(text, TextCase.Next(text));
            seen.Add(text);
        }
        Assert.AreEqual("MON TITRE DE FICHE|Mon Titre De Fiche|Mon titre de fiche|mon titre de fiche|MON TITRE DE FICHE", string.Join('|', seen));
        Assert.AreEqual(CaseStyle.Sentence, TextCase.Next("mOn TiTrE"));
    }

    [TestMethod]
    [DataRow("un mot ici", 4, 3, 6)]
    [DataRow("un mot ici", 6, 3, 6)]
    [DataRow("un mot ici", 3, 3, 6)]
    [DataRow("l'éléfant bleu", 3, 0, 9)]
    [DataRow("peut-être", 5, 0, 9)]
    [DataRow("'mot'", 2, 1, 4)]
    public void The_word_around_a_position_keeps_its_inner_apostrophes(string text, int position, int start, int end) =>
        Assert.AreEqual((start, end), TextCase.WordAround(text, position));

    [TestMethod]
    [DataRow("  ", 1)]
    [DataRow("a  b", 2)]
    [DataRow("123", 1)]
    [DataRow("--", 1)]
    public void Without_a_letter_there_is_no_word(string text, int position) =>
        Assert.IsNull(TextCase.WordAround(text, position));

    [TestMethod]
    public void The_list_of_sheets_opens_on_double_brackets_on_the_same_line()
    {
        Assert.AreEqual(("Mito", 6), SheetLinks.InputBefore("Voir [[Mito"));
        Assert.AreEqual((string.Empty, 2), SheetLinks.InputBefore("Voir [["));
        Assert.AreEqual(("la mitose", 11), SheetLinks.InputBefore("Voir [[la mitose"));
        Assert.IsNull(SheetLinks.InputBefore("[[Mitose]] et "));
        Assert.IsNull(SheetLinks.InputBefore("Voir [Mito"));
    }

    private static VaultSheet Sheet(string id, string title, string folder = "") =>
        new(id, title, $"fiches/{(folder.Length > 0 ? folder + "/" : string.Empty)}{title}.md", folder, [], false, true, null, null);

    private static readonly VaultSheet[] Sheets =
    [
        Sheet("1", "Méiose", "Bio"),
        Sheet("2", "La mitose", "Bio"),
        Sheet("3", "Mitose", "Bio"),
        Sheet("4", "Révolution française", "Histoire"),
        Sheet("5", "Prémitose (notes)"),
    ];

    [TestMethod]
    public void Sheets_are_proposed_by_title_start_then_word_start_then_elsewhere()
    {
        Assert.AreEqual("Mitose|La mitose|Prémitose (notes)", string.Join('|', SheetLinks.Propose(Sheets, "MITO", null).Select(sheet => sheet.Title)));
        Assert.AreEqual("1", string.Join('|', SheetLinks.Propose(Sheets, "meio", null).Select(sheet => sheet.Id)));
        Assert.AreEqual("4", string.Join('|', SheetLinks.Propose(Sheets, "histoire", null).Select(sheet => sheet.Id)));
        Assert.AreEqual("2|5", string.Join('|', SheetLinks.Propose(Sheets, "mito", "3").Select(sheet => sheet.Id)));
        Assert.HasCount(2, SheetLinks.Propose(Sheets, string.Empty, null, 2));
        Assert.IsEmpty(SheetLinks.Propose(Sheets, "zzz", null));
    }

    [TestMethod]
    [DataRow("fiches/Histoire", "fiches/Bio/Mitose.md", "../Bio/Mitose.md")]
    [DataRow("fiches/Bio", "fiches/Bio/Mitose.md", "Mitose.md")]
    [DataRow("fiches", "fiches/Bio/Cellules/Noyau.md", "Bio/Cellules/Noyau.md")]
    [DataRow("fiches/Bio/Cellules", "fiches/Index.md", "../../Index.md")]
    public void The_path_is_relative_to_the_folder_of_the_sheet(string folder, string target, string expected) =>
        Assert.AreEqual(expected, SheetLinks.RelativeFrom(folder, target));

    [TestMethod]
    [DataRow("Mitose", "../Bio/Mitose.md", "[Mitose](../Bio/Mitose.md)")]
    [DataRow("La mitose", "La mitose.md", "[La mitose](<La mitose.md>)")]
    [DataRow("Notes [brouillon]", "Notes.md", @"[Notes \[brouillon\]](Notes.md)")]
    [DataRow("a<b", "a<b.md", "[a<b](<a\\<b.md>)")]
    [DataRow("vide", "", "[vide](<>)")]
    public void A_link_uses_brackets_only_when_needed(string label, string address, string expected) =>
        Assert.AreEqual(expected, SheetLinks.MarkdownLink(label, address));

    [TestMethod]
    public void Signs_that_would_open_a_suffix_are_encoded() =>
        Assert.AreEqual("Info/C%23 à 100 %25AB.md", SheetLinks.AddressOf("fiches", "fiches/Info/C# à 100 %AB.md"));

    [TestMethod]
    public void Code_blocks_are_found_as_in_CommonMark()
    {
        var text = "avant\n```python\nx\n```\nmilieu\n~~~~\n```\ny\n~~~\n~~~~\naprès\n```\nouvert";
        var fences = MarkdownLines.Fences(text);
        Assert.HasCount(3, fences);
        Assert.AreEqual("```python", text[fences[0].Start..fences[0].OpeningEnd]);
        Assert.AreEqual("```python\nx\n```", text[fences[0].Start..fences[0].End]);
        Assert.AreEqual("~~~~\n```\ny\n~~~\n~~~~", text[fences[1].Start..fences[1].End]);
        Assert.AreEqual("```\nouvert", text[fences[2].Start..fences[2].End]);
        Assert.IsNull(MarkdownLines.FenceAt(text, 2));
        Assert.AreEqual(fences[0], MarkdownLines.FenceAt(text, text.IndexOf('x', StringComparison.Ordinal)));
        Assert.IsEmpty(MarkdownLines.Fences("``` a`b\ntexte"));
    }

    [TestMethod]
    [DataRow("un `code", true)]
    [DataRow("un `code` et", false)]
    [DataRow("``a ` b", true)]
    [DataRow("``a ` b`` c", false)]
    [DataRow("sans code", false)]
    [DataRow("", false)]
    public void Inline_code_is_recognised_before_the_caret(string line, bool expected) =>
        Assert.AreEqual(expected, MarkdownLines.IsInInlineCode(line));

    [TestMethod]
    public void Code_addresses_tags_and_formulas_are_not_spell_checked()
    {
        var line = "un `code` [lien](adresse.md) <u>mot</u> https://exemple.org $x^2$ fin";
        var skipped = MarkdownLines.Unchecked(line).Select(range => line[range.Start..range.End]);
        Assert.AreEqual("`code`|](adresse.md)|<u>|</u>|https://exemple.org|$x^2$", string.Join('|', skipped));
    }
}
