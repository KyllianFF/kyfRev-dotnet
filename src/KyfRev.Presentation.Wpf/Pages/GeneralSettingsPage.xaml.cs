using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La page des réglages généraux, qui sert deux sections de la vue « Paramètres » : « Apparence » et « Lecture ». Deux
/// colonnes quand la place le permet, une seule sinon.
/// </summary>
internal sealed partial class GeneralSettingsPage : UserControl
{
    /// <summary>En deçà, les cartes se suivent sur une seule colonne.</summary>
    internal const double TwoColumns = 720;

    public GeneralSettingsPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Sections.Columns = e.NewSize.Width < TwoColumns ? 1 : 2;
    }
}
