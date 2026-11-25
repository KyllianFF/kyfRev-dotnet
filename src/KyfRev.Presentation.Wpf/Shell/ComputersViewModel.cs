using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Ce que l'utilisateur répond quand un autre poste a le coffre ouvert.</summary>
public enum OccupiedChoice
{
    /// <summary>Ne pas l'ouvrir.</summary>
    Cancel,

    /// <summary>L'ouvrir quand même : les écritures des deux postes se rejoignent.</summary>
    Open,

    /// <summary>L'ouvrir en lecture seule : rien ne s'y écrit.</summary>
    ReadOnly,
}

/// <summary>La question posée quand un autre poste a le coffre ouvert : qui, depuis quand, et que faire.</summary>
public sealed class OccupiedVaultViewModel
{
    private readonly TaskCompletionSource<OccupiedChoice> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>La question pour les postes <paramref name="others"/>, vus à <paramref name="now"/>, à l'heure de <paramref name="zone"/> (celle du poste par défaut).</summary>
    public OccupiedVaultViewModel(IReadOnlyList<OtherComputer> others, DateTimeOffset now, TimeZoneInfo? zone = null)
    {
        ArgumentNullException.ThrowIfNull(others);
        var first = others.Count > 0 ? others[0] : new OtherComputer(string.Empty, now, now);
        ComputerName = string.Join(", ", others.Select(other => other.Name));
        Seen = ComputersText.SeenOf(TimeZoneInfo.ConvertTime(first.Since, zone ?? TimeZoneInfo.Local), now - first.SeenAt);
    }

    /// <summary>Le nom du poste (ou des postes) qui l'a ouvert.</summary>
    public string ComputerName { get; }

    /// <summary>« depuis 14 h 05 · vu il y a 1 minute ».</summary>
    public string Seen { get; }

    /// <summary>La réponse, une fois donnée.</summary>
    public Task<OccupiedChoice> Answer => _answer.Task;

    /// <summary>Le bouton choisi : « Ouvrir », « Lecture seule », ou « Annuler » (Échap compris).</summary>
    public void Choose(DialogChoice choice) => _answer.TrySetResult(choice switch
    {
        DialogChoice.Primary => OccupiedChoice.Open,
        DialogChoice.Extra => OccupiedChoice.ReadOnly,
        _ => OccupiedChoice.Cancel,
    });
}

/// <summary>
/// Un coffre ouvert sur plusieurs postes : la question avant de l'ouvrir quand un autre poste l'a ouvert, la bande « Lecture
/// seule » et son « Passer en écriture », et, chaque minute, la présence de ce poste redite et l'avis quand un autre poste
/// vient de l'ouvrir.
/// </summary>
public sealed partial class ComputersViewModel : ObservableObject, IDisposable
{
    /// <summary>Le rythme auquel ce poste redit sa présence et regarde celle des autres.</summary>
    public static readonly TimeSpan Beat = TimeSpan.FromMinutes(1);

    private readonly VaultUseCases _vaults;
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _ui;
    private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase);
    private ITimer? _timer;

    /// <summary>La présence des postes pour les coffres de <paramref name="vaults"/>.</summary>
    public ComputersViewModel(VaultUseCases vaults, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(vaults);
        ArgumentNullException.ThrowIfNull(time);
        _vaults = vaults;
        _time = time;
        _ui = SynchronizationContext.Current;
    }

    /// <summary>La question en cours ; rien s'il n'y en a pas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAsking))]
    public partial OccupiedVaultViewModel? Question { get; private set; }

    /// <summary>Une question attend sa réponse.</summary>
    public bool IsAsking => Question is not null;

    /// <summary>Le coffre ouvert l'est en lecture seule.</summary>
    [ObservableProperty]
    public partial bool IsReadOnly { get; private set; }

    /// <summary>« Lecture seule · ouvert aussi sur PC-BUREAU ».</summary>
    [ObservableProperty]
    public partial string? ReadOnlyText { get; private set; }

    /// <summary>L'avis qu'un autre poste vient d'ouvrir le coffre ; rien sinon.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArrival))]
    public partial string? Arrival { get; set; }

    /// <summary>Un avis attend d'être lu.</summary>
    public bool HasArrival => Arrival is not null;

    /// <summary>Ce qui suit le passage en écriture : le coffre rouvert, ou en attente de sa clé.</summary>
    public Action<AppState>? Reopened { get; set; }

    /// <summary>
    /// Pose la question tant que l'état dit le coffre ouvert par un autre poste, note la réponse, puis rejoue
    /// <paramref name="retry"/> (l'ouverture, ou le déverrouillage avec le même secret). Rien si l'utilisateur a choisi de
    /// ne pas l'ouvrir.
    /// </summary>
    public async Task<AppState?> SettleAsync(AppState state, Func<AppState> retry)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(retry);
        while (state.Occupied is { } root)
        {
            var question = new OccupiedVaultViewModel(state.Others ?? [], _time.GetUtcNow(), _time.LocalTimeZone);
            Question = question;
            var choice = await question.Answer.ConfigureAwait(true);
            Question = null;
            if (choice == OccupiedChoice.Cancel)
            {
                return null;
            }
            _vaults.Answer(root, choice == OccupiedChoice.ReadOnly);
            state = await Task.Run(retry).ConfigureAwait(true);
        }
        return state;
    }

    /// <summary>Le bouton choisi dans la question.</summary>
    public void Answer(DialogChoice choice) => Question?.Choose(choice);

    /// <summary>
    /// Le coffre de l'application a changé : la bande « Lecture seule » le suit, et la présence de ce poste se redit chaque
    /// minute tant qu'un coffre est ouvert.
    /// </summary>
    public void Show(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        IsReadOnly = state.ReadOnly;
        ReadOnlyText = state.ReadOnly
            ? state.Others is { Count: > 0 } others ? ComputersText.ReadOnlyBannerOf(Unbroken(string.Join(", ", others.Select(other => other.Name)))) : ComputersText.ReadOnlyAlone
            : null;
        _known.Clear();
        foreach (var other in state.Others ?? [])
        {
            _known.Add(other.Name);
        }
        Arrival = null;
        _timer?.Dispose();
        _timer = state.Vault is null ? null : _time.CreateTimer(_ => Listen(), null, Beat, Beat);
    }

    /// <summary>Un nom de poste ne se coupe pas à ses tirets (« PC-BUREAU ») : ils deviennent insécables.</summary>
    private static string Unbroken(string names) => names.Replace('-', '\u2011');

    /// <summary>
    /// Ce poste redit sa présence et regarde celle des autres. Un poste qui n'était pas là la dernière fois fait paraître
    /// l'avis, qui reste jusqu'à ce qu'on le ferme.
    /// </summary>
    internal void Listen()
    {
        var others = _vaults.Heartbeat();
        void Apply()
        {
            var arrived = others.Where(other => !_known.Contains(other.Name)).Select(other => other.Name).ToList();
            _known.Clear();
            foreach (var other in others)
            {
                _known.Add(other.Name);
            }
            if (arrived.Count > 0)
            {
                Arrival = ComputersText.ArrivedOf(string.Join(", ", arrived));
            }
        }
        if (_ui is null)
        {
            Apply();
        }
        else
        {
            _ui.Post(_ => Apply(), null);
        }
    }

    /// <summary>Ferme l'avis.</summary>
    [RelayCommand]
    private void CloseArrival() => Arrival = null;

    /// <summary>Le coffre ouvert en lecture seule passe en écriture.</summary>
    [RelayCommand]
    private async Task WritableAsync()
    {
        var state = await Task.Run(_vaults.Writable).ConfigureAwait(true);
        Reopened?.Invoke(state);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
