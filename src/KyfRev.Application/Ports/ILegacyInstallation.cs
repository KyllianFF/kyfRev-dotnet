namespace KyfRev.Application.Ports;

/// <summary>La 0.x encore installée sur ce poste.</summary>
/// <param name="Version">Sa version.</param>
/// <param name="Folder">Son dossier.</param>
/// <param name="SharesThisFolder">Elle est inscrite dans le dossier de cette version : sa désinstallation y retirerait <c>kyfRev.exe</c>.</param>
public sealed record LegacyInstall(string Version, string Folder, bool SharesThisFolder = false);

/// <summary>Ce que l'application sait de la 0.x : elle propose de la retirer, jamais d'office, et n'y touche pas elle-même.</summary>
public interface ILegacyInstallation
{
    /// <summary>La 0.x inscrite dans Windows ; rien si elle n'y est pas.</summary>
    LegacyInstall? Find();

    /// <summary>Ouvre la page des applications installées de Windows ; faux si c'est impossible.</summary>
    bool OpenInstalledApps();
}
