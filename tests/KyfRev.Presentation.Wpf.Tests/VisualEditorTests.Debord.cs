using System.Globalization;
using System.Text.Json;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// En écriture, comme en lecture (15A) : le texte garde la colonne choisie, et un tableau, une image seule sur sa ligne ou un
/// dessin plus large qu'elle déborde autant de chaque côté, jusqu'à la feuille de la colonne « Large ».
/// </summary>
public sealed partial class VisualEditorTests
{
    /// <summary>Ce que montre la page, en pixels de la page : la colonne de texte, chaque bloc, la feuille et la vue.</summary>
    private sealed record Spread(double[][] Text, double[][] Tables, double[]? Picture, double[]? Drawing, double[] Sheet, double View, double Scroll, bool Wide, string Cell);

    private static Spread Measure(Scene scene)
    {
        using var read = JsonDocument.Parse(JsonSerializer.Deserialize<string>(Read(scene))!);
        var root = read.RootElement;
        static double[] Box(JsonElement box) => [.. box.EnumerateArray().Select(value => value.GetDouble())];
        static double[]? Maybe(JsonElement box) => box.ValueKind == JsonValueKind.Null ? null : Box(box);
        return new Spread(
            [.. root.GetProperty("Text").EnumerateArray().Select(Box)],
            [.. root.GetProperty("Tables").EnumerateArray().Select(Box)],
            Maybe(root.GetProperty("Picture")),
            Maybe(root.GetProperty("Drawing")),
            Box(root.GetProperty("Sheet")),
            root.GetProperty("View").GetDouble(),
            root.GetProperty("Scroll").GetDouble(),
            root.GetProperty("Wide").GetBoolean(),
            root.GetProperty("Cell").GetString() ?? string.Empty);
    }

    private static string Read(Scene scene) => scene.Script("""
        JSON.stringify((() => {
          const boite = (e) => { const b = e.getBoundingClientRect(); return [b.left, b.width]; };
          const texte = document.querySelector('.ProseMirror');
          const dessin = texte.querySelector(':scope > .bloc-dessin .apercu-dessin svg');
          const image = texte.querySelector(':scope > p .image img');
          const ancre = getSelection().anchorNode;
          return {
            Text: [...texte.querySelectorAll(':scope > p')].filter((p) => p.textContent.startsWith('Un paragraphe')).map(boite),
            Tables: [...texte.querySelectorAll(':scope > table')].map(boite),
            Picture: image && image.complete && image.naturalWidth > 0 ? boite(image) : null,
            Drawing: dessin ? boite(dessin) : null,
            Sheet: boite(document.getElementById('fiche')),
            View: document.documentElement.clientWidth,
            Scroll: document.documentElement.scrollWidth - document.documentElement.clientWidth,
            Wide: document.getElementById('fiche').classList.contains('avec-debord'),
            Cell: (ancre && (ancre.nodeType === 1 ? ancre : ancre.parentElement)?.closest('td, th')?.textContent) || '',
          };
        })())
        """);

    [EngineTest]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void What_is_wider_than_the_text_column_breaks_out_of_it_while_writing(ThemeKind theme)
    {
        using var scene = new Scene(WideBlocksTests.Body(), width: 1600, theme: theme);
        var visual = scene.Visual!;
        var suffix = theme == ThemeKind.Dark ? "sombre" : "clair";
        UiThread.Until(() => scene.Script("document.querySelector('.ProseMirror .bloc-dessin .apercu-dessin svg') !== null") == "true");
        Assert.AreEqual(SheetPageViewModel.ComfortableTextWidth, visual.TextWidth, "la fiche s'écrit dans la colonne « Confortable »");
        Assert.AreEqual(SheetPageViewModel.TextMaxWidth, visual.WideWidth, "et ce qui est plus large qu'elle va jusqu'à la colonne « Large »");

        // Le curseur dans une case du tableau large : il y reste pendant que les blocs se mesurent.
        scene.Select("const cellule = [...page.querySelectorAll('td')].find((td) => td.textContent.startsWith('De deux')); selection.selectAllChildren(cellule); selection.collapseToEnd();");
        UiThread.Until(() => scene.Editor.IsInTable);

        foreach (var column in new[] { SheetPageViewModel.ComfortableTextWidth, SheetPageViewModel.NarrowTextWidth })
        {
            visual.TextWidth = column;
            var spread = Settled(scene, wide: true, column);
            var view = spread.View;
            var allowed = Math.Max(0, Math.Floor((Math.Min(SheetPageViewModel.TextMaxWidth, view - 24) - column) / 2));
            Assert.IsGreaterThan(0, allowed, "la vue de l'essai a la place de déborder");

            // Le texte : la colonne choisie, au milieu de la feuille, chaque paragraphe au même endroit.
            var (left, width) = (spread.Text[0][0], spread.Text[0][1]);
            Assert.AreEqual(column - 82, width, 1, "la colonne de texte ne change pas");
            Assert.IsTrue(spread.Text.All(line => Math.Abs(line[0] - left) < 0.5 && Math.Abs(line[1] - width) < 0.5), "tout le texte est dans la colonne");
            var centre = left + (width / 2);
            Assert.AreEqual(spread.Sheet[0] + (spread.Sheet[1] / 2), centre, 1, "au milieu de la feuille");

            // Le tableau large : plus large que le texte, dans ce qui est permis, centré sur lui.
            var table = spread.Tables[0];
            Assert.IsGreaterThan(width + 20, table[1], "le tableau large sort de la colonne");
            Assert.IsLessThanOrEqualTo(width + (2 * allowed) + 1, table[1]);
            Assert.AreEqual(centre, table[0] + (table[1] / 2), 1, "centré sur le texte");
            // Le tableau ajusté, plus étroit que le texte : à son bord, comme avant.
            Assert.AreEqual(left, spread.Tables[1][0], 0.5);
            Assert.IsLessThan(width, spread.Tables[1][1]);

            // L'image de 800 pixels : toute la place permise, centrée sur le texte.
            Assert.IsNotNull(spread.Picture);
            Assert.AreEqual(Math.Min(800, width + (2 * allowed)), spread.Picture[1], 1);
            Assert.AreEqual(centre, spread.Picture[0] + (spread.Picture[1] / 2), 1.5);

            // Le dessin : ses 640 pixels quand il y a la place, centré.
            Assert.IsNotNull(spread.Drawing);
            Assert.AreEqual(Math.Min(640, width + (2 * allowed)), spread.Drawing[1], 1);
            Assert.AreEqual(centre, spread.Drawing[0] + (spread.Drawing[1] / 2), 1.5);

            // La feuille s'élargit du plus grand débord, et rien ne sort de la vue.
            Assert.AreEqual(column + (2 * allowed), spread.Sheet[1], 1, "la feuille va jusqu'à la colonne « Large »");
            Assert.AreEqual(0, spread.Scroll, "jamais de défilement horizontal");
            StringAssert.StartsWith(spread.Cell, "De d", StringComparison.Ordinal, "le curseur reste dans sa case");
            Capture(visual, $"ecriture-debord-{column.ToString(CultureInfo.InvariantCulture)}-{suffix}");
        }
        scene.Script("document.querySelector('.ProseMirror .bloc-dessin').scrollIntoView({ block: 'center' })");
        Pump(TimeSpan.FromMilliseconds(200));
        Capture(visual, $"ecriture-debord-dessin-{suffix}");

        // Sur un papier : rien ne déborde, la feuille garde la largeur du texte.
        visual.SetPaper("lignes");
        var ruled = Settled(scene, wide: false, SheetPageViewModel.NarrowTextWidth);
        var ruledWidth = ruled.Text[0][1];
        Assert.IsTrue(ruled.Tables.All(table => table[1] <= ruledWidth + 1), "les tableaux restent dans la colonne");
        Assert.IsLessThanOrEqualTo(ruledWidth + 1, ruled.Picture![1]);
        Assert.IsLessThanOrEqualTo(ruledWidth + 1, ruled.Drawing![1]);
        Assert.AreEqual(SheetPageViewModel.NarrowTextWidth, ruled.Sheet[1], 1);
        Capture(visual, $"ecriture-debord-papier-{suffix}");
        visual.SetPaper(null);
        Settled(scene, wide: true, SheetPageViewModel.NarrowTextWidth);

        // Une colonne à la largeur de la vue (le code et son rendu) : rien ne déborde, comme avant.
        visual.TextWidth = double.PositiveInfinity;
        var full = Settled(scene, wide: false, null);
        Assert.IsTrue(full.Tables.All(table => table[1] <= full.Text[0][1] + 1));
        Assert.AreEqual(0, full.Scroll);
    }

    [EngineTest]
    public void In_a_window_that_only_has_room_for_the_text_nothing_breaks_out()
    {
        using var scene = new Scene(WideBlocksTests.Body(), width: 640);
        var visual = scene.Visual!;
        UiThread.Until(() => scene.Script("document.querySelector('.ProseMirror .bloc-dessin .apercu-dessin svg') !== null") == "true");
        visual.TextWidth = SheetPageViewModel.ComfortableTextWidth;
        var spread = Settled(scene, wide: false, null);
        var width = spread.Text[0][1];
        Assert.IsTrue(spread.Tables.All(table => table[1] <= width + 1), "les tableaux se replient dans la colonne, comme avant");
        Assert.IsLessThanOrEqualTo(width + 1, spread.Picture![1]);
        Assert.AreEqual(0, spread.Scroll, "jamais de défilement horizontal");
        Capture(visual, "ecriture-debord-etroit");
    }

    /// <summary>
    /// Attend que les blocs soient mesurés et posés : la feuille large ou non, la colonne à sa largeur, l'image lue ; puis deux
    /// mesures pareilles de suite.
    /// </summary>
    private static Spread Settled(Scene scene, bool wide, double? column)
    {
        Spread? last = null;
        Spread? spread = null;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            last = spread;
            spread = Measure(scene);
            if (spread.Wide == wide
                && spread.Text.Length > 0
                && spread.Picture is not null
                && spread.Drawing is not null
                && (column is not { } expected || Math.Abs(spread.Text[0][1] - (expected - 82)) < 1)
                && last is not null
                && Same(last, spread))
            {
                return spread;
            }
            Pump(TimeSpan.FromMilliseconds(50));
        }
        Assert.Fail($"les blocs ne se posent pas ({(wide ? "feuille large" : "feuille du texte")}) : {Read(scene)} {scene.Script("[...document.querySelectorAll('.ProseMirror > p')].map((p) => p.innerHTML.slice(0, 160)).join(' | ')")}");
        return spread!;
    }

    private static bool Same(Spread one, Spread other) =>
        one.Sheet.SequenceEqual(other.Sheet) && one.Tables.Zip(other.Tables).All(pair => pair.First.SequenceEqual(pair.Second)) && (one.Picture ?? []).SequenceEqual(other.Picture ?? []);

    private static void Capture(Editing.VisualEditor visual, string name)
    {
        var shot = visual.ShowSnapshotAsync();
        UiThread.Until(() => shot.IsCompleted);
        TestShell.Flush();
        visual.Capture(name);
    }
}
