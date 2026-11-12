using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace KyfRev.Rendering;

/// <summary>
/// Un masque posé sur une image : une zone cachée en révision, à se rappeler avant de l'afficher. Ses mesures sont des
/// parts de l'image, de 0 à 1 : il garde sa place quelle que soit la taille à laquelle elle se montre.
/// </summary>
/// <param name="X">Bord gauche.</param>
/// <param name="Y">Bord haut.</param>
/// <param name="Width">Largeur.</param>
/// <param name="Height">Hauteur.</param>
public readonly record struct ImageMask(double X, double Y, double Width, double Height);

/// <summary>
/// Les masques d'une image, écrits dans son titre Markdown :
/// <c>![Cellule](cellule.png "kyfrev-masques:10,20,30,15;50,50,20,10")</c>, en pourcentages de l'image (gauche, haut,
/// largeur, hauteur). Un titre est du Markdown ordinaire : un autre outil l'affiche en infobulle et le garde, l'éditeur
/// visuel de kyfRev aussi. Une image sans ce titre n'a pas de masque. Le titre peut porter d'autres réglages de l'image
/// (voir <see cref="ImageLooks"/>), que les masques ne font pas perdre.
/// </summary>
public static class ImageMasks
{
    /// <summary>Ce qui ouvre, dans le titre, la liste des masques.</summary>
    public const string Prefix = "kyfrev-masques:";

    /// <summary>Au plus autant de masques sur une image : au-delà, ce n'est plus une image à se rappeler.</summary>
    public const int Maximum = 60;

    /// <summary>Le plus petit côté d'un masque, en part de l'image : en dessous, il ne se verrait ni ne se cliquerait.</summary>
    public const double MinimumSide = 0.01;

    /// <summary>
    /// Les masques écrits dans un titre ; aucun si le titre n'en parle pas. Un masque mal écrit, vide ou hors de l'image
    /// est laissé de côté plutôt que de faire perdre les autres.
    /// </summary>
    public static ImmutableArray<ImageMask> FromTitle(string? title) => ImageLooks.FromTitle(title)?.Masks ?? [];

    /// <summary>Les zones d'une liste « 10,20,30,15;50,50,20,10 », en pourcentages ; les mal écrites sont laissées de côté.</summary>
    internal static ImmutableArray<ImageMask> Parse(string list)
    {
        var masks = ImmutableArray.CreateBuilder<ImageMask>();
        foreach (var part in list.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var numbers = part.Split(',');
            if (numbers.Length != 4 || masks.Count >= Maximum)
            {
                continue;
            }
            var values = new double[4];
            var read = true;
            for (var i = 0; i < 4 && read; i++)
            {
                read = double.TryParse(numbers[i], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out values[i]) && double.IsFinite(values[i]);
            }
            if (read && Normalize(new ImageMask(values[0] / 100, values[1] / 100, values[2] / 100, values[3] / 100)) is { } mask)
            {
                masks.Add(mask);
            }
        }
        return masks.ToImmutable();
    }

    /// <summary>Le titre qui écrit ces masques, au dixième de pourcent ; rien s'il n'y en a pas : l'image n'a alors pas de titre.</summary>
    public static string? ToTitle(IEnumerable<ImageMask> masks)
    {
        ArgumentNullException.ThrowIfNull(masks);
        return Written(masks) is { Length: > 0 } list ? Prefix + list : null;
    }

    /// <summary>La liste qui écrit ces zones, au dixième de pourcent ; vide s'il n'en reste aucune de visible.</summary>
    internal static string Written(IEnumerable<ImageMask> masks)
    {
        var text = new StringBuilder();
        var count = 0;
        foreach (var raw in masks)
        {
            if (count >= Maximum || Normalize(raw) is not { } mask)
            {
                continue;
            }
            if (count++ > 0)
            {
                text.Append(';');
            }
            text.Append(Percent(mask.X)).Append(',').Append(Percent(mask.Y)).Append(',').Append(Percent(mask.Width)).Append(',').Append(Percent(mask.Height));
        }
        return text.ToString();
    }

    /// <summary>
    /// Ramène un masque dans l'image : un rectangle tracé de droite à gauche est remis à l'endroit, ce qui dépasse est
    /// coupé. Rien s'il n'en reste pas de quoi le voir.
    /// </summary>
    public static ImageMask? Normalize(ImageMask mask)
    {
        var left = Math.Clamp(Math.Min(mask.X, mask.X + mask.Width), 0, 1);
        var right = Math.Clamp(Math.Max(mask.X, mask.X + mask.Width), 0, 1);
        var top = Math.Clamp(Math.Min(mask.Y, mask.Y + mask.Height), 0, 1);
        var bottom = Math.Clamp(Math.Max(mask.Y, mask.Y + mask.Height), 0, 1);
        // Au dix-millième de l'image : deux lectures du même masque donnent les mêmes nombres.
        static double Rounded(double part) => Math.Round(part, 4, MidpointRounding.AwayFromZero);
        return right - left >= MinimumSide && bottom - top >= MinimumSide
            ? new ImageMask(Rounded(left), Rounded(top), Rounded(right - left), Rounded(bottom - top))
            : null;
    }

    private static string Percent(double part) =>
        Math.Round(part * 100, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture);
}
