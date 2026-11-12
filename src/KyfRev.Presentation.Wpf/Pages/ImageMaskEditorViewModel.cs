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

    /// <summary>Ce que la fenêtre explique à qui n'a pas de souris.</summary>
    public const string KeyboardHelp = "Au clavier, sur l'image : Inser ajoute une zone, les flèches la déplacent, Maj et les flèches changent sa taille, Page précédente et Page suivante passent d'une zone à l'autre, Suppr la retire.";

    /// <summary>Le bouton qui ajoute une zone sans la tracer.</summary>
    public const string Add = "Ajouter une zone";

    /// <summary>Le bouton qui enregistre.</summary>
    public const string Save = "Enregistrer";

    /// <summary>Retire le dernier rectangle tracé.</summary>
    public const string Undo = "Retirer le dernier";

    /// <summary>Retire tous les rectangles.</summary>
    public const string Clear = "Tout retirer";

    /// <summary>La surface où l'on trace, pour le lecteur d'écran.</summary>
    public const string Surface = "Image à masquer : tracez les zones à la souris, ou ajoutez-les avec Inser et placez-les avec les flèches";

    /// <summary>L'image ne peut pas porter de masques.</summary>
    public const string CannotWrite = "Cette image ne peut pas porter de masques ici : elle est écrite par référence, ou elle a déjà un titre. Écrivez-la « ![texte](image) » dans la fiche, sans titre, puis recommencez.";

    /// <summary>La fiche a changé pendant la pose des masques.</summary>
    public const string Conflict = "La fiche a changé pendant que vous posiez les masques : rien n'a été enregistré. Fermez, puis recommencez.";

    /// <summary>Le titre de la fenêtre de rognage.</summary>
    public const string CropTitle = "Rogner l'image";

    /// <summary>Ce que la fenêtre de rognage explique.</summary>
    public const string CropHelp = "Tirez les poignées des bords et des angles pour garder la partie voulue, ou faites glisser le cadre pour le déplacer : ce qui est assombri ne se montre plus dans la fiche. Le fichier n'est pas modifié, et « Tout retirer » rend l'image entière. Changer le cadre retire les zones masquées, qui n'y seraient plus à leur place.";

    /// <summary>L'image est rognée.</summary>
    public const string Cropped = "Image rognée : le fichier d'origine est intact.";

    /// <summary>L'image est de nouveau entière.</summary>
    public const string Uncropped = "L'image se montre de nouveau entière.";

    /// <summary>L'image ne se lit pas : on ne peut rien tracer dessus.</summary>
    public const string Unreadable = "Cette image ne peut pas s'afficher ici : on ne peut pas y tracer de zones.";

    /// <summary>L'image a disparu de la fiche.</summary>
    public const string Gone = "Cette image n'est plus dans la fiche : rien n'a été enregistré.";

    /// <summary>Ce qui vient d'être fait.</summary>
    public static string Saved(int count) => count switch
    {
        0 => "Masques retirés : l'image se voit entière en révision.",
        1 => "1 zone masquée sur cette image, en révision.",
        _ => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{count} zones masquées sur cette image, en révision."),
    };

    /// <summary>La zone choisie, pour qui ne la voit pas : son rang, sa place et sa taille, en pourcentages de l'image.</summary>
    public static string Selected(int rank, int count, ImageMask mask) => string.Create(
        System.Globalization.CultureInfo.GetCultureInfo("fr-FR"),
        $"Zone {rank} sur {count} : à {mask.X * 100:0.#} % du bord gauche et {mask.Y * 100:0.#} % du haut, large de {mask.Width * 100:0.#} %, haute de {mask.Height * 100:0.#} %.");

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
    /// <summary>Le côté d'une zone ajoutée sans être tracée, en part de l'image.</summary>
    private const double DefaultSide = 0.2;

    private readonly SheetUseCases _sheets;
    private readonly EditingUseCases _editing;

    /// <summary>Une session d'édition par fenêtre : la fiche d'avant est gardée en version une fois.</summary>
    private readonly ulong _session = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong))) >> 16;

    private string _sheetId = string.Empty;
    private string _title = string.Empty;
    private string _body = string.Empty;
    private string _fingerprint = string.Empty;
    private int _imageIndex;

    /// <summary>En écriture : où va le titre de l'image, au lieu d'un enregistrement de la fiche.</summary>
    private Action<string?>? _apply;

    /// <summary>En écriture : le titre de l'image à l'ouverture, dont les autres réglages (largeur, place, cadre) sont gardés.</summary>
    private string? _openedTitle;

    /// <summary>La pose des masques, sur les cas d'usage qui lisent et enregistrent une fiche.</summary>
    public ImageMaskEditorViewModel(SheetUseCases sheets, EditingUseCases editing)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(editing);
        _sheets = sheets;
        _editing = editing;
        Masks.CollectionChanged += (_, change) =>
        {
            // La zone qu'on vient de tracer ou d'ajouter est la zone choisie ; une zone retirée laisse la main à sa voisine.
            SelectedIndex = change.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
                ? change.NewStartingIndex
                : Math.Min(SelectedIndex, Masks.Count - 1);
            OnPropertyChanged(nameof(Selection));
            OnPropertyChanged(nameof(Summary));
            UndoLastCommand.NotifyCanExecuteChanged();
            ClearCommand.NotifyCanExecuteChanged();
            AddZoneCommand.NotifyCanExecuteChanged();
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
    [NotifyCanExecuteChangedFor(nameof(AddZoneCommand))]
    public partial bool CanDraw { get; private set; }

    /// <summary>La fenêtre sert à rogner l'image : un seul rectangle, celui qu'on garde.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DialogTitle), nameof(HelpText))]
    public partial bool IsCrop { get; private set; }

    /// <summary>Le titre de la fenêtre.</summary>
    public string DialogTitle => IsCrop ? ImageMaskText.CropTitle : ImageMaskText.Title;

    /// <summary>Ce que la fenêtre explique.</summary>
    public string HelpText => IsCrop ? ImageMaskText.CropHelp : ImageMaskText.Help;

    /// <summary>« 3 zones ».</summary>
    public string Summary => ImageMaskText.Count(Masks.Count);

    /// <summary>
    /// Le rang de la zone choisie, celle que le clavier déplace, redimensionne ou retire ; -1 s'il n'y en a pas. C'est la
    /// dernière tracée, tant qu'on n'en choisit pas une autre.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Selection))]
    public partial int SelectedIndex { get; private set; } = -1;

    /// <summary>La zone choisie, dite en mots ; rien s'il n'y en a pas.</summary>
    public string Selection => SelectedIndex >= 0 && SelectedIndex < Masks.Count
        ? ImageMaskText.Selected(SelectedIndex + 1, Masks.Count, Masks[SelectedIndex])
        : string.Empty;

    private bool HasMasks => Masks.Count > 0;

    private bool CanAdd => CanDraw && (IsCrop || Masks.Count < ImageMasks.Maximum);

    /// <summary>
    /// Ouvre la fenêtre sur l'image de rang <paramref name="imageIndex"/> de la fiche, avec les masques qu'elle a déjà. La
    /// fiche est relue : c'est ce texte-là, et son empreinte, qui seront enregistrés.
    /// </summary>
    public async Task OpenAsync(string sheetId, int imageIndex, ImageSource? picture)
    {
        ArgumentNullException.ThrowIfNull(sheetId);
        var opened = await Task.Run(() => _sheets.Open(sheetId)).ConfigureAwait(true);
        _apply = null;
        IsCrop = false;
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
    /// Ouvre la fenêtre sur une image de la fiche en cours d'écriture. Rien n'est lu ni enregistré ici : les masques
    /// partent de <paramref name="title"/>, le titre Markdown de l'image, et le nouveau titre est rendu à
    /// <paramref name="apply"/>, qui l'écrit dans le texte en cours ; rien (null) si l'image n'a plus de masque.
    /// </summary>
    /// <param name="picture">L'image : entière pour la rogner, réduite à son cadre pour y poser des masques.</param>
    /// <param name="title">Son titre Markdown.</param>
    /// <param name="apply">Ce qui écrit le nouveau titre dans le texte en cours.</param>
    /// <param name="crop">Vrai pour tracer son cadre (un seul rectangle, celui qu'on garde) au lieu de ses masques.</param>
    public void OpenFor(ImageSource? picture, string? title, Action<string?> apply, bool crop = false)
    {
        ArgumentNullException.ThrowIfNull(apply);
        _apply = apply;
        _openedTitle = title;
        IsCrop = crop;
        Masks.Clear();
        Picture = picture;
        var look = ImageLooks.FromTitle(title);
        foreach (var mask in crop ? look?.Crop is { } frame ? [frame] : [] : look?.Masks ?? [])
        {
            Masks.Add(mask);
        }
        // Un titre qui n'est pas une liste de masques est une légende : on ne la remplace pas.
        var free = ImageLooks.IsOurs(title);
        CanDraw = picture is not null && free;
        Error = CanDraw ? null : picture is null ? ImageMaskText.Unreadable : ImageMaskText.CannotWrite;
        IsOpen = true;
    }

    /// <summary>
    /// Ajoute une zone tracée, en parts de l'image ; elle est ramenée dans l'image, et refusée si elle est trop petite pour
    /// être vue ou si l'image en a déjà autant qu'elle peut en porter.
    /// </summary>
    /// <returns>Vrai si la zone est gardée.</returns>
    public bool Add(ImageMask traced)
    {
        if (!CanDraw || ImageMasks.Normalize(traced) is not { } mask || (!IsCrop && Masks.Count >= ImageMasks.Maximum))
        {
            return false;
        }
        if (IsCrop)
        {
            // Un seul cadre : le nouveau remplace l'ancien.
            Masks.Clear();
        }
        Masks.Add(mask);
        return true;
    }

    /// <summary>
    /// Le cadre de l'image, pendant qu'on le règle : celui qu'on a tracé, ou l'image entière tant qu'on n'a rien touché.
    /// </summary>
    public ImageMask Crop => IsCrop && Masks.Count > 0 ? Masks[0] : new ImageMask(0, 0, 1, 1);

    /// <summary>Donne son cadre à l'image, en parts de l'image ; refusé s'il n'en reste pas de quoi la voir.</summary>
    /// <returns>Vrai si le cadre est pris.</returns>
    public bool SetCrop(ImageMask frame)
    {
        if (!CanDraw || !IsCrop || ImageMasks.Normalize(frame) is not { } crop)
        {
            return false;
        }
        if (Masks.Count == 0)
        {
            Masks.Add(crop);
        }
        else if (Masks[0] != crop)
        {
            Masks[0] = crop;
        }
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

    /// <summary>
    /// Ajoute une zone sans la tracer, au milieu de l'image : le clavier la place ensuite. Chaque nouvelle zone est un peu
    /// décalée de la précédente, pour ne pas la recouvrir exactement.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void AddZone()
    {
        var shift = Masks.Count % 5 * 0.04;
        Add(new ImageMask(0.3 + shift, 0.35 + shift, DefaultSide, DefaultSide));
    }

    /// <summary>Choisit la zone suivante (<paramref name="step"/> à 1) ou la précédente (-1), en boucle.</summary>
    public void SelectBy(int step)
    {
        if (Masks.Count > 0)
        {
            SelectedIndex = ((Math.Max(SelectedIndex, 0) + step) % Masks.Count + Masks.Count) % Masks.Count;
        }
    }

    /// <summary>Déplace la zone choisie, en parts de l'image ; elle s'arrête aux bords.</summary>
    public void MoveSelected(double right, double down)
    {
        if (CanDraw && SelectedIndex >= 0 && SelectedIndex < Masks.Count)
        {
            var mask = Masks[SelectedIndex];
            Replace(mask with { X = Math.Clamp(mask.X + right, 0, 1 - mask.Width), Y = Math.Clamp(mask.Y + down, 0, 1 - mask.Height) });
        }
    }

    /// <summary>
    /// Change la taille de la zone choisie par son coin bas droit, en parts de l'image ; elle ne sort pas de l'image et ne
    /// devient pas trop petite pour être vue.
    /// </summary>
    public void ResizeSelected(double wider, double taller)
    {
        if (CanDraw && SelectedIndex >= 0 && SelectedIndex < Masks.Count)
        {
            var mask = Masks[SelectedIndex];
            Replace(mask with
            {
                Width = Math.Clamp(mask.Width + wider, ImageMasks.MinimumSide, 1 - mask.X),
                Height = Math.Clamp(mask.Height + taller, ImageMasks.MinimumSide, 1 - mask.Y),
            });
        }
    }

    /// <summary>Retire la zone choisie.</summary>
    public void RemoveSelected() => RemoveAt(SelectedIndex);

    private void Replace(ImageMask changed)
    {
        if (ImageMasks.Normalize(changed) is { } mask && mask != Masks[SelectedIndex])
        {
            Masks[SelectedIndex] = mask;
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
            _apply = null;
            return;
        }
        var masks = Masks.ToList();
        if (_apply is { } apply)
        {
            _apply = null;
            Picture = null;
            if (IsCrop)
            {
                ImageMask? frame = masks.Count > 0 ? masks[0] : null;
                // Les masques se comptent sur ce qui se voit : un autre cadre les déplacerait, ils sont retirés.
                apply(ImageLooks.Change(_openedTitle, look => look with { Crop = frame, Masks = frame == look.Crop ? look.Masks : [] }).Title);
                LastSaved = frame is null ? ImageMaskText.Uncropped : ImageMaskText.Cropped;
            }
            else
            {
                apply(ImageLooks.Change(_openedTitle, look => look with { Masks = [.. masks] }).Title);
                LastSaved = ImageMaskText.Saved(Math.Min(masks.Count, ImageMasks.Maximum));
            }
            Saved?.Invoke(this, EventArgs.Empty);
            return;
        }
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
