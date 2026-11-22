using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Shell;

/// <remarks>
/// Capture rapide : une idée se note depuis n'importe quelle vue (Ctrl+Maj+N, la recherche Ctrl+K, ou le raccourci global
/// s'il est activé), sans quitter ce qu'on fait. Elle part dans la fiche « Boîte de réception ».
/// </remarks>
public sealed partial class ShellViewModel
{
    /// <summary>Ce qui range une note dans la boîte de réception, et rend sa fiche : la racine de composition le donne.</summary>
    public Func<string, Outcome<string>>? Capture
    {
        get;
        set
        {
            field = value;
            // La commande « Capture rapide » paraît dans la recherche dès qu'il y a de quoi ranger une note.
            RebuildCommands();
            UpdateResults();
        }
    }

    /// <summary>Ouvre la palette de capture, à la place de la fenêtre ; rien : la fenêtre (un essai, une page seule).</summary>
    public Action? ShowCapturePalette { get; set; }

    /// <summary>Le coffre ouvert est chiffré et attend son mot de passe : la page de déverrouillage est montrée.</summary>
    public bool IsLockedOut => Current.Id == UnlockPage;

    /// <summary>La page de déverrouillage.</summary>
    internal const string UnlockPage = "deverrouillage";

    /// <summary>La fenêtre de capture est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsCaptureOpen { get; private set; }

    /// <summary>La note en cours de saisie.</summary>
    [ObservableProperty]
    public partial string CaptureText { get; set; } = string.Empty;

    /// <summary>Le refus du coffre ; rien si la note est partie.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCaptureError))]
    public partial string? CaptureError { get; private set; }

    /// <summary>La note n'a pas pu être rangée.</summary>
    public bool HasCaptureError => CaptureError is not null;

    /// <summary>Un coffre est ouvert et déverrouillé : il y a où ranger une note.</summary>
    public bool CanCapture => Capture is not null && _workspace;

    /// <summary>
    /// Ouvre la fenêtre de capture ; sans coffre ouvert, ou coffre verrouillé, elle ne s'ouvre pas : rien ne doit s'écrire
    /// derrière un déverrouillage.
    /// </summary>
    /// <returns>Vrai si la fenêtre s'est ouverte.</returns>
    public bool OpenCapture()
    {
        // La palette de capture (rangée 25B), quand l'application l'a posée : elle sait dire, elle, qu'un coffre est verrouillé.
        if (ShowCapturePalette is { } show)
        {
            ClosePalette();
            IsHelpOpen = false;
            show();
            return true;
        }
        if (!CanCapture)
        {
            return false;
        }
        ClosePalette();
        IsHelpOpen = false;
        CaptureText = string.Empty;
        CaptureError = null;
        IsCaptureOpen = true;
        return true;
    }

    [RelayCommand]
    private void RequestCapture() => OpenCapture();

    /// <summary>La réponse de la fenêtre : la note est rangée ; un refus laisse la fenêtre ouverte, avec sa raison et le texte.</summary>
    [RelayCommand]
    private async Task AnswerCaptureAsync(bool confirmed)
    {
        IsCaptureOpen = false;
        if (!confirmed || Capture is not { } capture)
        {
            CaptureError = null;
            return;
        }
        var text = CaptureText;
        var outcome = await Task.Run(() => capture(text)).ConfigureAwait(true);
        if (outcome.Succeeded)
        {
            CaptureError = null;
            CaptureText = string.Empty;
            // La vue affichée montre tout de suite la note : le Carnet, Aujourd'hui… Une fiche ouverte n'est pas relue sous
            // la main de qui l'écrit.
            if (Current.Id != CorePages.Sheet)
            {
                Activate(Current);
            }
            return;
        }
        CaptureError = outcome.Error;
        // Verrouillé entre-temps, le coffre ne reçoit plus rien : la fenêtre ne se rouvre pas sur lui.
        IsCaptureOpen = CanCapture;
    }

    /// <summary>Une note, une fiche viennent d'être écrites ailleurs que dans la vue : elle les montre, sans qu'on la quitte.</summary>
    internal void ShowWritten()
    {
        // Une fiche ouverte n'est pas relue sous la main de qui l'écrit.
        if (Current.Id != CorePages.Sheet)
        {
            Activate(Current);
        }
    }

    /// <summary>La capture, dans la recherche Ctrl+K.</summary>
    private IEnumerable<PaletteEntry> CaptureCommands() =>
        Capture is null ? [] : [new(ShellText.Capture, ShortcutMap.Current.DisplayOf("capture"), Icons.Document, () => OpenCapture())];
}
