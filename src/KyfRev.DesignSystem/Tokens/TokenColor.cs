using System.Globalization;
using System.Windows.Media;

namespace KyfRev.DesignSystem.Tokens;

/// <summary>Couleur d'un jeton, avec son opacité ; les contrastes se calculent selon WCAG 2.1.</summary>
/// <param name="A">Opacité (255 : opaque).</param>
/// <param name="R">Rouge.</param>
/// <param name="G">Vert.</param>
/// <param name="B">Bleu.</param>
public readonly record struct TokenColor(byte A, byte R, byte G, byte B)
{
    /// <summary>Clarté (OKLCH) la plus basse d'un texte coloré sur un fond sombre.</summary>
    public const double ReadableLightnessOnDark = 0.78;

    /// <summary>Clarté (OKLCH) la plus haute d'un texte coloré sur un fond clair.</summary>
    public const double ReadableLightnessOnLight = 0.45;

    /// <summary>Opaque : sans transparence.</summary>
    public bool IsOpaque => A == byte.MaxValue;

    /// <summary>Luminance relative (WCAG 2.1, sRGB), de 0 (noir) à 1 (blanc).</summary>
    public double RelativeLuminance => (0.2126 * Linear(R)) + (0.7152 * Linear(G)) + (0.0722 * Linear(B));

    /// <summary>Couleur écrite <c>#rrggbb</c>, ou <c>#aarrggbb</c> avec son opacité.</summary>
    /// <exception cref="FormatException">L'écriture n'est pas l'une des deux formes.</exception>
    public static TokenColor FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        if (!hex.StartsWith('#') || hex.Length is not (7 or 9) || !uint.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"Couleur « {hex} » : #rrggbb ou #aarrggbb attendu.");
        }
        var alpha = hex.Length == 9 ? (byte)(value >> 24) : byte.MaxValue;
        return new TokenColor(alpha, (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    /// <summary>Rapport de contraste avec <paramref name="other"/>, de 1 à 21.</summary>
    /// <exception cref="InvalidOperationException">L'une des deux couleurs est transparente : son contraste dépend du fond.</exception>
    public double ContrastWith(TokenColor other)
    {
        if (!IsOpaque || !other.IsOpaque)
        {
            throw new InvalidOperationException("Le contraste ne se calcule qu'entre couleurs opaques.");
        }
        var (light, dark) = RelativeLuminance >= other.RelativeLuminance ? (this, other) : (other, this);
        return (light.RelativeLuminance + 0.05) / (dark.RelativeLuminance + 0.05);
    }

    /// <summary>
    /// Cette couleur, lisible sur <paramref name="ground"/> : sa teinte et sa saturation restent, sa clarté (OKLCH) s'arrête
    /// où le contraste est d'au moins 4,5:1. L'éditeur en fait autant par CSS, avec les mêmes bornes.
    /// </summary>
    /// <param name="ground">Le fond, opaque.</param>
    public TokenColor ReadableOn(TokenColor ground)
    {
        var (l, a, b) = ToOklab();
        var dark = ground.RelativeLuminance < 0.18;
        var lightness = dark ? Math.Max(l, ReadableLightnessOnDark) : Math.Min(l, ReadableLightnessOnLight);
        var result = FromOklab(lightness, a, b);
        while (result.ContrastWith(ground) < 4.5 && lightness is > 0 and < 1)
        {
            lightness = Math.Clamp(lightness + (dark ? 0.02 : -0.02), 0, 1);
            result = FromOklab(lightness, a, b);
        }
        return result;
    }

    /// <summary>La couleur WPF.</summary>
    public Color ToColor() => Color.FromArgb(A, R, G, B);

    /// <summary>La couleur de System.Drawing, pour les contrôles qui ne connaissent que celle-là.</summary>
    public System.Drawing.Color ToDrawingColor() => System.Drawing.Color.FromArgb(A, R, G, B);

    /// <summary><c>#rrggbb</c>, ou <c>#aarrggbb</c> si la couleur est transparente.</summary>
    public override string ToString() =>
        IsOpaque
            ? string.Create(CultureInfo.InvariantCulture, $"#{R:x2}{G:x2}{B:x2}")
            : string.Create(CultureInfo.InvariantCulture, $"#{A:x2}{R:x2}{G:x2}{B:x2}");

    private static TokenColor FromOklab(double l, double a, double b)
    {
        var x = Math.Pow(l + (0.3963377774 * a) + (0.2158037573 * b), 3);
        var y = Math.Pow(l - (0.1055613458 * a) - (0.0638541728 * b), 3);
        var z = Math.Pow(l - (0.0894841775 * a) - (1.2914855480 * b), 3);
        return new TokenColor(
            byte.MaxValue,
            Gamma((4.0767416621 * x) - (3.3077115913 * y) + (0.2309699292 * z)),
            Gamma((-1.2684380046 * x) + (2.6097574011 * y) - (0.3413193965 * z)),
            Gamma((-0.0041960863 * x) - (0.7034186147 * y) + (1.7076147010 * z)));
    }

    private static byte Gamma(double linear)
    {
        var clipped = Math.Clamp(linear, 0, 1);
        var encoded = clipped <= 0.0031308 ? 12.92 * clipped : (1.055 * Math.Pow(clipped, 1 / 2.4)) - 0.055;
        return (byte)Math.Round(encoded * byte.MaxValue);
    }

    private (double L, double A, double B) ToOklab()
    {
        var (r, g, b) = (Linear(R), Linear(G), Linear(B));
        var x = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        var y = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        var z = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));
        return (
            (0.2104542553 * x) + (0.7936177850 * y) - (0.0040720468 * z),
            (1.9779984951 * x) - (2.4285922050 * y) + (0.4505937099 * z),
            (0.0259040371 * x) + (0.7827717662 * y) - (0.8086757660 * z));
    }

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
