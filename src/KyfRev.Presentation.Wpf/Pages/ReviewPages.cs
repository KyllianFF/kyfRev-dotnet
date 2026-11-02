using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Rendering;

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
/// Une fiche ouverte pour la lire et la réviser : son plan à gauche, son texte, ses passages à droite, et la
/// barre de révision. Ouvrir une fiche ne valide rien. Les volets se replient quand la fenêtre rétrécit, le
/// plan d'abord ; ils s'ouvrent alors par-dessus le texte, avec les mêmes boutons.
/// </summary>
public sealed partial class SheetPageViewModel : ObservableObject, IReviewPage
{
    /// <summary>Largeur du plan à côté du texte.</summary>
    public const double OutlineWidth = 230;

    /// <summary>Largeur des passages à côté du texte.</summary>
    public const double PassagesWidth = 300;

    /// <summary>Largeur maximale du texte, pour rester lisible.</summary>
    public const double TextMaxWidth = 820;

    /// <summary>Largeur du plan ouvert par-dessus le texte.</summary>
    public const double OutlineOverlayWidth = 280;

    /// <summary>Largeur des passages ouverts par-dessus le texte.</summary>
    public const double PassagesOverlayWidth = 320;

    /// <summary>En dessous, les passages ne tiennent plus à côté du texte.</summary>
    public const double PassagesMinimum = 880;

    /// <summary>En dessous, le plan ne tient plus à côté du reste.</summary>
    public const double OutlineMinimum = 1120;

    /// <summary>Longueur maximale d'un passage, comme dans la 0.x.</summary>
    public const int PassageMaximum = 10_000;

    /// <summary>Longueur maximale d'un terme, comme dans la 0.x.</summary>
    public const int TermMaximum = 200;

    /// <summary>Longueur maximale d'un commentaire ou d'une explication, comme dans la 0.x.</summary>
    public const int CommentMaximum = 10_000;

    private readonly SheetUseCases _sheets;
    private readonly Func<ItemKind, string, ReviewState, ReviewBarViewModel> _reviewBar;
    private readonly Action _back;
    private readonly Action<string> _openSheet;
    private readonly IExternalLinks _links;
    private readonly AnnotationUseCases _annotations;
    private readonly Func<OpenedSheet, SheetEditorViewModel> _editor;
    private readonly Func<bool> _activeRecall;
    private TextPosition? _editedPosition;
    private PassageEntry? _editedPassage;

    /// <summary>La fiche <paramref name="sheetId"/>.</summary>
    public SheetPageViewModel(SheetUseCases sheets, AnnotationUseCases annotations, Func<ItemKind, string, ReviewState, ReviewBarViewModel> reviewBar, string sheetId, Action back, Action<string> openSheet, IExternalLinks links, Func<OpenedSheet, SheetEditorViewModel> editor, bool revising = false, Func<bool>? activeRecall = null)
    {
        _activeRecall = activeRecall ?? (() => false);
        IsRevising = revising;
        Holes.IsActive = revising && _activeRecall();
        Holes.Changed += OnHolesChanged;
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(reviewBar);
        ArgumentNullException.ThrowIfNull(sheetId);
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(openSheet);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(editor);
        _editor = editor;
        _sheets = sheets;
        _reviewBar = reviewBar;
        SheetId = sheetId;
        _back = back;
        _openSheet = openSheet;
        _links = links;
        _annotations = annotations;
        EditorComment = string.Empty;
        EditorTerm = string.Empty;
        EditorExplanation = string.Empty;
        WantsOutline = true;
        WantsPassages = true;
        PageWidth = double.PositiveInfinity;
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

    /// <summary>Un lien n'a pas pu être suivi, ou un passage enregistré.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; private set; }

    /// <summary>Un problème est affiché.</summary>
    public bool HasProblem => Problem is not null;

    /// <summary>Ce qui vient d'être fait, dit discrètement.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    /// <summary>La fiche prête à lire.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Outline))]
    public partial SheetReading? Reading { get; private set; }

    /// <summary>Ce qui se surligne dans le texte.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<Highlight>? Highlights { get; private set; }

    /// <summary>L'annotation qui ressort.</summary>
    [ObservableProperty]
    public partial string? ActivePassage { get; private set; }

    /// <summary>Une saisie est ouverte : un commentaire ou une définition.</summary>
    [ObservableProperty]
    public partial bool IsEditing { get; private set; }

    /// <summary>La saisie porte sur une définition.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditsComment), nameof(EditorTitle))]
    public partial bool EditsDefinition { get; private set; }

    /// <summary>La saisie porte sur un commentaire.</summary>
    public bool EditsComment => !EditsDefinition;

    /// <summary>Le titre de la saisie.</summary>
    public string EditorTitle => EditsDefinition ? ReadingText.DefinitionTitle : ReadingText.CommentTitle;

    /// <summary>Le commentaire écrit.</summary>
    [ObservableProperty]
    public partial string EditorComment { get; set; }

    /// <summary>Le terme écrit.</summary>
    [ObservableProperty]
    public partial string EditorTerm { get; set; }

    /// <summary>L'explication écrite.</summary>
    [ObservableProperty]
    public partial string EditorExplanation { get; set; }

    /// <summary>Ce qui empêche d'enregistrer la saisie.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditorProblem))]
    public partial string? EditorProblem { get; private set; }

    /// <summary>La saisie ne peut pas être enregistrée.</summary>
    public bool HasEditorProblem => EditorProblem is not null;

    /// <summary>L'annotation dont la suppression est demandée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeleting), nameof(DeleteTitle))]
    public partial PassageEntry? Deleting { get; private set; }

    /// <summary>Une suppression attend sa confirmation.</summary>
    public bool IsDeleting => Deleting is not null;

    /// <summary>Le titre de la confirmation.</summary>
    public string DeleteTitle => Deleting is { IsDefinition: true } ? ReadingText.DeleteDefinitionTitle : ReadingText.DeletePassageTitle;

    /// <summary>Le plan de la fiche : en écriture, celui du texte en cours.</summary>
    public IReadOnlyList<OutlineEntry> Outline => Editor?.Preview?.Outline ?? Reading?.Outline ?? [];

    /// <summary>Ses passages et définitions.</summary>
    public IReadOnlyList<PassageEntry> Passages => Reading?.Passages ?? [];

    /// <summary>La fiche a des titres.</summary>
    public bool HasOutline => Outline.Count > 0;

    /// <summary>La fiche a des passages ou des définitions.</summary>
    public bool HasPassages => Passages.Count > 0 || TrashedPassages.Count > 0;

    /// <summary>Ce qui est à la corbeille de la fiche.</summary>
    public IReadOnlyList<TrashedPassage> TrashedPassages => Reading?.Trashed ?? [];

    /// <summary>La corbeille a quelque chose à rendre.</summary>
    public bool HasTrashedPassages => TrashedPassages.Count > 0;

    /// <summary>« Corbeille · 2 ».</summary>
    public string TrashLabel => ReadingText.TrashOf(TrashedPassages.Count);

    /// <summary>« Passages · 3 ».</summary>
    public string PassagesLabel => ReadingText.PassagesOf(Passages.Count);

    /// <summary>La barre de révision de la fiche.</summary>
    [ObservableProperty]
    public partial ReviewBarViewModel? Review { get; private set; }

    /// <summary>Largeur de la page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutlineFits), nameof(PassagesFits), nameof(ShowsOutline), nameof(ShowsPassages), nameof(ShowsOutlineOverlay), nameof(ShowsPassagesOverlay), nameof(IsOutlineOn), nameof(IsPassagesOn))]
    public partial double PageWidth { get; set; }

    /// <summary>Le plan tient à côté du texte.</summary>
    public bool OutlineFits => PageWidth >= OutlineMinimum;

    /// <summary>Les passages tiennent à côté du texte.</summary>
    public bool PassagesFits => PageWidth >= PassagesMinimum;

    /// <summary>Le plan est voulu à côté du texte, quand il y tient.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsOutline), nameof(IsOutlineOn))]
    private partial bool WantsOutline { get; set; }

    /// <summary>Les passages sont voulus à côté du texte, quand ils y tiennent.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPassages), nameof(IsPassagesOn))]
    private partial bool WantsPassages { get; set; }

    /// <summary>Le plan est ouvert par-dessus le texte.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsOutlineOverlay), nameof(IsOutlineOn))]
    private partial bool OutlineOverlaid { get; set; }

    /// <summary>Les passages sont ouverts par-dessus le texte.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPassagesOverlay), nameof(IsPassagesOn))]
    private partial bool PassagesOverlaid { get; set; }

    /// <summary>Le plan est montré à côté du texte.</summary>
    public bool ShowsOutline => HasOutline && WantsOutline && OutlineFits && !ShowsCode;

    /// <summary>Les passages sont montrés à côté du texte.</summary>
    public bool ShowsPassages => HasPassages && WantsPassages && PassagesFits && !ShowsCode;

    /// <summary>Le plan est montré par-dessus le texte.</summary>
    public bool ShowsOutlineOverlay => OutlineOverlaid && !OutlineFits && !ShowsCode;

    /// <summary>Les passages sont montrés par-dessus le texte.</summary>
    public bool ShowsPassagesOverlay => PassagesOverlaid && !PassagesFits && !ShowsCode;

    /// <summary>Le bouton du plan : à côté du texte s'il y tient, sinon par-dessus.</summary>
    public bool IsOutlineOn
    {
        get => ShowsOutline || ShowsOutlineOverlay;
        set
        {
            if (OutlineFits)
            {
                WantsOutline = value;
            }
            else
            {
                PassagesOverlaid = false;
                OutlineOverlaid = value;
            }
        }
    }

    /// <summary>Le bouton des passages : à côté du texte s'ils y tiennent, sinon par-dessus.</summary>
    public bool IsPassagesOn
    {
        get => ShowsPassages || ShowsPassagesOverlay;
        set
        {
            if (PassagesFits)
            {
                WantsPassages = value;
            }
            else
            {
                OutlineOverlaid = false;
                PassagesOverlaid = value;
            }
        }
    }

    /// <summary>L'écriture en cours ; rien en lecture.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWriting), nameof(IsReading), nameof(IsReadOn), nameof(IsReviseOn), nameof(IsWriteOn), nameof(ValidateShortcut))]
    public partial SheetEditorViewModel? Editor { get; private set; }

    /// <summary>La fiche s'écrit.</summary>
    public bool IsWriting => Editor is not null;

    /// <summary>La fiche se lit.</summary>
    public bool IsReading => Editor is null;

    /// <summary>Le bouton « Lire » : le choisir revient à la lecture ; le mode en cours reste choisi.</summary>
    public bool IsReadOn
    {
        get => IsReading && !IsRevising;
        set
        {
            if (value)
            {
                StopWritingCommand.Execute(null);
                SetRevising(false);
            }
            OnPropertyChanged();
        }
    }

    /// <summary>Le bouton « Réviser » : la fiche se relit, et, si le rappel actif est choisi, elle est à trous.</summary>
    public bool IsReviseOn
    {
        get => IsReading && IsRevising;
        set
        {
            if (value)
            {
                StopWritingCommand.Execute(null);
                SetRevising(true);
            }
            OnPropertyChanged();
        }
    }

    /// <summary>La fiche est en révision.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadOn), nameof(IsReviseOn))]
    public partial bool IsRevising { get; private set; }

    /// <summary>Le lecteur à voix haute ; rien sans les voix de Windows.</summary>
    public ReadAloudViewModel? ReadAloud { get; init; }

    /// <summary>Le passage à montrer à l'ouverture, une seule fois ; rien pour la montrer depuis le début.</summary>
    public string? PassageToShow { get; set; }

    /// <summary>Le texte à trous de la révision.</summary>
    public TextHoles Holes { get; } = new();

    private void SetRevising(bool revising)
    {
        Holes.IsActive = revising && _activeRecall();
        IsRevising = revising;
    }

    /// <summary>Le volet des passages tait ce que le texte masque encore.</summary>
    private void OnHolesChanged(object? sender, EventArgs e)
    {
        foreach (var entry in Passages)
        {
            entry.IsMasked = Holes.Masks(entry.Annotation.Id);
        }
    }

    /// <summary>Le bouton « Écrire » : le choisir passe à l'écriture ; le mode en cours reste choisi.</summary>
    public bool IsWriteOn
    {
        get => IsWriting;
        set
        {
            if (value)
            {
                SetRevising(false);
                StartWritingCommand.Execute(null);
            }
            OnPropertyChanged();
        }
    }

    /// <summary>Le code Markdown et son rendu prennent toute la largeur.</summary>
    public bool ShowsCode => Editor?.IsMarkdownShown == true;

    /// <summary>Les boutons du plan et des passages ont un sens.</summary>
    public bool ShowsPanelToggles => !ShowsCode;

    /// <summary>La largeur du texte : celle de la lecture, ou toute la place pour le code et son rendu.</summary>
    public double TextWidth => ShowsCode ? double.PositiveInfinity : TextMaxWidth;

    /// <summary>Le raccourci de validation : en écriture, il n'y a rien à valider.</summary>
    public System.Windows.Input.ICommand? ValidateShortcut => IsWriting ? null : Review?.ValidateCommand;

    partial void OnEditorChanging(SheetEditorViewModel? value)
    {
        Editor?.PropertyChanged -= OnEditorPropertyChanged;
        value?.PropertyChanged += OnEditorPropertyChanged;
    }

    partial void OnEditorChanged(SheetEditorViewModel? value) => NotifyLayout();

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SheetEditorViewModel.IsMarkdownShown) or nameof(SheetEditorViewModel.Preview))
        {
            NotifyLayout();
        }
    }

    private void NotifyLayout()
    {
        OnPropertyChanged(nameof(Outline));
        OnPropertyChanged(nameof(HasOutline));
        OnPropertyChanged(nameof(ShowsCode));
        OnPropertyChanged(nameof(ShowsPanelToggles));
        OnPropertyChanged(nameof(TextWidth));
        OnPropertyChanged(nameof(ShowsOutline));
        OnPropertyChanged(nameof(ShowsPassages));
        OnPropertyChanged(nameof(ShowsOutlineOverlay));
        OnPropertyChanged(nameof(ShowsPassagesOverlay));
        OnPropertyChanged(nameof(IsOutlineOn));
        OnPropertyChanged(nameof(IsPassagesOn));
    }

    /// <summary>Passe à l'écriture : le texte du disque, relu maintenant.</summary>
    [RelayCommand]
    private async Task StartWritingAsync()
    {
        if (Editor is null)
        {
            CloseOverlay();
            CancelRebinding();
            var opened = await Task.Run(() => _sheets.Open(SheetId)).ConfigureAwait(true);
            if (opened.Succeeded)
            {
                Editor = _editor(opened.Value);
            }
            else
            {
                Problem = opened.Error;
            }
        }
        OnPropertyChanged(nameof(IsReadOn));
        OnPropertyChanged(nameof(IsWriteOn));
    }

    /// <summary>Revient à la lecture, une fois tout enregistré ; sinon, l'écriture reste, avec ce qui bloque.</summary>
    [RelayCommand]
    private async Task StopWritingAsync()
    {
        if (Editor is { } editor && await editor.FinishAsync().ConfigureAwait(true))
        {
            Editor = null;
            if (editor.HasChanged)
            {
                await LoadAsync().ConfigureAwait(true);
            }
        }
        OnPropertyChanged(nameof(IsReadOn));
        OnPropertyChanged(nameof(IsWriteOn));
    }

    /// <summary>Ctrl+E : de la lecture à l'écriture, et inversement.</summary>
    [RelayCommand]
    private async Task ToggleWritingAsync()
    {
        if (IsWriting)
        {
            await StopWritingAsync().ConfigureAwait(true);
        }
        else
        {
            await StartWritingAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Lit la fiche.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var (outcome, reading) = await Task.Run(() =>
            {
                var opened = _sheets.Open(SheetId);
                return (opened, opened.Succeeded ? SheetReading.Of(opened.Value, _sheets) : null);
            }).ConfigureAwait(true);
            if (!outcome.Succeeded)
            {
                Error = outcome.Error;
                return;
            }
            Reading = reading;
            ShowPassages();
            await SavePlacementsAsync().ConfigureAwait(true);
            Review = _reviewBar(ItemKind.Sheet, SheetId, outcome.Value.Review);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Surligne une position du texte, sans commentaire.</summary>
    [RelayCommand]
    private async Task HighlightAsync(TextPosition position) => await CreatePassageAsync(position, null).ConfigureAwait(true);

    /// <summary>Demande le commentaire d'un nouveau passage.</summary>
    [RelayCommand]
    private void StartAnnotating(TextPosition position)
    {
        _editedPosition = position;
        _editedPassage = null;
        EditsDefinition = false;
        EditorComment = string.Empty;
        OpenEditor();
    }

    /// <summary>Demande l'explication d'une nouvelle définition ; la sélection propose le terme.</summary>
    [RelayCommand]
    private void StartDefining(TextPosition position)
    {
        _editedPosition = position;
        _editedPassage = null;
        EditsDefinition = true;
        var term = string.Join(' ', Reading!.Map.Text[position.Start..position.End].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        EditorTerm = term.Length > TermMaximum ? term[..TermMaximum] : term;
        EditorExplanation = string.Empty;
        OpenEditor();
    }

    /// <summary>Demande le nouveau commentaire d'un passage, ou la nouvelle définition.</summary>
    [RelayCommand]
    private void StartEditing(PassageEntry passage)
    {
        _editedPosition = null;
        _editedPassage = passage;
        EditsDefinition = passage.IsDefinition;
        EditorComment = passage.Annotation.Comment ?? string.Empty;
        EditorTerm = passage.Annotation.Definition?.Term ?? string.Empty;
        EditorExplanation = passage.Annotation.Definition?.Explanation ?? string.Empty;
        OpenEditor();
    }

    /// <summary>Enregistre la saisie ; une définition incomplète la laisse ouverte.</summary>
    [RelayCommand]
    private async Task AnswerEditorAsync(bool confirmed)
    {
        IsEditing = false;
        if (!confirmed)
        {
            return;
        }
        var definition = new Definition(EditorTerm.Trim(), EditorExplanation.Trim());
        if (EditsDefinition && (definition.Term.Length == 0 || definition.Explanation.Length == 0))
        {
            EditorProblem = ReadingText.DefinitionIncomplete;
            IsEditing = true;
            return;
        }
        var comment = EditorComment.Trim() is { Length: > 0 } written ? written : null;
        var problem = (_editedPassage, _editedPosition) switch
        {
            ({ } passage, _) when EditsDefinition => await RunAsync(() => _annotations.ModifyDefinition(passage.Annotation.Id, definition), ReadingText.Saved).ConfigureAwait(true),
            ({ } passage, _) => await RunAsync(() => _annotations.ModifyComment(passage.Annotation.Id, comment), ReadingText.Saved).ConfigureAwait(true),
            (_, { } position) when EditsDefinition => await CreateDefinitionAsync(position, definition).ConfigureAwait(true),
            (_, { } position) => await CreatePassageAsync(position, comment).ConfigureAwait(true),
            _ => null,
        };
        if (problem is not null)
        {
            EditorProblem = problem;
            IsEditing = true;
        }
    }

    /// <summary>Demande confirmation avant de mettre une annotation à la corbeille.</summary>
    [RelayCommand]
    private void RequestDelete(PassageEntry passage) => Deleting = passage;

    /// <summary>Met l'annotation à la corbeille, si c'est confirmé.</summary>
    [RelayCommand]
    private async Task AnswerDeleteAsync(bool confirmed)
    {
        var passage = Deleting;
        Deleting = null;
        if (confirmed && passage is not null)
        {
            await RunAsync(() => _annotations.Trash(passage.Annotation.Id), ReadingText.Deleted).ConfigureAwait(true);
        }
    }

    /// <summary>Remet un passage de la corbeille.</summary>
    [RelayCommand]
    private async Task RestorePassageAsync(TrashedPassage passage) =>
        await RunAsync(() => _annotations.Restore(passage.Id), ReadingText.Restored).ConfigureAwait(true);

    /// <summary>Le passage que l'on rattache : il attend une nouvelle sélection dans le texte.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRebinding), nameof(RebindingPrompt))]
    public partial PassageEntry? Rebinding { get; private set; }

    /// <summary>La nouvelle place choisie, en attente de confirmation.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirmingRebinding), nameof(RebindingMessage))]
    public partial Anchor? RebindingTarget { get; private set; }

    /// <summary>Un passage attend sa nouvelle place.</summary>
    public bool IsRebinding => Rebinding is not null;

    /// <summary>Ce qu'il faut faire pour le rattacher.</summary>
    public string RebindingPrompt => Rebinding is { } passage ? ReadingText.RebindPrompt(passage.Title) : string.Empty;

    /// <summary>La nouvelle place attend d'être confirmée.</summary>
    public bool IsConfirmingRebinding => RebindingTarget is not null;

    /// <summary>La question posée pour la confirmer.</summary>
    public string RebindingMessage => RebindingTarget is { } target ? ReadingText.RebindMessage(target.Exact) : string.Empty;

    /// <summary>Attend dans le texte la nouvelle place d'un passage.</summary>
    [RelayCommand]
    private void StartRebinding(PassageEntry passage)
    {
        CloseOverlay();
        Rebinding = passage;
    }

    /// <summary>Échap : referme ce qui est ouvert par-dessus, et renonce à changer la place d'un passage.</summary>
    [RelayCommand]
    private void Escape()
    {
        CloseOverlay();
        CancelRebinding();
    }

    /// <summary>Renonce à changer la place du passage.</summary>
    [RelayCommand]
    private void CancelRebinding()
    {
        Rebinding = null;
        RebindingTarget = null;
    }

    /// <summary>Propose cette position comme nouvelle place du passage.</summary>
    [RelayCommand]
    private void RebindAt(TextPosition position)
    {
        if (Rebinding is not null && Reading is not null)
        {
            RebindingTarget = Reading.AnchorAt(position);
        }
    }

    /// <summary>Rappelle qu'il faut d'abord sélectionner la nouvelle place.</summary>
    internal void AskForRebindingSelection() => Notice = ReadingText.RebindNoSelection;

    /// <summary>Répond à la confirmation : sa progression reste, ou elle repart de J0. Rien : on peut encore choisir.</summary>
    [RelayCommand]
    private async Task AnswerRebindingAsync(bool? restart)
    {
        var (passage, target) = (Rebinding, RebindingTarget);
        RebindingTarget = null;
        if (restart is not { } again || passage is null || target is null)
        {
            return;
        }
        Rebinding = null;
        await RunAsync(() => _annotations.ModifyPassage(passage.Annotation.Id, target, again), again ? ReadingText.RebindRestarted : ReadingText.RebindKept).ConfigureAwait(true);
    }

    /// <summary>Accepte la position proposée pour un passage dont le texte a changé.</summary>
    [RelayCommand]
    private async Task ConfirmPositionAsync(PassageEntry passage)
    {
        var anchor = Reading!.AnchorAt(passage.Position!.Value);
        await RunAsync(
            () => anchor.Exact == passage.Annotation.Anchor.Exact
                ? _annotations.Reanchor(passage.Annotation.Id, anchor, AnchorState.Anchored)
                : _annotations.ModifyPassage(passage.Annotation.Id, anchor, restart: false),
            ReadingText.PositionConfirmed).ConfigureAwait(true);
    }

    /// <summary>Refuse la position proposée : le passage reste, sans place dans le texte.</summary>
    [RelayCommand]
    private async Task KeepWithoutPositionAsync(PassageEntry passage) =>
        await RunAsync(() => _annotations.Reanchor(passage.Annotation.Id, passage.Annotation.Anchor, AnchorState.NotFound), ReadingText.Saved).ConfigureAwait(true);

    /// <summary>Fait ressortir une annotation, dans le texte et dans le panneau.</summary>
    [RelayCommand]
    private void Activate(string? id)
    {
        ActivePassage = id;
        foreach (var passage in Passages)
        {
            passage.IsActive = passage.Annotation.Id == id;
        }
    }

    /// <summary>Referme le volet ouvert par-dessus le texte.</summary>
    [RelayCommand]
    private void CloseOverlay()
    {
        OutlineOverlaid = false;
        PassagesOverlaid = false;
    }

    /// <summary>Suit un lien de la fiche : vers le navigateur du système, ou vers une autre fiche.</summary>
    [RelayCommand]
    private void OpenLink(LinkTarget? target)
    {
        Problem = null;
        switch (target)
        {
            case ExternalLink external when !_links.Open(external.Address):
                Problem = ReadingText.CannotOpenAddress;
                break;
            case SheetLink sheet:
                var found = _sheets.IdAt(sheet.Path);
                if (found is { Succeeded: true, Value: { } id })
                {
                    _openSheet(id);
                }
                else
                {
                    Problem = ReadingText.MissingLinkedSheet;
                }
                break;
        }
    }

    /// <summary>Revient à la vue d'où la fiche a été ouverte ; ce qui s'écrit est d'abord enregistré.</summary>
    [RelayCommand]
    private async Task BackAsync()
    {
        if (Editor is not { } editor || await editor.FinishAsync().ConfigureAwait(true))
        {
            _back();
        }
    }

    private void OpenEditor()
    {
        EditorProblem = null;
        IsEditing = true;
    }

    private Task<string?> CreatePassageAsync(TextPosition position, string? comment)
    {
        var anchor = Reading!.AnchorAt(position);
        return RunAsync(() => _annotations.Create(SheetId, anchor, comment), ReadingText.PassageCreated);
    }

    private Task<string?> CreateDefinitionAsync(TextPosition position, Definition definition)
    {
        var anchor = Reading!.AnchorAt(position);
        return RunAsync(() => _annotations.CreateDefinition(SheetId, anchor, definition), ReadingText.DefinitionCreated);
    }

    /// <summary>Fait une action sur une annotation, puis relit les passages ; rend le refus, s'il y en a un.</summary>
    private async Task<string?> RunAsync(Func<Outcome<Done>> action, string notice)
    {
        Problem = null;
        var outcome = await Task.Run(action).ConfigureAwait(true);
        if (!outcome.Succeeded)
        {
            Problem = outcome.Error;
            return outcome.Error;
        }
        await RefreshAsync().ConfigureAwait(true);
        Notice = notice;
        return null;
    }

    /// <summary>Crée une annotation, la fait ressortir, puis relit les passages ; rend le refus, s'il y en a un.</summary>
    private async Task<string?> RunAsync(Func<Outcome<string>> create, string notice)
    {
        Problem = null;
        var outcome = await Task.Run(create).ConfigureAwait(true);
        if (!outcome.Succeeded)
        {
            Problem = outcome.Error;
            return outcome.Error;
        }
        await RefreshAsync().ConfigureAwait(true);
        Activate(outcome.Value);
        Notice = notice;
        return null;
    }

    /// <summary>Relit les passages après une modification, sans refaire le document.</summary>
    private async Task RefreshAsync()
    {
        var opened = await Task.Run(() => _sheets.Open(SheetId)).ConfigureAwait(true);
        if (!opened.Succeeded)
        {
            Problem = opened.Error;
            return;
        }
        if (opened.Value.Fingerprint != Reading!.Fingerprint)
        {
            await LoadAsync().ConfigureAwait(true);
            return;
        }
        Reading.Update(opened.Value.Annotations);
        ShowPassages();
        await SavePlacementsAsync().ConfigureAwait(true);
        if (Review is { } review)
        {
            await review.RefreshPassagesAsync().ConfigureAwait(true);
        }
    }

    private void ShowPassages()
    {
        Highlights = Reading!.Highlights;
        Activate(Reading.Passages.Any(passage => passage.Annotation.Id == ActivePassage) ? ActivePassage : null);
        OnPropertyChanged(nameof(Passages));
        OnPropertyChanged(nameof(TrashedPassages));
        OnPropertyChanged(nameof(HasTrashedPassages));
        OnPropertyChanged(nameof(TrashLabel));
        OnPropertyChanged(nameof(HasPassages));
        OnPropertyChanged(nameof(PassagesLabel));
        OnPropertyChanged(nameof(ShowsPassages));
        OnPropertyChanged(nameof(IsPassagesOn));
        OnPropertyChanged(nameof(HasOutline));
        OnPropertyChanged(nameof(ShowsOutline));
        OnPropertyChanged(nameof(IsOutlineOn));
    }

    /// <summary>Enregistre les ancrages qui ont changé : une nouvelle place du même texte, ou un nouvel état.</summary>
    private async Task SavePlacementsAsync()
    {
        var changes = Reading!.PlacementChanges().ToList();
        if (changes.Count == 0)
        {
            return;
        }
        var problem = await Task.Run(() => changes
            .Select(change => _annotations.Reanchor(change.Id, change.Anchor, change.State))
            .FirstOrDefault(outcome => !outcome.Succeeded)?.Error).ConfigureAwait(true);
        if (problem is not null)
        {
            Problem = problem;
        }
    }
}
