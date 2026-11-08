using System.Globalization;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.Modules.Citations;

/// <summary>
/// Module Citations : actif quand le coffre l'active (<c>coffre.json</c>, réglage du module). Il
/// apporte ses cas d'usage, une entrée du menu, l'encart « Citation du jour » du tableau de bord,
/// ses résultats pour la recherche globale et le signalement des fichiers de citation illisibles.
/// Il attend du cœur : le coffre ouvert
/// (<see cref="VaultHost"/>), l'horloge, le lecteur en ligne et le journal.
/// </summary>
public sealed class CitationsModule : IKyfRevModule
{
    /// <summary>Identifiant du module.</summary>
    public const string ModuleId = "citations";

    /// <inheritdoc/>
    public string Id => ModuleId;

    /// <inheritdoc/>
    public string Name => "Citations";

    /// <inheritdoc/>
    public bool IsActiveFor(VaultPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return preferences.QuotesActive;
    }

    /// <inheritdoc/>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<OnlineQuoteSearch>();
        services.AddSingleton<QuoteUseCases>();
        services.AddSingleton<INavigationContributor, QuotesNavigation>();
        services.AddSingleton<IDashboardWidget, QuoteOfTheDayCard>();
        services.AddSingleton<IVaultHealthCheck, UnreadableQuotes>();
        services.AddSingleton<ISearchProvider, QuoteSearch>();
    }
}

/// <summary>L'entrée « Citations » du menu.</summary>
internal sealed class QuotesNavigation : INavigationContributor
{
    public NavigationEntry Entry { get; } = new(CitationsModule.ModuleId, "Citations", 60);
}

/// <summary>L'encart « Citation du jour » du tableau de bord.</summary>
internal sealed class QuoteOfTheDayCard(QuoteUseCases quotes) : IDashboardWidget
{
    public string Id => "citation-du-jour";

    public int Order => 10;

    public DashboardCard? Card() => quotes.QuoteOfTheDay() is { Succeeded: true, Value: { } quote }
        ? new DashboardCard("Citation du jour", quote.Content.Text, Credit(quote))
        : null;

    /// <summary>« Lavoisier, Traité élémentaire de chimie (1789) ».</summary>
    private static string? Credit(Quote quote)
    {
        var parts = new[] { quote.Content.Author, quote.Content.Source.Title }.Where(part => part is not null).ToList();
        var credit = string.Join(", ", parts);
        if (quote.Year is { } year)
        {
            credit = credit.Length == 0 ? year.ToString(CultureInfo.InvariantCulture) : $"{credit} ({year.ToString(CultureInfo.InvariantCulture)})";
        }
        return credit.Length == 0 ? null : credit;
    }
}

/// <summary>Les citations hors corbeille, dans la recherche globale : le texte se lit à la place de l'extrait quand l'auteur, l'œuvre ou les étiquettes ont suffi.</summary>
internal sealed class QuoteSearch(VaultHost host) : ISearchProvider
{
    public Outcome<IReadOnlyList<SearchHit>> Search(string query, CancellationToken cancellationToken) => host.Use<IReadOnlyList<SearchHit>>(vault =>
    [
        .. vault.SearchQuotes(query, cancellationToken)
            .Select(hit => new SearchHit(SearchKinds.Quote, hit.Path, hit.Author ?? string.Empty, hit.Excerpt) { Text = hit.Excerpt.IsEmpty ? hit.Text : null }),
    ]);
}

/// <summary>Les fichiers de citation illisibles, signalés dans « Santé du coffre » (ils ne sont jamais réécrits).</summary>
internal sealed class UnreadableQuotes(VaultHost host) : IVaultHealthCheck
{
    public IReadOnlyList<HealthNotice> Notices() => host.Use(vault => vault.Quotes().Unreadable) is { Succeeded: true } list
        ? [.. list.Value.Select(file => new HealthNotice(file.Path, $"Citation illisible : {file.Message}"))]
        : [];
}
