using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.Application.Tests;

/// <summary>Le catalogue des modules : seuls ceux qu'un coffre active déclarent leurs services.</summary>
[TestClass]
public sealed class ModuleCatalogTests
{
    private static readonly VaultPreferences Preferences = new([1, 3, 7, 14, 30], true, 2, 30, [2, 4, 8, 12], true, true, false, true, ["philosophie"], false);

    [TestMethod]
    public void Only_the_modules_a_vault_activates_declare_their_services()
    {
        var quotes = new FakeModule("citations", preferences => preferences.QuotesActive);
        var schemas = new FakeModule("schemas", preferences => preferences.SchemasActive);
        var catalog = new ModuleCatalog([quotes, schemas]);
        var services = new ServiceCollection();

        Assert.IsEmpty(catalog.Register(services, Preferences));
        Assert.IsEmpty(services);
        Assert.AreEqual(0, quotes.Configured + schemas.Configured);

        var loaded = catalog.Register(services, Preferences with { QuotesActive = true });

        Assert.AreSame(quotes, loaded.Single());
        Assert.AreEqual(1, quotes.Configured);
        Assert.AreEqual(0, schemas.Configured);
        Assert.AreEqual(typeof(FakeModule), services.Single().ServiceType);
        CollectionAssert.AreEqual(new IKyfRevModule[] { quotes, schemas }, catalog.All.ToList());
        CollectionAssert.AreEqual(new IKyfRevModule[] { quotes, schemas }, catalog.ActiveFor(Preferences with { QuotesActive = true, SchemasActive = true }).ToList());
    }

    [TestMethod]
    public void Two_modules_cannot_share_an_identifier()
    {
        var refusal = Assert.ThrowsExactly<ArgumentException>(() => new ModuleCatalog([new FakeModule("citations", _ => true), new FakeModule("citations", _ => false)]));

        Assert.StartsWith("Deux modules portent l'identifiant « citations ».", refusal.Message);
        Assert.ThrowsExactly<ArgumentNullException>(() => new ModuleCatalog(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ModuleCatalog([]).Register(null!, Preferences));
    }

    [TestMethod]
    public void Extension_points_are_plain_values()
    {
        Assert.AreEqual(new SearchHit(SearchKinds.Sheet, "f_1", "Mitose", []), new SearchHit(SearchKinds.Sheet, "f_1", "Mitose", []));
        Assert.AreEqual("citations/x.md", new HealthNotice("citations/x.md", "illisible").Path);
        Assert.IsNull(new DashboardCard("Titre", "Texte", null).Detail);
        Assert.AreEqual(60, new NavigationEntry("citations", "Citations", 60).Order);
    }

    /// <summary>Module d'essai : il compte ses déclarations de services.</summary>
    private sealed class FakeModule(string id, Func<VaultPreferences, bool> active) : IKyfRevModule
    {
        public int Configured { get; private set; }

        public string Id => id;

        public string Name => id;

        public bool IsActiveFor(VaultPreferences preferences) => active(preferences);

        public void ConfigureServices(IServiceCollection services)
        {
            Configured++;
            services.AddSingleton(this);
        }
    }
}
