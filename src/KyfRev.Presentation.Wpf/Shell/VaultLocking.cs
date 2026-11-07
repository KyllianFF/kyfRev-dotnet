using System.Windows.Threading;
using KyfRev.Application.UseCases;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Le verrouillage du coffre chiffré ouvert, à la demande (Ctrl+Maj+L) ou de lui-même (<see cref="AutoLock"/>). Dans cet
/// ordre, pour que rien ne se perde ni ne reste :
/// <list type="number">
/// <item>ce qui s'écrit s'enregistre, et la séance en cours se garde ; si ce qui s'écrit n'a pas pu l'être, le coffre reste
/// ouvert ;</item>
/// <item>l'écran de déverrouillage remplace les vues, qui se ferment (le texte mis en forme et les schémas avec elles) ;</item>
/// <item>le coffre se ferme, une fois finie la lecture en cours, et la clé de son stockage est effacée ;</item>
/// <item>chaque vue oublie ce qu'elle avait lu du coffre, après les lectures qui finissaient.</item>
/// </list>
/// Il ne se rouvre ensuite qu'avec son mot de passe ou sa clé de récupération.
/// </summary>
/// <param name="shell">La coquille.</param>
/// <param name="vaults">Ouverture et verrouillage des coffres.</param>
/// <param name="picker">Le choix d'un autre coffre, depuis l'écran de déverrouillage.</param>
/// <param name="forget">Ce qui fait tout oublier aux vues (<see cref="IForgetsVault"/>) et aux moteurs.</param>
/// <param name="changed">Ce qui suit la réouverture d'un coffre ; rien : l'espace de travail revient.</param>
public sealed class VaultLocking(ShellViewModel shell, VaultUseCases vaults, IFolderPicker picker, Action forget, Action? changed = null)
{
    private bool _locking;

    /// <summary>
    /// Verrouille le coffre chiffré ouvert. Faux s'il n'y a rien à verrouiller (aucun coffre, un coffre en clair, l'écran de
    /// déverrouillage déjà là, un verrouillage en cours), ou si ce qui s'écrit n'a pas pu être enregistré.
    /// </summary>
    public async Task<bool> LockAsync()
    {
        if (_locking || shell.IsWelcome || vaults.State() is not { Encrypted: true, Vault: { } root })
        {
            return false;
        }
        _locking = true;
        try
        {
            if (shell.Current.Page is ILeaving page && !await page.LeaveAsync().ConfigureAwait(true))
            {
                return false;
            }
            var waiting = vaults.State() with { Vault = null, Locked = root, OpenError = null, Encrypted = false };
            VaultStartup.ShowUnlock(shell, vaults, picker, waiting, changed);
            // Les vues qui viennent de se fermer finissent ce qu'elles avaient commencé.
            await Dispatcher.Yield(DispatcherPriority.Background);
            vaults.Lock();
            // Une lecture finie juste avant la fermeture rend encore son résultat : il arrive avant l'oubli.
            await Dispatcher.Yield(DispatcherPriority.Background);
            forget();
            return true;
        }
        finally
        {
            _locking = false;
        }
    }
}
