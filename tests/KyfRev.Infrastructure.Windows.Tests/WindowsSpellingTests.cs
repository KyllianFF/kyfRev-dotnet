namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>Le vrai correcteur de Windows, sur les langues présentes ; aucun mot n'est ajouté au dictionnaire de la machine.</summary>
[TestClass]
public sealed class WindowsSpellingTests
{
    /// <summary>Le correcteur d'une langue installée, avec un mot faux et son texte juste.</summary>
    private static (WindowsSpelling Spelling, string Wrong, string Right) Installed()
    {
        var french = new WindowsSpelling("fr-FR");
        return french.Check("Une ékole").Count > 0
            ? (french, "ékole", "Une école claire")
            : (new WindowsSpelling("en-US"), "skool", "A clear school");
    }

    [TestMethod]
    public void A_mistake_is_found_where_it_is()
    {
        var (spelling, wrong, right) = Installed();

        var mistakes = spelling.Check($"{right} {wrong}");

        Assert.HasCount(1, mistakes);
        Assert.AreEqual(right.Length + 1, mistakes[0].Start);
        Assert.AreEqual(wrong.Length, mistakes[0].Length);
    }

    [TestMethod]
    public void A_correct_text_has_no_mistake()
    {
        var (spelling, _, right) = Installed();

        Assert.IsEmpty(spelling.Check(right));
        Assert.IsEmpty(spelling.Check("   "));
    }

    [TestMethod]
    public void A_wrong_word_comes_with_suggestions()
    {
        var (spelling, wrong, _) = Installed();

        var correction = spelling.Correct(wrong);

        Assert.IsNotNull(correction);
        Assert.AreEqual(0, correction.Start);
        Assert.AreEqual(wrong.Length, correction.Length);
        Assert.IsNotEmpty(correction.Suggestions);
        Assert.IsLessThanOrEqualTo(5, correction.Suggestions.Count);
    }

    [TestMethod]
    public void A_right_word_needs_no_correction()
    {
        var (spelling, _, right) = Installed();

        Assert.IsNull(spelling.Correct(right.Split(' ')[^1]));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("  ")]
    [DataRow("a\nb")]
    public void What_is_not_a_word_is_neither_corrected_nor_added(string word)
    {
        var (spelling, _, _) = Installed();

        Assert.IsNull(spelling.Correct(word));
        Assert.IsFalse(spelling.Add(word));
    }

    [TestMethod]
    public void Several_words_cannot_be_added_nor_a_very_long_one()
    {
        var (spelling, _, _) = Installed();

        Assert.IsFalse(spelling.Add("deux mots"));
        Assert.IsFalse(spelling.Add(new string('a', 101)));
        Assert.IsNull(spelling.Correct(new string('a', 101)));
    }

    [TestMethod]
    [DataRow("xx-XX")]
    [DataRow("")]
    public void Without_the_language_nothing_is_flagged(string language)
    {
        var spelling = new WindowsSpelling(language);

        Assert.IsEmpty(spelling.Check("Une ékole"));
        Assert.IsNull(spelling.Correct("ékole"));
        Assert.IsFalse(spelling.Add("ékole"));
    }

    [TestMethod]
    public void The_default_checker_is_french() =>
        Assert.IsNotNull(new WindowsSpelling().Check("Une école"));
}
