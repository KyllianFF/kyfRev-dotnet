using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>Le confort de lecture d'une fiche, retenu sur ce poste.</summary>
/// <param name="Size">Taille du texte.</param>
/// <param name="LineSpacing">Hauteur d'une ligne, en multiple de la taille du texte.</param>
/// <param name="Family">Police du texte ; rien pour celle de l'application.</param>
public sealed record ReadingComfort(double Size, double LineSpacing, FontFamily? Family)
{
    /// <summary>Sa clé dans les ressources, où le lecteur la trouve.</summary>
    public const string ResourceKey = "Kyf.Reading.Comfort";

    private const double LargeSize = 19;
    private const double ExtraLargeSize = 21;
    private const double AiryLineSpacing = 1.9;

    private static readonly FontFamily Serif = new("Georgia");

    /// <summary>Le confort par défaut.</summary>
    public static ReadingComfort Default { get; } = new(Typography.BodyLarge.Size, Typography.ReadingLineSpacing, null);

    /// <summary>Le confort que disent ces préférences.</summary>
    public static ReadingComfort Of(MachinePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return new ReadingComfort(
            preferences.TextSize switch
            {
                ReadingTextSize.Large => LargeSize,
                ReadingTextSize.ExtraLarge => ExtraLargeSize,
                _ => Typography.BodyLarge.Size,
            },
            preferences.LineSpacing == ReadingLineSpacing.Airy ? AiryLineSpacing : Typography.ReadingLineSpacing,
            preferences.Font == ReadingFont.Serif ? Serif : null);
    }
}
