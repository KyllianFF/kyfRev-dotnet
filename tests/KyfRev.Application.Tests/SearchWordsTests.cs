using System.Collections.Immutable;
using System.Text;
using KyfRev.Application.Model;
using KyfRev.Application.Text;

namespace KyfRev.Application.Tests;

/// <summary>Les mots d'une recherche, comme <c>recherche.rs</c> de la 0.x : ce qui correspond, et l'extrait qu'on en montre.</summary>
[TestClass]
public sealed class SearchWordsTests
{
    private const string Sample = "L’État, le Cœur, ÆTHER";

    [TestMethod]
    public void A_text_is_compared_without_case_accents_ligatures_or_curly_apostrophes()
    {
        var folded = FoldedText.Of(Sample);

        Assert.AreEqual("l'etat, le coeur, aether", folded.Text);
        var position = folded.Text.IndexOf("coeur", StringComparison.Ordinal);
        Assert.AreEqual((11, 15), folded.InOriginal(Sample, position, position + 5), "« Cœur » dans le texte d'origine");
    }

    [TestMethod]
    public void Apostrophes_ligatures_and_decomposed_letters_are_the_same_as_their_plain_forms()
    {
        Assert.AreEqual("l'a'b'", FoldedText.Of("l‘aʼb’").Text);
        Assert.AreEqual("oeoeaeae", FoldedText.Of("œŒæÆ").Text);
        Assert.AreEqual("ecole", FoldedText.Of("école").Text);
    }

    [TestMethod]
    public void A_character_outside_the_basic_plane_keeps_its_two_units()
    {
        const string text = "a😀b";

        var folded = FoldedText.Of(text);

        Assert.AreEqual("a😀b", folded.Text);
        Assert.AreEqual((1, 3), folded.InOriginal(text, 1, 3));
        Assert.AreEqual((3, 4), folded.InOriginal(text, 3, 4));
    }

    [TestMethod]
    public void Every_character_can_be_compared()
    {
        var characters = Enumerable.Range(0, 0x110000).Where(Rune.IsValid).Select(value => new Rune(value).ToString()).ToList();

        var folded = characters.Select(FoldedText.Of).ToList();

        Assert.HasCount(0x10F800, folded);
        Assert.AreEqual("￾", folded[0xFFFE - 0x800].Text, "un non-caractère reste tel quel");
    }

    [TestMethod]
    public void A_broken_surrogate_is_searched_like_any_other_character()
    {
        var text = "a\uD83Db";

        Assert.AreEqual("a�b", FoldedText.Of(text).Text);
        Assert.AreEqual((2, 3), FoldedText.Of(text).InOriginal(text, 2, 3));
    }

    [TestMethod]
    public void The_text_of_an_excerpt_stands_on_one_line_without_its_marks()
    {
        Assert.AreEqual("La mitose et code", SearchWords.Readable("La **mitose**\n\n  et `code`"));
        Assert.AreEqual(" début ", SearchWords.Readable("  début "));
        Assert.AreEqual(string.Empty, SearchWords.Readable("**"));
    }

    [TestMethod]
    public void Without_a_word_everything_matches_and_nothing_is_shown()
    {
        var words = new SearchWords("   ");

        Assert.IsTrue(words.IsEmpty);
        Assert.IsEmpty(words.Match("Mitose", "La prophase.")!.Value);
        Assert.IsFalse(new SearchWords("a").IsEmpty);
        Assert.ThrowsExactly<ArgumentNullException>(() => new SearchWords(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => words.Match(null!, string.Empty));
        Assert.ThrowsExactly<ArgumentNullException>(() => words.Match(string.Empty, null!));
    }

    [TestMethod]
    public void Each_word_must_be_in_the_title_or_in_the_text()
    {
        var words = new SearchWords("révolution DETTE");

        Assert.IsNotNull(words.Match("Révolution française", "La dette."));
        Assert.IsNotNull(words.Match(string.Empty, "La révolution et la dette."));
        Assert.IsNull(words.Match("Révolution française", "Les cellules."));
        Assert.IsNull(words.Match("Mitose", "Les cellules."));
    }

    [TestMethod]
    public void A_title_that_holds_every_word_needs_no_excerpt() =>
        Assert.IsEmpty(new SearchWords("mitose").Match("La Mitose", "La mitose est une division.")!.Value);

    [TestMethod]
    public void The_excerpt_goes_to_the_words_missing_from_the_title_and_marks_them_all() =>
        Assert.AreEqual(
            "La [mitose] donne des cellules [filles].",
            Read(new SearchWords("mitose filles").Match("Mitose", "La mitose donne des cellules filles.")));

    [TestMethod]
    public void A_word_of_the_title_is_not_where_the_excerpt_starts()
    {
        var before = new string('x', 300);

        var excerpt = Read(new SearchWords("mitose filles").Match("Mitose", $"mitose {before} filles au bout"));

        Assert.AreEqual("… [filles] au bout", excerpt, "le mot du titre, hors de la fenêtre, n'est pas repéré");
    }

    [TestMethod]
    public void Marks_are_found_in_the_original_text_accents_and_ligatures_included() =>
        Assert.AreEqual("Le [cœur] et l’[État].", Read(new SearchWords("coeur etat").Match(string.Empty, "Le cœur et l’État.")));

    [TestMethod]
    public void Marks_that_overlap_become_one() =>
        Assert.AreEqual("La [mitose] est là.", Read(new SearchWords("mito tose itos").Match(string.Empty, "La mitose est là.")));

    [TestMethod]
    public void Marks_that_touch_become_one() =>
        Assert.AreEqual("Un [cartable] noir.", Read(new SearchWords("car table").Match(string.Empty, "Un cartable noir.")));

    [TestMethod]
    public void Every_occurrence_in_the_window_is_marked() =>
        Assert.AreEqual("[ab] x [ab] y [AB]", Read(new SearchWords("ab").Match(string.Empty, "ab x ab y AB")));

    [TestMethod]
    public void A_text_shorter_than_the_excerpt_is_shown_whole() =>
        Assert.AreEqual("Le [clergé] et la noblesse", Read(new SearchWords("clerge").Match(string.Empty, "Le clergé et la noblesse")));

    [TestMethod]
    public void The_excerpt_stops_at_the_start_of_the_line_of_the_word() =>
        Assert.AreEqual("Le [clergé].", Read(new SearchWords("clerge").Match(string.Empty, "Une première ligne.\nLe clergé.")));

    [TestMethod]
    public void The_excerpt_starts_on_a_whole_word_and_says_it_was_cut()
    {
        var excerpt = Read(new SearchWords("milieu").Match(string.Empty, "Un deux trois quatre cinq six sept huit neuf dix onze douze treize MILIEU quatorze."));

        Assert.AreEqual("… sept huit neuf dix onze douze treize [MILIEU] quatorze.", excerpt);
    }

    [TestMethod]
    public void Characters_before_the_word_are_counted_as_characters_not_units()
    {
        var before = string.Concat(Enumerable.Repeat("😀", 50));

        var excerpt = Read(new SearchWords("milieu").Match(string.Empty, before + "milieu"));

        Assert.AreEqual("… " + string.Concat(Enumerable.Repeat("😀", 40)) + "[milieu]", excerpt);
    }

    [TestMethod]
    public void Characters_after_the_word_are_counted_as_characters_not_units()
    {
        var after = string.Concat(Enumerable.Repeat("😀", 200));

        var excerpt = Read(new SearchWords("milieu").Match(string.Empty, "milieu" + after));

        Assert.AreEqual("[milieu]" + string.Concat(Enumerable.Repeat("😀", 134)) + "…", excerpt);
    }

    [TestMethod]
    public void A_text_of_exactly_the_length_of_the_excerpt_is_not_cut()
    {
        var text = "milieu" + new string('x', 134);

        Assert.AreEqual("[milieu]" + new string('x', 134), Read(new SearchWords("milieu").Match(string.Empty, text)));
    }

    [TestMethod]
    public void A_word_longer_than_the_excerpt_is_cut_at_its_length()
    {
        var text = "milieu" + new string('x', 300);

        Assert.AreEqual("[milieu]" + new string('x', 134) + "…", Read(new SearchWords("milieu").Match(string.Empty, text)));
    }

    [TestMethod]
    public void The_excerpt_ends_between_two_words_and_says_it_was_cut()
    {
        var text = "milieu " + string.Concat(Enumerable.Repeat("abcd ", 100));

        var excerpt = Read(new SearchWords("milieu").Match(string.Empty, text));

        Assert.EndsWith("abcd…", excerpt);
    }

    [TestMethod]
    public void A_word_beyond_the_window_is_not_marked()
    {
        var text = "début " + new string('x', 130) + " lointain fin";

        var excerpt = Read(new SearchWords("debut lointain").Match(string.Empty, text));

        Assert.AreEqual("[début] " + new string('x', 130) + "…", excerpt);
    }

    [TestMethod]
    public void A_mark_made_only_of_markdown_marks_leaves_nothing_empty_behind() =>
        Assert.AreEqual("[un]", Read(new SearchWords("* un").Match(string.Empty, "un *")));

    [TestMethod]
    public void The_excerpt_has_no_blank_at_either_end()
    {
        var pieces = new SearchWords("mot").Match(string.Empty, "   mot   ")!.Value;

        Assert.AreEqual("mot", pieces.Single().Text);
        Assert.IsTrue(pieces.Single().Found);
    }

    /// <summary>L'extrait, avec les mots trouvés entre crochets.</summary>
    private static string Read(ImmutableArray<ExcerptPiece>? excerpt) =>
        string.Concat(excerpt!.Value.Select(piece => piece.Found ? $"[{piece.Text}]" : piece.Text));
}
