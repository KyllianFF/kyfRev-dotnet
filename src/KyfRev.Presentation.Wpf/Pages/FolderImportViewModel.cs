using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La fenêtre « Importer un dossier de notes », comme dans la 0.x : le dossier à copier, le choix d'entrer ou non en révision,
/// puis le bilan. L'import écrit dans le coffre et ne s'interrompt pas : fermer la fenêtre le laisse finir. Le bilan cite des
/// noms de fichiers : un coffre verrouillé l'oublie, et un import parti avant ne remet rien à l'écran.
/// </summary>
public sealed partial class FolderImportViewModel : ObservableObject, IForgetsVault
{
    private readonly SheetUseCases _sheets;
    private readonly IFolderPicker _picker;

    /// <summary>Change à chaque oubli du coffre : un import parti avant ne remet rien à l'écran.</summary>
    private int _generation;

    /// <summary>La fenêtre, sur les cas d'usage des fiches ; <paramref name="picker"/> demande le dossier.</summary>
    public FolderImportViewModel(SheetUseCases sheets, IFolderPicker picker)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(picker);
        _sheets = sheets;
        _picker = picker;
    }

    /// <summary>Un dossier vient d'être importé : la vue relit ses fiches.</summary>
    public Action<ImportReport>? Imported { get; set; }

    /// <summary>La fenêtre est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>Le dossier choisi, en entier.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceName), nameof(HasSource), nameof(ChooseLabel), nameof(CanConfirm))]
    public partial string? Source { get; private set; }

    /// <summary>Le nom de ce dossier.</summary>
    public string SourceName => Source is null ? string.Empty : Path.GetFileName(Path.TrimEndingDirectorySeparator(Source)) is { Length: > 0 } name ? name : Source;

    /// <summary>Un dossier est choisi.</summary>
    public bool HasSource => Source is not null;

    /// <summary>« Choisir le dossier… », ou en changer.</summary>
    public string ChooseLabel => HasSource ? SharingText.ImportChange : SharingText.ImportChoose;

    /// <summary>Les fiches importées entrent en révision ; non par défaut, comme dans la 0.x.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopiesOnly))]
    public partial bool Tracks { get; set; }

    /// <summary>Les fiches sont seulement copiées.</summary>
    public bool CopiesOnly
    {
        get => !Tracks;
        set => Tracks = !value;
    }

    /// <summary>Un import est en cours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(CanConfirm), nameof(ConfirmLabel))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Rien n'est en cours : le dossier et le choix de révision répondent.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Pourquoi l'import a été refusé.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>L'import a été refusé.</summary>
    public bool HasError => Error is not null;

    /// <summary>Le bilan du dernier import, tant que la fenêtre le montre.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReport), nameof(IsChoosing), nameof(Summary), nameof(Follow), nameof(HasFollow), nameof(Ignored), nameof(HasIgnored), nameof(IgnoredLabel), nameof(ConfirmLabel), nameof(CancelLabel), nameof(CanConfirm))]
    public partial ImportReport? Report { get; private set; }

    /// <summary>Le bilan est montré.</summary>
    public bool HasReport => Report is not null;

    /// <summary>La fenêtre demande encore ce qu'il faut importer.</summary>
    public bool IsChoosing => !HasReport;

    /// <summary>« 3 fiches importées dans « Notes », dont 3 en révision (J1 demain). 2 liens [[…]] convertis. ».</summary>
    public string Summary => Report is { } report ? SummaryOf(report) : string.Empty;

    /// <summary>Comment mettre en révision des fiches seulement copiées ; rien si elles y sont déjà.</summary>
    public string Follow => Report is { Sheets: > 0, Tracked: 0 } ? SharingText.ReportFollow : string.Empty;

    /// <summary>Il y a de quoi le dire.</summary>
    public bool HasFollow => Follow.Length > 0;

    /// <summary>Ce qui n'a pas été repris.</summary>
    public IReadOnlyList<string> Ignored => Report?.Ignored ?? [];

    /// <summary>Quelque chose n'a pas été repris.</summary>
    public bool HasIgnored => Ignored.Count > 0;

    /// <summary>« 2 éléments non repris ».</summary>
    public string IgnoredLabel => Dates.Plural(Ignored.Count, SharingText.ReportIgnoredOneOf, SharingText.ReportIgnoredManyOf);

    /// <summary>Le libellé du bouton principal : « Importer », « Import… » pendant le travail, « Fermer » sur le bilan.</summary>
    public string ConfirmLabel => HasReport ? SharingText.Close : IsBusy ? SharingText.ImportBusy : SharingText.ImportConfirm;

    /// <summary>« Annuler » ; rien sur le bilan, qui n'a plus que « Fermer ».</summary>
    public string CancelLabel => HasReport ? string.Empty : SharingText.Cancel;

    /// <summary>Le bouton principal répond : il faut un dossier, et rien en cours.</summary>
    public bool CanConfirm => HasReport || (HasSource && !IsBusy);

    /// <summary>Ouvre la fenêtre ; un import encore en cours s'y montre, sinon elle repart à vide.</summary>
    public void Request()
    {
        if (!IsBusy)
        {
            Source = null;
            Tracks = false;
            Error = null;
            Report = null;
        }
        IsOpen = true;
    }

    /// <summary>La page qui montrait la fenêtre est quittée : elle se ferme, et un import en cours continue.</summary>
    public void Dismiss() => IsOpen = false;

    /// <summary>Oublie le dossier choisi et le bilan, et ferme la fenêtre : ce que l'import en cours dira ne reviendra pas.</summary>
    public void Forget()
    {
        _generation++;
        IsOpen = false;
        Source = null;
        Tracks = false;
        IsBusy = false;
        Error = null;
        Report = null;
    }

    private static string SummaryOf(ImportReport report)
    {
        var sheets = Dates.Plural(report.Sheets, SharingText.ReportSheetsOneOf, SharingText.ReportSheetsManyOf);
        var tracked = report.Tracked > 0 ? SharingText.ReportTrackedOf(report.Tracked) : string.Empty;
        var parts = new List<string> { $"{SharingText.ReportInOf(sheets, report.Folder)}{tracked}." };
        if (report.Links > 0)
        {
            parts.Add(Dates.Plural(report.Links, SharingText.ReportLinksOneOf, SharingText.ReportLinksManyOf));
        }
        if (report.Resources > 0)
        {
            parts.Add(Dates.Plural(report.Resources, SharingText.ReportResourcesOneOf, SharingText.ReportResourcesManyOf));
        }
        return string.Join(' ', parts);
    }

    /// <summary>Le dossier à importer ; renoncer garde le précédent.</summary>
    [RelayCommand]
    private void Choose()
    {
        Error = null;
        var generation = _generation;
        if (_picker.Pick(SharingText.ImportPick) is { } folder && generation == _generation)
        {
            Source = folder;
        }
    }

    /// <summary>La réponse de la fenêtre : le bouton principal lance l'import, qui s'y déroule ; le reste la ferme.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AnswerAsync(bool confirmed)
    {
        IsOpen = false;
        if (!confirmed || HasReport)
        {
            return;
        }
        // La fenêtre se referme d'elle-même sur son bouton ; elle reste ouverte, c'est là que le bilan se lit.
        IsOpen = true;
        if (Source is not { } source || IsBusy)
        {
            return;
        }
        var (tracks, generation) = (Tracks, _generation);
        IsBusy = true;
        Error = null;
        var outcome = await Task.Run(() => _sheets.Import(source, tracks)).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant l'import : ni son bilan ni son refus ne reviennent.
            return;
        }
        IsBusy = false;
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            return;
        }
        Report = outcome.Value;
        Imported?.Invoke(outcome.Value);
    }
}
