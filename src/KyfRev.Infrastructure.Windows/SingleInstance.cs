using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// Un seul kyfRev par session Windows et par installation : relancé alors qu'il tourne déjà (réduit dans la barre d'état
/// ou derrière d'autres fenêtres), kyfRev réveille celui qui est ouvert, qui se montre, et se referme. Le nom tient au
/// chemin de l'exécutable : kyfRev installé et une version en cours de développement ne se gênent pas.
/// </summary>
public sealed partial class SingleInstance : IDisposable
{
    /// <summary>Autorise n'importe quel processus à passer au premier plan (ASFW_ANY).</summary>
    private const int AnyProcess = -1;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _wake;
    private RegisteredWaitHandle? _listening;
    private bool _disposed;

    /// <summary>Réclame le nom <paramref name="name"/> pour cette session Windows.</summary>
    public SingleInstance(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _mutex = new Mutex(initiallyOwned: true, @"Local\" + name, out var first);
        IsFirst = first;
        _wake = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + name + ".reveil");
    }

    /// <summary>Réclame la place du kyfRev lancé depuis <paramref name="executable"/>.</summary>
    public static SingleInstance Claim(string executable) => new(NameOf(executable));

    /// <summary>Le nom d'un kyfRev lancé depuis <paramref name="executable"/>, le même d'un lancement à l'autre.</summary>
    public static string NameOf(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        var path = Path.GetFullPath(executable).ToUpperInvariant();
        return "kyfRev.instance." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..16];
    }

    /// <summary>Aucun autre kyfRev de cette installation ne tournait : c'est celui-ci qui reste ouvert.</summary>
    public bool IsFirst { get; }

    /// <summary>
    /// Réveille le kyfRev déjà ouvert. Celui qu'on vient de lancer est au premier plan : il lui cède ce droit, sans quoi
    /// Windows ne ferait que clignoter sa fenêtre dans la barre des tâches.
    /// </summary>
    public void WakeFirst()
    {
        AllowSetForegroundWindow(AnyProcess);
        _wake.Set();
    }

    /// <summary>À chaque relance, <paramref name="woken"/> est appelé, hors du fil de l'interface.</summary>
    public void Listen(Action woken)
    {
        ArgumentNullException.ThrowIfNull(woken);
        _listening?.Unregister(null);
        _listening = ThreadPool.RegisterWaitForSingleObject(_wake, (_, _) => woken(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>Rend le nom : un kyfRev lancé ensuite sera le premier. À appeler sur le fil qui l'a réclamé.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _listening?.Unregister(null);
        if (IsFirst)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
        _wake.Dispose();
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}
