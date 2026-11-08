using KyfRev.Application.Ports;

namespace KyfRev.Application.UseCases;

/// <summary>
/// Le coffre ouvert, partagé par tous les cas d'usage, ceux des modules compris. Une seule
/// opération à la fois : l'écran, les tâches de fond et les modules passent par lui.
/// </summary>
public sealed class VaultHost : IDisposable
{
    /// <summary>Message de refus quand aucun coffre n'est ouvert (celui de la 0.x).</summary>
    public const string NoVaultOpen = "Aucun coffre n'est ouvert.";

    private readonly Lock _gate = new();
    private IVault? _vault;

    /// <summary>Le dossier de <see cref="_vault"/>, tenu à part : il se lit sans passer par <see cref="_gate"/>.</summary>
    private volatile string? _root;

    /// <summary>
    /// Dossier du coffre ouvert ; rien si aucun ne l'est. Se lit sans attendre : une longue opération (un import) tient le
    /// coffre, et le fil de l'interface n'a pas à la laisser finir pour savoir lequel est ouvert.
    /// </summary>
    public string? Root => _root;

    /// <summary>Ferme le coffre ouvert (son verrou est libéré).</summary>
    public void Dispose() => Close();

    /// <summary>
    /// Ferme le coffre ouvert, une fois finie l'opération en cours : son verrou est libéré, et la clé d'un coffre chiffré
    /// effacée. Un autre coffre peut ensuite prendre sa place.
    /// </summary>
    internal void Close()
    {
        lock (_gate)
        {
            // Retiré d'abord : même si sa fermeture échoue, plus rien ne l'atteint.
            var vault = _vault;
            _vault = null;
            _root = null;
            vault?.Dispose();
        }
    }

    /// <summary>
    /// Exécute <paramref name="action"/> sur le coffre ouvert. Un refus du coffre devient un
    /// <see cref="Outcome{T}"/> refusé, avec son message.
    /// </summary>
    public Outcome<T> Use<T>(Func<IVault, T> action)
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
    public Outcome<Done> Use(Action<IVault> action) => Use(vault =>
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
            _root = vault.Root;
        }
        previous?.Dispose();
    }
}
