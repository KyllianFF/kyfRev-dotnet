using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Une partie du guide : son titre dans le plan, et son texte prêt à lire.</summary>
public sealed class GuideSection
{
    private readonly string _body;
    private SheetReading? _reading;

    internal GuideSection(string title, string body)
    {
        Title = title;
        _body = body;
    }

    /// <summary>Son titre, comme dans le plan.</summary>
    public string Title { get; }

    /// <summary>Son texte, mis en forme comme une fiche ; préparé la première fois qu'il est demandé.</summary>
    public SheetReading Reading => _reading ??= SheetReading.Of(
        new OpenedSheet("guide", Title, "guide.md", string.Empty, [], _body, 1, "guide", null, DateTimeOffset.UnixEpoch, null!, []),
        _ => Outcome.Refused<byte[]>(GuideText.NoImage));

    /// <inheritdoc/>
    public override string ToString() => Title;
}

/// <summary>
/// Le guide de kyfRev, lu dans l'application sans connexion : celui du dépôt (<c>docs/guide-utilisateur.md</c>), livré dans
/// le programme, découpé en parties par ses titres de niveau 2.
/// </summary>
public sealed partial class GuideViewModel : ObservableObject
{
    /// <summary>Le nom du guide dans les ressources du programme.</summary>
    internal const string Resource = "kyfrev-guide.md";

    private readonly Lazy<IReadOnlyList<GuideSection>> _sections;

    /// <summary>Le guide livré avec le programme.</summary>
    public GuideViewModel()
        : this(ReadShipped)
    {
    }

    /// <summary>Le guide que rend <paramref name="read"/>, lu la première fois qu'il est montré.</summary>
    internal GuideViewModel(Func<string> read)
    {
        _sections = new Lazy<IReadOnlyList<GuideSection>>(() => Split(read()));
    }

    /// <summary>Les parties du guide, dans l'ordre : son plan.</summary>
    public IReadOnlyList<GuideSection> Sections => _sections.Value;

    /// <summary>La partie montrée ; la première tant qu'aucune n'est choisie.</summary>
    public GuideSection? Selected
    {
        get => field ?? (Sections.Count > 0 ? Sections[0] : null);
        set => SetProperty(ref field, value);
    }

    /// <summary>Montre la partie de ce titre, si le guide en a une ; sinon, rien ne change.</summary>
    public void Show(string title)
    {
        if (Sections.FirstOrDefault(section => string.Equals(section.Title, title, StringComparison.OrdinalIgnoreCase)) is { } section)
        {
            Selected = section;
        }
    }

    /// <summary>
    /// Découpe le guide : ce qui précède le premier titre de niveau 2 en est la présentation, chaque titre de niveau 2
    /// ouvre une partie. Un lien vers un autre document du dépôt, qui ne mène nulle part ici, garde son texte seul.
    /// </summary>
    internal static IReadOnlyList<GuideSection> Split(string guide)
    {
        ArgumentNullException.ThrowIfNull(guide);
        var sections = new List<GuideSection>();
        var title = GuideText.Introduction;
        var body = new StringBuilder();
        void Close()
        {
            if (body.ToString().Trim() is { Length: > 0 } text)
            {
                sections.Add(new GuideSection(title, $"# {title}\n\n{text}\n"));
            }
            body.Clear();
        }
        foreach (var line in guide.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Close();
                title = line[3..].Trim();
            }
            else if (!line.StartsWith("# ", StringComparison.Ordinal))
            {
                body.Append(LocalLink().Replace(line, "$1")).Append('\n');
            }
        }
        Close();
        return sections;
    }

    private static string ReadShipped()
    {
        using var stream = typeof(GuideViewModel).Assembly.GetManifestResourceStream(Resource);
        if (stream is null)
        {
            return GuideText.Missing;
        }
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Un lien vers un fichier du dépôt (« [texte](chiffrement.md#…) ») : pas une adresse, pas une ancre de ce guide.</summary>
    [GeneratedRegex(@"\[([^\]]+)\]\((?!https?:|#)[^)]*\)")]
    private static partial Regex LocalLink();
}

/// <summary>Les textes de l'aide et de son guide.</summary>
public static class GuideText
{
    /// <summary>L'onglet des raccourcis.</summary>
    public const string ShortcutsTab = "Raccourcis";

    /// <summary>L'onglet du guide.</summary>
    public const string GuideTab = "Guide";

    /// <summary>Le nom des deux onglets, pour le lecteur d'écran.</summary>
    public const string Tabs = "Aide";

    /// <summary>Le nom du plan, pour le lecteur d'écran.</summary>
    public const string Plan = "Plan du guide";

    /// <summary>Le nom du texte, pour le lecteur d'écran.</summary>
    public const string Text = "Texte du guide";

    /// <summary>Le titre de ce qui précède la première partie.</summary>
    public const string Introduction = "Présentation";

    /// <summary>Sous le guide : d'où il vient.</summary>
    public const string Shipped = "Ce guide est livré avec kyfRev : il se lit sans connexion.";

    /// <summary>Le guide n'a pas d'image.</summary>
    public const string NoImage = "Le guide n'a pas d'image.";

    /// <summary>Le programme n'a pas son guide : cela ne devrait pas arriver, et se dit plutôt que de ne rien montrer.</summary>
    public const string Missing = "## Guide\n\nLe guide n'a pas été trouvé dans cette installation de kyfRev.\n";
}
