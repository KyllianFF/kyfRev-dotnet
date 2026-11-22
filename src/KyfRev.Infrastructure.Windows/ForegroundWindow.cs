using System.Runtime.InteropServices;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// Mettre une fenêtre de kyfRev au premier plan quand kyfRev n'est pas l'application active : le menu de l'icône de la
/// barre d'état ne se refermerait pas d'un clic ailleurs, sans cela.
/// </summary>
public static partial class ForegroundWindow
{
    /// <summary>Met la fenêtre au premier plan ; faux si Windows le refuse.</summary>
    public static bool Bring(IntPtr window) => window != IntPtr.Zero && SetForegroundWindow(window);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr window);
}
