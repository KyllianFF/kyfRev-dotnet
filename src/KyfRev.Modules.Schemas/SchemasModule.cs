using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.Modules.Schemas;

/// <summary>
/// Module Schémas réseau : actif quand le coffre l'active (<c>coffre.json</c>, réglage du module), ou, sans réglage, s'il a déjà des
/// schémas. Il apporte ses cas d'usage et une entrée du menu. Il attend du cœur le coffre ouvert (<see cref="KyfRev.Application.UseCases.VaultHost"/>).
/// </summary>
public sealed class SchemasModule : IKyfRevModule
{
    /// <summary>Identifiant du module.</summary>
    public const string ModuleId = "schemas";

    /// <inheritdoc/>
    public string Id => ModuleId;

    /// <inheritdoc/>
    public string Name => "Schémas réseau";

    /// <inheritdoc/>
    public bool IsActiveFor(VaultPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return preferences.SchemasActive;
    }

    /// <inheritdoc/>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SchemaUseCases>();
        services.AddSingleton<INavigationContributor, SchemasNavigation>();
    }
}

/// <summary>L'entrée « Schémas réseau » du menu.</summary>
internal sealed class SchemasNavigation : INavigationContributor
{
    private const int MenuOrder = 45;

    /// <inheritdoc/>
    public NavigationEntry Entry { get; } = new(SchemasModule.ModuleId, "Schémas réseau", MenuOrder);
}
