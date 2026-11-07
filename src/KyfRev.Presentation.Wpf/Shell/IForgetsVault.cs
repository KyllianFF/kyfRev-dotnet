namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Une vue qui garde ce qu'elle a lu du coffre (listes, aperçus, messages, champs remplis) : elle l'oublie quand le coffre
/// se verrouille. Elle revient alors à son état d'avant toute lecture, et relit le coffre à sa prochaine ouverture ; une
/// lecture partie avant l'oubli n'y remet rien.
/// </summary>
public interface IForgetsVault
{
    /// <summary>Oublie tout ce qui vient du coffre.</summary>
    void Forget();
}
