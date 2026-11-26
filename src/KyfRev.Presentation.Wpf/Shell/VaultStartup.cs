using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Le démarrage, comme dans la 0.x : le coffre prévu est ouvert (celui du mode portable, le
/// dernier ouvert, ou celui du profil en mode installé). Sans coffre ouvert, la coquille montre
/// l'accueil, avec le message d'un coffre qui n'a pas pu être ouvert ; le menu revient dès qu'un
/// coffre l'est, sur la vue « Aujourd'hui ». Un coffre chiffré dont ce poste n'a pas retenu la clé
/// attend son mot de passe : l'écran de déverrouillage le demande.
/// </summary>
public static class VaultStartup
{
    /// <summary>Ouvre le coffre prévu ; à défaut, montre l'accueil ou le déverrouillage. Rend l'état de l'application.</summary>
    public static AppState Run(ShellViewModel shell, VaultUseCases vaults, IFolderPicker picker)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(vaults);
        var state = vaults.OpenAtStartup();
        if (state.Occupied is { } occupied)
        {
            // Un autre poste l'a ouvert : rien n'est encore ouvert ni écrit ; l'accueil paraît, et la question par-dessus.
            var welcome = new WelcomePageViewModel(vaults, picker, state, _ => shell.ShowWorkspace(), locked => ShowUnlock(shell, vaults, picker, locked))
            {
                Settle = SettleOf(shell),
            };
            shell.ShowWelcome(welcome);
            welcome.Continue(state, () => vaults.Open(occupied));
        }
        else if (state.Locked is not null)
        {
            ShowUnlock(shell, vaults, picker, state);
        }
        else if (state.Vault is null)
        {
            shell.ShowWelcome(new WelcomePageViewModel(vaults, picker, state, _ => shell.ShowWorkspace(), locked => ShowUnlock(shell, vaults, picker, locked)) { Settle = SettleOf(shell) });
        }
        else
        {
            shell.ShowWorkspace();
        }
        return state;
    }

    /// <summary>
    /// Montre le déverrouillage du coffre chiffré qu'attend <paramref name="state"/>. Le coffre ouvert, s'il y en a un, reste
    /// là derrière, et l'écran propose d'y revenir ; sinon, il propose l'accueil, pour créer ou ouvrir un autre coffre : sans
    /// son mot de passe ni sa clé de récupération, on n'y reste pas bloqué.
    /// </summary>
    /// <param name="shell">La coquille.</param>
    /// <param name="vaults">Ouverture des coffres.</param>
    /// <param name="picker">Le choix d'un autre coffre.</param>
    /// <param name="state">L'état qui attend une clé (<see cref="AppState.Locked"/>).</param>
    /// <param name="changed">Ce qui suit l'ouverture d'un coffre ; rien : l'espace de travail revient, sur « Aujourd'hui ».</param>
    public static void ShowUnlock(ShellViewModel shell, VaultUseCases vaults, IFolderPicker picker, AppState state, Action? changed = null)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(state);
        var opened = changed ?? shell.ShowWorkspace;
        void Locked(AppState other) => ShowUnlock(shell, vaults, picker, other, changed);
        Action back = state.Vault is null
            ? () => shell.ShowWelcome(new WelcomePageViewModel(vaults, picker, vaults.State(), _ => opened(), Locked) { Settle = SettleOf(shell) })
            : shell.ShowWorkspace;
        shell.ShowUnlock(new UnlockPageViewModel(vaults, picker, state, _ => opened(), Locked, back) { Settle = SettleOf(shell) });
    }

    /// <summary>La question de la coquille quand un autre poste a le coffre ouvert ; rien sans elle (galerie, essais).</summary>
    public static Func<AppState, Func<AppState>, Task<AppState?>>? SettleOf(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        return shell.Computers is { } computers ? computers.SettleAsync : null;
    }
}
