using System.IO;

namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>
/// Le vrai correcteur de Windows, sur les langues présentes ; aucun mot n'est ajouté au dictionnaire de la machine, seul un mot
/// accepté pour la session y passe, et il n'y laisse rien.
/// </summary>
[TestClass]
public sealed class WindowsSpellingTests
{
    /// <summary>Le correcteur d'une langue installée, avec un mot faux et son texte juste.</summary>
    private static (WindowsSpelling Spelling, string Wrong, string Right) Installed(bool untilClosing = false)
    {
        var french = new WindowsSpelling("fr-FR", () => untilClosing);
        return french.Check("Une ékole").Count > 0
            ? (french, "ékole", "Une école claire")
            : (new WindowsSpelling("en-US", () => untilClosing), "skool", "A clear school");
    }

    /// <summary>Tout ce que Windows garde des dictionnaires de l'utilisateur, dans le fichier où il le garde.</summary>
    private static string[] UserDictionaries()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Spelling");
        if (!Directory.Exists(folder))
        {
            return [];
        }
        return [.. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Select(file =>
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        })];
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
    public void A_word_accepted_on_an_encrypted_vault_never_reaches_the_dictionary_of_windows()
    {
        var (spelling, _, _) = Installed(untilClosing: true);
        var word = string.Concat(Guid.NewGuid().ToByteArray().Select(value => "bcdfgjkmpqvwxz"[value % 14]));
        Assert.HasCount(1, spelling.Check(word), "Un mot qu'aucun dictionnaire ne connaît.");

        Assert.IsTrue(spelling.AddsUntilClosing);
        Assert.IsTrue(spelling.Add(word));

        Assert.IsEmpty(spelling.Check(word), "Il est accepté le temps de la session.");
        Assert.IsFalse(UserDictionaries().Any(text => text.Contains(word, StringComparison.OrdinalIgnoreCase)), "Rien de ce mot sur le disque.");
    }

    [TestMethod]
    public void Without_its_answer_the_checker_cannot_be_built() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new WindowsSpelling(null!));

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
        var spelling = new WindowsSpelling(language, () => false);

        Assert.IsEmpty(spelling.Check("Une ékole"));
        Assert.IsNull(spelling.Correct("ékole"));
        Assert.IsFalse(spelling.Add("ékole"));
    }

    [TestMethod]
    public void The_default_checker_is_french() =>
        Assert.IsNotNull(new WindowsSpelling(() => false).Check("Une école"));
}
