using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes des masques d'une image.</summary>
public static class ImageMaskText
{
    /// <summary>L'entrée du clic droit sur une image.</summary>
    public const string Action = "Masquer des zones…";

    /// <summary>Le titre de la fenêtre.</summary>
    public const string Title = "Masquer des zones de l'image";

    /// <summary>Ce que la fenêtre explique.</summary>
    public const string Help = "Tracez un rectangle sur chaque zone à retrouver : une étiquette, une région, un nom. En révision, ces zones sont masquées et se dévoilent une à une. Un clic sur un rectangle le retire.";

    /// <summary>Le bouton qui enregistre.</summary>
    public const string Save = "Enregistrer";

    /// <summary>Retire le dernier rectangle tracé.</summary>
    public const string Undo = "Retirer le dernier";

    /// <summary>Retire tous les rectangles.</summary>
    public const string Clear = "Tout retirer";

    /// <summary>La surface où l'on trace, pour le lecteur d'écran.</summary>
    public const string Surface = "Image à masquer : tracez les zones à la souris";

    /// <summary>L'image ne peut pas porter de masques.</summary>
    public const string CannotWrite = "Cette image ne peut pas porter de masques ici : elle est écrite par référence, ou elle a déjà un titre. Écrivez-la « ![texte](image) » dans la fiche, sans titre, puis recommencez.";

    /// <summary>La fiche a changé pendant la pose des masques.</summary>
    public const string Conflict = "La fiche a changé pendant que vous posiez les masques : rien n'a été enregistré. Fermez, puis recommencez.";

    /// <summary>L'image a disparu de la fiche.</summary>
    public const string Gone = "Cette image n'est plus dans la fiche : rien n'a été enregistré.";

    /// <summary>Ce qui vient d'être fait.</summary>
    public static string Saved(int count) => count switch
    {
        0 => "Masques retirés : l'image se voit entière en révision.",
        1 => "1 zone masquée sur cette image, en révision.",
        _ => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{count} zones masquées sur cette image, en révision."),
    };

    /// <summary>Combien de zones sont tracées.</summary>
    public static string Count(int count) => count switch
    {
        0 => "Aucune zone",
        1 => "1 zone",
        _ => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{count} zones"),
    };
}

/// <summary>
/// La pose des masques d'une image : on trace des rectangles sur elle, et ils s'écrivent dans son titre, dans le Markdown
/// de la fiche (voir <see cref="ImageMasks"/>). La fiche est relue à l'ouverture et enregistrée avec son empreinte : si
/// elle a changé entre-temps, rien n'est écrit.
/// </summary>
public sealed partial class ImageMaskEditorViewModel : ObservableObject
{
    private readonly SheetUseCases _sheets;
    private readonly EditingUseCases _editing;

    /// <summary>Une session d'édition par fenêtre : la fiche d'avant est gardée en version une fois.</summary>
    private readonly ulong _session = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong))) >> 16;

    private string _sheetId = string.Empty;
    private string _title = string.Empty;
    private string _body = string.Empty;
    private string _fingerprint = string.Empty;
    private int _imageIndex;

    /// <summary>La pose des masques, sur les cas d'usage qui lisent et enregistrent une fiche.</summary>
    public ImageMaskEditorViewModel(SheetUseCases sheets, EditingUseCases editing)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(editing);
        _sheets = sheets;
        _editing = editing;
        Masks.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Summary));
            UndoLastCommand.NotifyCanExecuteChanged();
            ClearCommand.NotifyCanExecuteChanged();
        };
    }

    /// <summary>Les masques viennent d'être enregistrés : la fiche est à relire, et <see cref="LastSaved"/> dit ce qui a été fait.</summary>
    public event EventHandler? Saved;

    /// <summary>Ce que le dernier enregistrement a fait, à dire à l'utilisateur.</summary>
    public string LastSaved { get; private set; } = string.Empty;

    /// <summary>Les zones tracées, dans l'ordre où elles l'ont été.</summary>
    public ObservableCollection<ImageMask> Masks { get; } = [];

    /// <summary>La fenêtre est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>L'image, telle qu'elle est affichée dans la fiche.</summary>
    [ObservableProperty]
    public partial ImageSource? Picture { get; private set; }

    /// <summary>Ce qui empêche d'enregistrer ; rien si tout va bien.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Quelque chose empêche d'enregistrer.</summary>
    public bool HasError => Error is not null;

    /// <summary>L'image peut porter des masques : on peut tracer et enregistrer.</summary>
    [ObservableProperty]
    public partial bool CanDraw { get; private set; }

    /// <summary>« 3 zones ».</summary>
    public string Summary => ImageMaskText.Count(Masks.Count);

    private bool HasMasks => Masks.Count > 0;

    /// <summary>
    /// Ouvre la fenêtre sur l'image de rang <paramref name="imageIndex"/> de la fiche, avec les masques qu'elle a déjà. La
    /// fiche est relue : c'est ce texte-là, et son empreinte, qui seront enregistrés.
    /// </summary>
    public async Task OpenAsync(string sheetId, int imageIndex, ImageSource? picture)
    {
        ArgumentNullException.ThrowIfNull(sheetId);
        var opened = await Task.Run(() => _sheets.Open(sheetId)).ConfigureAwait(true);
        Masks.Clear();
        Picture = picture;
        Error = null;
        CanDraw = false;
        if (!opened.Succeeded)
        {
            Error = opened.Error;
            IsOpen = true;
            return;
        }
        (_sheetId, _title, _body, _fingerprint, _imageIndex) = (sheetId, opened.Value.Title, opened.Value.Body, opened.Value.Fingerprint, imageIndex);
        var images = ImageMaskWriter.Images(SheetRenderer.Render(_body, _title));
        if (imageIndex < 0 || imageIndex >= images.Count)
        {
            Error = ImageMaskText.Gone;
            IsOpen = true;
            return;
        }
        foreach (var mask in images[imageIndex].Masks)
        {
            Masks.Add(mask);
        }
        // Dit tout de suite, avant qu'on ait tracé pour rien : la même écriture sera refaite à l'enregistrement.
        CanDraw = ImageMaskWriter.Write(_body, _title, imageIndex, [.. Masks]) is not null;
        Error = CanDraw ? null : ImageMaskText.CannotWrite;
        IsOpen = true;
    }

    /// <summary>
    /// Ajoute une zone tracée, en parts de l'image ; elle est ramenée dans l'image, et refusée si elle est trop petite pour
    /// être vue ou si l'image en a déjà autant qu'elle peut en porter.
    /// </summary>
    /// <returns>Vrai si la zone est gardée.</returns>
    public bool Add(ImageMask traced)
    {
        if (!CanDraw || Masks.Count >= ImageMasks.Maximum || ImageMasks.Normalize(traced) is not { } mask)
        {
            return false;
        }
        Masks.Add(mask);
        return true;
    }

    /// <summary>Retire la zone de rang <paramref name="index"/>.</summary>
    public void RemoveAt(int index)
    {
        if (index >= 0 && index < Masks.Count)
        {
            Masks.RemoveAt(index);
        }
    }

    /// <summary>Retire la dernière zone tracée.</summary>
    [RelayCommand(CanExecute = nameof(HasMasks))]
    private void UndoLast() => RemoveAt(Masks.Count - 1);

    /// <summary>Retire toutes les zones.</summary>
    [RelayCommand(CanExecute = nameof(HasMasks))]
    private void Clear() => Masks.Clear();

    /// <summary>La réponse de la fenêtre : les masques s'écrivent dans la fiche ; un refus laisse la fenêtre ouverte, avec sa raison.</summary>
    [RelayCommand]
    private async Task AnswerAsync(bool confirmed)
    {
        IsOpen = false;
        if (!confirmed || !CanDraw)
        {
            Error = null;
            Picture = null;
            return;
        }
        var masks = Masks.ToList();
        if (ImageMaskWriter.Write(_body, _title, _imageIndex, masks) is not { } written)
        {
            Error = ImageMaskText.CannotWrite;
            IsOpen = true;
            return;
        }
        var (id, fingerprint) = (_sheetId, _fingerprint);
        var outcome = await Task.Run(() => _editing.Save(id, written, fingerprint, _session)).ConfigureAwait(true);
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            IsOpen = true;
            return;
        }
        if (outcome.Value is not SheetSaved)
        {
            Error = ImageMaskText.Conflict;
            CanDraw = false;
            IsOpen = true;
            return;
        }
        Error = null;
        Picture = null;
        LastSaved = ImageMaskText.Saved(masks.Count);
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
