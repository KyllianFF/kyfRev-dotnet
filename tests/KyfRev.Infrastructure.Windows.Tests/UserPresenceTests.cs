using Microsoft.Win32;

namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>La présence de l'utilisateur : le temps depuis la dernière saisie, et son départ quand Windows se verrouille ou se met en veille.</summary>
[TestClass]
public sealed class UserPresenceTests
{
    [TestMethod]
    public void The_idle_time_is_read_from_windows()
    {
        // Windows le dit : ce n'est pas le zéro de repli.
        Assert.IsTrue(UserPresence.TryReadIdle(out var idle));

        // Le temps écoulé est positif et borné par celui depuis le démarrage du poste.
        Assert.IsGreaterThanOrEqualTo(TimeSpan.Zero, idle);
        Assert.IsLessThanOrEqualTo(TimeSpan.FromMilliseconds(Environment.TickCount64), idle);
        using var presence = new UserPresence();
        Assert.IsGreaterThanOrEqualTo(TimeSpan.Zero, presence.Idle);
        Assert.IsLessThanOrEqualTo(TimeSpan.FromMilliseconds(Environment.TickCount64), presence.Idle);
    }

    [TestMethod]
    public void The_idle_time_survives_the_clock_starting_again_at_zero()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(5), UserPresence.Elapsed(10_000, 5_000));
        // L'horloge de Windows, sur 32 bits, repart de zéro tous les 49 jours.
        Assert.AreEqual(TimeSpan.FromMilliseconds(1_500), UserPresence.Elapsed(1_000, uint.MaxValue - 499));
        Assert.AreEqual(TimeSpan.Zero, UserPresence.Elapsed(42, 42));
    }

    [TestMethod]
    public void Leaving_is_a_locked_session_or_a_sleeping_computer_nothing_else()
    {
        using var presence = new UserPresence();
        var away = 0;
        presence.Away += (_, _) => away++;

        presence.OnSessionSwitch(null, new SessionSwitchEventArgs(SessionSwitchReason.SessionUnlock));
        presence.OnSessionSwitch(null, new SessionSwitchEventArgs(SessionSwitchReason.ConsoleConnect));
        presence.OnPowerModeChanged(null, new PowerModeChangedEventArgs(PowerModes.Resume));
        Assert.AreEqual(0, away);

        presence.OnSessionSwitch(null, new SessionSwitchEventArgs(SessionSwitchReason.SessionLock));
        Assert.AreEqual(1, away);
        presence.OnPowerModeChanged(null, new PowerModeChangedEventArgs(PowerModes.Suspend));
        Assert.AreEqual(2, away);

        presence.Dispose();
        presence.Dispose();
    }
}
