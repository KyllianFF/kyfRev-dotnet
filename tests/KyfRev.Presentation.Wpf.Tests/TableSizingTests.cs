using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le partage des colonnes d'un tableau pleine largeur, en lecture : jamais un mot coupé quand le tableau peut l'éviter.</summary>
[TestClass]
public sealed class TableSizingTests
{
    [TestMethod]
    public void A_fitted_table_keeps_what_it_asks_for_and_leaves_the_rest_empty()
    {
        var sizing = new TableSizing([40, 60], [120, 180], Fitted: true);

        Assert.AreEqual("120|180", string.Join('|', sizing.For(680)), "dans une colonne de texte plus large que lui");
        Assert.AreEqual(380, sizing.RestFor(680), "le reste de la ligne à la colonne vide");
        Assert.AreEqual(520, sizing.RestFor(820));
        Assert.AreEqual(0, sizing.RestFor(300), "pile sa largeur : rien ne reste");
        Assert.AreEqual(0, sizing.RestFor(200), 1e-9, "plus étroit que lui : il se resserre, et rien ne reste");
        Assert.AreEqual(200, sizing.For(200).Sum(), 1e-9);
        Assert.IsFalse(new TableSizing([40], [120]).Fitted, "pleine largeur, sauf mention");
    }

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
