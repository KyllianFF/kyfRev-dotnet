using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La mise en page d'une fiche : ses choix, et le calcul des sauts de page.</summary>
[TestClass]
public sealed class PageSetupTests
{
    private const double Usable = 100;
    private const double Between = 40;

    [TestMethod]
    public void The_choices_are_kept_for_all_sheets_or_for_this_one()
    {
        var saved = new List<(PageLayout Layout, bool ForAll)>();
        var setup = new PageSetupViewModel(save: (layout, forAll) => saved.Add((layout, forAll)));
        Assert.IsTrue(setup.IsEndless && setup.AppliesToAll && !setup.HasStatus);
        Assert.AreEqual(string.Empty, setup.Status);

        setup.SetFormatCommand.Execute("A4");
        Assert.IsTrue(setup.IsA4 && setup.IsPortrait && setup.IsNormal && setup.IsPaged);
        Assert.AreEqual("A4 portrait · marges de 2 cm", setup.Status);
        setup.Pages = 3;
        Assert.AreEqual("A4 portrait · marges de 2 cm · 3 pages", setup.Status);
        setup.Pages = 1;
        Assert.AreEqual("A4 portrait · marges de 2 cm · 1 page", setup.Status);

        setup.SetOrientationCommand.Execute("landscape");
        Assert.IsTrue(setup.IsLandscape);
        Assert.AreEqual(0, setup.Pages, "une autre feuille : le nombre de pages est à recompter");
        setup.SetMarginCommand.Execute("narrow");
        Assert.IsTrue(setup.IsNarrow);
        Assert.AreEqual("1,27", setup.MarginText);
        setup.SetMarginCommand.Execute("wide");
        Assert.IsTrue(setup.IsWide);
        setup.NudgeMarginCommand.Execute("-");
        Assert.AreEqual("2,5", setup.MarginText);
        Assert.IsTrue(setup.IsCustomMargin);
        setup.MarginText = "1,5 cm";
        Assert.AreEqual(1.5, setup.Layout.MarginCm);
        setup.MarginText = "n'importe quoi";
        Assert.AreEqual("1,5", setup.MarginText);

        // Une marge large sur une A4 ne tient pas sur une A6 : elle est ramenée à ce que la feuille accepte.
        setup.MarginText = "8";
        setup.SetFormatCommand.Execute("A6");
        Assert.AreEqual(3.25, setup.Layout.MarginCm);
        Assert.IsTrue(saved.All(choice => choice.ForAll));

        var before = saved.Count;
        setup.SetScopeCommand.Execute("sheet");
        Assert.IsTrue(setup.AppliesToSheet);
        Assert.AreEqual((setup.Layout, false), saved[^1]);
        setup.SetScopeCommand.Execute("sheet");
        Assert.HasCount(before + 1, saved, "le même choix ne s'écrit pas deux fois");
        setup.SetFormatCommand.Execute("Endless");
        Assert.AreEqual((PageFormat.Endless, false), (saved[^1].Layout.Format, saved[^1].ForAll));
        setup.SetFormatCommand.Execute("inconnu");
        Assert.IsTrue(setup.IsEndless);
        setup.SetScopeCommand.Execute("all");
        Assert.IsTrue(saved[^1].ForAll);
    }

    [TestMethod]
    public void What_fits_on_the_page_stays_where_it_is()
    {
        var result = PageBreaks.Compute([new PageBox(0, 40), new PageBox(40, 60)], Usable, Between);

        Assert.AreEqual(1, result.Pages);
        Assert.IsEmpty(result.Breaks);
        Assert.IsTrue(result.Placements.All(placement => placement.Spacer == 0 && placement.ShrinkTo is null));
        Assert.AreEqual(0, result.Remaining);
        Assert.AreEqual(0, PageBreaks.Compute([], 0, Between).Pages, "sans feuille, pas de pages");
        Assert.AreEqual(1, PageBreaks.Compute([], Usable, Between).Pages);
    }

    [TestMethod]
    public void A_block_that_would_cross_the_page_end_starts_the_next_page()
    {
        var result = PageBreaks.Compute([new PageBox(0, 70), new PageBox(70, 50), new PageBox(120, 20)], Usable, Between);

        Assert.AreEqual(2, result.Pages);
        // Il restait 30 sur la page : l'espace les prend, puis les marges et l'écart entre les feuilles.
        Assert.AreEqual(70d, result.Placements[1].Spacer);
        Assert.AreEqual(0d, result.Placements[2].Spacer);
        Assert.AreEqual(new PageBreak(70, 100, 140), result.Breaks.Single());
        Assert.AreEqual(30, result.Remaining);
    }

    [TestMethod]
    public void A_table_runs_over_the_pages_one_whole_row_at_a_time()
    {
        var rows = Enumerable.Range(0, 7).Select(index => new PageBox(index * 40, 40, PageBoxKind.Row)).ToList();

        var result = PageBreaks.Compute(rows, Usable, Between);

        Assert.AreEqual(4, result.Pages);
        CollectionAssert.AreEqual(new[] { 0d, 0, 60, 0, 60, 0, 60 }, result.Placements.Select(placement => placement.Spacer).ToArray());
        Assert.HasCount(3, result.Breaks);
    }

    [TestMethod]
    public void A_figure_never_spans_two_pages_and_shrinks_when_it_is_taller_than_one()
    {
        var result = PageBreaks.Compute([new PageBox(0, 30), new PageBox(30, 250, PageBoxKind.Figure), new PageBox(280, 20)], Usable, Between);

        Assert.AreEqual(100d, result.Placements[1].ShrinkTo);
        Assert.AreEqual(110d, result.Placements[1].Spacer, "elle commence la page suivante");
        Assert.AreEqual(3, result.Pages, "ce qui la suit commence une troisième page : elle remplit la sienne");
        Assert.AreEqual(40d, result.Placements[2].Spacer);

        // Un texte plus haut qu'une page, lui, déborde : il commence une page et se poursuit.
        var text = PageBreaks.Compute([new PageBox(0, 30), new PageBox(30, 250), new PageBox(280, 20)], Usable, Between);
        Assert.IsNull(text.Placements[1].ShrinkTo);
        Assert.AreEqual(110d, text.Placements[1].Spacer);
        Assert.AreEqual(5, text.Pages);
        Assert.AreEqual(40d, text.Placements[2].Spacer);
    }

    [TestMethod]
    public void A_page_break_starts_a_new_page_and_a_blank_page_leaves_one_empty()
    {
        // Il restait de la place : le saut fait quand même commencer la suite sur une autre page.
        var broken = PageBreaks.Compute([new PageBox(0, 20), new PageBox(20, 1, PageBoxKind.Break), new PageBox(21, 20)], Usable, Between);
        Assert.AreEqual(2, broken.Pages);
        Assert.AreEqual(0d, broken.Placements[1].Spacer, "la marque reste au bas de sa page");
        Assert.AreEqual(119d, broken.Placements[2].Spacer);
        Assert.AreEqual(new PageBreak(21, 100, 140), broken.Breaks.Single());

        // Une page blanche : une feuille entière entre les deux.
        var blank = PageBreaks.Compute([new PageBox(0, 20), new PageBox(20, 1, PageBoxKind.BlankPage), new PageBox(21, 20)], Usable, Between);
        Assert.AreEqual(3, blank.Pages);
        Assert.AreEqual(259d, blank.Placements[2].Spacer);
        CollectionAssert.AreEqual(new[] { new PageBreak(21, 100, 140), new PageBreak(140, 240, 280) }, blank.Breaks.ToArray());

        // En tête de page, un saut ouvre encore une page, comme dans un traitement de texte.
        var first = PageBreaks.Compute([new PageBox(0, 1, PageBoxKind.Break), new PageBox(1, 20)], Usable, Between);
        Assert.AreEqual(2, first.Pages);

        // En fin de fiche, il ouvre sa page, vide : la dernière feuille est entière.
        var last = PageBreaks.Compute([new PageBox(0, 20), new PageBox(20, 1, PageBoxKind.Break)], Usable, Between);
        Assert.AreEqual(2, last.Pages);
        Assert.AreEqual(79 + Between + Usable, last.Remaining);
    }

    [TestMethod]
    public void A_cut_table_repeats_its_header_and_a_cut_quote_says_it_continues()
    {
        // Un tableau à en-tête de 30 : la ligne qui commence une page laisse sa place à l'en-tête répété.
        var rows = new List<PageBox> { new(0, 30, PageBoxKind.Row) };
        rows.AddRange(Enumerable.Range(0, 4).Select(index => new PageBox(30 + (index * 30), 30, PageBoxKind.Row, Continues: true, Lead: 30)));
        var table = PageBreaks.Compute(rows, Usable, Between);
        Assert.AreEqual(2, table.Pages);
        Assert.IsTrue(table.Placements[3].Repeats);
        Assert.AreEqual(50d, table.Placements[3].Spacer, "l'espace va jusqu'au haut de la page ; l'en-tête répété vient ensuite");
        Assert.IsFalse(table.Breaks.Single().Continues, "l'en-tête répété tient lieu de mention");
        Assert.AreEqual(Usable - 90, table.Remaining, "l'en-tête répété et deux lignes sur la seconde page");

        // Sans en-tête, ou dans une citation : la coupure dit « suite ».
        var quote = PageBreaks.Compute([new PageBox(0, 60), new PageBox(60, 60, Continues: true)], Usable, Between);
        Assert.IsTrue(quote.Breaks.Single().Continues);
        Assert.IsFalse(quote.Placements[1].Repeats);
        // Un bloc qui ne poursuit rien ne dit rien.
        Assert.IsFalse(PageBreaks.Compute([new PageBox(0, 60), new PageBox(60, 60)], Usable, Between).Breaks.Single().Continues);
        // Après un saut demandé, rien n'est coupé.
        var asked = PageBreaks.Compute([new PageBox(0, 30, PageBoxKind.Row), new PageBox(30, 1, PageBoxKind.Break), new PageBox(31, 30, PageBoxKind.Row, Continues: true, Lead: 30)], Usable, Between);
        Assert.IsFalse(asked.Placements[2].Repeats || asked.Breaks.Single().Continues);
    }

    [TestMethod]
    public void The_same_text_always_gives_the_same_breaks()
    {
        var boxes = new List<PageBox>();
        var top = 0d;
        foreach (var height in new[] { 35d, 60, 20, 90, 15, 45, 80, 10, 55 })
        {
            boxes.Add(new PageBox(top, height));
            top += height;
        }

        var first = PageBreaks.Compute(boxes, Usable, Between);
        var again = PageBreaks.Compute(boxes, Usable, Between);

        CollectionAssert.AreEqual(first.Placements.ToArray(), again.Placements.ToArray());
        Assert.AreEqual(first.Pages, again.Pages);
        // Aucun bloc, une fois placé, ne traverse le bas de sa page.
        var shift = 0d;
        var pageTop = 0d;
        for (var index = 0; index < boxes.Count; index++)
        {
            shift += first.Placements[index].Spacer;
            if (first.Placements[index].Spacer > 0)
            {
                pageTop = boxes[index].Top + shift;
            }
            Assert.IsLessThanOrEqualTo(pageTop + Usable + 0.5, boxes[index].Top + shift + boxes[index].Height, $"bloc {index}");
        }
    }
}
