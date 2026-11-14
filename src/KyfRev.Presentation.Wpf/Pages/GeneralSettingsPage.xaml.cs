using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La page des réglages généraux, qui sert deux sections de la vue « Paramètres » : « Apparence » et « Lecture ». Deux
/// colonnes quand la place le permet, une seule sinon.
/// </summary>
internal sealed partial class GeneralSettingsPage : UserControl
{
    public GeneralSettingsPage()
    {
        InitializeComponent();
    }
}
