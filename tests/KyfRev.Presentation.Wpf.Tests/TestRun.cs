using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Ce qui vaut pour tous les essais de ce projet.</summary>
[TestClass]
public static class TestRun
{
    /// <summary>
    /// Les transitions se terminent aussitôt : une capture ou une mesure prise juste après une navigation porte sur la vue
    /// finie, pas sur une vue à demi apparue. Les transitions elles-mêmes sont essayées dans KyfRev.DesignSystem.Tests.
    /// </summary>
    [AssemblyInitialize]
    public static void Start(TestContext context) => Transitions.Skip = true;
}
