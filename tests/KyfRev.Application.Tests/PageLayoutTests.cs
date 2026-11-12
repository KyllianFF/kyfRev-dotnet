using KyfRev.Application.Model;

namespace KyfRev.Application.Tests;

/// <summary>La mise en page d'une fiche : format, orientation, marges, et ce qui s'en écrit.</summary>
[TestClass]
public sealed class PageLayoutTests
{
    [TestMethod]
    public void A_sheet_is_endless_until_a_format_is_chosen()
    {
        var layout = PageLayout.Default;

        Assert.IsFalse(layout.IsPaged);
        Assert.AreEqual((0d, 0d), (layout.WidthPixels, layout.HeightPixels));
        Assert.AreEqual("sans-fin", layout.Written());
        Assert.AreEqual(layout, PageLayout.Parse(layout.Written()));
    }

    [TestMethod]
    public void The_formats_have_their_real_sizes_and_turn_with_the_orientation()
    {
        var a4 = new PageLayout(PageFormat.A4);
        Assert.AreEqual((21d, 29.7), (a4.WidthCm, a4.HeightCm));
        Assert.AreEqual((794d, 1123d, 76d), (a4.WidthPixels, a4.HeightPixels, a4.MarginPixels));

        var a5 = new PageLayout(PageFormat.A5, Landscape: true);
        Assert.AreEqual((21d, 14.8), (a5.WidthCm, a5.HeightCm));
        Assert.AreEqual((10.5, 14.8), (new PageLayout(PageFormat.A6).WidthCm, new PageLayout(PageFormat.A6).HeightCm));
    }

    [TestMethod]
    public void Margins_always_leave_room_for_the_text()
    {
        Assert.AreEqual(3, new PageLayout(PageFormat.A4, MarginCm: 3).EffectiveMarginCm);
        Assert.AreEqual(3.25, new PageLayout(PageFormat.A6, MarginCm: 9).EffectiveMarginCm, "quatre centimètres de texte restent sur une A6");
        Assert.AreEqual(PageLayout.MinimumMargin, new PageLayout(PageFormat.A4, MarginCm: 0).EffectiveMarginCm);
        Assert.AreEqual(PageLayout.NormalMargin, new PageLayout(PageFormat.A4, MarginCm: double.NaN).EffectiveMarginCm);
    }

    [TestMethod]
    public void What_is_written_is_read_back_and_anything_else_is_the_endless_sheet()
    {
        var layout = new PageLayout(PageFormat.A5, Landscape: true, MarginCm: 1.5);
        Assert.AreEqual("a5 paysage 1.5", layout.Written());
        Assert.AreEqual(layout, PageLayout.Parse("a5 paysage 1.5"));
        Assert.AreEqual(new PageLayout(PageFormat.A4), PageLayout.Parse("A4"));
        Assert.AreEqual(new PageLayout(PageFormat.A6, MarginCm: 3.25), PageLayout.Parse("a6 portrait 12"), "une marge impossible est ramenée à ce que la feuille accepte");

        foreach (var text in new[] { null, "", "   ", "a3 portrait 2", "7", "endless paysage 2", "sans-fin" })
        {
            Assert.AreEqual(PageLayout.Default, PageLayout.Parse(text), text);
        }
    }

    [TestMethod]
    public void Sheets_with_their_own_layout_compare_by_value()
    {
        var a5 = new PageLayout(PageFormat.A5);
        var some = SheetPageLayouts.None.With("f_b", a5).With("f_a", PageLayout.Default);

        Assert.AreEqual(a5, some.Of("f_b"));
        Assert.AreEqual(PageLayout.Default, some.Of("f_a"), "une fiche peut garder la feuille sans fin quand les autres ont des pages");
        Assert.IsNull(some.Of("f_c"));
        Assert.AreEqual(some, SheetPageLayouts.None.With("f_a", PageLayout.Default).With("f_b", a5));
        Assert.AreEqual(some.GetHashCode(), SheetPageLayouts.None.With("f_a", PageLayout.Default).With("f_b", a5).GetHashCode());
        Assert.AreNotEqual(some, some.With("f_b", null));
        Assert.AreEqual(SheetPageLayouts.None, some.With("f_b", null).With("f_a", null));
        Assert.AreEqual(new MachinePreferences { SheetPages = some }, new MachinePreferences { SheetPages = SheetPageLayouts.None.With("f_a", PageLayout.Default).With("f_b", a5) });
        Assert.ThrowsExactly<ArgumentException>(() => some.With(string.Empty, a5));
    }

    [TestMethod]
    public void Each_paper_has_a_name_a_writing_step_and_a_recommended_font()
    {
        Assert.HasCount(6, PaperStyles.All);
        Assert.AreEqual("Endless, A4, A5, A6", string.Join(", ", PaperStyles.Formats));
        foreach (var style in PaperStyles.All)
        {
            Assert.AreEqual(style, PaperStyles.Parse(PaperStyles.Name(style)), "un nom écrit se relit");
        }
        Assert.AreEqual("grands-carreaux", PaperStyles.Name(PaperStyle.LargeSquares));
        Assert.AreEqual(PaperStyle.Plain, PaperStyles.Parse("un papier inconnu"));
        Assert.AreEqual(PaperStyle.Plain, PaperStyles.Parse(null));
        Assert.AreEqual("sans-fin, a4, a5, a6", string.Join(", ", PaperStyles.Formats.Select(PaperStyles.FormatName)));

        // Le pas d'écriture : 8 mm sur les grands carreaux et les lignes, 10 mm ailleurs, rien sur la feuille unie.
        Assert.AreEqual(0.8, PaperStyles.LineStepCm(PaperStyle.LargeSquares));
        Assert.AreEqual(0.8, PaperStyles.LineStepCm(PaperStyle.Lines));
        Assert.AreEqual(1, PaperStyles.LineStepCm(PaperStyle.SmallSquares));
        Assert.AreEqual(1, PaperStyles.LineStepCm(PaperStyle.Dots));
        Assert.AreEqual(1, PaperStyles.LineStepCm(PaperStyle.Graph));
        Assert.AreEqual(0, PaperStyles.LineStepCm(PaperStyle.Plain));
        // En points d'écran, arrondi au trois-centième de pouce : l'unité de la mise en page de la lecture.
        Assert.AreEqual(30.08, PaperStyles.LineStepPixels(PaperStyle.Lines), 0.0001);
        Assert.AreEqual(37.76, PaperStyles.LineStepPixels(PaperStyle.Graph), 0.0001);
        Assert.AreEqual(0, PaperStyles.LineStepPixels(PaperStyle.Plain));
        foreach (var style in PaperStyles.All)
        {
            Assert.AreEqual(0, (PaperStyles.LineStepPixels(style) * 300 / 96) % 1, 1e-9, style.ToString());
        }

        Assert.AreEqual(ReadingFont.Sans, PaperStyles.RecommendedFont(PaperStyle.Plain));
        Assert.AreEqual(ReadingFont.Sans, PaperStyles.RecommendedFont(PaperStyle.SmallSquares));
        Assert.AreEqual(ReadingFont.Handwriting, PaperStyles.RecommendedFont(PaperStyle.LargeSquares));
        Assert.AreEqual(ReadingFont.Serif, PaperStyles.RecommendedFont(PaperStyle.Lines));
        Assert.AreEqual(ReadingFont.Marker, PaperStyles.RecommendedFont(PaperStyle.Dots));
        Assert.AreEqual(ReadingFont.Typewriter, PaperStyles.RecommendedFont(PaperStyle.Graph));
    }

    [TestMethod]
    public void A_paper_is_chosen_for_each_kind_of_sheet()
    {
        var choices = PaperChoices.Default;
        Assert.IsFalse(choices.IsOn);
        Assert.IsTrue(PaperStyles.Formats.All(format => choices.Of(format) == PaperStyle.Plain));

        choices = choices.With(PageFormat.Endless, PaperStyle.Lines).With(PageFormat.A4, PaperStyle.LargeSquares).With(PageFormat.A5, PaperStyle.SmallSquares).With(PageFormat.A6, PaperStyle.Dots);
        Assert.AreEqual((PaperStyle.Lines, PaperStyle.LargeSquares, PaperStyle.SmallSquares, PaperStyle.Dots), (choices.Of(PageFormat.Endless), choices.Of(PageFormat.A4), choices.Of(PageFormat.A5), choices.Of(PageFormat.A6)));
        Assert.AreEqual(new PaperChoices(false, PaperStyle.Lines, PaperStyle.LargeSquares, PaperStyle.SmallSquares, PaperStyle.Dots), choices);
        Assert.AreNotEqual(choices, choices with { IsOn = true });
    }

    [TestMethod]
    public void A_sheet_takes_its_own_layout_gives_it_back_and_the_list_has_an_end()
    {
        var a5 = new PageLayout(PageFormat.A5, Landscape: true);
        var layouts = SheetPageLayouts.None.With("f_2", a5).With("f_1", new PageLayout(PageFormat.A4));
        Assert.AreEqual("f_1, f_2", string.Join(", ", layouts.Items.Select(item => item.Key)), "dans l'ordre des identifiants");
        Assert.AreEqual(a5, layouts.Of("f_2"));
        Assert.IsNull(layouts.Of("f_3"));

        // Sans mise en page, la fiche suit de nouveau celle de toutes ; la retirer deux fois ne gêne pas.
        var back = layouts.With("f_2", null).With("f_2", null);
        Assert.IsNull(back.Of("f_2"));
        Assert.HasCount(1, back.Items);
        Assert.HasCount(2, layouts.Items, "une liste ne change jamais : chaque choix en donne une autre");
        Assert.ThrowsExactly<ArgumentException>(() => layouts.With(string.Empty, a5));

        // Deux listes de même contenu sont égales, et se rangent ensemble.
        var same = SheetPageLayouts.None.With("f_1", new PageLayout(PageFormat.A4)).With("f_2", a5);
        Assert.AreEqual(layouts, same);
        Assert.AreEqual(layouts.GetHashCode(), same.GetHashCode());
        Assert.IsTrue(layouts.Equals((object)same));
        Assert.IsFalse(layouts.Equals((object)"autre chose") || layouts.Equals(back) || layouts.Equals(null));

        // Au-delà du maximum, rien de neuf n'entre ; ce qui y est déjà se change encore.
        var full = SheetPageLayouts.None;
        for (var index = 0; index < SheetPageLayouts.Maximum; index++)
        {
            full = full.With($"f_{index:0000}", a5);
        }
        Assert.HasCount(SheetPageLayouts.Maximum, full.Items);
        Assert.IsNull(full.With("g_de_trop", a5).Of("g_de_trop"));
        Assert.AreEqual(PageLayout.Default, full.With("f_0000", PageLayout.Default).Of("f_0000"));
    }
}
