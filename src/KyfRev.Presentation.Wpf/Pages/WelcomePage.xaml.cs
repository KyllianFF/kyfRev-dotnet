using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Vue de l'accueil, sans coffre ouvert : trois entrées (créer, ouvrir, essayer avec un coffre d'exemple) et la méthode
/// des J en trois temps. Côte à côte tant que la place le permet ; l'une sous l'autre en fenêtre étroite.
/// </summary>
internal sealed partial class WelcomePage : UserControl
{
    /// <summary>En deçà, trois cartes ne tiennent plus côte à côte : elles s'empilent.</summary>
    internal const double SideBySideMinimum = 760;

    public WelcomePage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width);
    }

    private void Arrange(double width)
    {
        var stacked = width < SideBySideMinimum;
        // Une rangée, ou une colonne : UniformGrid compte les entrées visibles.
        Choices.Rows = Steps.Rows = stacked ? 0 : 1;
        Choices.Columns = Steps.Columns = stacked ? 1 : 0;
    }
}
