using System.Collections.Immutable;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// Signale à l'installateur que kyfRev est ouvert (<c>AppMutex</c> de <c>installer/kyfRev.iss</c>) :
/// un mutex nommé pour la session, un autre pour tout le poste, gardés jusqu'à la fermeture.
/// L'installateur demande alors de fermer kyfRev avant de le remplacer ou de le retirer.
/// </summary>
public sealed class OpenMarker : IDisposable
{
    /// <summary>Noms que l'installateur cherche.</summary>
    public static readonly ImmutableArray<string> Names = ["kyfRev", @"Global\kyfRev"];

    private readonly List<Mutex> _held = [];

    /// <summary>Signale kyfRev ouvert sous les noms que l'installateur cherche.</summary>
    public OpenMarker()
        : this(Names)
    {
    }

    internal OpenMarker(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            try
            {
                _held.Add(new Mutex(false, name));
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
            {
                // Nom déjà pris : par kyfRev ouvert dans la session d'une autre personne (accès refusé),
                // ou par un objet d'un autre genre. Le signal manque, l'application fonctionne normalement.
                continue;
            }
        }
    }

    /// <summary>Nombre de noms effectivement tenus.</summary>
    internal int Held => _held.Count;

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var mutex in _held)
        {
            mutex.Dispose();
        }
        _held.Clear();
    }
}
