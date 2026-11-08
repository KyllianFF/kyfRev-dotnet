using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>L'aperçu d'une fiche liée, posé sous le lien : dossier, titre, étiquettes, début du texte, étape. Un clic l'ouvre.</summary>
internal sealed partial class LinkPreviewCard : UserControl
{
    /// <summary>La largeur de la carte, comme dans la 0.x : elle tient dans la fenêtre la plus étroite.</summary>
    public const double CardWidth = 380;

    public LinkPreviewCard()
    {
        InitializeComponent();
        Card.Width = CardWidth;
    }
}
