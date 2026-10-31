using KyfRev.Application.Model;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.Application.Modules;

/// <summary>
/// Les modules internes connus de l'application, et ceux qu'un coffre active. La racine de
/// composition reconstruit ses services à chaque ouverture de coffre : seuls les modules actifs
/// pour ce coffre y sont déclarés.
/// </summary>
public sealed class ModuleCatalog
{
    /// <summary>Le catalogue des modules compilés avec l'application.</summary>
    /// <exception cref="ArgumentException">Deux modules portent le même identifiant.</exception>
    public ModuleCatalog(IEnumerable<IKyfRevModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        All = [.. modules];
        if (All.GroupBy(module => module.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is { } twice)
        {
            throw new ArgumentException($"Deux modules portent l'identifiant « {twice.Key} ».", nameof(modules));
        }
    }

    /// <summary>Tous les modules connus.</summary>
    public IReadOnlyList<IKyfRevModule> All { get; }

    /// <summary>Les modules actifs pour un coffre qui a ces réglages.</summary>
    public IReadOnlyList<IKyfRevModule> ActiveFor(VaultPreferences preferences) =>
        [.. All.Where(module => module.IsActiveFor(preferences))];

    /// <summary>
    /// Déclare les services des seuls modules actifs ; un module désactivé n'est pas chargé (ses
    /// services ne sont jamais déclarés).
    /// </summary>
    /// <returns>Les modules chargés.</returns>
    public IReadOnlyList<IKyfRevModule> Register(IServiceCollection services, VaultPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(services);
        var active = ActiveFor(preferences);
        foreach (var module in active)
        {
            module.ConfigureServices(services);
        }
        return active;
    }
}
