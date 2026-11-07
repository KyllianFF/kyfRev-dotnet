using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Un dossier du glossaire : le choisir montre aussi ses sous-dossiers.</summary>
/// <param name="Path">Son chemin ; vide pour tout le coffre.</param>
/// <param name="Label">Ce qui s'affiche.</param>
public sealed record DefinitionFolder(string Path, string Label)
{
    /// <summary>Ce qu'en dit un lecteur d'écran : son nom, sans le retrait qui le place dans la liste.</summary>
    public override string ToString() => Label.Trim();
}

/// <summary>Une définition du glossaire : on peut la modifier sur place, ou aller la voir dans sa fiche.</summary>
public sealed partial class DefinitionRow : ObservableObject
{
    private readonly DefinitionsPageViewModel _page;

    internal DefinitionRow(VaultDefinition definition, DefinitionsPageViewModel page)
    {
        Definition = definition;
        _page = page;
        EditedTerm = definition.Term;
        EditedExplanation = definition.Explanation;
        IsMarkedEverywhere = definition.Everywhere;
    }

    /// <summary>La définition.</summary>
    public VaultDefinition Definition { get; private set; }

    /// <summary>Le terme se marque dans toutes les fiches qui le contiennent ; sinon, seulement là où il est défini.</summary>
    [ObservableProperty]
    public partial bool IsMarkedEverywhere { get; set; }

    /// <summary>Le choix est enregistré : la définition le garde.</summary>
    internal void Marked(VaultDefinition definition) => Definition = definition;

    partial void OnIsMarkedEverywhereChanged(bool value)
    {
        if (value != Definition.Everywhere)
        {
            _ = _page.MarkAsync(this, value);
        }
    }

    /// <summary>Le terme.</summary>
    public string Term => Definition.Term;

    /// <summary>L'explication.</summary>
    public string Explanation => Definition.Explanation;

    /// <summary>D'où elle vient : la fiche, sa section, et sa prochaine révision.</summary>
    public string Source
    {
        get
        {
            var place = string.Join(" › ", new[] { Definition.SheetTitle }.Concat(Definition.Section));
            return Definition is { Step: { } step, DueDate: { } due }
                ? string.Create(CultureInfo.InvariantCulture, $"{place} · {step} le {due.ToString("d MMM", DefinitionsText.Culture)}")
                : place;
        }
    }

    /// <summary>Ce qu'il faut savoir de sa position dans la fiche ; rien si elle est sûre.</summary>
    public string? Warning => Definition.AnchorState switch
    {
        AnchorState.Anchored => null,
        AnchorState.NeedsReview => DefinitionsText.ToCheck,
        _ => DefinitionsText.Lost,
    };

    /// <summary>Sa position pose question.</summary>
    public bool HasWarning => Warning is not null;

    /// <summary>La définition se modifie.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    public partial bool IsEditing { get; private set; }

    /// <summary>La définition se lit.</summary>
    public bool IsShown => !IsEditing;

    /// <summary>Le terme, en cours de modification.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string EditedTerm { get; set; }

    /// <summary>L'explication, en cours de modification.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string EditedExplanation { get; set; }

    /// <summary>La définition est enregistrée : elle se lit de nouveau.</summary>
    internal void EndEditing() => IsEditing = false;

    /// <summary>Modifie la définition sur place.</summary>
    [RelayCommand]
    private void Edit() => IsEditing = true;

    /// <summary>Renonce à la modification.</summary>
    [RelayCommand]
    private void Cancel()
    {
        IsEditing = false;
        EditedTerm = Term;
        EditedExplanation = Explanation;
    }

    /// <summary>Le terme et l'explication sont écrits : la définition peut s'enregistrer.</summary>
    internal bool CanSave() => EditedTerm.Trim().Length > 0 && EditedExplanation.Trim().Length > 0;

    /// <summary>Rien n'a changé dans la modification : il n'y a rien à enregistrer.</summary>
    internal bool IsUnchanged => EditedTerm.Trim() == Term && EditedExplanation.Trim() == Explanation;

    /// <summary>Enregistre la définition.</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => _page.SaveAsync(this);

    /// <summary>Ouvre la fiche, sur le passage défini.</summary>
    [RelayCommand]
    private void Open() => _page.Open(this);
}

/// <summary>La vue « Définitions » : le glossaire du coffre, par dossier, avec une recherche.</summary>
public sealed partial class DefinitionsPageViewModel : ObservableObject, IForgetsVault, ILeaving
{
    private readonly AnnotationUseCases _annotations;
    private readonly IReviewOpener _opener;
    private readonly Dictionary<string, DefinitionRow> _rows = new(StringComparer.Ordinal);
    private DefinitionRow? _toDelete;
    private IReadOnlyList<VaultDefinition> _all = [];
    private string _folder = string.Empty;

    /// <summary>Compte les oublis du coffre : une lecture partie avant le dernier oubli n'y remet rien.</summary>
    private int _generation;

    /// <summary>Le glossaire, sur ces cas d'usage.</summary>
    public DefinitionsPageViewModel(AnnotationUseCases annotations, IReviewOpener opener)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(opener);
        _annotations = annotations;
        _opener = opener;
        SelectedFolder = Folders[0];
    }

    /// <summary>Les dossiers qui ont des définitions, et tout le coffre.</summary>
    public ObservableCollection<DefinitionFolder> Folders { get; } = [new(string.Empty, DefinitionsText.WholeVault)];

    /// <summary>Les définitions montrées.</summary>
    public ObservableCollection<DefinitionRow> Rows { get; } = [];

    /// <summary>Le dossier choisi ; rien pendant que la vue se défait, sans que le choix soit perdu.</summary>
    [ObservableProperty]
    public partial DefinitionFolder? SelectedFolder { get; set; }

    /// <summary>Ce qui est cherché.</summary>
    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    /// <summary>La première lecture est en cours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(HasNoMatch))]
    public partial bool IsLoading { get; private set; }

    /// <summary>Ce qui vient d'échouer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(IsEmpty))]
    public partial string? Error { get; private set; }

    /// <summary>Quelque chose vient d'échouer.</summary>
    public bool HasError => Error is not null;

    /// <summary>Le coffre n'a encore aucune définition.</summary>
    public bool IsEmpty => !IsLoading && !HasError && _all.Count == 0;

    /// <summary>Aucune définition ne répond à la recherche ou au dossier.</summary>
    public bool HasNoMatch => !IsLoading && _all.Count > 0 && Rows.Count == 0;

    /// <summary>Combien de définitions sont montrées ; rien tant que le coffre n'en a pas.</summary>
    public string Summary => _all.Count == 0 ? string.Empty : DefinitionsText.Count(Rows.Count);

    /// <summary>Relit le glossaire.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = _all.Count == 0;
        var generation = _generation;
        var outcome = await Task.Run(_annotations.Definitions).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }
        IsLoading = false;
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            return;
        }
        Error = null;
        _all = outcome.Value;
        KeepRows();
        var chosen = _folder;
        Folders.Clear();
        Folders.Add(new DefinitionFolder(string.Empty, DefinitionsText.WholeVault));
        foreach (var folder in _all.SelectMany(definition => Ancestors(definition.Folder)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            Folders.Add(new DefinitionFolder(folder, Label(folder)));
        }
        SelectedFolder = Folders.FirstOrDefault(folder => folder.Path == chosen) ?? Folders[0];
        Refresh();
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Enregistre une définition modifiée, puis relit le glossaire.</summary>
    internal async Task SaveAsync(DefinitionRow row)
    {
        var (id, definition) = (row.Definition.Id, new Definition(row.EditedTerm.Trim(), row.EditedExplanation.Trim()));
        var generation = _generation;
        var outcome = await Task.Run(() => _annotations.ModifyDefinition(id, definition)).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }
        if (outcome.Succeeded)
        {
            row.EndEditing();
            await LoadAsync().ConfigureAwait(true);
        }
        else
        {
            Error = outcome.Error;
        }
    }

    /// <summary>Marque le terme d'une définition dans toutes les fiches, ou seulement à sa place ; un refus rend la case.</summary>
    internal async Task MarkAsync(DefinitionRow row, bool everywhere)
    {
        var id = row.Definition.Id;
        var generation = _generation;
        var outcome = await Task.Run(() => _annotations.MarkEverywhere(id, everywhere)).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            row.IsMarkedEverywhere = row.Definition.Everywhere;
            return;
        }
        var marked = row.Definition with { Everywhere = everywhere };
        _all = [.. _all.Select(definition => definition.Id == id ? marked : definition)];
        row.Marked(marked);
    }

    /// <summary>La mise à la corbeille attend sa confirmation.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; private set; }

    /// <summary>Demande confirmation avant de mettre une définition à la corbeille.</summary>
    internal void RequestDelete(DefinitionRow row)
    {
        _toDelete = row;
        IsConfirmingDelete = true;
    }

    /// <summary>La réponse à la mise à la corbeille.</summary>
    [RelayCommand]
    private async Task AnswerDeleteAsync(bool confirmed)
    {
        IsConfirmingDelete = false;
        if (!confirmed || _toDelete is not { } row)
        {
            return;
        }
        var id = row.Definition.Id;
        var generation = _generation;
        var outcome = await Task.Run(() => _annotations.Trash(id)).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }
        if (outcome.Succeeded)
        {
            await LoadAsync().ConfigureAwait(true);
        }
        else
        {
            Error = outcome.Error;
        }
    }

    /// <summary>Ouvre la fiche d'une définition, sur son passage.</summary>
    internal void Open(DefinitionRow row) => _opener.OpenPassageInSheet(row.Definition.SheetId, row.Definition.Id);

    /// <summary>
    /// Avant que le coffre se verrouille, chaque définition en cours de modification s'enregistre, comme avec son bouton ;
    /// celle où rien n'a changé se referme. Faux si l'une n'a pas pu l'être (un terme ou une explication vide, un refus du
    /// coffre) : elle reste en modification, avec ce qui bloque, et le coffre reste ouvert.
    /// </summary>
    public async Task<bool> LeaveAsync()
    {
        var editing = _rows.Values.Where(row => row.IsEditing).ToList();
        foreach (var row in editing)
        {
            if (row.IsUnchanged)
            {
                row.CancelCommand.Execute(null);
            }
            else if (row.CanSave() && !ChangedElsewhere(row))
            {
                await SaveAsync(row).ConfigureAwait(true);
            }
        }
        // Ce qui n'a pas pu s'enregistrer (incomplet, refusé, ou changé ailleurs depuis) est abandonné : la vue n'est
        // peut-être pas affichée, et un brouillon que personne ne voit ne doit pas garder le coffre ouvert.
        return true;
    }

    /// <summary>La définition a changé dans le coffre depuis qu'on a commencé à la modifier ici : le brouillon l'écraserait.</summary>
    private bool ChangedElsewhere(DefinitionRow row) =>
        _annotations.Definitions() is { Succeeded: true, Value: var stored }
        && stored.FirstOrDefault(definition => definition.Id == row.Definition.Id) is { } current
        && (current.Term != row.Term || current.Explanation != row.Explanation);

    /// <summary>
    /// Oublie le glossaire quand le coffre se verrouille : définitions, brouillons, dossiers, recherche, confirmation et
    /// message ; la vue revient à son état d'avant toute lecture et relit le coffre à sa prochaine ouverture.
    /// </summary>
    public void Forget()
    {
        _generation++;
        _all = [];
        _rows.Clear();
        _toDelete = null;
        _folder = string.Empty;
        Folders.Clear();
        Folders.Add(new DefinitionFolder(string.Empty, DefinitionsText.WholeVault));
        SelectedFolder = Folders[0];
        Search = string.Empty;
        IsConfirmingDelete = false;
        IsLoading = false;
        Error = null;
        Refresh();
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnSelectedFolderChanged(DefinitionFolder? value)
    {
        if (value is not null)
        {
            _folder = value.Path;
            Refresh();
        }
    }

    partial void OnSearchChanged(string value) => Refresh();

    private static IEnumerable<string> Ancestors(string folder)
    {
        for (var end = folder.Length; end > 0; end = folder.LastIndexOf('/', end - 1))
        {
            yield return folder[..end];
        }
    }

    /// <summary>Un sous-dossier se lit en retrait : le chemin entier n'aide pas à parcourir.</summary>
    private static string Label(string folder) =>
        new string(' ', folder.Count(character => character == '/') * 3) + folder[(folder.LastIndexOf('/') + 1)..];

    /// <summary>Une définition en cours de modification garde son brouillon quand le glossaire se relit.</summary>
    private void KeepRows()
    {
        var kept = _rows.Values.Where(row => row.IsEditing).ToDictionary(row => row.Definition.Id, StringComparer.Ordinal);
        _rows.Clear();
        foreach (var definition in _all)
        {
            _rows[definition.Id] = kept.GetValueOrDefault(definition.Id) ?? new DefinitionRow(definition, this);
        }
    }

    private void Refresh()
    {
        var folder = _folder;
        var wanted = DefinitionsText.WithoutAccents(Search.Trim());
        Rows.Clear();
        foreach (var definition in _all)
        {
            var inFolder = folder.Length == 0 || definition.Folder == folder || definition.Folder.StartsWith(folder + "/", StringComparison.Ordinal);
            if (inFolder && DefinitionsText.WithoutAccents($"{definition.Term} {definition.Explanation}").Contains(wanted, StringComparison.Ordinal))
            {
                Rows.Add(_rows[definition.Id]);
            }
        }
        OnPropertyChanged(nameof(HasNoMatch));
        OnPropertyChanged(nameof(Summary));
    }
}
