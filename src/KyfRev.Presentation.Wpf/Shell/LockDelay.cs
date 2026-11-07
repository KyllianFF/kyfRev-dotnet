namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Le délai du verrouillage automatique du coffre chiffré ouvert, retenu quand il s'ouvre et quand la carte
/// « Chiffrement » le lit ou l'enregistre. <see cref="AutoLock"/> le lit à chaque regard, toutes les 20 secondes, sans
/// toucher au coffre, qu'une longue opération (un export, une restauration) tient peut-être. Sans coffre chiffré ouvert,
/// rien : l'inactivité ne verrouille rien, et le minuteur ne demande rien.
/// </summary>
public sealed class LockDelay
{
    /// <summary>Le délai sans rien toucher avant le verrouillage ; rien : jamais sur l'inactivité.</summary>
    public TimeSpan? Value { get; private set; }

    /// <summary>Retient le délai réglé dans le coffre, en minutes ; 0 : jamais.</summary>
    /// <param name="minutes">Le délai en minutes, positif ou nul.</param>
    public void Set(int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minutes);
        Value = minutes == 0 ? null : TimeSpan.FromMinutes(minutes);
    }

    /// <summary>Le délai par défaut : le coffre chiffré ouvert n'a pas pu dire le sien.</summary>
    public void Reset() => Value = AutoLock.DefaultDelay;

    /// <summary>Jamais sur l'inactivité : aucun coffre chiffré n'est ouvert.</summary>
    public void Clear() => Value = null;
}
