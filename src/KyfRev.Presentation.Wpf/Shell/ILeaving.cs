namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Une page qui a quelque chose à finir avant que le coffre se verrouille : ce qui s'écrit s'enregistre, une séance se
/// garde pour être reprise. Rien ne doit se perdre.
/// </summary>
public interface ILeaving
{
    /// <summary>Finit ce qui est en cours ; faux si ce qui s'écrit n'a pas pu être enregistré (le coffre reste alors ouvert).</summary>
    Task<bool> LeaveAsync();

    /// <summary>Un brouillon reste en cours, que <see cref="LeaveAsync"/> n'a pas pu enregistrer : un verrouillage l'abandonne, une conversion attend.</summary>
    bool HasUnsavedDraft => false;
}
