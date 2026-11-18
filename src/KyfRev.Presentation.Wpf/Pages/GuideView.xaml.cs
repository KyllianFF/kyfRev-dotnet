using System.Windows;
using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Le guide de kyfRev, lu dans l'aide : son plan à gauche et le texte de la partie choisie ; en fenêtre étroite, le plan
/// passe dans une liste déroulante au-dessus du texte.
/// </summary>
internal sealed partial class GuideView : UserControl
{
    /// <summary>En deçà, le plan ne tient plus à côté du texte.</summary>
    internal const double SidePlanMinimum = 560;

    public GuideView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width);
    }

    private void Arrange(double width)
    {
        var wide = width >= SidePlanMinimum;
        Plan.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        PlanChoice.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
    }
}
