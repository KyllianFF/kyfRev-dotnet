using System.Windows.Threading;
using KyfRev.Application.Ports;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Le coffre chiffré se verrouille de lui-même : après un temps sans rien toucher dans toute la session de Windows (15
/// minutes par défaut ; ce qui se tape dans le texte mis en forme compte aussi), et dès que la session se verrouille ou que
/// le poste se met en veille. Un départ qui tombe pendant une ouverture reste dû : le coffre se verrouille dès qu'il le
/// peut, sauf si l'utilisateur est revenu entre-temps. Un coffre en clair ne se verrouille pas.
/// </summary>
public sealed class AutoLock : IDisposable
{
    /// <summary>Le délai par défaut.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMinutes(15);

    /// <summary>Le temps entre deux regards sur l'inactivité.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(20);

    private readonly IUserPresence _presence;
    private readonly Func<TimeSpan?> _delay;
    private readonly Func<Task<bool>> _lock;
    private readonly Func<long> _clock;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;

    /// <summary>Le moment du départ qui n'a pas encore pu verrouiller (en millisecondes de <c>_clock</c>) ; rien s'il n'y en a pas.</summary>
    private long? _awayAt;

    /// <summary>Le verrouillage de lui-même, sur le fil de l'interface de <paramref name="dispatcher"/> (celui qui le crée par défaut).</summary>
    /// <param name="presence">La présence de l'utilisateur.</param>
    /// <param name="delay">
    /// Le délai sans rien toucher ; rien : jamais sur l'inactivité (le départ de l'utilisateur verrouille toujours). Il est lu
    /// à chaque regard, sur le fil de l'interface : il ne touche pas au coffre (<see cref="LockDelay"/>).
    /// </param>
    /// <param name="lockAsync">Ce qui verrouille (<see cref="VaultLocking.LockAsync"/>) ; faux s'il n'y avait rien à verrouiller.</param>
    /// <param name="dispatcher">Le fil de l'interface.</param>
    /// <param name="clock">L'horloge en millisecondes ; rien : celle du système.</param>
    public AutoLock(IUserPresence presence, Func<TimeSpan?> delay, Func<Task<bool>> lockAsync, Dispatcher? dispatcher = null, Func<long>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(presence);
        ArgumentNullException.ThrowIfNull(delay);
        ArgumentNullException.ThrowIfNull(lockAsync);
        _presence = presence;
        _delay = delay;
        _lock = lockAsync;
        _clock = clock ?? (() => Environment.TickCount64);
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer(CheckInterval, DispatcherPriority.Background, OnTick, _dispatcher);
        _timer.Stop();
        presence.Away += OnAway;
    }

    /// <summary>Commence à regarder l'inactivité.</summary>
    public void Start() => _timer.Start();

    /// <summary>Cesse de regarder.</summary>
    public void Dispose()
    {
        _timer.Stop();
        _presence.Away -= OnAway;
    }

    /// <summary>
    /// Verrouille si un départ est encore dû (et que l'utilisateur n'est pas revenu depuis), ou s'il n'a rien touché depuis le
    /// délai ; faux sinon, ou s'il n'y avait rien à verrouiller.
    /// </summary>
    internal async Task<bool> CheckAsync()
    {
        if (_awayAt is { } away)
        {
            if (_presence.Idle < TimeSpan.FromMilliseconds(_clock() - away))
            {
                // Une saisie depuis le départ : l'utilisateur est revenu.
                _awayAt = null;
            }
            else
            {
                return await LeaveAsync().ConfigureAwait(true);
            }
        }
        return _delay() is { } delay && _presence.Idle >= delay && await _lock().ConfigureAwait(true);
    }

    private void OnTick(object? sender, EventArgs e) => _ = CheckAsync();

    /// <summary>L'utilisateur s'en va : le verrouillage se fait sur le fil de l'interface, et reste dû s'il n'a pas pu se faire.</summary>
    private void OnAway(object? sender, EventArgs e) => _dispatcher.BeginInvoke(() =>
    {
        _awayAt = _clock();
        _ = LeaveAsync();
    });

    private async Task<bool> LeaveAsync()
    {
        var locked = await _lock().ConfigureAwait(true);
        if (locked)
        {
            _awayAt = null;
        }
        return locked;
    }
}
