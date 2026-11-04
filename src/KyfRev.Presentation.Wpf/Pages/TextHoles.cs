using System.Collections.Immutable;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Un trou du texte, en révision : un passage bien ancré, ou du gras.</summary>
/// <param name="Key">Son nom, stable d'un affichage à l'autre.</param>
/// <param name="Start">Son début dans le texte affiché.</param>
/// <param name="End">Sa fin dans le texte affiché.</param>
/// <param name="PassageId">Le passage qu'il cache ; rien pour du gras.</param>
public sealed record TextHole(string Key, int Start, int End, string? PassageId);

/// <summary>
/// Le texte à trous d'une fiche en révision : ses passages et son gras restent masqués jusqu'au clic, pour se les rappeler
/// avant de les relire. Ce qui est affiché le reste quand la fiche se redessine ; chaque révision repart de zéro.
/// </summary>
public sealed partial class TextHoles : ObservableObject
{
    private readonly HashSet<string> _revealed = new(StringComparer.Ordinal);
    private ImmutableArray<TextHole> _all = [];

    /// <summary>Les trous existent ; ils changent ou sont affichés.</summary>
    public event EventHandler? Changed;

    /// <summary>Le texte est à trous.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHoles))]
    public partial bool IsActive { get; set; }

    /// <summary>Tous les trous du texte, dans l'ordre.</summary>
    public IReadOnlyList<TextHole> All => _all;

    /// <summary>Le texte a des trous, à montrer.</summary>
    public bool HasHoles => IsActive && !_all.IsEmpty;

    /// <summary>Les trous encore masqués, dans l'ordre.</summary>
    public IReadOnlyList<TextHole> Hidden => IsActive ? [.. _all.Where(hole => !_revealed.Contains(hole.Key))] : [];

    /// <summary>Le nombre de trous déjà affichés.</summary>
    public int RevealedCount => _all.Count(hole => _revealed.Contains(hole.Key));

    /// <summary>Où l'on en est.</summary>
    public string Summary => ReadingText.HolesSummary(RevealedCount, _all.Length);

    /// <summary>Il reste quelque chose à afficher.</summary>
    public bool CanRevealAll => RevealedCount < _all.Length;

    /// <summary>Quelque chose est déjà affiché.</summary>
    public bool CanHideAll => RevealedCount > 0;

    /// <summary>
    /// Les trous d'un texte. Un passage est un trou, même coupé en morceaux ; le gras qui est dans un passage disparaît avec lui.
    /// Un passage dont la position est incertaine n'en est pas un.
    /// </summary>
    /// <param name="highlights">Les surlignages du texte.</param>
    /// <param name="bold">Les étendues de texte en gras.</param>
    public static ImmutableArray<TextHole> Compute(IReadOnlyList<Highlight> highlights, IReadOnlyList<(int Start, int End)> bold)
    {
        ArgumentNullException.ThrowIfNull(highlights);
        ArgumentNullException.ThrowIfNull(bold);
        var passages = highlights.Where(highlight => !highlight.IsCandidate).ToList();
        var holes = passages.Select(highlight => new TextHole($"passage:{highlight.Id}", highlight.Start, highlight.End, highlight.Id)).ToList();
        var rank = 0;
        foreach (var (start, end) in bold)
        {
            if (!passages.Exists(passage => passage.Start <= start && end <= passage.End))
            {
                holes.Add(new TextHole(string.Create(CultureInfo.InvariantCulture, $"gras:{rank}"), start, end, null));
            }
            rank++;
        }
        return [.. holes.OrderBy(hole => hole.Start)];
    }

    /// <summary>Les passages encore masqués : le volet des passages les tait aussi.</summary>
    public bool Masks(string passageId) =>
        Hidden.Any(hole => hole.PassageId == passageId);

    /// <summary>Change les trous du texte ; ce qui était affiché le reste.</summary>
    internal void Set(ImmutableArray<TextHole> holes)
    {
        _all = holes;
        Notify();
    }

    /// <summary>Affiche un trou.</summary>
    internal void Reveal(string key)
    {
        if (_revealed.Add(key))
        {
            Notify();
        }
    }

    /// <summary>Affiche les trous d'un passage, s'il en a de masqués.</summary>
    internal void RevealPassage(string passageId)
    {
        var added = false;
        foreach (var hole in _all.Where(hole => hole.PassageId == passageId))
        {
            added |= _revealed.Add(hole.Key);
        }
        if (added)
        {
            Notify();
        }
    }

    /// <summary>Affiche le premier trou encore masqué, et le rend.</summary>
    internal TextHole? RevealNext()
    {
        var hidden = Hidden;
        var next = hidden.Count > 0 ? hidden[0] : null;
        if (next is not null)
        {
            Reveal(next.Key);
        }
        return next;
    }

    /// <summary>Tout est de nouveau masqué : une nouvelle révision.</summary>
    internal void Restart()
    {
        _revealed.Clear();
        Notify();
    }

    /// <summary>Affiche tous les trous.</summary>
    [RelayCommand]
    private void RevealAll()
    {
        _revealed.UnionWith(_all.Select(hole => hole.Key));
        Notify();
    }

    /// <summary>Masque de nouveau tous les trous.</summary>
    [RelayCommand]
    private void HideAll() => Restart();

    partial void OnIsActiveChanged(bool value)
    {
        _revealed.Clear();
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(HasHoles));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(CanRevealAll));
        OnPropertyChanged(nameof(CanHideAll));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
