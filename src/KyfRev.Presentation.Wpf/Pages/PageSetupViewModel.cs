using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La mise en page de la fiche ouverte : sa feuille (sans fin, A4, A5, A6), son orientation, ses marges, et si le choix
/// vaut pour cette fiche seule ou pour toutes. Elle règle l'affichage en lecture, en révision et en écriture ; le texte
/// de la fiche n'est jamais touché. Le choix est retenu sur cet ordinateur.
/// </summary>
public sealed partial class PageSetupViewModel : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly CompositeFormat SummaryFormat = CompositeFormat.Parse(ReviewText.PageSummary);
    private static readonly CompositeFormat OnePageFormat = CompositeFormat.Parse(ReviewText.PageStatusOne);
    private static readonly CompositeFormat ManyPagesFormat = CompositeFormat.Parse(ReviewText.PageStatusMany);

    private readonly Action<PageLayout, bool>? _save;
    private readonly Func<PaperChoices>? _readPaper;
    private readonly Action<PaperChoices>? _savePaper;
    private PaperChoices _paper = PaperChoices.Default;

    /// <param name="layout">La mise en page à l'ouverture.</param>
    /// <param name="forAll">Elle est celle de toutes les fiches ; sinon, celle de cette fiche seule.</param>
    /// <param name="save">Retient un choix : la mise en page, et si elle vaut pour toutes les fiches.</param>
    /// <param name="readPaper">Le papier choisi sur cet ordinateur, relu à chaque fois : il se règle aussi ailleurs.</param>
    /// <param name="savePaper">Retient un choix de papier.</param>
    public PageSetupViewModel(PageLayout? layout = null, bool forAll = true, Action<PageLayout, bool>? save = null, Func<PaperChoices>? readPaper = null, Action<PaperChoices>? savePaper = null)
    {
        Layout = layout ?? PageLayout.Default;
        AppliesToAll = forAll;
        _save = save;
        _readPaper = readPaper;
        _savePaper = savePaper;
    }

    /// <summary>Le papier de chaque type de feuille, et s'il est montré : un réglage de l'ordinateur, pas de la fiche.</summary>
    public PaperChoices Paper => _readPaper?.Invoke() ?? _paper;

    /// <summary>Le papier est montré derrière le texte.</summary>
    public bool IsPaperOn => Paper.IsOn;

    /// <summary>Le papier du type de feuille en cours.</summary>
    public PaperStyle PaperStyle => Paper.Of(Layout.Format);

    /// <summary>Feuille unie.</summary>
    public bool IsPaperPlain => PaperStyle == PaperStyle.Plain;

    /// <summary>Petits carreaux.</summary>
    public bool IsPaperSmallSquares => PaperStyle == PaperStyle.SmallSquares;

    /// <summary>Grands carreaux.</summary>
    public bool IsPaperLargeSquares => PaperStyle == PaperStyle.LargeSquares;

    /// <summary>Lignes.</summary>
    public bool IsPaperLines => PaperStyle == PaperStyle.Lines;

    /// <summary>Points.</summary>
    public bool IsPaperDots => PaperStyle == PaperStyle.Dots;

    /// <summary>Millimétré.</summary>
    public bool IsPaperGraph => PaperStyle == PaperStyle.Graph;

    /// <summary>Allume ou éteint le papier.</summary>
    [RelayCommand]
    private void TogglePaper() => SavePaper(Paper with { IsOn = !Paper.IsOn });

    /// <summary>Choisit le papier du type de feuille en cours, par son nom écrit (« lignes », « points »…), et l'allume.</summary>
    [RelayCommand]
    private void SetPaper(string? name) => SavePaper(Paper.With(Layout.Format, PaperStyles.Parse(name)) with { IsOn = true });

    private void SavePaper(PaperChoices paper)
    {
        _paper = paper;
        _savePaper?.Invoke(paper);
        NotifyPaper();
    }

    /// <summary>Le papier a pu changer, ici ou dans les paramètres : les boutons le redisent.</summary>
    public void NotifyPaper()
    {
        foreach (var name in new[] { nameof(Paper), nameof(IsPaperOn), nameof(PaperStyle), nameof(IsPaperPlain), nameof(IsPaperSmallSquares), nameof(IsPaperLargeSquares), nameof(IsPaperLines), nameof(IsPaperDots), nameof(IsPaperGraph) })
        {
            OnPropertyChanged(name);
        }
    }

    partial void OnLayoutChanged(PageLayout value) => NotifyPaper();

    /// <summary>La mise en page en cours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaged), nameof(IsEndless), nameof(IsA4), nameof(IsA5), nameof(IsA6), nameof(IsPortrait), nameof(IsLandscape))]
    [NotifyPropertyChangedFor(nameof(IsNarrow), nameof(IsNormal), nameof(IsWide), nameof(IsCustomMargin), nameof(MarginText), nameof(Summary), nameof(Status), nameof(HasStatus))]
    public partial PageLayout Layout { get; private set; }

    /// <summary>Le choix vaut pour toutes les fiches ; sinon, pour cette fiche seule.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppliesToSheet))]
    public partial bool AppliesToAll { get; private set; }

    /// <summary>Le nombre de pages de la fiche, telle qu'elle est affichée ; zéro tant qu'il n'est pas connu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status), nameof(HasStatus))]
    public partial int Pages { get; set; }

    /// <summary>Le choix vaut pour cette fiche seule.</summary>
    public bool AppliesToSheet => !AppliesToAll;

    /// <summary>La feuille a des pages.</summary>
    public bool IsPaged => Layout.IsPaged;

    /// <summary>La feuille est sans fin.</summary>
    public bool IsEndless => Layout.Format == PageFormat.Endless;

    /// <summary>La feuille est au format A4.</summary>
    public bool IsA4 => Layout.Format == PageFormat.A4;

    /// <summary>La feuille est au format A5.</summary>
    public bool IsA5 => Layout.Format == PageFormat.A5;

    /// <summary>La feuille est au format A6.</summary>
    public bool IsA6 => Layout.Format == PageFormat.A6;

    /// <summary>La feuille est en portrait.</summary>
    public bool IsPortrait => IsPaged && !Layout.Landscape;

    /// <summary>La feuille est en paysage.</summary>
    public bool IsLandscape => IsPaged && Layout.Landscape;

    /// <summary>Les marges sont étroites.</summary>
    public bool IsNarrow => IsPaged && Same(PageLayout.NarrowMargin);

    /// <summary>Les marges sont normales.</summary>
    public bool IsNormal => IsPaged && Same(PageLayout.NormalMargin);

    /// <summary>Les marges sont larges.</summary>
    public bool IsWide => IsPaged && Same(PageLayout.WideMargin);

    /// <summary>Les marges sont sur mesure.</summary>
    public bool IsCustomMargin => IsPaged && !Same(PageLayout.NarrowMargin) && !Same(PageLayout.NormalMargin) && !Same(PageLayout.WideMargin);

    /// <summary>Les marges, en centimètres, telles qu'on les lit et qu'on les tape : « 2 », « 1,5 ».</summary>
    public string MarginText
    {
        get => Layout.EffectiveMarginCm.ToString("0.##", French);
        set
        {
            var typed = (value ?? string.Empty).Replace("cm", string.Empty, StringComparison.OrdinalIgnoreCase).Trim().Replace(',', '.');
            if (double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out var margin) && double.IsFinite(margin) && margin > 0)
            {
                Apply(Layout with { MarginCm = margin });
            }
            // Une saisie illisible, ou hors de ce que la feuille accepte, laisse voir la valeur réellement appliquée.
            OnPropertyChanged();
        }
    }

    /// <summary>« A4 portrait · marges de 2 cm ».</summary>
    public string Summary => IsPaged
        ? string.Format(French, SummaryFormat, Layout.Format, Layout.Landscape ? ReviewText.PageLandscape : ReviewText.PagePortrait, MarginText)
        : string.Empty;

    /// <summary>La ligne d'état sous la fiche : la feuille, puis le nombre de pages dès qu'il est connu.</summary>
    public string Status => !IsPaged
        ? string.Empty
        : Pages <= 0 ? Summary : string.Format(French, Pages == 1 ? OnePageFormat : ManyPagesFormat, Summary, Pages);

    /// <summary>Il y a une ligne d'état à montrer.</summary>
    public bool HasStatus => IsPaged;

    /// <summary>Choisit la feuille : « Endless », « A4 », « A5 » ou « A6 ».</summary>
    [RelayCommand]
    private void SetFormat(string? name)
    {
        if (Enum.TryParse<PageFormat>(name, ignoreCase: true, out var format) && Enum.IsDefined(format))
        {
            Apply(Layout with { Format = format });
        }
    }

    /// <summary>Choisit l'orientation : « landscape » pour le paysage, autre chose pour le portrait.</summary>
    [RelayCommand]
    private void SetOrientation(string? name) => Apply(Layout with { Landscape = string.Equals(name, "landscape", StringComparison.OrdinalIgnoreCase) });

    /// <summary>Choisit des marges toutes faites : « narrow », « normal » ou « wide ».</summary>
    [RelayCommand]
    private void SetMargin(string? name) => Apply(Layout with
    {
        MarginCm = name switch
        {
            "narrow" => PageLayout.NarrowMargin,
            "wide" => PageLayout.WideMargin,
            _ => PageLayout.NormalMargin,
        },
    });

    /// <summary>Un cran de marge en plus (« + ») ou en moins : un demi-centimètre.</summary>
    [RelayCommand]
    private void NudgeMargin(string? direction) =>
        Apply(Layout with { MarginCm = Math.Round((Layout.EffectiveMarginCm + (direction == "+" ? 0.5 : -0.5)) * 2) / 2 });

    /// <summary>Le choix vaut pour toutes les fiches (« all ») ou pour cette fiche seule.</summary>
    [RelayCommand]
    private void SetScope(string? name)
    {
        var forAll = string.Equals(name, "all", StringComparison.OrdinalIgnoreCase);
        if (forAll != AppliesToAll)
        {
            AppliesToAll = forAll;
            _save?.Invoke(Layout, AppliesToAll);
        }
    }

    private bool Same(double margin) => Math.Abs(Layout.EffectiveMarginCm - margin) < 0.005;

    private void Apply(PageLayout wanted)
    {
        // Les marges suivent ce que la feuille accepte : une marge large sur une feuille A4 ne tient pas sur une A6.
        var layout = wanted with { MarginCm = wanted.EffectiveMarginCm };
        if (layout == Layout)
        {
            return;
        }
        if (layout.IsPaged != Layout.IsPaged || layout.Format != Layout.Format || layout.Landscape != Layout.Landscape)
        {
            Pages = 0;
        }
        Layout = layout;
        _save?.Invoke(Layout, AppliesToAll);
    }
}
