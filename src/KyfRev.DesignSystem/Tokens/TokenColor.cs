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

    /// <summary>La couleur WPF.</summary>
    public Color ToColor() => Color.FromArgb(A, R, G, B);

    /// <summary>La couleur de System.Drawing, pour les contrôles qui ne connaissent que celle-là.</summary>
    public System.Drawing.Color ToDrawingColor() => System.Drawing.Color.FromArgb(A, R, G, B);

    /// <summary><c>#rrggbb</c>, ou <c>#aarrggbb</c> si la couleur est transparente.</summary>
    public override string ToString() =>
        IsOpaque
            ? string.Create(CultureInfo.InvariantCulture, $"#{R:x2}{G:x2}{B:x2}")
            : string.Create(CultureInfo.InvariantCulture, $"#{A:x2}{R:x2}{G:x2}{B:x2}");

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
