using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Schemas;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Insérer un schéma réseau dans une fiche : un nouveau, un existant, ou une copie indépendante.</summary>
public sealed partial class NetworkChoiceViewModel : ObservableObject
{
    private readonly INetworkSchemas _schemas;

    /// <summary>Le choix, parmi les schémas du coffre.</summary>
    public NetworkChoiceViewModel(INetworkSchemas schemas)
    {
        _schemas = schemas;
    }

    /// <summary>Les schémas du coffre.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExisting))]
    public partial IReadOnlyList<SchemaSummary> Existing { get; private set; } = [];

    /// <summary>Le coffre a déjà des schémas.</summary>
    public bool HasExisting => Existing.Count > 0;

    /// <summary>Le schéma existant choisi.</summary>
    [ObservableProperty]
    public partial SchemaSummary? Selected { get; set; }

    /// <summary>Le titre d'un nouveau schéma : s'il est écrit, c'est un nouveau schéma qui s'insère.</summary>
    [ObservableProperty]
    public partial string NewTitle { get; set; } = string.Empty;

    /// <summary>Insérer une copie, qui vivra sa vie, plutôt que le schéma lui-même.</summary>
    [ObservableProperty]
    public partial bool IsCopy { get; set; }

    /// <summary>Ce qui empêche d'insérer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Un refus est affiché.</summary>
    public bool HasError => Error is not null;

    /// <summary>Le nouveau schéma vient d'être créé : il s'ouvre dans l'éditeur une fois inséré.</summary>
    public bool OpensEditor { get; private set; }

    /// <summary>Lit les schémas du coffre.</summary>
    public async Task LoadAsync() => Existing = await _schemas.ListAsync().ConfigureAwait(true);

    /// <summary>Le schéma à citer, créé ou copié au besoin ; rien si le choix ne mène à rien, et <see cref="Error"/> dit pourquoi.</summary>
    public async Task<string?> ChooseAsync()
    {
        Error = null;
        var title = NewTitle.Trim();
        string? id;
        if (title.Length > 0)
        {
            id = await _schemas.CreateAsync(title).ConfigureAwait(true);
            OpensEditor = id is not null;
        }
        else if (Selected is { } selected)
        {
            id = IsCopy ? await _schemas.DuplicateAsync(selected.Id).ConfigureAwait(true) : selected.Id;
        }
        else
        {
            Error = SchemaText.NothingChosen;
            return null;
        }
        Error = id is null ? SchemaText.Unavailable : null;
        return id;
    }
}
