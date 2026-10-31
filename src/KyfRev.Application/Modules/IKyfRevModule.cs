using KyfRev.Application.Model;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.Application.Modules;

/// <summary>
/// Un module interne de kyfRev (Citations, Schémas réseau…) : un assembly compilé avec
/// l'application, branché par injection de dépendances, activé ou non selon le coffre ouvert. Un
/// module désactivé n'est pas chargé : ses services n'existent pas, et rien n'en dépend. Aucun
/// code .NET tiers n'est jamais chargé dynamiquement.
/// </summary>
public interface IKyfRevModule
{
    /// <summary>Identifiant stable (« citations »).</summary>
    string Id { get; }

    /// <summary>Nom affiché (« Citations »).</summary>
    string Name { get; }

    /// <summary>Vrai si le module est actif pour un coffre qui a ces réglages (<c>coffre.json</c>).</summary>
    bool IsActiveFor(VaultPreferences preferences);

    /// <summary>Déclare les services du module : ses cas d'usage et ses points d'extension.</summary>
    void ConfigureServices(IServiceCollection services);
}
