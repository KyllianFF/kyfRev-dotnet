using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Une nouvelle définition, écrite depuis la vue « Définitions » : son terme, son explication, et l'endroit d'une fiche où
/// l'accrocher. Une définition s'attache à un mot écrit dans une fiche : à mesure qu'on tape le terme, la vue cherche où il est
/// écrit et propose ces endroits, le premier choisi d'avance.
/// </summary>
public sealed partial class DefinitionDraft : ObservableObject, IDisposable
{
    private readonly DefinitionsPageViewModel _page;
    private readonly Func<string, CancellationToken, IReadOnlyList<DefinitionPlace>> _find;
    private readonly TimeSpan _pause;
    private CancellationTokenSource? _search;

    internal DefinitionDraft(DefinitionsPageViewModel page, Func<string, CancellationToken, IReadOnlyList<DefinitionPlace>> find, TimeSpan pause, string term)
    {
        _page = page;
        _find = find;
        _pause = pause;
        Term = term;
    }

    /// <summary>Le terme à définir.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Term { get; set; }

    /// <summary>Son explication.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Explanation { get; set; } = string.Empty;

    /// <summary>Le terme se marquera dans toutes les fiches qui le contiennent ; sinon, seulement là où il est défini.</summary>
    [ObservableProperty]
    public partial bool IsMarkedEverywhere { get; set; } = true;

    /// <summary>Les endroits où le terme est écrit.</summary>
    public ObservableCollection<DefinitionPlace> Places { get; } = [];

    /// <summary>L'endroit choisi pour accrocher la définition.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial DefinitionPlace? ChosenPlace { get; set; }

    /// <summary>La recherche des endroits est en cours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlacesHeading), nameof(HasNoPlace))]
    public partial bool IsSearching { get; private set; }

    /// <summary>Le terme cherché a été cherché jusqu'au bout.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlacesHeading), nameof(HasNoPlace))]
    public partial string? Searched { get; private set; }

    /// <summary>Le terme n'est écrit dans aucune fiche : la définition n'a pas où s'accrocher.</summary>
    public bool HasNoPlace => !IsSearching && Searched is not null && Places.Count == 0;

    /// <summary>Ce que dit la liste des endroits : qu'elle cherche, combien elle en a trouvé, ou qu'il n'y en a pas.</summary>
    public string PlacesHeading =>
        IsSearching ? DefinitionsText.SearchingPlaces
        : Searched is null ? DefinitionsText.TypeTheTerm
        : Places.Count == 0 ? DefinitionsText.NoPlace(Searched)
        : DefinitionsText.PlacesFound(Places.Count, Places.Count >= DefinitionPlaces.Places);

    /// <summary>L'enregistrement est en cours.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; internal set; }

    /// <summary>Ce que le coffre a refusé.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; internal set; }

    /// <summary>Le coffre a refusé quelque chose.</summary>
    public bool HasProblem => Problem is not null;

    /// <summary>Quelque chose a été écrit : le fermer perdrait un brouillon.</summary>
    public bool HasText => Term.Trim().Length > 0 || Explanation.Trim().Length > 0;

    /// <summary>Le brouillon peut s'enregistrer : un terme, une explication, un endroit.</summary>
    internal bool CanSave() => !IsSaving && Term.Trim().Length > 0 && Explanation.Trim().Length > 0 && ChosenPlace is not null;

    /// <inheritdoc/>
    public void Dispose()
    {
        _search?.Cancel();
        _search?.Dispose();
        _search = null;
    }

    /// <summary>Cherche où <paramref name="term"/> est écrit, après une courte pause : une frappe de plus remplace la recherche.</summary>
    internal async Task SearchAsync(string term)
    {
        // La nouvelle recherche prend sa place avant que l'ancienne s'arrête : une frappe de plus la remplacera à son tour.
        var previous = _search;
        var search = _search = new CancellationTokenSource();
        try
        {
            if (previous is not null)
            {
                await previous.CancelAsync().ConfigureAwait(true);
                previous.Dispose();
            }
            await Task.Delay(_pause, search.Token).ConfigureAwait(true);
            IsSearching = true;
            var found = await Task.Run(() => _find(term, search.Token), search.Token).ConfigureAwait(true);
            if (!ReferenceEquals(_search, search))
            {
                return;
            }
            Places.Clear();
            foreach (var place in found)
            {
                Places.Add(place);
            }
            ChosenPlace = Places.FirstOrDefault();
            Searched = term.Trim().Length == 0 ? null : term.Trim();
        }
        catch (OperationCanceledException)
        {
            // Une frappe plus récente cherche à sa place : ses endroits remplaceront ceux-ci.
            return;
        }
        finally
        {
            if (ReferenceEquals(_search, search))
            {
                IsSearching = false;
            }
        }
    }

    partial void OnTermChanged(string value)
    {
        OnPropertyChanged(nameof(HasText));
        _ = SearchAsync(value);
    }

    partial void OnExplanationChanged(string value) => OnPropertyChanged(nameof(HasText));

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => _page.CreateAsync(this);

    [RelayCommand]
    private void Cancel() => _page.CloseDraft();
}
