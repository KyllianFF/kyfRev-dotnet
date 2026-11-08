using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Shell;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Les façons de partager une fiche.</summary>
public enum ShareKind
{
    /// <summary>Une page web d'un seul fichier.</summary>
    WebPage,

    /// <summary>Un PDF.</summary>
    Pdf,

    /// <summary>Le texte Markdown.</summary>
    Markdown,

    /// <summary>La fiche et ses images, dans une archive.</summary>
    Archive,

    /// <summary>Le texte brut.</summary>
    Text,

    /// <summary>La page et le texte, au presse-papiers.</summary>
    Clipboard,
}

/// <summary>Un format proposé par la fenêtre « Partager ».</summary>
public sealed partial class ShareChoice : ObservableObject
{
    private readonly Action<ShareChoice> _chosen;

    internal ShareChoice(ShareKind kind, string label, string help, Action<ShareChoice> chosen)
    {
        Kind = kind;
        Label = label;
        Help = help;
        _chosen = chosen;
    }

    /// <summary>Ce qu'il fait.</summary>
    public ShareKind Kind { get; }

    /// <summary>Son nom.</summary>
    public string Label { get; }

    /// <summary>Ce qu'il donne.</summary>
    public string Help { get; }

    /// <summary>Il est choisi.</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    partial void OnIsCheckedChanged(bool value)
    {
        if (value)
        {
            _chosen(this);
        }
    }
}

/// <summary>
/// La fenêtre « Partager » et la question « Enregistrer comme modèle », communes à la vue « Fiches » et à la lecture d'une fiche,
/// comme dans la 0.x. Une seule demande compte à la fois : une nouvelle annule l'ancienne. Ce qui est lu d'un coffre verrouillé est
/// oublié, et une demande partie avant n'écrit plus rien.
/// </summary>
public sealed partial class SheetSharingViewModel : ObservableObject, IForgetsVault
{
    private readonly SheetUseCases _sheets;
    private readonly TemplateUseCases _templates;
    private readonly VaultUseCases _vaults;
    private readonly IFolderPicker _picker;
    private readonly SharedSheetPictures _pictures;
    private readonly ISheetPrinter _printer;
    private readonly TimeProvider _time;
    private readonly Action<string, string, bool> _copy;
    private CancellationTokenSource? _work;
    private string _sheetId = string.Empty;
    private string _templateSheet = string.Empty;
    private Action<string>? _templateDone;

    /// <summary>Change à chaque oubli du coffre : une demande partie avant n'y remet rien.</summary>
    private int _generation;

    /// <summary>La fenêtre, sur les cas d'usage du coffre ; <paramref name="copy"/> va au presse-papiers (page, texte, contenu privé).</summary>
    public SheetSharingViewModel(
        SheetUseCases sheets,
        TemplateUseCases templates,
        VaultUseCases vaults,
        IFolderPicker picker,
        SharedSheetPictures pictures,
        ISheetPrinter printer,
        TimeProvider time,
        Action<string, string, bool>? copy = null)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(vaults);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(pictures);
        ArgumentNullException.ThrowIfNull(printer);
        ArgumentNullException.ThrowIfNull(time);
        _sheets = sheets;
        _templates = templates;
        _vaults = vaults;
        _picker = picker;
        _pictures = pictures;
        _printer = printer;
        _time = time;
        _copy = copy ?? SharedClipboard.Copy;
        Choices =
        [
            new(ShareKind.WebPage, SharingText.FormatHtml, SharingText.FormatHtmlHelp, Choose),
            new(ShareKind.Pdf, SharingText.FormatPdf, SharingText.FormatPdfHelp, Choose),
            new(ShareKind.Markdown, SharingText.FormatMarkdown, SharingText.FormatMarkdownHelp, Choose),
            new(ShareKind.Archive, SharingText.FormatArchive, SharingText.FormatArchiveHelp, Choose),
            new(ShareKind.Text, SharingText.FormatText, SharingText.FormatTextHelp, Choose),
            new(ShareKind.Clipboard, SharingText.FormatCopy, SharingText.FormatCopyHelp, Choose),
        ];
        Choices[0].IsChecked = true;
    }

    /// <summary>Les formats, dans l'ordre de la 0.x.</summary>
    public IReadOnlyList<ShareChoice> Choices { get; }

    /// <summary>Le format choisi.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionLabel))]
    public partial ShareChoice? Selected { get; private set; }

    /// <summary>La fenêtre « Partager » est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsSharing { get; private set; }

    /// <summary>« Partager « Mitose » ».</summary>
    [ObservableProperty]
    public partial string ShareTitle { get; private set; } = string.Empty;

    /// <summary>Un partage est en cours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Rien n'est en cours : les formats et les boutons répondent.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Le libellé du bouton principal, selon le format.</summary>
    public string ActionLabel => Selected?.Kind == ShareKind.Clipboard ? SharingText.ActionCopy : SharingText.ActionSave;

    /// <summary>Le fichier créé, ou ce qui vient d'être copié.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage), nameof(CloseLabel))]
    public partial string? Message { get; private set; }

    /// <summary>Un partage vient d'aboutir.</summary>
    public bool HasMessage => Message is not null;

    /// <summary>« Fermer » une fois quelque chose partagé, « Annuler » avant.</summary>
    public string CloseLabel => HasMessage ? SharingText.Close : SharingText.Cancel;

    /// <summary>Ce qui a empêché le partage.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Le partage a été refusé.</summary>
    public bool HasError => Error is not null;

    /// <summary>Le coffre ouvert est chiffré : ce qui en sort est en clair, et la fenêtre le dit.</summary>
    [ObservableProperty]
    public partial bool IsEncrypted { get; private set; }

    /// <summary>La question « Nom du modèle » est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsNamingTemplate { get; private set; }

    /// <summary>Ce qui est écrit dans le champ du nom.</summary>
    [ObservableProperty]
    public partial string TemplateName { get; set; } = string.Empty;

    /// <summary>Pourquoi le modèle n'a pas été enregistré.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTemplateError))]
    public partial string? TemplateError { get; private set; }

    /// <summary>Le modèle a été refusé.</summary>
    public bool HasTemplateError => TemplateError is not null;

    /// <summary>Une de ses fenêtres est ouverte : ce qui a sa propre fenêtre, comme le texte mis en forme, doit le savoir.</summary>
    public bool IsShowing => IsSharing || IsNamingTemplate;

    /// <summary>Ouvre la fenêtre « Partager » sur cette fiche.</summary>
    /// <param name="sheetId">La fiche.</param>
    /// <param name="title">Son titre.</param>
    public void RequestShare(string sheetId, string title)
    {
        Renew();
        _sheetId = sheetId;
        ShareTitle = SharingText.ShareTitleOf(title);
        Message = null;
        Error = null;
        IsBusy = false;
        IsEncrypted = _vaults.State().Encrypted;
        IsSharing = true;
    }

    /// <summary>Ouvre la question du nom d'un modèle, le titre de la fiche proposé.</summary>
    /// <param name="sheetId">La fiche.</param>
    /// <param name="title">Son titre.</param>
    /// <param name="done">Dit où a été enregistré le modèle, là où la demande a été faite.</param>
    public void RequestSaveAsTemplate(string sheetId, string title, Action<string> done)
    {
        _templateSheet = sheetId;
        _templateDone = done;
        TemplateName = title;
        TemplateError = null;
        IsNamingTemplate = true;
    }

    /// <summary>La page qui montrait ces fenêtres est quittée : elles se ferment avec elle, et ce qui était en cours s'arrête.</summary>
    public void Dismiss()
    {
        Renew();
        IsSharing = false;
        IsBusy = false;
        Message = null;
        Error = null;
        IsNamingTemplate = false;
        TemplateError = null;
    }

    /// <summary>
    /// Oublie la fiche visée, ce que la fenêtre montre, le nom écrit, et annule ce qui est en cours : un partage parti avant
    /// n'écrit plus rien. Le format choisi est un réglage de la fenêtre, pas du coffre : il reste.
    /// </summary>
    public void Forget()
    {
        _generation++;
        Renew();
        _sheetId = string.Empty;
        _templateSheet = string.Empty;
        _templateDone = null;
        IsSharing = false;
        ShareTitle = string.Empty;
        IsBusy = false;
        Message = null;
        Error = null;
        IsEncrypted = false;
        IsNamingTemplate = false;
        TemplateName = string.Empty;
        TemplateError = null;
    }

    partial void OnIsSharingChanged(bool value) => OnPropertyChanged(nameof(IsShowing));

    partial void OnIsNamingTemplateChanged(bool value) => OnPropertyChanged(nameof(IsShowing));

    /// <summary>La réponse de la fenêtre « Partager » : le bouton principal lance le partage, qui s'y déroule ; le reste la ferme.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AnswerShareAsync(bool confirmed)
    {
        IsSharing = false;
        if (!confirmed)
        {
            Renew();
            IsBusy = false;
            Message = null;
            Error = null;
            return;
        }
        // La fenêtre se referme d'elle-même sur son bouton ; elle reste ouverte, c'est là que le résultat se lit.
        IsSharing = true;
        if (IsBusy || Selected is not { } choice)
        {
            return;
        }
        var work = Renew();
        var generation = _generation;
        Message = null;
        Error = null;
        var folder = choice.Kind == ShareKind.Clipboard ? null : _picker.Pick(SharingText.PickFolder);
        if (choice.Kind != ShareKind.Clipboard && folder is null)
        {
            return;
        }
        IsBusy = true;
        try
        {
            await ShareAsync(choice.Kind, _sheetId, folder ?? string.Empty, generation, work.Token).ConfigureAwait(true);
        }
        finally
        {
            if (!work.IsCancellationRequested)
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>La réponse à la question du nom : un refus la laisse ouverte, avec sa raison.</summary>
    [RelayCommand]
    private async Task AnswerTemplateAsync(bool confirmed)
    {
        IsNamingTemplate = false;
        if (!confirmed)
        {
            TemplateError = null;
            _templateDone = null;
            return;
        }
        var name = TemplateName.Trim();
        if (name.Length == 0)
        {
            TemplateError = SheetActionsText.NameRequired;
            IsNamingTemplate = true;
            return;
        }
        var (sheetId, done, generation) = (_templateSheet, _templateDone, _generation);
        var outcome = await Task.Run(() => _templates.SaveAsTemplate(sheetId, name)).ConfigureAwait(true);
        if (generation != _generation)
        {
            // Le coffre a été oublié pendant l'enregistrement : ni son nom ni son refus ne reviennent.
            return;
        }
        if (!outcome.Succeeded)
        {
            TemplateError = outcome.Error;
            IsNamingTemplate = true;
            return;
        }
        TemplateError = null;
        _templateDone = null;
        done?.Invoke(SharingText.TemplateDoneOf(outcome.Value));
    }

    private void Choose(ShareChoice choice)
    {
        Selected = choice;
        foreach (var other in Choices.Where(other => !ReferenceEquals(other, choice)))
        {
            other.IsChecked = false;
        }
    }

    /// <summary>Annule ce qui est en cours, et rend de quoi annuler ce qui commence.</summary>
    private CancellationTokenSource Renew()
    {
        _work?.Cancel();
        _work?.Dispose();
        var work = new CancellationTokenSource();
        _work = work;
        return work;
    }

    private async Task ShareAsync(ShareKind kind, string sheetId, string folder, int generation, CancellationToken cancellation)
    {
        var result = kind switch
        {
            ShareKind.Markdown => await SaveAsync(sheetId, ShareFormat.Markdown, folder, null, cancellation).ConfigureAwait(true),
            ShareKind.Archive => await SaveAsync(sheetId, ShareFormat.MarkdownWithImages, folder, null, cancellation).ConfigureAwait(true),
            ShareKind.Text => await SaveTextAsync(sheetId, folder, cancellation).ConfigureAwait(true),
            ShareKind.WebPage => await SavePageAsync(sheetId, folder, cancellation).ConfigureAwait(true),
            ShareKind.Pdf => await SavePdfAsync(sheetId, folder, cancellation).ConfigureAwait(true),
            _ => await CopyAsync(sheetId, cancellation).ConfigureAwait(true),
        };
        if (generation != _generation || cancellation.IsCancellationRequested)
        {
            // Le coffre a été oublié, ou la demande dépassée : ni son chemin ni son refus ne se montrent.
            return;
        }
        Message = result.Message;
        Error = result.Refusal;
    }

    /// <summary>Écrit le fichier, sauf si la demande est dépassée d'ici là : une demande annulée ne laisse rien derrière elle.</summary>
    private async Task<ShareResult> SaveAsync(string sheetId, ShareFormat format, string folder, string? content, CancellationToken cancellation)
    {
        var outcome = await Task.Run(() => cancellation.IsCancellationRequested ? null : _sheets.Share(sheetId, format, folder, content)).ConfigureAwait(true);
        return ShareResult.Of(outcome);
    }

    private async Task<ShareResult> SaveTextAsync(string sheetId, string folder, CancellationToken cancellation)
    {
        var prepared = await PrepareAsync(sheetId, drawing: false, cancellation).ConfigureAwait(true);
        if (prepared.Root is not { } root)
        {
            return new ShareResult(null, prepared.Refusal);
        }
        var text = await Task.Run(() => SharedSheet.PlainText(root, prepared.Facts, prepared.Drawings)).ConfigureAwait(true);
        return await SaveAsync(sheetId, ShareFormat.Text, folder, text, cancellation).ConfigureAwait(true);
    }

    private async Task<ShareResult> SavePageAsync(string sheetId, string folder, CancellationToken cancellation)
    {
        var prepared = await PrepareAsync(sheetId, drawing: true, cancellation).ConfigureAwait(true);
        if (prepared.Root is not { } root)
        {
            return new ShareResult(null, prepared.Refusal);
        }
        var page = await Task.Run(() => SharedSheet.Page(root, prepared.Facts, prepared.Drawings)).ConfigureAwait(true);
        return await SaveAsync(sheetId, ShareFormat.Html, folder, page, cancellation).ConfigureAwait(true);
    }

    private async Task<ShareResult> SavePdfAsync(string sheetId, string folder, CancellationToken cancellation)
    {
        var (page, refusal) = await WholePageAsync(sheetId, cancellation).ConfigureAwait(true);
        if (page is null)
        {
            return new ShareResult(null, refusal);
        }
        var pdf = await _printer.PrintAsync(page, cancellation).ConfigureAwait(true);
        if (pdf is null)
        {
            return new ShareResult(null, cancellation.IsCancellationRequested ? null : SharingText.PdfFailed);
        }
        var outcome = await Task.Run(() => cancellation.IsCancellationRequested ? null : _sheets.SharePdf(sheetId, folder, pdf)).ConfigureAwait(true);
        return ShareResult.Of(outcome);
    }

    private async Task<ShareResult> CopyAsync(string sheetId, CancellationToken cancellation)
    {
        var prepared = await PrepareAsync(sheetId, drawing: true, cancellation).ConfigureAwait(true);
        if (prepared.Root is not { } root)
        {
            return new ShareResult(null, prepared.Refusal);
        }
        var (embedded, text) = await Task.Run(() =>
        {
            var whole = _sheets.EmbedSharedImages(SharedSheet.Page(root, prepared.Facts, prepared.Drawings));
            return (whole, SharedSheet.PlainText(root, prepared.Facts, prepared.Drawings));
        }).ConfigureAwait(true);
        if (!embedded.Succeeded)
        {
            return new ShareResult(null, embedded.Error);
        }
        if (cancellation.IsCancellationRequested)
        {
            return new ShareResult(null, null);
        }
        try
        {
            _copy(embedded.Value, text, IsEncrypted);
            return new ShareResult(SharingText.Copied, null);
        }
        catch (ExternalException)
        {
            return new ShareResult(null, SharingText.CopyFailed);
        }
    }

    /// <summary>La page de la fiche, ses images du coffre intégrées.</summary>
    private async Task<(string? Page, string? Refusal)> WholePageAsync(string sheetId, CancellationToken cancellation)
    {
        var prepared = await PrepareAsync(sheetId, drawing: true, cancellation).ConfigureAwait(true);
        if (prepared.Root is not { } root)
        {
            return (null, prepared.Refusal);
        }
        var embedded = await Task.Run(() => _sheets.EmbedSharedImages(SharedSheet.Page(root, prepared.Facts, prepared.Drawings))).ConfigureAwait(true);
        return embedded.Succeeded ? (embedded.Value, null) : (null, embedded.Error);
    }

    /// <summary>La fiche telle qu'elle se lit : son contenu, hors du fil de l'interface, et ce qui s'y dessine. L'ouvrir ne valide rien.</summary>
    private async Task<PreparedSheet> PrepareAsync(string sheetId, bool drawing, CancellationToken cancellation)
    {
        var (opened, root) = await Task.Run(() =>
        {
            var sheet = _sheets.Open(sheetId);
            return (sheet, sheet.Succeeded ? SheetRenderer.Render(sheet.Value.Body, sheet.Value.Title) : null);
        }).ConfigureAwait(true);
        if (root is null)
        {
            return new PreparedSheet(null, new SharedFacts(string.Empty, string.Empty, default), SharedDrawings.None, opened.Error);
        }
        var drawings = await _pictures.DrawAsync(root, drawing, cancellation).ConfigureAwait(true);
        var facts = new SharedFacts(opened.Value.Title, ContentTargets.FolderOf(opened.Value.Path), Present.Of(_time).Now.Day);
        return drawings is null ? new PreparedSheet(null, facts, SharedDrawings.None, null) : new PreparedSheet(root, facts, drawings, null);
    }

    private sealed record PreparedSheet(RenderElement? Root, SharedFacts Facts, SharedDrawings Drawings, string? Refusal);

    private sealed record ShareResult(string? Message, string? Refusal)
    {
        /// <summary>Le fichier créé, le refus, ou rien pour une demande dépassée.</summary>
        public static ShareResult Of(Outcome<string>? outcome) => outcome switch
        {
            null => new(null, null),
            { Succeeded: true } => new(SharingText.CreatedOf(outcome.Value), null),
            _ => new(null, outcome.Error),
        };
    }
}
