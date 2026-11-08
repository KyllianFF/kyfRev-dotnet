namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Une vue qui cherche dans le coffre, de bout en bout, sans le lâcher. Dès qu'une fermeture est demandée, sa recherche
/// s'arrête à la fiche en cours : sinon le fil de l'interface attendrait la fin du passage, et la clé resterait en mémoire
/// jusque-là. Si le coffre reste ouvert finalement, ce qui a été arrêté repart.
/// </summary>
public interface IStopsSearch
{
    /// <summary>Arrête la recherche en cours, sans rien oublier de ce qui est affiché.</summary>
    void StopSearch();

    /// <summary>Le coffre reste ouvert : relance la recherche que <see cref="StopSearch"/> a arrêtée ; rien si aucune ne l'a été.</summary>
    void ResumeSearch();
}
