namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>Ce qui se pose d'un seul tenant sur une page.</summary>
internal enum PageBoxKind
{
    /// <summary>Un bloc de texte : paragraphe, titre, élément de liste, bloc de code.</summary>
    Block,

    /// <summary>Une ligne de tableau : le tableau peut courir sur plusieurs pages, la ligne jamais.</summary>
    Row,

    /// <summary>Une image ou un schéma : jamais à cheval sur deux pages, et rétréci s'il est plus haut qu'une page.</summary>
    Figure,

    /// <summary>Un saut de page : ce qui suit commence la page suivante.</summary>
    Break,

    /// <summary>Une page blanche : une page entière reste vide, et ce qui suit commence celle d'après.</summary>
    BlankPage,
}

/// <summary>Un bloc mesuré, là où il serait sans aucune page : son haut, depuis le premier bloc, et sa hauteur.</summary>
/// <param name="Top">Son haut, sans aucun espace ouvert.</param>
/// <param name="Height">Sa hauteur.</param>
/// <param name="Kind">Ce qu'il est.</param>
/// <param name="Continues">Il poursuit un bloc commencé plus haut (une citation, un tableau) : coupé là, ce bloc le dit.</param>
/// <param name="Lead">La place à réserver au-dessus de lui s'il commence une page : l'en-tête de son tableau, répété.</param>
/// <param name="Snap">
/// Sur un papier réglé, où son haut doit tomber dans le pas d'écriture, depuis le haut du texte de la page (zéro pour un
/// paragraphe ordinaire) ; rien pour ce qui ne s'aligne pas : un tableau, une image, un bloc de code.
/// </param>
internal readonly record struct PageBox(double Top, double Height, PageBoxKind Kind = PageBoxKind.Block, bool Continues = false, double Lead = 0, double? Snap = null);

/// <summary>Ce que la mise en pages décide pour un bloc.</summary>
/// <param name="Spacer">L'espace à ouvrir au-dessus de lui pour qu'il commence la page suivante ; zéro s'il reste où il est.</param>
/// <param name="ShrinkTo">La hauteur à laquelle le ramener (une figure plus haute qu'une page) ; rien s'il garde sa taille.</param>
/// <param name="Repeats">L'en-tête de son tableau est répété au-dessus de lui.</param>
internal readonly record struct PagePlacement(double Spacer, double? ShrinkTo = null, bool Repeats = false);

/// <summary>Un passage à la page suivante.</summary>
/// <param name="ContentEnd">Le bas de ce qui précède, sur la page qui se termine.</param>
/// <param name="PageEnd">Le bas de la zone de texte de la page qui se termine.</param>
/// <param name="NextTop">Le haut de la zone de texte de la page suivante.</param>
/// <param name="Continues">Un bloc est coupé là, sans en-tête à répéter : il dit « suite ».</param>
internal readonly record struct PageBreak(double ContentEnd, double PageEnd, double NextTop, bool Continues = false);

/// <summary>Le résultat d'une mise en pages.</summary>
/// <param name="Placements">Une décision par bloc, dans l'ordre reçu.</param>
/// <param name="Breaks">Les passages d'une page à la suivante, de haut en bas, espaces compris.</param>
/// <param name="Pages">Le nombre de pages.</param>
/// <param name="Remaining">Ce qu'il faut encore sous le dernier bloc pour que la dernière feuille soit entière, marge du bas exclue.</param>
internal sealed record PageBreakResult(IReadOnlyList<PagePlacement> Placements, IReadOnlyList<PageBreak> Breaks, int Pages, double Remaining);

/// <summary>
/// La mise en pages d'une fiche, comme dans un traitement de texte, mais sans jamais couper un bloc : celui qui dépasserait
/// le bas de la page commence la suivante. Un tableau court sur plusieurs pages sans qu'une ligne soit coupée, et son
/// en-tête se répète ; une image ou un schéma tient toujours sur une page, quitte à rétrécir. Seul un bloc de texte plus
/// haut qu'une page déborde : il commence une page et se poursuit. Un saut de page fait commencer la suite sur une autre
/// page ; une page blanche en laisse une vide entre les deux. Sur un papier réglé, le même calcul cale le texte : un bloc de
/// texte qui ne tomberait pas sur une ligne (après un tableau, une image) descend jusqu'à la prochaine.
/// </summary>
internal static class PageBreaks
{
    /// <summary>L'écart toléré entre deux mesures : en dessous, un bloc « tient ».</summary>
    private const double Tolerance = 0.5;

    /// <summary>
    /// Calcule les sauts de page. Les hauts reçus sont ceux des blocs sans aucun espace ajouté : le calcul rend donc le même
    /// résultat tant que le texte ne change pas, quels que soient les espaces déjà ouverts.
    /// </summary>
    /// <param name="boxes">Les blocs, de haut en bas.</param>
    /// <param name="usable">La hauteur de texte d'une page.</param>
    /// <param name="between">Ce qui sépare le bas du texte d'une page du haut du texte de la suivante : deux marges et l'écart entre les feuilles.</param>
    /// <param name="step">Le pas d'écriture du papier ; zéro quand rien ne cale le texte.</param>
    /// <remarks>Une hauteur de page infinie est une feuille sans fin : rien n'y change de page, seul le calage joue.</remarks>
    public static PageBreakResult Compute(IReadOnlyList<PageBox> boxes, double usable, double between, double step = 0)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        var placements = new PagePlacement[boxes.Count];
        var breaks = new List<PageBreak>();
        if (usable <= 0 || double.IsNaN(usable))
        {
            return new PageBreakResult(placements, breaks, 0, 0);
        }
        var endless = double.IsPositiveInfinity(usable);
        var pages = 1;
        var shift = 0d;
        var pageTop = 0d;
        var contentEnd = 0d;
        var forced = false;
        var blanks = 0;

        // Passe à la page suivante, pages blanches demandées comprises ; rend le haut du texte de la page où l'on arrive.
        double Turn(double from, bool continues)
        {
            var pageEnd = pageTop + usable;
            var next = Math.Max(from, pageEnd) + between;
            breaks.Add(new PageBreak(Math.Min(contentEnd, pageEnd), pageEnd, next, continues));
            pages++;
            for (; blanks > 0; blanks--)
            {
                var blankEnd = next + usable;
                breaks.Add(new PageBreak(next, blankEnd, blankEnd + between));
                next = blankEnd + between;
                pages++;
            }
            forced = false;
            pageTop = next;
            return next;
        }

        for (var index = 0; index < boxes.Count; index++)
        {
            var box = boxes[index];
            var height = Math.Max(0, box.Height);
            if (box.Kind is PageBoxKind.Break or PageBoxKind.BlankPage)
            {
                // La marque reste où elle est, au bas de la page qu'elle termine. Sur une feuille sans fin, elle ne fait rien.
                contentEnd = Math.Max(contentEnd, box.Top + shift + height);
                forced = !endless;
                blanks += box.Kind == PageBoxKind.BlankPage && !endless ? 1 : 0;
                continue;
            }
            double? shrink = null;
            var top = box.Top + shift;
            if (box.Kind == PageBoxKind.Figure && height > usable + Tolerance)
            {
                // La figure rétrécit : tout ce qui la suit remontera d'autant.
                shrink = usable;
                shift -= height - usable;
                height = usable;
            }
            var spacer = 0d;
            var repeats = false;
            // Ce qu'il faut ajouter au-dessus du bloc pour qu'il commence sur une ligne, là où il est.
            var snap = Missing(box, top - pageTop, step);
            if (forced || (top + snap + height > pageTop + usable + Tolerance && top > pageTop + Tolerance))
            {
                // Après un saut demandé, rien n'est « coupé » : ni en-tête répété, ni mention de suite.
                var cut = !forced && box.Continues;
                repeats = cut && box.Lead > 0;
                var next = Turn(top, cut && !repeats);
                spacer = next - top;
                shift += spacer + (repeats ? box.Lead : 0);
                top = next + (repeats ? box.Lead : 0);
                snap = Missing(box, top - pageTop, step);
            }
            if (snap > 0)
            {
                spacer += snap;
                shift += snap;
                top += snap;
            }
            if (height > usable + Tolerance)
            {
                // Plus haut qu'une page : il se poursuit, et ce qui le suit commencera une nouvelle page.
                pages += (int)Math.Ceiling(height / usable) - 1;
                pageTop = top + height - usable;
            }
            contentEnd = top + height;
            placements[index] = new PagePlacement(spacer, shrink, repeats);
        }
        var remaining = endless ? 0 : Math.Max(0, pageTop + usable - contentEnd);
        if (forced)
        {
            // Un saut en fin de fiche ouvre quand même sa page, vide.
            var end = contentEnd;
            var next = Turn(contentEnd, continues: false);
            remaining = next + usable - end;
        }
        return new PageBreakResult(placements, breaks, pages, remaining);
    }

    /// <summary>Ce qui manque au-dessus d'un bloc pour que son haut tombe où il doit dans le pas ; zéro s'il y est, ou s'il ne s'aligne pas.</summary>
    private static double Missing(PageBox box, double offset, double step)
    {
        if (step <= 0 || box.Snap is not { } phase)
        {
            return 0;
        }
        var missing = (phase - offset) % step;
        if (missing < 0)
        {
            missing += step;
        }
        return missing < Tolerance || step - missing < Tolerance ? 0 : missing;
    }
}
