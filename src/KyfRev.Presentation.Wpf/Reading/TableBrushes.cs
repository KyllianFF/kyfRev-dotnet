using System.Windows;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>Les pinceaux d'un thème de tableau, par leur nom de ressource : ils suivent le thème de l'application.</summary>
/// <param name="Header">Fond de l'en-tête.</param>
/// <param name="HeaderText">Texte de l'en-tête.</param>
/// <param name="Band">Fond d'une bande.</param>
/// <param name="Line">Traits.</param>
/// <param name="Total">Trait au-dessus de la dernière ligne, quand elle ressort.</param>
internal sealed record TableBrushes(string Header, string HeaderText, string Band, string Line, string Total)
{
    private static readonly TableBrushes Sober = new("Kyf.Brush.SurfaceAlt", "Kyf.Brush.Text", "Kyf.Brush.TableSoberBand", "Kyf.Brush.Divider", "Kyf.Brush.TextSecondary");

    /// <summary>Les pinceaux d'un thème.</summary>
    public static TableBrushes Of(TableTheme theme)
    {
        if (theme == TableTheme.Sober)
        {
            return Sober;
        }
        var name = $"Kyf.Brush.Table{theme}";
        return new($"{name}Header", $"{name}HeaderText", $"{name}Band", $"{name}Line", $"{name}Header");
    }
}

/// <summary>La place d'une case dans son tableau, et ce qu'elle en tire.</summary>
/// <param name="Look">L'apparence du tableau.</param>
/// <param name="Paints">Ses pinceaux.</param>
/// <param name="Row">Sa ligne, à partir de 0.</param>
/// <param name="Column">Sa colonne, à partir de 0.</param>
/// <param name="Rows">Le nombre de lignes.</param>
/// <param name="Columns">Le nombre de colonnes.</param>
internal sealed record TablePlace(TableLook Look, TableBrushes Paints, int Row, int Column, int Rows, int Columns)
{
    /// <summary>Une case de la ligne ou de la colonne d'en-tête.</summary>
    public bool IsHeader => (Look.HeaderRow && Row == 0) || (Look.HeaderColumn && Column == 0);

    /// <summary>Une case de la dernière ligne, quand elle ressort ; jamais l'en-tête.</summary>
    public bool IsTotal => Look.TotalRow && Row == Rows - 1 && !(Look.HeaderRow && Row == 0);

    /// <summary>Une case d'une bande : une ligne ou une colonne sur deux, en comptant après l'en-tête.</summary>
    public bool IsBand => Look.Bands switch
    {
        TableBands.Rows => !IsTotal && (Row - (Look.HeaderRow ? 1 : 0)) % 2 == 1,
        TableBands.Columns => (Column - (Look.HeaderColumn ? 1 : 0)) % 2 == 1,
        _ => false,
    };

    /// <summary>Une case de la colonne cachée en révision ; l'en-tête reste lisible, il dit ce qui est à retrouver.</summary>
    public bool IsHiddenInReview => Look.HiddenInReview == Column + 1 && !(Look.HeaderRow && Row == 0);

    /// <summary>
    /// Ses traits. Chaque case trace sa droite et son bas, et celles du bord le contour : le tableau lui-même n'en trace
    /// aucun, pour que la colonne vide d'un tableau ajusté reste vide. La dernière ligne qui ressort a un trait épais au-dessus.
    /// </summary>
    public Thickness Border
    {
        get
        {
            var (first, top, last, bottom) = (Column == 0, Row == 0, Column == Columns - 1, Row == Rows - 1);
            var edges = Look.Lines switch
            {
                TableLines.All => new Thickness(first ? 1 : 0, top ? 1 : 0, 1, 1),
                TableLines.Horizontal => new Thickness(0, top ? 1 : 0, 0, 1),
                TableLines.Vertical => new Thickness(first ? 1 : 0, 0, 1, 0),
                TableLines.Outline => new Thickness(first ? 1 : 0, top ? 1 : 0, last ? 1 : 0, bottom ? 1 : 0),
                TableLines.Header => new Thickness(0, 0, Look.HeaderColumn && first ? 2 : 0, Look.HeaderRow && top ? 2 : 0),
                _ => default,
            };
            return IsTotal ? edges with { Top = 2 } : edges;
        }
    }
}
