using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>Le confort de lecture d'une fiche, retenu sur ce poste.</summary>
/// <param name="Size">Taille du texte.</param>
/// <param name="LineSpacing">Hauteur d'une ligne, en multiple de la taille du texte.</param>
/// <param name="Family">Police du texte ; rien pour une police de l'application.</param>
/// <param name="Step">
/// Le pas d'écriture du papier montré derrière le texte : chaque ligne de texte en occupe un, et se pose sur sa ligne.
/// Zéro sans papier réglé : le texte garde alors son interligne.
/// </param>
public sealed record ReadingComfort(double Size, double LineSpacing, FontFamily? Family, double Step = 0)
{
    /// <summary>Sa clé dans les ressources, où le lecteur la trouve.</summary>
    public const string ResourceKey = "Kyf.Reading.Comfort";

    /// <summary>La place qu'une ligne de texte laisse au-dessus et au-dessous de ses lettres, en part de sa taille.</summary>
    private const double SlotRoom = 1.1;

    private const double LargeSize = 19;
    private const double ExtraLargeSize = 21;
    private const double AiryLineSpacing = 1.9;

    private static readonly FontFamily Serif = new("Georgia");
    private static readonly FontFamily Handwriting = new("Segoe Print");
    private static readonly FontFamily Marker = new("Ink Free, Segoe Print");

    /// <summary>Le confort par défaut.</summary>
    public static ReadingComfort Default { get; } = new(Typography.BodyLarge.Size, Typography.ReadingLineSpacing, null);

    /// <summary>La police est une de celles de l'application, à trouver dans les ressources sous cette clé ; rien pour celle de l'interface.</summary>
    public string? FamilyKey { get; init; }

    /// <summary>
    /// Les titres prennent la police du texte, dès qu'elle n'est plus celle de l'application : une fiche manuscrite n'a pas
    /// de titres d'imprimerie, et la lecture montre les mêmes titres que l'écriture.
    /// </summary>
    public bool HeadingsFollow { get; init; }

    /// <summary>La même police, dite à la page de l'écriture.</summary>
    public string CssFamily { get; init; } = "var(--police)";

    /// <summary>Le texte se pose sur les lignes d'un papier.</summary>
    public bool IsRuled => Step > 0;

    /// <summary>La hauteur d'une ligne de texte : celle de l'interligne, ou, sur un papier réglé, un nombre entier de pas.</summary>
    public double LineHeight => IsRuled ? Slots(Size, Step) * Step : Size * LineSpacing;

    /// <summary>Le nombre de pas qu'occupe une ligne de texte de cette taille : un seul tant que ses lettres y tiennent.</summary>
    public static int Slots(double size, double step) => step <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((size * SlotRoom / step) - 0.001));

    /// <summary>La hauteur d'une ligne d'un titre de cette taille, sur ce papier.</summary>
    public double HeadingLineHeight(double size) => IsRuled ? Slots(size, Step) * Step : size * 1.3;

    /// <summary>Le confort que disent ces préférences.</summary>
    public static ReadingComfort Of(MachinePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var size = preferences.TextSize switch
        {
            ReadingTextSize.Large => LargeSize,
            ReadingTextSize.ExtraLarge => ExtraLargeSize,
            _ => Typography.BodyLarge.Size,
        };
        var spacing = preferences.LineSpacing == ReadingLineSpacing.Airy ? AiryLineSpacing : Typography.ReadingLineSpacing;
        return preferences.Font switch
        {
            ReadingFont.Serif => new ReadingComfort(size, spacing, Serif) { CssFamily = "Georgia, serif", HeadingsFollow = true },
            ReadingFont.Handwriting => new ReadingComfort(size, spacing, Handwriting) { CssFamily = "\"Segoe Print\", cursive", HeadingsFollow = true },
            ReadingFont.Marker => new ReadingComfort(size, spacing, Marker) { CssFamily = "\"Ink Free\", \"Segoe Print\", cursive", HeadingsFollow = true },
            ReadingFont.Typewriter => new ReadingComfort(size, spacing, null) { FamilyKey = "Kyf.Font.Code", CssFamily = "var(--police-code)", HeadingsFollow = true },
            _ => new ReadingComfort(size, spacing, null),
        };
    }
}
