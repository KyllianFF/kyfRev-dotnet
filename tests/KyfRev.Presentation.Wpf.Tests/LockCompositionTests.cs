using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Le verrouillage tel que l'application l'assemble : chaque vue de la présentation qui garde ce qu'elle a lu du coffre
/// (<see cref="IForgetsVault"/>), et que la composition tient en un seul exemplaire, l'oublie quand il se verrouille. Une
/// vue ajoutée sans être ajoutée à la liste de l'oubli garderait le coffre verrouillé à l'écran.
/// </summary>
[TestClass]
public sealed class LockCompositionTests
{
    [STATestMethod]
    public void Every_view_the_application_keeps_forgets_the_vault_when_it_locks()
    {
        using var services = KyfRev.App.Composition.Build();
        var registered = services.GetRequiredService<IServiceProviderIsService>();
        var forgetting = KyfRev.App.Composition.ForgettingViews(services);

        var kept = typeof(IForgetsVault).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IForgetsVault).IsAssignableFrom(type) && registered.IsService(type))
            .ToList();
        Assert.IsNotEmpty(kept, "les pages qui lisent le coffre sont tenues par la composition");

        var missing = new List<string>();
        foreach (var type in kept)
        {
            var view = services.GetRequiredService(type);
            // Une vue que la composition tient en un seul exemplaire : celui que les pages montrent, et que la liste doit atteindre.
            if (ReferenceEquals(view, services.GetRequiredService(type)) && !forgetting.Any(item => ReferenceEquals(item, view)))
            {
                missing.Add(type.Name);
            }
        }
        Assert.AreEqual(string.Empty, string.Join(", ", missing), "ces vues garderaient le coffre verrouillé");

        // La liste ne fait pas de nouvelles vues : ce sont celles que les pages montrent, chacune une fois.
        Assert.HasCount(forgetting.Length, forgetting.Distinct(ReferenceEqualityComparer.Instance));
        foreach (var view in forgetting.Where(item => registered.IsService(item.GetType())))
        {
            Assert.AreSame(services.GetRequiredService(view.GetType()), view);
        }
    }

    [STATestMethod]
    public void Another_vault_forgets_what_the_views_read_from_the_previous_one()
    {
        using var ui = new UiThread();
        using var services = KyfRev.App.Composition.Build();
        var workload = services.GetRequiredService<KyfRev.Presentation.Wpf.Pages.WorkloadPageViewModel>();
        var read = workload.RefreshCommand.ExecuteAsync(null);
        UiThread.Until(() => read.IsCompleted);
        Assert.IsTrue(workload.HasError, "sans coffre ouvert, la lecture est refusée, et la vue le garde");

        KyfRev.App.Composition.VaultChanged(services);

        Assert.IsFalse(workload.HasError);
    }
}
