using System.Windows.Threading;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Le verrouillage du coffre chiffré ouvert, à la demande (Ctrl+Maj+L) ou de lui-même (<see cref="AutoLock"/>). Dans cet
/// ordre, pour que rien ne se perde ni ne reste :
/// <list type="number">
/// <item>les recherches s'arrêtent à la fiche en cours (<see cref="IStopsSearch"/>) : elles tiennent le coffre, et le fil de
/// l'interface attendrait la fin de leur passage ;</item>
/// <item>ce qui s'écrit s'enregistre, sur la page affichée et sur les vues qui gardent un brouillon (<see cref="ILeaving"/>),
/// et la séance en cours se garde ; si ce qui s'écrit n'a pas pu l'être, le coffre reste ouvert, et les recherches repartent ;</item>
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
/// <param name="drafts">Les vues, hors de la page affichée, qui peuvent garder un brouillon à enregistrer ; rien : aucune.</param>
/// <param name="starting">
/// Ce qui oublie dès que le verrouillage est décidé, ce qui s'écrit enregistré, juste avant que l'écran de déverrouillage
/// ne remplace les vues : une réponse qui arrive pendant la fermeture ne s'y montre plus ; rien : rien.
/// </param>
/// <param name="searches">Les vues dont une recherche peut tenir le coffre ; rien : aucune.</param>
public sealed class VaultLocking(
    ShellViewModel shell,
    VaultUseCases vaults,
    IFolderPicker picker,
    Action forget,
    Action? changed = null,
    Func<IEnumerable<ILeaving>>? drafts = null,
    Action? starting = null,
    Func<IEnumerable<IStopsSearch>>? searches = null)
{
    private bool _locking;

    /// <summary>
    /// Verrouille le coffre chiffré ouvert, même derrière l'écran de déverrouillage d'un autre coffre. Faux s'il n'y a rien
    /// à verrouiller (aucun coffre, un coffre en clair, un verrouillage ou une ouverture en cours), ou si ce qui s'écrit n'a
    /// pas pu être enregistré.
    /// </summary>
    public async Task<bool> LockAsync()
    {
        // Le coffre ouvert n'est pas chiffré : rien à verrouiller, et rien ne le lit (une longue opération le tient peut-être).
        if (_locking || !shell.CanLock || IsOpening())
        {
            return false;
        }
        // Avant le premier regard sur le coffre : une recherche le tient, et ce regard l'attendrait.
        StopSearches();
        if (vaults.State() is not { Encrypted: true, Vault: { } root })
        {
            return KeepOpen();
        }
        _locking = true;
        try
        {
            if (!await FinishAsync().ConfigureAwait(true))
            {
                return KeepOpen();
            }
            // Le temps d'enregistrer, une autre recherche a pu partir.
            StopSearches();
            var waiting = vaults.State() with { Vault = null, Locked = root, OpenError = null, Encrypted = false };
            starting?.Invoke();
            VaultStartup.ShowUnlock(shell, vaults, picker, waiting, changed);
            shell.CanLock = false;
            // Les vues qui viennent de se fermer finissent ce qu'elles avaient commencé.
            await Dispatcher.Yield(DispatcherPriority.Background);
            return await CloseAsync(root).ConfigureAwait(true);
        }
        finally
        {
            _locking = false;
        }
    }

    /// <summary>
    /// Ferme le coffre ouvert pour la page <paramref name="page"/>, déjà affichée hors de l'espace de travail (une
    /// conversion) : ce qui s'écrit s'enregistre, le coffre se ferme, une fois finie la lecture en cours, puis chaque vue
    /// oublie, comme à un verrouillage. Rien de lui n'est retenu : chiffré, il n'attend pas sa clé. Faux, le coffre restant
    /// ouvert, si la page n'est plus affichée, si un verrouillage ou une ouverture est en cours, s'il n'y a pas de coffre
    /// ouvert, ou si ce qui s'écrit n'a pas pu être enregistré : à la différence d'un verrouillage, une conversion peut
    /// attendre, et aucun brouillon n'est abandonné pour elle.
    /// </summary>
    /// <param name="page">La page qui reste affichée pendant et après la fermeture.</param>
    public async Task<bool> CloseForAsync(object page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (_locking || !ReferenceEquals(shell.Current.Page, page) || IsOpening())
        {
            return false;
        }
        StopSearches();
        if (vaults.State() is not { Vault: not null })
        {
            return KeepOpen();
        }
        _locking = true;
        try
        {
            if (!await FinishAsync().ConfigureAwait(true) || (drafts?.Invoke() ?? []).Any(view => view.HasUnsavedDraft) || !ReferenceEquals(shell.Current.Page, page))
            {
                return KeepOpen();
            }
            StopSearches();
            starting?.Invoke();
            shell.CanLock = false;
            // Les vues qui viennent de se fermer finissent ce qu'elles avaient commencé.
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (!ReferenceEquals(shell.Current.Page, page))
            {
                return KeepOpen();
            }
            AppState closed;
            try
            {
                closed = vaults.Close();
            }
            catch
            {
                // La fermeture a échoué, mais le coffre n'est plus joignable : les vues oublient quand même.
                if (vaults.State().Vault is null)
                {
                    forget();
                }
                throw;
            }
            // Une lecture finie juste avant la fermeture rend encore son résultat : il arrive avant l'oubli.
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (closed.Vault is not null)
            {
                // Une ouverture a commencé entre-temps : le coffre reste ouvert.
                return KeepOpen();
            }
            forget();
            return true;
        }
        finally
        {
            _locking = false;
        }
    }

    /// <summary>Une ouverture est en cours, d'où qu'elle vienne (un écran, les paramètres) : elle changera de coffre juste après.</summary>
    private bool IsOpening() =>
        vaults.IsOpening || shell.Current.Page is UnlockPageViewModel { IsBusy: true } or WelcomePageViewModel { IsBusy: true };

    /// <summary>Arrête les recherches, sans rien faire oublier : le coffre, qu'elles tenaient, se libère à la fiche en cours.</summary>
    private void StopSearches()
    {
        foreach (var view in searches?.Invoke() ?? [])
        {
            view.StopSearch();
        }
    }

    /// <summary>Le coffre reste ouvert : ce qui a été arrêté repart. Toujours faux, ce que rendent les refus.</summary>
    private bool KeepOpen()
    {
        foreach (var view in searches?.Invoke() ?? [])
        {
            view.ResumeSearch();
        }
        return false;
    }

    /// <summary>Ce qui s'écrit, sur la page affichée puis sur les vues qui gardent un brouillon, s'enregistre.</summary>
    private async Task<bool> FinishAsync()
    {
        if (shell.Current.Page is ILeaving page && !await page.LeaveAsync().ConfigureAwait(true))
        {
            return false;
        }
        foreach (var view in drafts?.Invoke() ?? [])
        {
            if (!ReferenceEquals(view, shell.Current.Page) && !await view.LeaveAsync().ConfigureAwait(true))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Ferme le coffre ; si, entre-temps, un autre coffre a pris sa place, c'est lui qui se verrouille, ou, en clair, qui
    /// revient. Les vues oublient dès que plus aucun coffre n'est ouvert, même si la fermeture a échoué.
    /// </summary>
    private async Task<bool> CloseAsync(string root)
    {
        AppState locked;
        try
        {
            locked = vaults.Lock();
        }
        catch
        {
            // La fermeture a échoué, mais le coffre n'est plus joignable : les vues oublient quand même.
            if (vaults.State().Vault is null)
            {
                forget();
            }
            throw;
        }
        // Une lecture finie juste avant la fermeture rend encore son résultat : il arrive avant l'oubli.
        await Dispatcher.Yield(DispatcherPriority.Background);
        if (locked.Locked is not { } closed)
        {
            // Rien n'a été fermé : le coffre ouvert maintenant est en clair, son espace de travail revient.
            shell.ShowWorkspace();
            return KeepOpen();
        }
        forget();
        if (closed != root)
        {
            VaultStartup.ShowUnlock(shell, vaults, picker, locked, changed);
        }
        return true;
    }
}
