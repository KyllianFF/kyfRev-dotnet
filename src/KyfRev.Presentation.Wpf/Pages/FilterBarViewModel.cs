using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Un filtre ou un tri : son nom, ses choix et celui qui est retenu, à choisir dans un menu.</summary>
public sealed partial class ListFilter : ObservableObject
{
    private readonly bool _counts;
    private readonly Action _changed;

    internal ListFilter(string label, IReadOnlyList<Choice<object?>> choices, bool counts, Action changed)
    {
        Label = label;
        _counts = counts;
        _changed = changed;
        Choices = choices;
        Selected = choices[0];
    }

    /// <summary>« Dossier ».</summary>
    public string Label { get; }

    /// <summary>Les choix du menu.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<Choice<object?>> Choices { get; private set; }

    /// <summary>Le choix retenu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name), nameof(RemoveName))]
    public partial Choice<object?> Selected { get; set; }

    /// <summary>« Dossier : Tous » : ce que dit le bouton, au lecteur d'écran aussi.</summary>
    public string Name => $"{Label} : {Selected.Label}";

    /// <summary>« Retirer le filtre Dossier : Tous » : le nom de son étiquette, en fenêtre étroite.</summary>
    public string RemoveName => ListText.RemoveFilterOf(Name);

    /// <summary>La valeur retenue.</summary>
    internal object? Value => Selected.Value;

    /// <summary>Ce filtre retire des lignes de la liste ; un tri n'en retire aucune.</summary>
    internal bool IsActive => _counts && !Equals(Value, Choices[0].Value);

    /// <summary>D'autres choix ; celui qui est retenu le reste s'il en fait encore partie.</summary>
    internal void Replace(IReadOnlyList<Choice<object?>> choices)
    {
        Choices = choices;
        Selected = choices.FirstOrDefault(choice => Equals(choice.Value, Selected.Value)) ?? choices[0];
    }

    partial void OnSelectedChanged(Choice<object?> value) => _changed();

    /// <summary>Retire le filtre : il revient à son premier choix.</summary>
    [RelayCommand]
    private void Clear() => Selected = Choices[0];
}

/// <summary>
/// La recherche et les filtres d'une liste. Sur une fenêtre étroite, les filtres se replient derrière
/// un bouton, et ceux qui sont choisis restent visibles en étiquettes.
/// </summary>
public sealed partial class FilterBarViewModel : ObservableObject
{
    private readonly Action _changed;
    private readonly List<ListFilter> _filters = [];

    internal FilterBarViewModel(Action changed)
    {
        _changed = changed;
        ActiveFilters = [];
        Search = string.Empty;
    }

    /// <summary>Les filtres, dans l'ordre de la barre.</summary>
    public IReadOnlyList<ListFilter> Filters => _filters;

    /// <summary>Ce que le champ de recherche dit, vide.</summary>
    public string SearchPlaceholder { get; init; } = ListText.SearchPlaceholder;

    /// <summary>Le nom du champ de recherche.</summary>
    public string SearchName { get; init; } = ListText.SearchName;

    /// <summary>Ce qui s'ajoute au filtrage quand le texte cherché change : relire le coffre, pour une recherche dans le texte.</summary>
    internal Action? SearchChanged { get; init; }

    /// <summary>Le texte cherché.</summary>
    [ObservableProperty]
    public partial string Search { get; set; }

    /// <summary>Les filtres choisis, sans le tri, qui ne retire aucune ligne.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters), nameof(ShowsActive), nameof(ToggleLabel))]
    public partial IReadOnlyList<ListFilter> ActiveFilters { get; private set; }

    /// <summary>Au moins un filtre est choisi.</summary>
    public bool HasActiveFilters => ActiveFilters.Count > 0;

    /// <summary>« Filtres » ou « Filtres · 2 ».</summary>
    public string ToggleLabel => HasActiveFilters ? ListText.FiltersWithCountOf(ActiveFilters.Count) : ListText.Filters;

    /// <summary>Assez de largeur pour montrer tous les filtres.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsToggle), nameof(ShowsFilters), nameof(ShowsActive))]
    public partial bool HasRoom { get; set; } = true;

    /// <summary>En fenêtre étroite, le panneau des filtres est ouvert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsFilters), nameof(ShowsActive))]
    public partial bool AreOpen { get; set; }

    /// <summary>Le bouton « Filtres » : seulement en fenêtre étroite, où les filtres se replient.</summary>
    public bool ShowsToggle => !HasRoom;

    /// <summary>Les filtres sont affichés : toujours avec de la place, sinon à la demande.</summary>
    public bool ShowsFilters => HasRoom || AreOpen;

    /// <summary>Les étiquettes des filtres choisis : ce qui filtre la liste reste visible, panneau replié.</summary>
    public bool ShowsActive => !HasRoom && !AreOpen && HasActiveFilters;

    /// <summary>Ajoute un filtre ; <paramref name="counts"/> : il retire des lignes, ce qu'un tri ne fait pas.</summary>
    internal ListFilter Add(string label, IReadOnlyList<Choice<object?>> choices, bool counts = true)
    {
        var filter = new ListFilter(label, choices, counts, OnFilterChanged);
        _filters.Add(filter);
        return filter;
    }

    partial void OnSearchChanged(string value)
    {
        SearchChanged?.Invoke();
        _changed();
    }

    private void OnFilterChanged()
    {
        ActiveFilters = [.. _filters.Where(filter => filter.IsActive)];
        _changed();
    }
}
