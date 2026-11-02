using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Une version de la liste.</summary>
/// <param name="Version">La version.</param>
public sealed record VersionEntry(SheetVersion Version)
{
    /// <summary>Quand elle a été gardée.</summary>
    public string Date => Version.SavedAt.ToLocalTime().ToString("d MMMM yyyy, HH:mm", EditingText.Culture);

    /// <summary>Sa taille.</summary>
    public string Size => EditingText.FileSize(Version.Size);
}

/// <summary>L'historique d'une fiche : ses versions, comparées au texte actuel, et de quoi en remettre une. Rien n'est jamais effacé.</summary>
public sealed partial class VersionsViewModel : ObservableObject
{
    private readonly EditingUseCases _editing;
    private readonly string _sheetId;
    private readonly string _current;
    private readonly Func<string, string, Task<bool>> _restore;

    /// <summary>L'historique de cette fiche.</summary>
    /// <param name="editing">Les cas d'usage.</param>
    /// <param name="sheetId">La fiche.</param>
    /// <param name="current">Son texte actuel.</param>
    /// <param name="restore">Remet une version (son nom, son texte) ; rend vrai si elle a été remise.</param>
    internal VersionsViewModel(EditingUseCases editing, string sheetId, string current, Func<string, string, Task<bool>> restore)
    {
        _editing = editing;
        _sheetId = sheetId;
        _current = current;
        _restore = restore;
        var versions = editing.Versions(sheetId);
        if (versions.Succeeded)
        {
            Entries = [.. versions.Value.Select(version => new VersionEntry(version))];
            Selected = Entries.Count > 0 ? Entries[0] : null;
        }
        else
        {
            Entries = [];
            Problem = versions.Error;
        }
    }

    /// <summary>La fenêtre est à refermer.</summary>
    public event EventHandler? Closed;

    /// <summary>Les versions, de la plus récente à la plus ancienne.</summary>
    public IReadOnlyList<VersionEntry> Entries { get; }

    /// <summary>Il y a au moins une version.</summary>
    public bool HasVersions => Entries.Count > 0;

    /// <summary>Il n'y a encore aucune version.</summary>
    public bool IsEmpty => !HasVersions;

    /// <summary>Combien de versions, et ce qu'elles pèsent.</summary>
    public string Summary => EditingText.VersionsSummary(Entries.Count, Entries.Sum(entry => entry.Version.Size));

    /// <summary>La version choisie.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionLabel))]
    public partial VersionEntry? Selected { get; set; }

    /// <summary>La version choisie comparée au texte actuel.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<DiffRow>? Rows { get; private set; }

    /// <summary>L'en-tête du côté de la version.</summary>
    public string VersionLabel => Selected is { } entry ? string.Create(CultureInfo.InvariantCulture, $"Version du {entry.Date}") : string.Empty;

    /// <summary>La remise de la version attend sa confirmation.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotConfirming))]
    public partial bool IsConfirming { get; private set; }

    /// <summary>Le bouton « Restaurer cette version… » se montre.</summary>
    public bool IsNotConfirming => !IsConfirming;

    /// <summary>Ce qui vient d'échouer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; private set; }

    /// <summary>Quelque chose vient d'échouer.</summary>
    public bool HasProblem => Problem is not null;

    /// <summary>Demande confirmation : le texte actuel sera d'abord gardé.</summary>
    [RelayCommand]
    private void AskRestore() => IsConfirming = true;

    /// <summary>Renonce à remettre la version.</summary>
    [RelayCommand]
    private void CancelRestore() => IsConfirming = false;

    /// <summary>Remet la version choisie ; le texte actuel va d'abord dans l'historique.</summary>
    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (Selected is not { } entry)
        {
            return;
        }
        var body = _editing.ReadVersion(_sheetId, entry.Version.Name);
        if (!body.Succeeded)
        {
            Problem = body.Error;
            return;
        }
        if (await _restore(entry.Version.Name, body.Value).ConfigureAwait(true))
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            IsConfirming = false;
            Problem = EditingText.VersionNotRestored;
        }
    }

    partial void OnSelectedChanged(VersionEntry? value)
    {
        IsConfirming = false;
        Rows = null;
        if (value is null)
        {
            return;
        }
        var body = _editing.ReadVersion(_sheetId, value.Version.Name);
        if (body.Succeeded)
        {
            Problem = null;
            Rows = TextDiff.Rows(body.Value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'), _current);
        }
        else
        {
            Problem = body.Error;
        }
    }
}
