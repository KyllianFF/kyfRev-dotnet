using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le partage des colonnes d'un tableau pleine largeur, en lecture : jamais un mot coupé quand le tableau peut l'éviter.</summary>
[TestClass]
public sealed class TableSizingTests
{
    private static readonly TableSizing Verbs = new([60, 80], [150, 100]);
    private static readonly double[] Asked = [150, 100];
    private static readonly double[] Words = [60, 80];
    private static readonly double[] Even = [50, 50];

    [TestMethod]
    public void With_room_enough_each_column_grows_with_what_it_asks() =>
        CollectionAssert.AreEqual(Asked, Verbs.For(400).ToArray());

    [TestMethod]
    public void A_narrow_text_keeps_each_longest_word_and_shares_the_rest()
    {
        var widths = Verbs.For(200);

        Assert.AreEqual(200, widths.Sum(), 1e-9);
        Assert.AreEqual(60 + (60 * 90.0 / 110), widths[0], 1e-9);
        Assert.IsGreaterThanOrEqualTo(80, widths[1], "la colonne courte garde son plus long mot");
    }

    [TestMethod]
    public void When_even_the_words_do_not_fit_they_share_in_proportion()
    {
        CollectionAssert.AreEqual(Words, Verbs.For(100).ToArray());
        CollectionAssert.AreEqual(Even, new TableSizing(Even, Even).For(80).ToArray());
    }
}
