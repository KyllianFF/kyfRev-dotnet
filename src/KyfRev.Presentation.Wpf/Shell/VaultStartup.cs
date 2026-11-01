using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Le démarrage, comme dans la 0.x : le coffre prévu est ouvert (celui du mode portable, le
/// dernier ouvert, ou celui du profil en mode installé). Sans coffre ouvert, la coquille montre
/// l'accueil, avec le message d'un coffre qui n'a pas pu être ouvert ; le menu revient dès qu'un
/// coffre l'est.
/// </summary>
public static class VaultStartup
{
    /// <summary>Ouvre le coffre prévu ; à défaut, montre l'accueil. Rend l'état de l'application.</summary>
    public static AppState Run(ShellViewModel shell, VaultUseCases vaults, IFolderPicker picker)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(vaults);
        var state = vaults.OpenAtStartup();
        if (state.Vault is null)
        {
            shell.ShowWelcome(new WelcomePageViewModel(vaults, picker, state, _ => shell.ShowWorkspace()));
        }
        return state;
    }
}
