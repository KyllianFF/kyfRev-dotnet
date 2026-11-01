using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Infrastructure.Windows;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La racine de composition, et les règles qui séparent la présentation de l'infrastructure.</summary>
[TestClass]
public sealed class CompositionTests
{
    [STATestMethod]
    public void The_application_assembles_the_shell_with_the_windows_services()
    {
        using var services = KyfRev.App.Composition.Build();

        Assert.IsInstanceOfType<WindowsAppearance>(services.GetRequiredService<ISystemAppearance>());
        Assert.IsInstanceOfType<WindowChrome>(services.GetRequiredService<IWindowChrome>());
        var window = services.GetRequiredService<ShellWindow>();
        var shell = (ShellViewModel)window.DataContext;
        Assert.AreSame(services.GetRequiredService<ShellViewModel>(), shell);
        Assert.HasCount(5, shell.MainNavigation);
        Assert.HasCount(2, shell.SecondaryNavigation);
        window.Close();
    }

    [TestMethod]
    public void The_application_uses_the_vault_locations_of_the_running_program()
    {
        using var services = KyfRev.App.Composition.Build();

        Assert.IsInstanceOfType<VaultLocations>(services.GetRequiredService<IVaultLocations>());
        Assert.IsInstanceOfType<FolderPicker>(services.GetRequiredService<IFolderPicker>());
        var vaults = services.GetRequiredService<VaultUseCases>();
        Assert.AreSame(services.GetRequiredService<KyfRevApplication>().Vaults, vaults);
        // Ni portable.json ni installation.json à côté des essais : mode développement, aucun coffre ouvert.
        var state = vaults.State();
        Assert.AreEqual(AppMode.Development, state.Mode);
        Assert.IsNull(state.Vault);
        Assert.AreEqual(KyfRev.App.Composition.Version, state.Version);
        Assert.DoesNotContain("+", state.Version);
        Assert.StartsWith("1.0.0", state.Version);
    }

    [TestMethod]
    public void The_presentation_references_no_infrastructure()
    {
        var references = typeof(ShellViewModel).Assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToList();
        Assert.IsFalse(references.Any(name => name.StartsWith("KyfRev.Infrastructure.", StringComparison.Ordinal)), string.Join(", ", references));
        CollectionAssert.Contains(references, "KyfRev.Application");
        CollectionAssert.Contains(references, "KyfRev.DesignSystem");
        var designSystem = typeof(KyfRev.DesignSystem.DesignResources).Assembly.GetReferencedAssemblies().Select(reference => reference.Name!);
        Assert.IsFalse(designSystem.Any(name => name.StartsWith("KyfRev.", StringComparison.Ordinal)));
    }
}
