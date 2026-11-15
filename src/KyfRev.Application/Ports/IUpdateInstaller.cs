namespace KyfRev.Application.Ports;

/// <summary>Pour qui l'installateur a mis kyfRev sur ce poste.</summary>
public enum InstallScope
{
    /// <summary>Pour la personne seule, dans son profil, sans droits d'administrateur.</summary>
    CurrentUser,

    /// <summary>Pour toutes les personnes du poste, dans Program Files : Windows demande l'autorisation d'un administrateur.</summary>
    AllUsers,
}

/// <summary>
/// L'installateur de kyfRev, lancé sans aucune fenêtre pour une mise à jour, une fois kyfRev fermé : il garde le dossier,
/// les raccourcis et l'emplacement des fiches choisis à la première installation.
/// </summary>
public interface IUpdateInstaller
{
    /// <summary>Pour qui l'installateur a mis le kyfRev qui tourne ; rien s'il ne vient pas de lui (version portable, développement).</summary>
    InstallScope? Scope { get; }

    /// <summary>
    /// Lance <paramref name="installer"/> en silence, dans le même mode que l'installation en place, et rend la main
    /// aussitôt ; faux si Windows ne l'a pas lancé.
    /// </summary>
    /// <param name="installer">L'installateur, déjà vérifié.</param>
    /// <param name="reopen">kyfRev se rouvre à la fin de l'installation.</param>
    /// <param name="log">Le journal de l'installation, à écrire là.</param>
    bool Start(string installer, bool reopen, string log);
}

/// <summary>
/// Télécharge un fichier en HTTPS, sans dépasser une taille donnée, dans un fichier de ce poste. Un échec est une
/// <see cref="OperationException"/>, et ne laisse rien à la place du fichier demandé.
/// </summary>
public interface IFileDownloader
{
    /// <summary>Écrit dans <paramref name="destination"/> la réponse à <paramref name="address"/>.</summary>
    /// <param name="address">L'adresse du fichier.</param>
    /// <param name="destination">Le fichier à écrire ; remplacé s'il existe, seulement une fois tout reçu.</param>
    /// <param name="maxBytes">Au-delà, le téléchargement s'arrête.</param>
    /// <param name="progress">Reçoit le nombre d'octets reçus.</param>
    /// <param name="cancellationToken">Arrête le téléchargement.</param>
    Task DownloadAsync(Uri address, string destination, long maxBytes, IProgress<long>? progress, CancellationToken cancellationToken);
}
