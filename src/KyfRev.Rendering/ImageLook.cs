using System.Collections.Immutable;
using System.Globalization;

namespace KyfRev.Rendering;

/// <summary>Où une image se place dans la largeur du texte.</summary>
public enum ImageAlign
{
    /// <summary>Dans le fil du texte, comme l'écrit Markdown.</summary>
    Inline,

    /// <summary>Seule sur sa ligne, à gauche.</summary>
    Left,

    /// <summary>Seule sur sa ligne, au centre.</summary>
    Center,

    /// <summary>Seule sur sa ligne, à droite.</summary>
    Right,
}

/// <summary>
/// L'apparence d'une image dans la fiche : sa largeur, sa place, son cadre et ses masques. Tout est écrit dans son titre
/// Markdown, que les autres outils gardent et montrent en infobulle : l'image, son fichier et le texte de la fiche ne
/// changent pas.
/// </summary>
/// <param name="Width">Sa largeur, en pourcentage de la largeur du texte ; rien : sa taille d'origine, sans dépasser le texte.</param>
/// <param name="Align">Sa place.</param>
/// <param name="Crop">
/// Son cadre : la part de l'image qui se montre, en parts de l'image entière ; rien : l'image entière. Le fichier n'est
/// pas rogné : retirer le cadre rend l'image d'origine.
/// </param>
/// <param name="Masks">Ses zones cachées en révision, en parts de ce qui se montre.</param>
public sealed record ImageLook(int? Width, ImageAlign Align, ImageMask? Crop, ImmutableArray<ImageMask> Masks)
{
    /// <summary>Une image ordinaire : taille d'origine, dans le fil du texte, entière, sans masque.</summary>
    public static ImageLook Plain { get; } = new(null, ImageAlign.Inline, null, []);

    /// <summary>Rien n'est réglé : l'image n'a pas besoin de titre.</summary>
    public bool IsPlain => Width is null && Align == ImageAlign.Inline && Crop is null && Masks.IsDefaultOrEmpty;

    /// <inheritdoc/>
    public bool Equals(ImageLook? other) =>
        other is not null && Width == other.Width && Align == other.Align && Crop == other.Crop
        && (Masks.IsDefault ? [] : Masks).SequenceEqual(other.Masks.IsDefault ? [] : other.Masks);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Width, Align, Crop, Masks.IsDefault ? 0 : Masks.Length);
}

/// <summary>
/// L'apparence d'une image, lue et écrite dans son titre Markdown :
/// <c>![Cellule](cellule.png "kyfrev-largeur:60 kyfrev-position:centre kyfrev-cadre:10,5,80,90 kyfrev-masques:10,20,30,15")</c>.
/// Des mots séparés par une espace, chacun de la forme <c>kyfrev-nom:valeur</c> ; seuls ceux qui sont réglés sont écrits,
/// toujours dans cet ordre. Un titre qui contient autre chose est une légende écrite par l'utilisateur : il n'est pas à
/// nous, et l'image reste ordinaire.
/// </summary>
public static class ImageLooks
{
    /// <summary>Ce qui ouvre chacun de nos mots.</summary>
    public const string Marker = "kyfrev-";

    /// <summary>La plus petite largeur, en pourcentage du texte : en dessous, l'image ne se lit plus.</summary>
    public const int MinimumWidth = 5;

    /// <summary>La plus grande : toute la largeur du texte.</summary>
    public const int MaximumWidth = 100;

    private const string WidthWord = "kyfrev-largeur:";
    private const string AlignWord = "kyfrev-position:";
    private const string CropWord = "kyfrev-cadre:";

    /// <summary>
    /// L'apparence écrite dans un titre. Sans titre, l'image est ordinaire ; un titre qui n'est pas à nous (une légende) ne
    /// rend rien : on ne peut ni le lire ni le remplacer. Un mot à nous mais mal écrit est laissé de côté sans faire
    /// perdre les autres.
    /// </summary>
    public static ImageLook? FromTitle(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return ImageLook.Plain;
        }
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || !words.All(IsOurWord))
        {
            return null;
        }
        var look = ImageLook.Plain;
        foreach (var word in words)
        {
            if (word.StartsWith(WidthWord, StringComparison.Ordinal))
            {
                if (int.TryParse(word.AsSpan(WidthWord.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var width))
                {
                    look = look with { Width = Math.Clamp(width, MinimumWidth, MaximumWidth) };
                }
            }
            else if (word.StartsWith(AlignWord, StringComparison.Ordinal))
            {
                look = look with
                {
                    Align = word[AlignWord.Length..] switch
                    {
                        "gauche" => ImageAlign.Left,
                        "centre" => ImageAlign.Center,
                        "droite" => ImageAlign.Right,
                        _ => look.Align,
                    },
                };
            }
            else if (word.StartsWith(CropWord, StringComparison.Ordinal))
            {
                // Un cadre s'écrit comme un masque seul ; celui qui couvre toute l'image n'en est pas un.
                var crop = ImageMasks.Parse(word[CropWord.Length..]);
                look = look with { Crop = crop.Length == 1 && !IsWhole(crop[0]) ? crop[0] : look.Crop };
            }
            else
            {
                look = look with { Masks = ImageMasks.Parse(word[ImageMasks.Prefix.Length..]) };
            }
        }
        return look;
    }

    /// <summary>Le titre qui écrit cette apparence ; rien pour une image ordinaire : elle n'a alors pas de titre.</summary>
    public static string? ToTitle(ImageLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        var words = new List<string>(4);
        if (look.Width is { } width)
        {
            words.Add(string.Create(CultureInfo.InvariantCulture, $"{WidthWord}{Math.Clamp(width, MinimumWidth, MaximumWidth)}"));
        }
        if (look.Align != ImageAlign.Inline)
        {
            words.Add(AlignWord + look.Align switch { ImageAlign.Left => "gauche", ImageAlign.Center => "centre", _ => "droite" });
        }
        if (look.Crop is { } wanted && ImageMasks.Normalize(wanted) is { } crop && !IsWhole(crop))
        {
            words.Add(CropWord + ImageMasks.Written([crop]));
        }
        if (!look.Masks.IsDefaultOrEmpty && ImageMasks.Written(look.Masks) is { Length: > 0 } masks)
        {
            words.Add(ImageMasks.Prefix + masks);
        }
        return words.Count == 0 ? null : string.Join(' ', words);
    }

    /// <summary>Le titre d'une image dont on change un réglage, les autres gardés ; rien si ce titre n'est pas à nous.</summary>
    public static (bool Ours, string? Title) Change(string? title, Func<ImageLook, ImageLook> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return FromTitle(title) is { } look ? (true, ToTitle(change(look))) : (false, title);
    }

    /// <summary>Ce titre peut porter nos réglages : il est vide, ou il n'a que des mots à nous.</summary>
    public static bool IsOurs(string? title) => FromTitle(title) is not null;

    private static bool IsOurWord(string word) =>
        word.StartsWith(WidthWord, StringComparison.Ordinal) || word.StartsWith(AlignWord, StringComparison.Ordinal)
        || word.StartsWith(CropWord, StringComparison.Ordinal) || word.StartsWith(ImageMasks.Prefix, StringComparison.Ordinal);

    private static bool IsWhole(ImageMask crop) => crop is { X: 0, Y: 0, Width: 1, Height: 1 };
}
