namespace KyfRev.Application.Ports;

/// <summary>
/// La présence de l'utilisateur à ce poste : depuis quand il n'a rien touché, dans toute la session de Windows (ce qu'il
/// tape dans le texte mis en forme compris), et le moment où il s'en va, sa session verrouillée ou le poste mis en veille.
/// Le coffre chiffré se verrouille sur l'un ou l'autre.
/// </summary>
public interface IUserPresence
{
    /// <summary>Le temps écoulé depuis la dernière saisie : clavier, souris, toucher.</summary>
    TimeSpan Idle { get; }

    /// <summary>La session de Windows se verrouille, ou le poste se met en veille. Peut venir d'un autre fil.</summary>
    event EventHandler? Away;
}
