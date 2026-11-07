using System.Windows.Threading;
using KyfRev.Application.Ports;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Le coffre chiffré se verrouille de lui-même : après un temps sans rien toucher dans toute la session de Windows (15
/// minutes par défaut ; ce qui se tape dans le texte mis en forme compte aussi), et dès que la session se verrouille ou que
/// le poste se met en veille. Un coffre en clair ne se verrouille pas.
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
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;

    /// <summary>Le verrouillage de lui-même, sur le fil de l'interface de <paramref name="dispatcher"/> (celui qui le crée par défaut).</summary>
    /// <param name="presence">La présence de l'utilisateur.</param>
    /// <param name="delay">Le délai sans rien toucher ; rien : jamais sur l'inactivité (le départ de l'utilisateur verrouille toujours).</param>
    /// <param name="lockAsync">Ce qui verrouille (<see cref="VaultLocking.LockAsync"/>) ; faux s'il n'y avait rien à verrouiller.</param>
    /// <param name="dispatcher">Le fil de l'interface.</param>
    public AutoLock(IUserPresence presence, Func<TimeSpan?> delay, Func<Task<bool>> lockAsync, Dispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(presence);
        ArgumentNullException.ThrowIfNull(delay);
        ArgumentNullException.ThrowIfNull(lockAsync);
        _presence = presence;
        _delay = delay;
        _lock = lockAsync;
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

    /// <summary>Verrouille si l'utilisateur n'a rien touché depuis le délai ; faux sinon, ou s'il n'y avait rien à verrouiller.</summary>
    internal Task<bool> CheckAsync() =>
        _delay() is { } delay && _presence.Idle >= delay ? _lock() : Task.FromResult(false);

    private void OnTick(object? sender, EventArgs e) => _ = CheckAsync();

    /// <summary>L'utilisateur s'en va : le verrouillage se fait sur le fil de l'interface.</summary>
    private void OnAway(object? sender, EventArgs e) => _dispatcher.BeginInvoke(() => _ = _lock());
}
