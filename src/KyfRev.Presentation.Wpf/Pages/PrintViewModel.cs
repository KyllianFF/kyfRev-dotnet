using System.Globalization;
using System.Text;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Printing;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Les textes de la fenêtre « Imprimer ».</summary>
public static class PrintText
{
    /// <summary>Le titre de la fenêtre, et le nom de son bouton.</summary>
    public const string Print = "Imprimer";

    /// <summary>Le titre de la fenêtre, avec celui de la fiche.</summary>
    public const string Title = "Imprimer « {0} »";

    /// <summary>Le choix de l'imprimante.</summary>
    public const string Printer = "Imprimante";

    /// <summary>Aucune imprimante n'est installée.</summary>
    public const string NoPrinter = "Aucune imprimante n'est installée sur cet ordinateur. « Microsoft Print to PDF », fournie avec Windows, suffit pour faire un PDF.";

    /// <summary>La liste des imprimantes se lit.</summary>
    public const string LookingForPrinters = "Recherche des imprimantes…";

    /// <summary>L'imprimante choisie n'est plus là.</summary>
    public const string PrinterGone = "Cette imprimante n'est plus installée : choisissez-en une autre.";

    /// <summary>Le nombre d'exemplaires.</summary>
    public const string Copies = "Copies";

    /// <summary>Un exemplaire de moins.</summary>
    public const string FewerCopies = "Une copie de moins";

    /// <summary>Un exemplaire de plus.</summary>
    public const string MoreCopies = "Une copie de plus";

    /// <summary>Quelles feuilles imprimer.</summary>
    public const string Sheets = "Feuilles";

    /// <summary>Toutes les feuilles.</summary>
    public const string AllSheets = "Toutes";

    /// <summary>Certaines feuilles, par leurs numéros.</summary>
    public const string SomeSheets = "Certaines";

    /// <summary>Le champ des numéros.</summary>
    public const string SheetNumbers = "Numéros des feuilles à imprimer";

    /// <summary>Comment écrire les numéros : {0} est le nombre de feuilles.</summary>
    public const string SheetNumbersHelp = "Des numéros et des plages, séparés par des virgules : 1-2, 4. La fiche fait {0} feuille(s).";

    /// <summary>La saisie ne se lit pas : {0} est le nombre de feuilles.</summary>
    public const string SheetNumbersWrong = "Écrivez des numéros de 1 à {0}, séparés par des virgules, ou des plages comme 1-2.";

    /// <summary>Le groupe du recto verso.</summary>
    public const string Sides = "Recto verso";

    /// <summary>Une seule face.</summary>
    public const string OneSided = "Recto seul";

    /// <summary>Deux faces, retournées sur le bord long.</summary>
    public const string LongEdge = "Bord long";

    /// <summary>Deux faces, retournées sur le bord court.</summary>
    public const string ShortEdge = "Bord court";

    /// <summary>Ce que veut dire « Bord long ».</summary>
    public const string LongEdgeHelp = "Recto verso, bord long : les pages se tournent comme celles d'un livre";

    /// <summary>Ce que veut dire « Bord court ».</summary>
    public const string ShortEdgeHelp = "Recto verso, bord court : les pages se tournent comme celles d'un bloc-notes";

    /// <summary>Le groupe de l'orientation.</summary>
    public const string Orientation = "Orientation";

    /// <summary>La feuille en hauteur.</summary>
    public const string Portrait = "Portrait";

    /// <summary>La feuille en largeur.</summary>
    public const string Landscape = "Paysage";

    /// <summary>Le groupe des marges.</summary>
    public const string Margins = "Marges";

    /// <summary>Les marges de la fiche : {0} est leur valeur.</summary>
    public const string MarginsOfSheet = "Celles de la fiche ({0} cm)";

    /// <summary>Des marges de 1,27 cm.</summary>
    public const string MarginsNarrow = "Étroites (1,27 cm)";

    /// <summary>Des marges de 2 cm.</summary>
    public const string MarginsNormal = "Normales (2 cm)";

    /// <summary>Des marges de 3 cm.</summary>
    public const string MarginsWide = "Larges (3 cm)";

    /// <summary>Des marges choisies au clavier.</summary>
    public const string MarginsCustom = "Sur mesure";

    /// <summary>Le champ des marges sur mesure.</summary>
    public const string MarginsCustomField = "Marges, en centimètres";

    /// <summary>La case des numéros de page.</summary>
    public const string Numbers = "Numéros de page en bas (1/4)";

    /// <summary>La feuille montrée : {0} sur {1}.</summary>
    public const string SheetOf = "{0} sur {1}";

    /// <summary>La feuille précédente.</summary>
    public const string PreviousSheet = "Feuille précédente";

    /// <summary>La feuille suivante.</summary>
    public const string NextSheet = "Feuille suivante";

    /// <summary>L'aperçu.</summary>
    public const string Preview = "Aperçu de la feuille";

    /// <summary>L'aperçu se prépare.</summary>
    public const string Preparing = "Préparation de l'aperçu…";

    /// <summary>La feuille montrée n'est pas parmi celles à imprimer.</summary>
    public const string NotPrinted = "Cette feuille ne sera pas imprimée";

    /// <summary>Le compte : {0} feuilles de la fiche, {1} feuilles de papier.</summary>
    public const string Summary = "{0} feuille(s) à imprimer · {1} feuille(s) de papier";

    /// <summary>Le travail est parti : {0} est l'imprimante.</summary>
    public const string Sent = "Envoyé à l'imprimante « {0} ».";

    /// <summary>L'entrée des menus du clic droit.</summary>
    public const string PrintAction = "Imprimer…";

    /// <summary>L'impression est demandée par un raccourci.</summary>
    public const string PrintSheet = "Imprimer la fiche (Ctrl+P)";

    /// <summary>L'imprimante a refusé : la raison donnée par Windows suit.</summary>
    public static string PrintFailed(string reason) => $"L'impression n'a pas pu partir : {reason}";
}

/// <summary>Un choix de marges.</summary>
public enum PrintMargin
{
    /// <summary>Celles de la mise en page de la fiche.</summary>
    Sheet,

    /// <summary>1,27 cm.</summary>
    Narrow,

    /// <summary>2 cm.</summary>
    Normal,

    /// <summary>3 cm.</summary>
    Wide,

    /// <summary>Une valeur tapée.</summary>
    Custom,
}

/// <summary>Un choix de marges, avec son nom.</summary>
/// <param name="Kind">Le choix.</param>
/// <param name="Label">Ce qui se lit dans la liste.</param>
public sealed record PrintMarginOption(PrintMargin Kind, string Label)
{
    /// <inheritdoc/>
    public override string ToString() => Label;
}

/// <summary>
/// La fenêtre « Imprimer » d'une fiche (Ctrl+P) : l'imprimante, les copies, les feuilles (toutes ou certaines), le recto
/// verso, l'orientation, les marges (par défaut celles de la fiche), les numéros de page, et l'aperçu feuille par feuille.
/// Les pages sont celles de la mise en page de la fiche ; une fiche restée en feuille sans fin s'imprime en A4.
/// </summary>
public sealed partial class PrintViewModel : ObservableObject
{
    /// <summary>Le plus grand nombre de copies.</summary>
    public const int MaximumCopies = 99;

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly CompositeFormat TitleFormat = CompositeFormat.Parse(PrintText.Title);
    private static readonly CompositeFormat SheetOfFormat = CompositeFormat.Parse(PrintText.SheetOf);
    private static readonly CompositeFormat SummaryFormat = CompositeFormat.Parse(PrintText.Summary);
    private static readonly CompositeFormat HelpFormat = CompositeFormat.Parse(PrintText.SheetNumbersHelp);
    private static readonly CompositeFormat WrongFormat = CompositeFormat.Parse(PrintText.SheetNumbersWrong);
    private static readonly CompositeFormat OfSheetFormat = CompositeFormat.Parse(PrintText.MarginsOfSheet);
    private static readonly CompositeFormat SentFormat = CompositeFormat.Parse(PrintText.Sent);

    private readonly IPrintService? _service;
    private PageFormat _format = PageFormat.A4;
    private double _sheetMargin = PageLayout.NormalMargin;
    private string _sheetTitle = string.Empty;
    private int _request;

    /// <param name="service">Les imprimantes ; sans elles, la fenêtre montre l'aperçu mais n'imprime pas.</param>
    public PrintViewModel(IPrintService? service = null)
    {
        _service = service;
        MarginOptions = Options(PageLayout.NormalMargin);
        Margin = MarginOptions[0];
    }

    /// <summary>La fenêtre est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>La fiche à imprimer, telle qu'elle est enregistrée.</summary>
    [ObservableProperty]
    public partial SheetReading? Reading { get; private set; }

    /// <summary>Le titre de la fenêtre.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = PrintText.Print;

    /// <summary>Les imprimantes installées.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoPrinter))]
    public partial IReadOnlyList<string> Printers { get; private set; } = [];

    /// <summary>L'imprimante choisie.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPrint))]
    public partial string? Printer { get; set; }

    /// <summary>La liste des imprimantes se lit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoPrinter))]
    public partial bool IsLookingForPrinters { get; private set; }

    /// <summary>Le nombre de copies.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial int Copies { get; set; } = 1;

    /// <summary>Toutes les feuilles s'impriment ; sinon, celles de <see cref="PagesText"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SomePages), nameof(PagesToPrint), nameof(PagesMessage), nameof(HasPagesError), nameof(ShowsPagesHelp), nameof(Summary), nameof(CanPrint), nameof(IsCurrentPrinted))]
    public partial bool AllPages { get; private set; } = true;

    /// <summary>Les numéros des feuilles à imprimer : « 1-2, 4 ».</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PagesToPrint), nameof(PagesMessage), nameof(HasPagesError), nameof(ShowsPagesHelp), nameof(Summary), nameof(CanPrint), nameof(IsCurrentPrinted))]
    public partial string PagesText { get; set; } = string.Empty;

    /// <summary>Recto seul ou recto verso.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOneSided), nameof(IsLongEdge), nameof(IsShortEdge), nameof(Summary))]
    public partial PrintSides Sides { get; private set; }

    /// <summary>En paysage.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPortrait), nameof(Layout))]
    public partial bool IsLandscape { get; private set; }

    /// <summary>Les choix de marges.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PrintMarginOption> MarginOptions { get; private set; }

    /// <summary>Le choix de marges en cours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomMargin), nameof(Layout))]
    public partial PrintMarginOption Margin { get; set; }

    /// <summary>Les marges sur mesure, en centimètres, telles qu'on les tape.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Layout))]
    public partial string CustomMargin { get; set; } = "2";

    /// <summary>Les numéros de page s'impriment en bas, au format « 1/4 ».</summary>
    [ObservableProperty]
    public partial bool ShowsNumbers { get; set; } = true;

    /// <summary>Le nombre de feuilles de la fiche, mise en page comme elle sera imprimée ; zéro tant qu'il n'est pas connu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PagesToPrint), nameof(PagesMessage), nameof(HasPagesError), nameof(ShowsPagesHelp), nameof(Summary), nameof(CanPrint), nameof(PageLabel), nameof(IsCurrentPrinted), nameof(HasPages))]
    public partial int TotalPages { get; set; }

    /// <summary>La feuille montrée par l'aperçu, à partir de 1.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageLabel), nameof(IsCurrentPrinted))]
    public partial int CurrentPage { get; private set; } = 1;

    /// <summary>L'aperçu se prépare : rien ne s'imprime avant qu'il soit prêt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPrint))]
    public partial bool IsPreparing { get; set; }

    /// <summary>Ce qui a empêché l'impression ; rien sinon.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>La vue fournit les pages, mises en page : les feuilles demandées, avec ou sans numéros.</summary>
    internal Func<IReadOnlyList<int>, bool, DocumentPaginator?>? Paginator { get; set; }

    /// <summary>Le travail est parti : ce qu'il faut en dire.</summary>
    public Action<string>? Printed { get; set; }

    /// <summary>Il y a une erreur à montrer.</summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>Aucune imprimante n'est installée.</summary>
    public bool HasNoPrinter => !IsLookingForPrinters && Printers.Count == 0;

    /// <summary>Seules certaines feuilles s'impriment.</summary>
    public bool SomePages => !AllPages;

    /// <summary>Recto seul.</summary>
    public bool IsOneSided => Sides == PrintSides.OneSided;

    /// <summary>Recto verso, bord long.</summary>
    public bool IsLongEdge => Sides == PrintSides.LongEdge;

    /// <summary>Recto verso, bord court.</summary>
    public bool IsShortEdge => Sides == PrintSides.ShortEdge;

    /// <summary>En portrait.</summary>
    public bool IsPortrait => !IsLandscape;

    /// <summary>Les marges sont tapées.</summary>
    public bool IsCustomMargin => Margin.Kind == PrintMargin.Custom;

    /// <summary>L'aperçu a des feuilles à montrer.</summary>
    public bool HasPages => TotalPages > 0;

    /// <summary>La feuille telle qu'elle s'imprime : le format de la fiche, l'orientation et les marges choisies ici.</summary>
    public PageLayout Layout
    {
        get
        {
            var layout = new PageLayout(_format, IsLandscape, MarginCm);
            return layout with { MarginCm = layout.EffectiveMarginCm };
        }
    }

    /// <summary>Les feuilles à imprimer, dans l'ordre ; aucune si la saisie ne se lit pas.</summary>
    public IReadOnlyList<int> PagesToPrint => AllPages ? [.. Enumerable.Range(1, Math.Max(0, TotalPages))] : PageSelection.Parse(PagesText, TotalPages) ?? [];

    /// <summary>La saisie des numéros ne se lit pas.</summary>
    public bool HasPagesError => SomePages && TotalPages > 0 && PagesToPrint.Count == 0 && !string.IsNullOrWhiteSpace(PagesText);

    /// <summary>La saisie des numéros se lit, ou attend encore : l'aide se montre.</summary>
    public bool ShowsPagesHelp => !HasPagesError;

    /// <summary>Sous le champ des numéros : comment les écrire, ou ce qui ne va pas.</summary>
    public string PagesMessage => string.Format(French, HasPagesError ? WrongFormat : HelpFormat, Math.Max(1, TotalPages));

    /// <summary>« 2 sur 4 ».</summary>
    public string PageLabel => TotalPages > 0 ? string.Format(French, SheetOfFormat, CurrentPage, TotalPages) : string.Empty;

    /// <summary>La feuille montrée fait partie de celles à imprimer.</summary>
    public bool IsCurrentPrinted => PagesToPrint.Contains(CurrentPage) || TotalPages == 0;

    /// <summary>« 3 feuilles à imprimer · 2 feuilles de papier ».</summary>
    public string Summary => string.Format(French, SummaryFormat, PagesToPrint.Count, PageSelection.PaperSheets(PagesToPrint.Count, Sides, Copies));

    /// <summary>Tout est prêt : une imprimante, des feuilles, un aperçu fini.</summary>
    public bool CanPrint => _service is not null && !string.IsNullOrEmpty(Printer) && TotalPages > 0 && PagesToPrint.Count > 0 && !IsPreparing;

    private double MarginCm => Margin.Kind switch
    {
        PrintMargin.Narrow => PageLayout.NarrowMargin,
        PrintMargin.Normal => PageLayout.NormalMargin,
        PrintMargin.Wide => PageLayout.WideMargin,
        PrintMargin.Custom => double.TryParse(CustomMargin.Replace("cm", string.Empty, StringComparison.OrdinalIgnoreCase).Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var typed) && typed > 0
            ? typed
            : _sheetMargin,
        _ => _sheetMargin,
    };

    /// <summary>Ouvre la fenêtre sur une fiche : ses réglages repartent de la mise en page de la fiche.</summary>
    /// <param name="reading">La fiche.</param>
    /// <param name="sheet">Sa mise en page ; sans fin, elle s'imprime en A4, en portrait, avec des marges normales.</param>
    public void Open(SheetReading reading, PageLayout? sheet)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var layout = sheet is { IsPaged: true } ? sheet : new PageLayout(PageFormat.A4);
        _format = layout.Format;
        _sheetMargin = layout.EffectiveMarginCm;
        _sheetTitle = reading.Title;
        Error = null;
        Title = string.Format(French, TitleFormat, reading.Title);
        MarginOptions = Options(_sheetMargin);
        Margin = MarginOptions[0];
        CustomMargin = _sheetMargin.ToString("0.##", French);
        IsLandscape = layout.Landscape;
        Copies = 1;
        AllPages = true;
        PagesText = string.Empty;
        Sides = PrintSides.OneSided;
        TotalPages = 0;
        CurrentPage = 1;
        IsPreparing = true;
        OnPropertyChanged(nameof(Layout));
        Reading = reading;
        IsOpen = true;
        _ = ReadPrintersAsync();
    }

    /// <summary>Ferme la fenêtre sans imprimer.</summary>
    public void Close()
    {
        _request++;
        IsOpen = false;
        Reading = null;
    }

    /// <summary>Envoie le travail à l'imprimante. La fenêtre se ferme s'il est parti ; sinon elle dit pourquoi.</summary>
    /// <returns>Le travail est parti.</returns>
    public bool Print()
    {
        if (!CanPrint || _service is null || Printer is not { } printer)
        {
            return false;
        }
        var pages = Paginator?.Invoke(PagesToPrint, ShowsNumbers);
        if (pages is null)
        {
            Error = PrintText.PrintFailed(PrintText.Preparing);
            return false;
        }
        Error = _service.Print(new PrintJob(printer, _sheetTitle, Layout, Sides, Copies), pages);
        if (Error is not null)
        {
            return false;
        }
        Close();
        Printed?.Invoke(string.Format(French, SentFormat, printer));
        return true;
    }

    partial void OnCopiesChanged(int value)
    {
        var bounded = Math.Clamp(value, 1, MaximumCopies);
        if (bounded != value)
        {
            Copies = bounded;
        }
    }

    partial void OnTotalPagesChanged(int value)
    {
        if (CurrentPage > Math.Max(1, value))
        {
            CurrentPage = Math.Max(1, value);
        }
    }

    /// <summary>Une copie de plus (« + ») ou de moins.</summary>
    [RelayCommand]
    private void NudgeCopies(string? direction) => Copies += direction == "+" ? 1 : -1;

    /// <summary>Toutes les feuilles (« all ») ou certaines.</summary>
    [RelayCommand]
    private void SetPages(string? choice) => AllPages = choice == "all";

    /// <summary>Recto seul (« one »), bord long (« long ») ou bord court (« short »).</summary>
    [RelayCommand]
    private void SetSides(string? choice) => Sides = choice switch
    {
        "long" => PrintSides.LongEdge,
        "short" => PrintSides.ShortEdge,
        _ => PrintSides.OneSided,
    };

    /// <summary>Paysage (« landscape ») ou portrait.</summary>
    [RelayCommand]
    private void SetOrientation(string? choice) => IsLandscape = choice == "landscape";

    /// <summary>La feuille suivante (« + ») ou précédente de l'aperçu.</summary>
    [RelayCommand]
    private void Turn(string? direction) => CurrentPage = Math.Clamp(CurrentPage + (direction == "+" ? 1 : -1), 1, Math.Max(1, TotalPages));

    private static List<PrintMarginOption> Options(double sheetMargin) =>
    [
        new(PrintMargin.Sheet, string.Format(French, OfSheetFormat, sheetMargin.ToString("0.##", French))),
        new(PrintMargin.Narrow, PrintText.MarginsNarrow),
        new(PrintMargin.Normal, PrintText.MarginsNormal),
        new(PrintMargin.Wide, PrintText.MarginsWide),
        new(PrintMargin.Custom, PrintText.MarginsCustom),
    ];

    private async Task ReadPrintersAsync()
    {
        if (_service is not { } service)
        {
            Printers = [];
            return;
        }
        var request = ++_request;
        IsLookingForPrinters = true;
        var printers = await Task.Run(service.Printers).ConfigureAwait(true);
        IsLookingForPrinters = false;
        if (request != _request)
        {
            return;
        }
        Printers = [.. printers.Select(printer => printer.Name)];
        // L'imprimante déjà choisie reste, tant qu'elle existe ; sinon celle de Windows, sinon la première.
        if (Printer is null || !Printers.Contains(Printer))
        {
            Printer = printers.FirstOrDefault(printer => printer.IsDefault)?.Name ?? (Printers.Count > 0 ? Printers[0] : null);
        }
        OnPropertyChanged(nameof(CanPrint));
    }
}
