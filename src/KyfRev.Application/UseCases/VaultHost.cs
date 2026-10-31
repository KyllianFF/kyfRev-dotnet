using KyfRev.Application.Ports;

namespace KyfRev.Application.UseCases;

/// <summary>
/// Le coffre ouvert, partagé par tous les cas d'usage. Une seule opération à la fois : l'écran,
/// les tâches de fond et les modules passent par lui.
/// </summary>
public sealed class VaultHost : IDisposable
{
    /// <summary>Message de refus quand aucun coffre n'est ouvert (celui de la 0.x).</summary>
    public const string NoVaultOpen = "Aucun coffre n'est ouvert.";

    private readonly Lock _gate = new();
    private IVault? _vault;

    /// <summary>Dossier du coffre ouvert ; rien si aucun ne l'est.</summary>
    public string? Root
    {
        get
        {
            lock (_gate)
            {
                return _vault?.Root;
            }
        }
    }

    /// <summary>Ferme le coffre ouvert (son verrou est libéré).</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _vault?.Dispose();
            _vault = null;
        }
    }

    /// <summary>
    /// Exécute <paramref name="action"/> sur le coffre ouvert. Un refus du coffre devient un
    /// <see cref="Outcome{T}"/> refusé, avec son message.
    /// </summary>
    internal Outcome<T> Use<T>(Func<IVault, T> action)
    {
        lock (_gate)
        {
            if (_vault is null)
            {
                return Outcome.Refused<T>(NoVaultOpen);
            }
            try
            {
                return Outcome.Of(action(_vault));
            }
            catch (OperationException exception)
            {
                return Outcome.Refused<T>(exception.Message);
            }
        }
    }

    /// <summary>Comme <see cref="Use{T}"/>, pour une opération sans valeur.</summary>
    internal Outcome<Done> Use(Action<IVault> action) => Use(vault =>
    {
        action(vault);
        return default(Done);
    });

    /// <summary>Vrai si le coffre ouvert est celui de <paramref name="root"/>.</summary>
    internal bool IsOpen(string root, Func<string, string, bool> sameLocation)
    {
        lock (_gate)
        {
            return _vault is not null && sameLocation(root, _vault.Root);
        }
    }

    /// <summary>Le coffre ouvert est remplacé ; l'ancien n'est fermé qu'une fois le nouveau en place.</summary>
    internal void Replace(IVault vault)
    {
        IVault? previous;
        lock (_gate)
        {
            previous = _vault;
            _vault = vault;
        }
        previous?.Dispose();
    }
}
