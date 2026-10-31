using KyfRev.Application.Model;
using KyfRev.Application.Text;

namespace KyfRev.Application.Tests;

/// <summary>
/// Texte aux règles de Rust et années des dates libres. Leur comparaison avec la 0.x passe par les
/// vecteurs et scénarios de la suite du coffre ; ces cas fixent les règles une à une.
/// </summary>
[TestClass]
public sealed class TextTests
{
    private static readonly string DotAbove = ((char)0x0307).ToString();

    [TestMethod]
    public void Characters_are_unicode_scalars()
    {
        Assert.AreEqual(3, RustText.CharCount("a😀é"));
        Assert.AreEqual("a😀", RustText.Take("a😀é", 2));
        Assert.AreEqual("ab", RustText.Take("ab", 5));
        Assert.AreEqual(string.Empty, RustText.Take("ab", 0));
        Assert.AreEqual("a b c", RustText.CollapseWhitespace($" a {(char)0xA0} b{(char)0x2003}c\n"));
    }

    [TestMethod]
    [DataRow("ÉCOLE", "école")]
    [DataRow("ΟΔΟΣ", "οδος")]
    [DataRow("ΣΑ", "σα")]
    [DataRow("Σ", "σ")]
    [DataRow("ΑΣ'", "ας'")]
    [DataRow("ΑΣ'Β", "ασ'β")]
    [DataRow("Α.Σ", "α.ς")]
    [DataRow("ªΣ", "ªς")]
    [DataRow("1Σ", "1σ")]
    public void Lowercase_follows_the_full_unicode_mappings(string text, string expected) =>
        Assert.AreEqual(expected, RustText.ToLowercase(text));

    [TestMethod]
    public void A_dotted_capital_i_becomes_two_characters() =>
        Assert.AreEqual("i" + DotAbove + "stanbul", RustText.ToLowercase("İstanbul"));

    [TestMethod]
    public void Case_insensitive_equality_uses_the_same_lowercase()
    {
        Assert.IsTrue(RustText.EqualsIgnoringCase("Œuvre", "œUVRE"));
        Assert.IsFalse(RustText.EqualsIgnoringCase("a", "b"));
    }

    [TestMethod]
    public void Strings_are_ordered_by_their_utf8_bytes()
    {
        var replacement = ((char)0xFFFD).ToString();

        Assert.IsLessThan(0, Utf8Order.Instance.Compare(replacement, "😀"));
        Assert.IsGreaterThan(0, string.CompareOrdinal(replacement, "😀"));
        Assert.IsLessThan(0, Utf8Order.Instance.Compare("Z", "a"));
        Assert.AreEqual(0, Utf8Order.Instance.Compare(null, string.Empty));
        Assert.IsGreaterThan(0, Utf8Order.Instance.Compare("é", null));
    }

    [TestMethod]
    [DataRow("1670", 1670)]
    [DataRow("mai 1968", 1968)]
    [DataRow("vers 1650", 1650)]
    [DataRow("30 juillet 1903", 1903)]
    [DataRow("12 av. J.-C.", -12)]
    [DataRow("XVIIe siècle", 1650)]
    [DataRow("IVe siècle av. J.-C.", -350)]
    [DataRow("IIIe siècle avant J.-C.", -250)]
    [DataRow("Ier siècle", 50)]
    [DataRow("XIXè", 1850)]
    [DataRow("XLe", 3950)]
    public void The_year_of_a_free_date(string date, int year) =>
        Assert.AreEqual(year, QuoteDates.YearOf(date));

    [TestMethod]
    [DataRow("")]
    [DataRow("inconnue")]
    [DataRow("12345")]
    [DataRow("MMe siècle")]
    [DataRow("IIIIIIIIIIIXe")]
    public void A_date_without_a_year(string date) =>
        Assert.IsNull(QuoteDates.YearOf(date));

    [TestMethod]
    public void A_missing_date_is_a_programming_error() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => QuoteDates.YearOf(null!));
}
