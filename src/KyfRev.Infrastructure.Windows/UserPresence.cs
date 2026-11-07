using System.Runtime.InteropServices;
using KyfRev.Application.Ports;
using Microsoft.Win32;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// La présence de l'utilisateur, par Windows : le temps depuis la dernière saisie de toute la session
/// (<c>GetLastInputInfo</c>), et son départ, quand la session se verrouille ou que le poste se met en veille.
/// </summary>
public sealed partial class UserPresence : IUserPresence, IDisposable
{
    private bool _disposed;

    /// <summary>Suit le verrouillage de la session et la mise en veille.</summary>
    public UserPresence()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    /// <inheritdoc/>
    public event EventHandler? Away;

    /// <summary>Le temps depuis la dernière saisie ; zéro si Windows ne le dit pas (rien ne se verrouille alors pour cela).</summary>
    public TimeSpan Idle => TryReadIdle(out var idle) ? idle : TimeSpan.Zero;

    /// <summary>Cesse de suivre la session et la veille.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _disposed = true;
        }
    }

    /// <summary>
    /// Le temps entre deux relevés de l'horloge de Windows en millisecondes, sur 32 bits : elle repart de zéro tous les
    /// 49 jours, et la soustraction sur 32 bits reste juste par-dessus.
    /// </summary>
    internal static TimeSpan Elapsed(uint now, uint lastInput) => TimeSpan.FromMilliseconds(unchecked(now - lastInput));

    /// <summary>Lit dans Windows le temps depuis la dernière saisie de la session ; faux, et zéro, s'il ne le dit pas.</summary>
    /// <param name="idle">Le temps depuis la dernière saisie.</param>
    internal static bool TryReadIdle(out TimeSpan idle)
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            idle = TimeSpan.Zero;
            return false;
        }
        // L'horloge est relevée après la saisie : l'écart ne passe jamais sous zéro, ce qui ferait 49 jours.
        idle = Elapsed((uint)Environment.TickCount, info.Time);
        return true;
    }

    /// <summary>La session se verrouille : l'utilisateur s'en va.</summary>
    internal void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            Away?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Le poste se met en veille : l'utilisateur s'en va.</summary>
    internal void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            Away?.Invoke(this, EventArgs.Empty);
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LastInputInfo info);

    /// <summary><c>LASTINPUTINFO</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }
}
