using System.Collections.Immutable;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Texte à trous d'un passage, comme dans la 0.x (<c>rappel.ts</c>) : le texte enregistré autour
/// de lui, le passage remplacé par un trou dont on donne le nombre de mots. Un contexte coupé à sa
/// longueur maximale perd son premier (ou dernier) mot, peut-être entamé, et se signale par « … ».
/// </summary>
/// <param name="Before">Texte juste avant le passage, sans mot coupé.</param>
/// <param name="After">Texte juste après le passage, sans mot coupé.</param>
/// <param name="MoreBefore">Le texte continue avant.</param>
/// <param name="MoreAfter">Le texte continue après.</param>
/// <param name="Words">Mots du passage, ponctuation isolée exclue.</param>
public sealed partial record Cloze(string Before, string After, bool MoreBefore, bool MoreAfter, int Words)
{
    /// <summary>Longueur du contexte enregistré de chaque côté d'un passage (en unités UTF-16).</summary>
    public const int Context = 32;

    /// <summary>« … texte avant » : ce qui précède le trou.</summary>
    public string Leading => MoreBefore ? $"… {Before}" : Before;

    /// <summary>« texte après … » : ce qui suit le trou.</summary>
    public string Trailing => MoreAfter ? $"{After} …" : After;

    /// <summary>Le texte à trous du passage ancré par <paramref name="anchor"/>.</summary>
    public static Cloze Of(Anchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        var moreBefore = anchor.Before.Length >= Context;
        var moreAfter = anchor.After.Length >= Context;
        var before = Spaces().Replace(moreBefore ? FirstWord().Replace(anchor.Before, string.Empty) : anchor.Before, " ").TrimStart();
        var after = Spaces().Replace(moreAfter ? LastWord().Replace(anchor.After, string.Empty) : anchor.After, " ").TrimEnd();
        // Une ponctuation isolée (« : », « — ») ne compte pas pour un mot.
        var words = Spaces().Split(anchor.Exact).Count(word => LetterOrNumber().IsMatch(word));
        return new Cloze(before, after, moreBefore, moreAfter, words);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^\S*\s+")]
    private static partial Regex FirstWord();

    [GeneratedRegex(@"\s+\S*\z")]
    private static partial Regex LastWord();

    [GeneratedRegex(@"[\p{L}\p{N}]")]
    private static partial Regex LetterOrNumber();
}

/// <summary>
/// Un passage ou une définition à réviser, comme dans la 0.x (<c>Passage.svelte</c>) : une carte
/// mémoire. L'explication d'une définition reste cachée jusqu'à ce qu'on la demande ; avec le
/// rappel actif, le passage aussi : son commentaire et le texte autour d'abord, le passage
/// remplacé par un trou. La barre de révision suit.
/// </summary>
public sealed partial class PassagePageViewModel : ObservableObject, IReviewPage
{
    private readonly SheetUseCases _sheets;
    private readonly Func<ItemKind, string, ReviewState, ReviewBarViewModel> _reviewBar;
    private readonly bool _activeRecall;
    private readonly Action _back;
    private readonly Action<string> _openSheet;

    /// <summary>Le passage <paramref name="passageId"/> de la fiche <paramref name="sheetId"/>.</summary>
    public PassagePageViewModel(SheetUseCases sheets, Func<ItemKind, string, ReviewState, ReviewBarViewModel> reviewBar, string passageId, string sheetId, bool activeRecall, Action back, Action<string> openSheet)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(reviewBar);
        ArgumentNullException.ThrowIfNull(passageId);
        ArgumentNullException.ThrowIfNull(sheetId);
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(openSheet);
        _sheets = sheets;
        _reviewBar = reviewBar;
        PassageId = passageId;
        SheetId = sheetId;
        _activeRecall = activeRecall;
        _back = back;
        _openSheet = openSheet;
        SheetTitle = string.Empty;
        IsLoading = true;
    }

    /// <summary>L'annotation.</summary>
    public string PassageId { get; }

    /// <summary>Sa fiche.</summary>
    public string SheetId { get; }

    /// <summary>Lecture en cours.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Le passage n'a pas pu être ouvert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Un refus est affiché.</summary>
    public bool HasError => Error is not null;

    /// <summary>Titre de la fiche.</summary>
    [ObservableProperty]
    public partial string SheetTitle { get; private set; }

    /// <summary>Le passage, une fois lu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoaded), nameof(IsDefinition), nameof(IsRecall), nameof(IsPlain), nameof(TypeLabel), nameof(Source), nameof(Term),
        nameof(Explanation), nameof(Quote), nameof(Comment), nameof(HasComment), nameof(ShowsCommentBelow), nameof(Cloze), nameof(HiddenPassage), nameof(AnchorWarning),
        nameof(HasAnchorWarning), nameof(ClozeMiddle), nameof(ClozeSpoken))]
    public partial AnnotationView? Annotation { get; private set; }

    /// <summary>La barre de révision du passage.</summary>
    [ObservableProperty]
    public partial ReviewBarViewModel? Review { get; private set; }

    /// <summary>L'explication, ou le passage en rappel actif, a été demandé.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHidden), nameof(ClozeMiddle), nameof(ClozeSpoken))]
    public partial bool IsRevealed { get; private set; }

    /// <summary>Le passage est lu.</summary>
    public bool IsLoaded => Annotation is not null;

    /// <summary>Une définition.</summary>
    public bool IsDefinition => Annotation?.Definition is not null;

    /// <summary>Passage en rappel actif : le texte autour d'abord, le passage remplacé par un trou.</summary>
    public bool IsRecall => _activeRecall && Annotation is not null && !IsDefinition;

    /// <summary>Passage montré tel quel.</summary>
    public bool IsPlain => Annotation is not null && !IsDefinition && !IsRecall;

    /// <summary>L'explication ou le passage reste à demander.</summary>
    public bool IsHidden => !IsRevealed;

    /// <summary>« Définition · J3 », « Passage · archivé ».</summary>
    public string TypeLabel => Annotation is null ? string.Empty
        : IsDefinition ? $"{ReviewText.Definition} · {Annotation.Review.Step ?? ReviewText.DefinitionArchivedStep}"
        : $"{ReviewText.Passage} · {Annotation.Review.Step ?? ReviewText.PassageArchivedStep}";

    /// <summary>« Fiche › Section › Sous-section ».</summary>
    public string Source => Annotation is null ? string.Empty : string.Join(" › ", [SheetTitle, .. Annotation.Anchor.Section]);

    /// <summary>Terme d'une définition.</summary>
    public string? Term => Annotation?.Definition?.Term;

    /// <summary>Explication d'une définition.</summary>
    public string? Explanation => Annotation?.Definition?.Explanation;

    /// <summary>« « Le passage surligné » ».</summary>
    public string Quote => Annotation is null ? string.Empty : $"« {Annotation.Anchor.Exact} »";

    /// <summary>Mon commentaire.</summary>
    public string? Comment => Annotation?.Comment;

    /// <summary>Un commentaire est écrit.</summary>
    public bool HasComment => Comment is not null;

    /// <summary>Hors rappel actif, le commentaire suit la carte (en rappel actif, il la précède comme indice).</summary>
    public bool ShowsCommentBelow => HasComment && !IsRecall;

    /// <summary>Texte à trous, en rappel actif.</summary>
    public Cloze? Cloze => IsRecall ? Cloze.Of(Annotation!.Anchor) : null;

    /// <summary>« passage caché : 3 mots », pour le lecteur d'écran.</summary>
    public string HiddenPassage => Cloze is { } cloze
        ? ReviewText.HiddenPassageOf(Dates.Plural(cloze.Words, ReviewText.HiddenWordsOneOf, ReviewText.HiddenWordsManyOf))
        : string.Empty;

    /// <summary>À la place du passage : le nombre de ses mots tant qu'il est caché, puis le passage.</summary>
    public string ClozeMiddle => Cloze is not { } cloze ? string.Empty
        : IsRevealed ? Annotation!.Anchor.Exact
        : Dates.Plural(cloze.Words, ReviewText.HiddenWordsOneOf, ReviewText.HiddenWordsManyOf);

    /// <summary>Le texte à trous tel que le lit le lecteur d'écran (« … avant passage caché : 3 mots après … »).</summary>
    public string ClozeSpoken => Cloze is not { } cloze ? string.Empty
        : string.Join(" ", new[] { cloze.Leading, IsRevealed ? Annotation!.Anchor.Exact : HiddenPassage, cloze.Trailing }.Select(part => part.Trim()).Where(part => part.Length > 0));

    /// <summary>Position du passage à vérifier ou introuvable ; rien s'il est ancré.</summary>
    public string? AnchorWarning => Annotation?.AnchorState switch
    {
        AnchorState.NeedsReview => ReviewText.AnchorNeedsReview,
        AnchorState.NotFound => ReviewText.AnchorNotFound,
        _ => null,
    };

    /// <summary>Un avertissement d'ancrage est affiché.</summary>
    public bool HasAnchorWarning => AnchorWarning is not null;

    /// <summary>Lit la fiche et y trouve le passage.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var outcome = await Task.Run(() => _sheets.Open(SheetId)).ConfigureAwait(true);
            if (!outcome.Succeeded)
            {
                Error = outcome.Error;
                return;
            }
            SheetTitle = outcome.Value.Title;
            var annotation = outcome.Value.Annotations.FirstOrDefault(candidate => candidate.Id == PassageId);
            if (annotation is null)
            {
                Error = ReviewText.PassageNotFound;
                return;
            }
            Annotation = annotation;
            Review = _reviewBar(ItemKind.Passage, PassageId, annotation.Review);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Montre l'explication, ou le passage caché.</summary>
    [RelayCommand]
    private void Reveal() => IsRevealed = true;

    /// <summary>Revient à la vue d'où le passage a été ouvert.</summary>
    [RelayCommand]
    private void Back() => _back();

    /// <summary>Ouvre la fiche du passage.</summary>
    [RelayCommand]
    private void SeeInSheet() => _openSheet(SheetId);
}

/// <summary>
/// Une fiche ouverte pour la lire et la réviser : son titre, son dossier, ses étiquettes, son
/// texte, et la barre de révision. Ouvrir une fiche ne valide rien. Le texte est montré tel qu'il
/// est écrit ; le rendu natif du Markdown, les passages surlignés et le texte à trous du mode
/// Réviser viennent avec la lecture native (J7).
/// </summary>
public sealed partial class SheetPageViewModel : ObservableObject, IReviewPage
{
    private readonly SheetUseCases _sheets;
    private readonly Func<ItemKind, string, ReviewState, ReviewBarViewModel> _reviewBar;
    private readonly Action _back;

    /// <summary>La fiche <paramref name="sheetId"/>.</summary>
    public SheetPageViewModel(SheetUseCases sheets, Func<ItemKind, string, ReviewState, ReviewBarViewModel> reviewBar, string sheetId, Action back)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(reviewBar);
        ArgumentNullException.ThrowIfNull(sheetId);
        ArgumentNullException.ThrowIfNull(back);
        _sheets = sheets;
        _reviewBar = reviewBar;
        SheetId = sheetId;
        _back = back;
        Title = string.Empty;
        Body = string.Empty;
        Details = [];
        IsLoading = true;
    }

    /// <summary>La fiche.</summary>
    public string SheetId { get; }

    /// <summary>Lecture en cours.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>La fiche n'a pas pu être ouverte.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Un refus est affiché.</summary>
    public bool HasError => Error is not null;

    /// <summary>Titre.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; }

    /// <summary>Dossier et étiquettes (« Biologie », « #partiel »).</summary>
    [ObservableProperty]
    public partial ImmutableArray<string> Details { get; private set; }

    /// <summary>Texte de la fiche, sans son en-tête.</summary>
    [ObservableProperty]
    public partial string Body { get; private set; }

    /// <summary>La barre de révision de la fiche.</summary>
    [ObservableProperty]
    public partial ReviewBarViewModel? Review { get; private set; }

    /// <summary>Lit la fiche.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var outcome = await Task.Run(() => _sheets.Open(SheetId)).ConfigureAwait(true);
            if (!outcome.Succeeded)
            {
                Error = outcome.Error;
                return;
            }
            var sheet = outcome.Value;
            Title = sheet.Title;
            Details = [.. (sheet.Folder.Length > 0 ? [sheet.Folder] : Array.Empty<string>()).Concat(sheet.Tags.Select(tag => $"#{tag}"))];
            Body = sheet.Body;
            Review = _reviewBar(ItemKind.Sheet, SheetId, sheet.Review);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Revient à la vue d'où la fiche a été ouverte.</summary>
    [RelayCommand]
    private void Back() => _back();
}
