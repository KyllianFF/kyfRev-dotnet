namespace KyfRev.Application.Model;

/// <summary>Comment l'application est installée, selon ce qui se trouve à côté de l'exécutable.</summary>
public enum AppMode
{
    /// <summary>Portable (<c>portable.json</c>) : tout reste dans le dossier transportable, coffre compris.</summary>
    Portable,

    /// <summary>Installé (<c>installation.json</c>) : chaque utilisateur du poste a ses données dans son profil.</summary>
    Installed,

    /// <summary>Développement : ni l'un ni l'autre.</summary>
    Development,
}
