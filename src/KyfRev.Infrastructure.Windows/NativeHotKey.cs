using System.Runtime.InteropServices;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// Les raccourcis globaux de Windows : une combinaison de touches remise à une fenêtre même quand une autre application
/// est au premier plan. Windows refuse une combinaison qu'une autre application tient déjà.
/// </summary>
public static partial class NativeHotKey
{
    private const uint Alt = 0x0001;
    private const uint Control = 0x0002;
    private const uint NoRepeat = 0x4000;

    /// <summary>Le message que Windows envoie à la fenêtre quand le raccourci est tapé ; son paramètre est l'identifiant.</summary>
    public const int Message = 0x0312;

    /// <summary>Demande Ctrl+Alt+<paramref name="key"/> pour la fenêtre, sous l'identifiant donné ; faux si Windows refuse.</summary>
    /// <param name="window">La fenêtre qui recevra le message.</param>
    /// <param name="identifier">Ce qui désignera ce raccourci dans le message.</param>
    /// <param name="key">Le code de touche virtuelle : 0x4E pour N.</param>
    public static bool RegisterControlAlt(IntPtr window, int identifier, uint key) =>
        RegisterHotKey(window, identifier, Control | Alt | NoRepeat, key);

    /// <summary>Rend le raccourci à Windows.</summary>
    public static void Unregister(IntPtr window, int identifier) => _ = UnregisterHotKey(window, identifier);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr window, int id);
}
