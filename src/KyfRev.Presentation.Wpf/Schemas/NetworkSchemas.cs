using System.Text.RegularExpressions;
using KyfRev.Application.Model;
using KyfRev.Modules.Schemas;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Schemas;

/// <summary>Ce que les fiches demandent au module Schémas réseau : le dessin d'un bloc, l'insertion, l'ouverture de l'éditeur.</summary>
public interface INetworkSchemas
{
    /// <summary>Le module est actif pour ce coffre.</summary>
    bool IsActive { get; }

    /// <summary>Le texte d'un schéma ; rien s'il ne se lit pas.</summary>
    Task<string?> ReadAsync(string id);

    /// <summary>Les schémas du coffre, pour en insérer un.</summary>
    Task<IReadOnlyList<SchemaSummary>> ListAsync();

    /// <summary>Crée un schéma vide ; rend son identifiant, rien s'il est refusé.</summary>
    Task<string?> CreateAsync(string title);

    /// <summary>Copie un schéma ; rend l'identifiant de la copie, rien si elle est refusée.</summary>
    Task<string?> DuplicateAsync(string id);

    /// <summary>Ouvre l'éditeur sur ce schéma ; le fermer revient à la vue d'où il a été ouvert.</summary>
    void Open(string id);
}

/// <summary>Le bloc qu'une fiche écrit pour citer un schéma, tel que la 0.x l'écrit.</summary>
public static partial class NetworkBlock
{
    /// <summary>Le langage du bloc.</summary>
    public const string Language = "reseau";

    /// <summary>La clé sous laquelle les vues trouvent le module, dans les ressources de l'application.</summary>
    public const string ResourceKey = "Kyf.NetworkSchemas";

    /// <summary>Le contenu du bloc qui cite ce schéma.</summary>
    public static string Reference(string id) => $"schema: {id}";

    /// <summary>L'identifiant cité par un bloc ; rien si ce n'est pas une référence.</summary>
    public static string? ReferenceOf(string? source)
    {
        var found = ReferencePattern().Match(source ?? string.Empty);
        return found.Success ? found.Groups[1].Value : null;
    }

    /// <summary>Le bloc contient le schéma lui-même : le format 1 de la 0.x.</summary>
    public static bool IsInline(string? source) => (source ?? string.Empty).TrimStart().StartsWith('{');

    /// <summary>Le texte à dessiner pour ce bloc ; rien si le bloc ne mène à aucun schéma.</summary>
    public static async Task<string?> TextOfAsync(INetworkSchemas schemas, string? source)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        if (ReferenceOf(source) is { } id)
        {
            return await schemas.ReadAsync(id).ConfigureAwait(true);
        }
        return IsInline(source) ? source : null;
    }

    [GeneratedRegex(@"^\s*schema\s*:\s*(s_[0-9A-Za-z]{26})\s*$")]
    private static partial Regex ReferencePattern();
}

/// <summary>Le module tel qu'il est : ses cas d'usage changent avec le coffre, et il peut ne pas être actif.</summary>
public sealed class NetworkSchemas(Func<SchemaUseCases?> current, Func<ShellViewModel?> shell) : INetworkSchemas
{
    private const string EmptySchema = """{"format":2,"id":"","titre":""}""";

    /// <inheritdoc/>
    public bool IsActive => current() is not null;

    /// <inheritdoc/>
    public async Task<string?> ReadAsync(string id)
    {
        if (current() is not { } schemas)
        {
            return null;
        }
        var read = await Task.Run(() => schemas.Read(id)).ConfigureAwait(true);
        return read.Succeeded ? read.Value.Text : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SchemaSummary>> ListAsync()
    {
        if (current() is not { } schemas)
        {
            return [];
        }
        var list = await Task.Run(schemas.Schemas).ConfigureAwait(true);
        return list.Succeeded ? list.Value : [];
    }

    /// <inheritdoc/>
    public async Task<string?> CreateAsync(string title)
    {
        if (current() is not { } schemas)
        {
            return null;
        }
        var node = System.Text.Json.Nodes.JsonNode.Parse(EmptySchema)!;
        node["titre"] = title;
        var text = node.ToJsonString();
        var created = await Task.Run(() => schemas.Create(text)).ConfigureAwait(true);
        return created.Succeeded ? created.Value.Id : null;
    }

    /// <inheritdoc/>
    public async Task<string?> DuplicateAsync(string id)
    {
        if (current() is not { } schemas)
        {
            return null;
        }
        var copy = await Task.Run(() => schemas.Duplicate(id)).ConfigureAwait(true);
        return copy.Succeeded ? copy.Value.Id : null;
    }

    /// <inheritdoc/>
    public void Open(string id)
    {
        if (shell() is { } view && view.MainNavigation.FirstOrDefault(item => item.Id == SchemasModule.ModuleId) is { Page: SchemasPageViewModel page } entry)
        {
            var from = view.Current;
            page.OpenFrom(id, () => view.Navigate(from));
            view.Navigate(entry);
        }
    }
}
