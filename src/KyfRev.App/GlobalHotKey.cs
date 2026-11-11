using System.Windows;
using System.Windows.Interop;
using KyfRev.Infrastructure.Windows;

namespace KyfRev.App;

/// <summary>
/// Un raccourci clavier global, que Windows remet à la fenêtre même quand une autre application est au premier plan :
/// Ctrl+Alt+N, pour la capture rapide. Il n'est demandé à Windows que si l'utilisateur l'a activé, et rendu dès qu'il le
/// coupe ou que la fenêtre se ferme. Si une autre application le tient déjà, l'enregistrement échoue et le dit.
/// </summary>
internal sealed class GlobalHotKey
{
    /// <summary>Le raccourci, tel qu'il s'écrit pour l'utilisateur.</summary>
    public const string Keys = "Ctrl+Alt+N";

    private const int Identifier = 0x4B52;
    private const uint KeyN = 0x4E;

    private readonly Window _window;
    private readonly Action _pressed;
    private HwndSource? _source;

    /// <summary>Le raccourci de <paramref name="window"/> ; <paramref name="pressed"/> est appelé quand il est tapé.</summary>
    public GlobalHotKey(Window window, Action pressed)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(pressed);
        _window = window;
        _pressed = pressed;
    }

    /// <summary>Windows tient le raccourci pour cette fenêtre.</summary>
    public bool IsRegistered => _source is not null;

    /// <summary>
    /// Demande le raccourci à Windows, ou le rend.
    /// </summary>
    /// <returns>Faux si on le voulait et que Windows l'a refusé : une autre application le tient.</returns>
    public bool Set(bool wanted)
    {
        if (wanted == IsRegistered)
        {
            return true;
        }
        if (!wanted)
        {
            Release();
            return true;
        }
        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero || HwndSource.FromHwnd(handle) is not { } source)
        {
            // La fenêtre n'est pas encore montrée : rien à quoi remettre le raccourci.
            return false;
        }
        if (!NativeHotKey.RegisterControlAlt(handle, Identifier, KeyN))
        {
            return false;
        }
        source.AddHook(OnMessage);
        _source = source;
        return true;
    }

    /// <summary>Rend le raccourci à Windows ; sans effet s'il n'était pas tenu.</summary>
    public void Release()
    {
        if (_source is not { } source)
        {
            return;
        }
        source.RemoveHook(OnMessage);
        NativeHotKey.Unregister(source.Handle, Identifier);
        _source = null;
    }

    private IntPtr OnMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeHotKey.Message && wParam.ToInt32() == Identifier)
        {
            handled = true;
            _pressed();
        }
        return IntPtr.Zero;
    }
}
