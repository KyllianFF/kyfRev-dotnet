using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.App;

/// <summary>
/// Les services des modules actifs pour le coffre ouvert, reconstruits à chaque ouverture de coffre
/// et à chaque activation : un module désactivé n'a aucun service, et rien n'en dépend.
/// </summary>
internal sealed class ActiveModules : IDisposable
{
    private readonly ModuleCatalog _catalog;
    private readonly Action<IServiceCollection> _core;
    private ServiceProvider? _services;

    /// <summary>Les modules de <paramref name="catalog"/> ; <paramref name="core"/> déclare ce qu'ils attendent du cœur.</summary>
    public ActiveModules(ModuleCatalog catalog, Action<IServiceCollection> core)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(core);
        _catalog = catalog;
        _core = core;
    }

    /// <summary>Les modules chargés viennent de changer.</summary>
    public event EventHandler? Changed;

    /// <summary>Les encarts du tableau de bord, relus à chaque lecture.</summary>
    public IEnumerable<IDashboardWidget> Widgets => Live<IDashboardWidget>();

    /// <summary>Les contrôles de « Santé du coffre », relus à chaque lecture.</summary>
    public IEnumerable<IVaultHealthCheck> HealthChecks => Live<IVaultHealthCheck>();

    /// <summary>Charge les modules actifs pour ces réglages ; aucun si aucun coffre n'est ouvert.</summary>
    public void Load(VaultPreferences? preferences)
    {
        var services = new ServiceCollection();
        _core(services);
        if (preferences is not null)
        {
            _catalog.Register(services, preferences);
        }
        var previous = _services;
        _services = services.BuildServiceProvider();
        previous?.Dispose();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Le service d'un module actif ; rien si aucun ne l'apporte.</summary>
    public T? Find<T>()
        where T : class => _services?.GetService<T>();

    /// <summary>Tous les services de ce type apportés par les modules actifs.</summary>
    public IEnumerable<T> All<T>()
        where T : notnull => _services?.GetServices<T>() ?? [];

    /// <inheritdoc/>
    public void Dispose() => _services?.Dispose();

    /// <summary>Les modules changent après la composition : on relit ce qui est actif à chaque parcours.</summary>
    private IEnumerable<T> Live<T>()
        where T : notnull
    {
        foreach (var service in All<T>())
        {
            yield return service;
        }
    }
}
