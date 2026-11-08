using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Une fiche qui mène à la fiche ouverte, dans son panneau.</summary>
public sealed class BacklinkEntry
{
    internal BacklinkEntry(Backlink backlink)
    {
        SheetId = backlink.SheetId;
        Title = backlink.Title;
        Folder = ReadingText.FolderPath(backlink.Folder);
        IsArchived = backlink.Archived;
        Excerpt = backlink.Excerpt;
        SeveralLinks = backlink.Links > 1 ? ReadingText.LinksOf(backlink.Links) : null;
    }

    /// <summary>Son identifiant.</summary>
    public string SheetId { get; }

    /// <summary>Son titre.</summary>
    public string Title { get; }

    /// <summary>« Histoire › XVIIIe », vide à la racine.</summary>
    public string Folder { get; }

    /// <summary>Elle est dans un dossier.</summary>
    public bool HasFolder => Folder.Length > 0;

    /// <summary>Elle est archivée.</summary>
    public bool IsArchived { get; }

    /// <summary>Le texte autour de son premier lien ; le texte du lien ressort.</summary>
    public IReadOnlyList<ExcerptPart> Excerpt { get; }

    /// <summary>« 2 liens » ; rien pour un seul.</summary>
    public string? SeveralLinks { get; }

    /// <summary>Elle a plusieurs liens vers la fiche ouverte.</summary>
    public bool HasSeveralLinks => SeveralLinks is not null;

    /// <summary>Tout ce que montre l'entrée, pour le lecteur d'écran.</summary>
    public string Spoken => string.Join(
        ", ",
        new[] { Title, Folder, IsArchived ? ReadingText.Archived : string.Empty, SeveralLinks ?? string.Empty, string.Concat(Excerpt.Select(part => part.Text)) }.Where(text => text.Length > 0));
}

/// <summary>
/// L'aperçu d'une fiche liée, comme dans la 0.x (<c>ApercuFiche.svelte</c>) : son dossier, son titre, ses étiquettes, le
/// début de son texte et son étape. Il est lu quand il est demandé, jamais gardé : ouvert de nouveau, il dit ce que la fiche
/// est devenue ; et une demande plus récente, ou le pointeur qui s'en va, annule la lecture en cours.
/// </summary>
public sealed partial class LinkPreviewViewModel : ObservableObject
{
    private readonly Func<string, Outcome<SheetPreview?>> _read;
    private readonly Action<string> _open;
    private CancellationTokenSource? _request;

    /// <summary>Un aperçu lu par <paramref name="read"/>, qui ouvre une fiche par <paramref name="open"/>.</summary>
    public LinkPreviewViewModel(Func<string, Outcome<SheetPreview?>> read, Action<string> open)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(open);
        _read = read;
        _open = open;
    }

    /// <summary>La fiche montrée ; rien tant que l'aperçu est fermé.</summary>
    [ObservableProperty]
    public partial SheetPreview? Shown { get; private set; }

    /// <summary>L'aperçu est ouvert.</summary>
    public bool IsOpen => Shown is not null;

    /// <summary>« Histoire › XVIIIe ».</summary>
    public string Folder => ReadingText.FolderPath(Shown?.Folder ?? string.Empty);

    /// <summary>La fiche est dans un dossier.</summary>
    public bool HasFolder => Folder.Length > 0;

    /// <summary>Son titre.</summary>
    public string Title => Shown?.Title ?? string.Empty;

    /// <summary>« #bio  #cellule ».</summary>
    public string Tags => string.Join("  ", (Shown?.Tags ?? []).Select(tag => $"#{tag}"));

    /// <summary>Elle a des étiquettes.</summary>
    public bool HasTags => Tags.Length > 0;

    /// <summary>Le début de son texte, ou ce qui dit qu'elle est vide.</summary>
    public string Excerpt => Shown is { Excerpt.Length: > 0 } preview ? preview.Excerpt : ReadingText.PreviewEmpty;

    /// <summary>Elle n'a pas encore de texte.</summary>
    public bool IsEmpty => Shown is { Excerpt.Length: 0 };

    /// <summary>Elle a un début de texte.</summary>
    public bool HasText => Shown is { Excerpt.Length: > 0 };

    /// <summary>Elle est archivée.</summary>
    public bool IsArchived => Shown?.Archived == true;

    /// <summary>« J7 · prévue ven. 9 oct. », pour une fiche active.</summary>
    public string Step => Shown switch
    {
        null or { Archived: true } or { Step: null } => string.Empty,
        { Step: var step, DueDate: { } day } => ReadingText.PreviewDue(step, Dates.Short(day)),
        { Step: var step } => step,
    };

    /// <summary>Elle attend une étape.</summary>
    public bool HasStep => Step.Length > 0;

    /// <summary>Le nom du bouton qui ouvre la fiche.</summary>
    public string OpenName => ReadingText.PreviewOpen(Title);

    /// <summary>Ce que dit l'aperçu, pour le lecteur d'écran : la description du lien.</summary>
    public string Description => string.Join(
        ". ",
        new[] { Folder, Title, Tags, Excerpt, IsArchived ? ReadingText.Archived : Step }.Where(text => text.Length > 0));

    /// <summary>Lit la fiche située à <paramref name="path"/> et la montre ; la demande précédente s'arrête.</summary>
    /// <returns>Faux si la demande a été annulée ou remplacée entre-temps.</returns>
    public async Task<bool> ShowAsync(string path)
    {
        Stop();
        var request = new CancellationTokenSource();
        _request = request;
        var token = request.Token;
        Outcome<SheetPreview?> outcome;
        try
        {
            outcome = await Task.Run(() => _read(path), token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        if (token.IsCancellationRequested)
        {
            return false;
        }
        Shown = outcome.Succeeded ? outcome.Value : null;
        return true;
    }

    /// <summary>Referme l'aperçu et annule la lecture en cours.</summary>
    public void Hide()
    {
        Stop();
        Shown = null;
    }

    partial void OnShownChanged(SheetPreview? value) => OnPropertyChanged(string.Empty);

    /// <summary>Ouvre la fiche montrée.</summary>
    [RelayCommand]
    private void Open()
    {
        if (Shown is { } preview)
        {
            Hide();
            _open(preview.Id);
        }
    }

    private void Stop()
    {
        _request?.Cancel();
        _request?.Dispose();
        _request = null;
    }
}
