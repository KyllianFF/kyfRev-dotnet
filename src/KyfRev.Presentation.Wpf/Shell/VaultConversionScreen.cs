using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// La page de conversion du coffre ouvert, montrée hors de l'espace de travail. Elle ferme le coffre par le verrouillage
/// (<see cref="VaultLocking.CloseForAsync"/>), qui enregistre ce qui s'écrit et fait oublier les vues ; ensuite, le coffre
/// qu'elle ouvre suit le chemin d'une ouverture : l'espace de travail, ou l'écran de déverrouillage d'un coffre chiffré.
/// </summary>
public static class VaultConversionScreen
{
    /// <summary>Montre la conversion du coffre ouvert dans le sens <paramref name="direction"/> ; rien, et rien ne change, sans coffre ouvert.</summary>
    /// <param name="shell">La coquille.</param>
    /// <param name="vaults">Ouverture, fermeture et conversion des coffres.</param>
    /// <param name="encryption">Les clés du coffre chiffré ouvert.</param>
    /// <param name="locking">Le verrouillage, qui ferme le coffre pour la conversion.</param>
    /// <param name="picker">Le choix d'un dossier, ou d'un autre coffre depuis l'écran de déverrouillage.</param>
    /// <param name="copy">Copie la clé de récupération dans le presse-papiers.</param>
    /// <param name="direction">Chiffrer, ou déchiffrer.</param>
    /// <param name="changed">Ce qui suit l'ouverture d'un autre coffre ; rien : l'espace de travail revient.</param>
    public static ConversionPageViewModel? Show(
        ShellViewModel shell,
        VaultUseCases vaults,
        EncryptionUseCases encryption,
        VaultLocking locking,
        IFolderPicker picker,
        Action<string> copy,
        ConversionDirection direction,
        Action? changed = null)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(vaults);
        ArgumentNullException.ThrowIfNull(locking);
        if (vaults.State() is not { Vault: { } source })
        {
            return null;
        }
        var page = new ConversionPageViewModel(
            vaults,
            encryption,
            direction,
            source,
            locking.CloseForAsync,
            state =>
            {
                var workspace = changed ?? shell.ShowWorkspace;
                if (state.Locked is not null)
                {
                    VaultStartup.ShowUnlock(shell, vaults, picker, state, changed);
                }
                else if (state.Vault is null)
                {
                    // Le coffre n'a pas pu s'ouvrir, et aucun autre ne l'est : l'accueil dit pourquoi et en propose un autre.
                    shell.ShowWelcome(new WelcomePageViewModel(vaults, picker, state, _ => workspace(), locked => VaultStartup.ShowUnlock(shell, vaults, picker, locked, changed)));
                }
                else
                {
                    workspace();
                }
            },
            shell.ShowWorkspace,
            picker,
            copy);
        shell.ShowConversion(page);
        return page;
    }
}
